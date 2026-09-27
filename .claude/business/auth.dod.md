# Auth — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
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

## 4. Main Workflows / Use Cases

1. Login bằng (org alias + email + password) → resolve Organization theo alias → verify credential → phát access + refresh token pair (result kèm `OrgId`/`OrgAlias`), lưu `UserToken`.
2. Access token hết hạn → client gọi refresh → verify refresh token → rotate (phát pair mới, revoke pair cũ).
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

## 6. Edge Cases & Notes

- `GoogleSubjectId` không đổi dù user đổi email phía Google → dùng để giữ ổn định định danh.
- Login cần alias nghĩa là **UI phải thu thập org alias** (form field hoặc lấy từ URL/subdomain) — user không thể login chỉ bằng email + password.
- User có thể có quan hệ "manager" (hierarchical) — không ảnh hưởng auth nhưng nằm trên cùng entity `User`.
- Đăng nhập Google **phụ thuộc** vào feature Invitations (`GoogleSubjectId` chỉ được set lúc accept invitation) — xem thêm `invitations.dod.md`. Không có cách nào tạo account Google-only ngoài luồng invite.
