# Auth — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-10-28 | — | Refresh chủ động khi access token hết hạn + so sánh internal token fixed-time | (1) **Refresh trước khi gửi**: server đã trả `accessTokenExpiresAt` nhưng client vứt đi. Nay `AuthService` giữ lại mốc này; request nào xuất phát khi token **đã biết chắc** hết hạn (trừ margin 10s cho thời gian bay + lệch đồng hồ) thì refresh **trước**, rồi mới gửi — 1 round trip thay vì 3 (gửi → 401 → refresh → gửi lại), và không còn 401 vô cớ trong console. Dùng **chung** 1 exchange với đường 401-và-retry (`shareReplay`), nên trộn cả hai vẫn chỉ refresh 1 lần. Không áp cho endpoint credential (đăng nhập bằng session hết hạn chính là cách thay session mới) và `/auth/logout`. Sau reload thì **không biết** mốc hết hạn (token nằm trong HttpOnly cookie, JS không đọc được) → `isAccessTokenExpired` trả false, rơi về đường 401-và-retry như cũ; đoán mò sẽ tiêu refresh token cho một session có thể vẫn còn tốt. Mốc được xoá khi logout, nếu không session kế tiếp sẽ thấy "hết hạn" ngay từ request đầu. (2) **So sánh secret bằng `CryptographicOperations.FixedTimeEquals`** thay cho `string.Equals` ở **cả hai** middleware: `InternalApiKeyMiddleware` (token nội bộ) và `CsrfValidationMiddleware` (cặp double-submit). `string.Equals` thoát ngay tại byte đầu khác nhau nên thời gian phản hồi tiết lộ độ dài prefix trùng khớp — đủ để dò ra secret từng ký tự một qua nhiều request. Với CSRF nguy hiểm hơn ở chỗ: kẻ tấn công vốn đã khiến browser gửi được cookie (đúng tình huống guard này sinh ra để chặn), đọc ngược được secret là forge luôn được header còn thiếu. Password (`PasswordService`) và GitHub webhook đã dùng fixed-time từ trước; giờ toàn bộ codebase nhất quán. Internal token rỗng vẫn trả 503 (không được hiểu thành "khớp mọi thứ"); CSRF thiếu cookie **hoặc** thiếu header đều 403 (hai giá trị cùng vắng không được coi là bằng nhau, nếu không chỉ cần không gửi gì là qua guard). |
| 2026-10-28 | — | Refresh-and-retry khi 401 + không xoá session oan | Chuyển sang cookie đã làm mất đường refresh phía client: `AuthService.refresh()` tồn tại nhưng **không caller nào gọi**, nên mọi 401 logout thẳng → session rụng liên tục (SSE reconnect mỗi lần hot-reload tự bắn 1 request catch-up, 401 của nó cũng logout). Nay `errorInterceptor` xử lý 401 theo thứ tự: (1) endpoint credential (`/auth/login`, `/google-login`, `/set-password`, `/refresh`, `/logout`) → 401 là kết luận cuối, logout; (2) chưa có session → logout, không refresh vô ích; (3) còn lại → gọi refresh **1 lần dùng chung cho mọi 401 đồng thời** (`shareReplay`, vì refresh rotate token — nhiều lời gọi song song sẽ tự vô hiệu hoá nhau) → thành công thì replay request gốc, thất bại mới logout. Thêm `BACKGROUND_REQUEST` context: request nền (poll, catch-up read của SSE reconnect) vẫn được refresh nhưng **không** logout/navigate khi refresh trượt — không đá user ra khỏi form đang gõ vì một request họ không hề bấm. Server: `POST /auth/refresh` khi **không có** refresh token nữa thì **không** xoá cookie (trước đây xoá cả `nflow_at` còn hiệu lực → 1 lời gọi refresh lạc làm chết session đang dùng được); nhánh refresh token sai/hết hạn vẫn trả 401 qua `UnauthorizedAccessException` |
| 2026-10-24 | — | Token sang HttpOnly cookie + CSRF | Access token và refresh token không còn lưu ở browser storage mà phát dưới dạng **HttpOnly cookie** (`nflow_at`, `nflow_rt`) — JS không đọc được → chống XSS đánh cắp token. Login/google-login/refresh **vẫn trả token trong body** để luồng SSO sang HUB (origin khác) đính token vào URL redirect. Request state-changing qua cookie phải kèm CSRF token double-submit: server set thêm cookie đọc được `XSRF-TOKEN`, client echo lại ở header `X-XSRF-TOKEN`, không khớp → 403. Request mang `Authorization: Bearer` hoặc `X-Internal-Token` (SSO/service) được miễn CSRF. Refresh token đọc từ cookie `nflow_rt` khi body rỗng. Logout xoá cả 3 cookie server-side |
| 2026-09-26 | 14:02 | Login theo Organization | Login giờ yêu cầu `OrgAlias` — xác thực là (OrgAlias + Email + Password); email chỉ unique trong 1 Organization. Sai alias trả cùng lỗi chung để chống enumeration tenant. Login result mang thêm `OrgId`/`OrgAlias`. Xem [organizations.dod.md](organizations.dod.md) |
| 2026-07-11 | 22:15 | Thêm đăng nhập bằng Google | Thêm `POST /api/auth/google-login`: verify Google id_token, tìm `User` theo `GoogleSubjectId` đã liên kết sẵn (không tạo account mới, không cần Invitation) → phát session giống login thường. User chưa từng accept invite (chưa có `GoogleSubjectId` liên kết) bấm Sign in with Google sẽ bị từ chối, yêu cầu được invite trước |
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document business ban đầu cho feature Auth |

---

## 1. Purpose

Xác thực người dùng (đăng nhập/đăng xuất) và quản lý session qua JWT trong phạm vi 1 **Organization** (tenant), hỗ trợ 2 provider: System (email/password) và Google OAuth.

## 2. Key Entities & Relationships

- `Organization`: tenant chứa user. `Alias` (unique toàn hệ thống) là định danh tenant user phải cung cấp khi login.
- `User`: `OrgId` (tenant), email (lowercase, **unique theo từng Organization** — không phải unique toàn hệ thống), password hash/salt (PBKDF2), `IsGlobalAdmin`, `AuthProvider` (System/Google), `GoogleSubjectId` (chỉ có khi đăng nhập Google).
- `UserToken`: mỗi lần issue token sinh 1 record — lưu `JwtId` (access token) và hash SHA-256 của refresh token (không lưu plaintext), kèm trạng thái revoke.

## 3. Business Rules & Invariants

- **Login là (OrgAlias + Email + Password)**: resolve `Organization` theo alias trước, rồi tìm `User` theo (`OrgId` + Email). Cùng 1 email tồn tại ở 2 tenant khác nhau là hợp lệ.
- `OrgAlias` được trim + normalize lowercase trước khi so khớp.
- Email so khớp không phân biệt hoa/thường, luôn normalize về lowercase.
- Đăng nhập sai — dù là **alias không tồn tại**, user không tồn tại, hay sai password — đều trả về **cùng 1 lỗi chung** (`"Invalid organization, email or password."`) để chống enumeration cả tenant lẫn user.
- Refresh token chỉ lưu hash SHA-256, không bao giờ lưu plaintext trong DB.
- Refresh hợp lệ khi đồng thời: chưa revoke + chưa hết hạn + hash khớp.
- Refresh sẽ rotate: phát token pair mới và revoke token pair cũ ngay lập tức.
- Logout revoke `JwtId` hiện tại ở server (access token bị vô hiệu dù chưa hết hạn theo thời gian).
- Đổi password: chỉ chính chủ tài khoản hoặc Global Admin được đổi; phải verify password cũ trước khi đổi.
- Tài khoản Google OAuth không có password hash/salt → không thể login bằng password.
- Đăng nhập bằng Google chỉ dùng cho account **đã tồn tại** và đã liên kết `GoogleSubjectId` (qua accept invitation trước đó) — không tự tạo account mới. Không match được → từ chối, yêu cầu liên hệ admin để được invite.
- Access token mang claim `OrgId` để downstream request không phải resolve lại tenant.
- Google id_token được verify bằng `Google.Apis.Auth` (chữ ký + audience khớp `Google:ClientId`) trước khi tin tưởng email/subject bên trong.
- **Token không bao giờ nằm trong browser storage (localStorage/sessionStorage)** — chỉ ở HttpOnly cookie. Chỉ `userProfile` (không phải secret) được persist để restore UI sau reload; token thật do cookie giữ.
- Server resolve access token theo thứ tự: `Authorization: Bearer` → cookie `nflow_at` → query `?access_token=` (SSE). Bearer thắng để SSO/service không bị ảnh hưởng.
- Cookie `nflow_rt` (refresh) scope `Path=/api`, không gửi kèm mọi request thường; `nflow_at` scope `Path=/`.
- CSRF double-submit: chỉ áp cho method unsafe (POST/PUT/PATCH/DELETE) **đi bằng cookie**; request có `Authorization`/`X-Internal-Token` được miễn (không bị trình duyệt nạn nhân điều khiển).
- Cookie `Secure` theo config (`true` prod HTTPS, `false` dev http); `SameSite=Lax` để top-level navigation tới accept-invite vẫn mang cookie mà chặn cross-site POST.
- **401 ≠ hết session.** Mặc định 401 được hiểu là access token hết hạn: client refresh 1 lần rồi replay request. Chỉ logout khi refresh trượt, hoặc khi 401 đến từ endpoint credential (`/auth/login`, `/google-login`, `/set-password`, `/refresh` — ở đó 401 nghĩa là sai credential, phải để user thấy lỗi), hoặc khi client biết chưa có session.
- **401 từ `/auth/logout` phải trơ hoàn toàn** — không refresh, không logout, không navigate. `logout()` chính nó POST tới đây, nên mọi phản ứng đều gọi `logout()` lần nữa → vòng lặp vô hạn (đã đo 464 request/3 giây khi thiếu guard này). Session lúc đó đã đang được dọn bởi người gọi logout, interceptor không còn gì phải làm.
- Nhiều 401 đồng thời chỉ sinh **1** lời gọi refresh. Bắt buộc, vì refresh rotate token: gọi song song thì lời gọi đầu revoke token mà các lời gọi sau đang cầm → tất cả 401 → logout oan.
- Request nền (poll, catch-up read sau khi SSE reconnect) đánh dấu `BACKGROUND_REQUEST`: vẫn được refresh, nhưng refresh trượt thì **không** logout/navigate — hành động đó không phải của user, không được phá việc họ đang làm. Request kế tiếp do user thực sự bấm sẽ gặp cùng session chết và kết thúc nó ở thời điểm họ hiểu được.
- `POST /auth/refresh` khi **không nhận được** refresh token thì trả 401 nhưng **không xoá cookie** — nhánh này không chứng minh được session đã chết (access cookie có thể vẫn còn hiệu lực), xoá là tự giết session đang dùng tốt.

## 4. Main Workflows / Use Cases

1. Login bằng (org alias + email + password) → resolve Organization theo alias → verify credential → phát access + refresh token pair (result kèm `OrgId`/`OrgAlias`), lưu `UserToken`.
2. Access token hết hạn → request bất kỳ nhận 401 → `errorInterceptor` tự gọi refresh (1 lần, dùng chung cho mọi 401 đang chờ) → verify refresh token → rotate (phát pair mới, revoke pair cũ) → replay request gốc. User không thấy gì cả. Chỉ khi refresh cũng trượt mới logout + về sign-in.
3. Logout → revoke `JwtId` hiện tại.
4. Admin tạo account mới với password ban đầu → hash + salt trước khi lưu.
5. Đăng nhập bằng Google (user đã từng accept invitation trước đó) → bấm Sign in with Google → verify id_token → tìm `User` theo `GoogleSubjectId` → phát access + refresh token pair, lưu `UserToken`, vào thẳng app.

## 5. Definition of Done

- [ ] Email luôn được normalize lowercase trước khi so sánh/lưu.
- [ ] Lookup user luôn scope theo `OrgId`, không bao giờ query email đơn lẻ toàn hệ thống.
- [ ] Không có code path nào trả lỗi khác nhau giữa "alias không tồn tại", "user không tồn tại" và "sai password".
- [ ] Refresh token không bao giờ được log hoặc lưu dạng plaintext.
- [ ] Mọi refresh thành công đều revoke token cũ (không cho dùng lại refresh token đã dùng).
- [ ] Logout revoke đúng `JwtId` của session hiện tại, không ảnh hưởng session khác của cùng user.
- [x] Đăng nhập Google không bao giờ tự tạo account mới hay repository membership mới.
- [x] Google id_token luôn được verify chữ ký + audience trước khi tin tưởng thông tin bên trong.
- [x] Access/refresh token không bao giờ được ghi vào localStorage/sessionStorage — chỉ HttpOnly cookie.
- [x] Request state-changing bằng cookie mà thiếu/sai CSRF token double-submit bị trả 403.
- [x] Logout xoá cả `nflow_at`, `nflow_rt`, `XSRF-TOKEN` phía server (không chỉ clear phía client).

## 6. Edge Cases & Notes

- `GoogleSubjectId` không đổi dù user đổi email phía Google → dùng để giữ ổn định định danh.
- Login cần alias nghĩa là **UI phải thu thập org alias** (form field hoặc lấy từ URL/subdomain) — user không thể login chỉ bằng email + password.
- User có thể có quan hệ "manager" (hierarchical) — không ảnh hưởng auth nhưng nằm trên cùng entity `User`.
- Đăng nhập Google **phụ thuộc** vào feature Invitations (`GoogleSubjectId` chỉ được set lúc accept invitation) — xem thêm `invitations.dod.md`. Không có cách nào tạo account Google-only ngoài luồng invite.
