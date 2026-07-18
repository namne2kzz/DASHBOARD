# Invitations — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-07-11 | 12:58 | Thêm list + revoke invitation | Thêm màn hình xem danh sách toàn bộ invitation của Repository (email, status, discipline/role, người mời, ngày gửi/hết hạn/accept) trong Settings → Members, chỉ user có quyền `InviteMembers` xem được. Thêm action Revoke thủ công cho invitation đang Pending (vd gửi nhầm, không cần nữa) — set status Revoked, không xoá record. Nếu invitee đã nhận email và bấm link sau khi bị revoke, accept vẫn trả message chung "Invitation not found or already used." (không lộ lý do cụ thể, giữ đúng rule chống token-enumeration đã có) |
| 2026-07-10 | 22:12 | Nối API, chọn role khi mời | Nối end-to-end (Controller + UI): thêm permission check `InviteMembers` khi tạo invite, người mời chọn Discipline + Role lúc invite (lưu vào `Invitation`, không còn hard-code role Dev lúc accept), accept trả về session đăng nhập (access + refresh token) luôn vì user Google-only không có password để login lại |
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document business ban đầu cho feature Invitations |

---

## 1. Purpose

Onboard người dùng mới (ngoài hệ thống) vào 1 Repository qua email + xác thực Google OAuth, dùng token một-lần.

## 2. Key Entities & Relationships

- `Invitation`: email, Repository đích, người mời, **Discipline (`DefaultRole`) + Role (`RoleId`) được chọn lúc mời**, hash SHA-256 của token, ngày hết hạn, status (Pending/Accepted/Expired/Revoked), thời điểm accept.
- Liên quan `Repository`, `User` (người mời + người được tạo sau khi accept), `Role` (role sẽ gán khi accept), `RepositoryMember` (kết quả accept).

## 3. Business Rules & Invariants

- Chỉ user có quyền `InviteMembers` trên Repository đó mới tạo được invitation (kiểm tra qua `IRequestUserContext.CanAsync`).
- Repository đích phải tồn tại và chưa bị archive.
- Người mời phải chọn **Discipline** (team role, dùng cho capacity planning) và **Role** (default hoặc custom, quyết định permission) ngay khi tạo invite — Role phải tồn tại trong Repository đó (default hoặc custom thuộc đúng repo), giống rule khi Add Member trực tiếp.
- Email được mời không được trùng email đã có account trong hệ thống.
- Tạo invitation mới cho cùng email + Repository sẽ tự revoke các invitation Pending cũ.
- Token có TTL theo config app (`Invitation:TokenTtlMinutes`, hiện đang set rất ngắn — 5 phút ở môi trường dev/prod hiện tại, không phải 7 ngày như bản nháp ban đầu), tính theo UTC timestamp.
- Token chỉ lưu hash, plaintext token chỉ nằm trong link mời và **không lưu DB**.
- Token được nhúng vào URL fragment (`#...`) để tránh log lại ở server/proxy/CDN.
- Khi accept: email Google phải khớp chính xác (không phân biệt hoa/thường) với email được mời.
- Nếu `GoogleSubjectId` đã từng accept invite ở Repository khác trước đó → không tạo `User` mới, chỉ tạo thêm `RepositoryMember`.
- User mới từ invitation join với đúng Discipline + Role đã được chọn lúc tạo invite (không còn hard-code role "Developer"). Nếu Role đó bị xoá trước khi accept → accept fail với message rõ ràng, yêu cầu admin gửi lại invite mới.
- Accept invitation set status + timestamp; invitation hết hạn được phát hiện ngay tại thời điểm accept.
- Khi tạo invitation, publish `InvitationCreatedMessage` (async) để gửi email.
- **Accept thành công trả về session đăng nhập luôn** (access token + refresh token, giống response của `/api/auth/login`) — vì user được tạo qua invitation là Google-only (không có password), không thể tự login lại qua form email/password sau đó.
- Endpoint accept có rate-limit theo IP (`Invitation:RateLimit`) để chống brute-force token.
- Chỉ user có quyền `InviteMembers` mới xem được danh sách invitation của Repository (toàn bộ status, không chỉ Pending).
- Chỉ invitation đang **Pending** mới revoke được; revoke set `Status = Revoked`, không xoá record khỏi DB (giữ lại lịch sử).

## 4. Main Workflows / Use Cases

1. Member có quyền `InviteMembers` mở form "Invite by Email" trong Settings → Members, chọn Discipline + Role, nhập email ngoài hệ thống → hệ thống sinh token, lưu invitation kèm Discipline/Role đã chọn → publish message gửi email.
2. User ngoài click link trong email (`/invite/accept#<token>`) → trang xác nhận hiện nút Sign in with Google → verify token + đăng nhập Google → tạo account (hoặc reuse theo `GoogleSubjectId`) → trở thành `RepositoryMember` với đúng Discipline + Role đã chọn ở bước 1 → nhận session đăng nhập, vào thẳng app.
3. Member có quyền `InviteMembers` xem danh sách toàn bộ invitation đã gửi cho Repository (mọi status) trong Settings → Members → nếu 1 invitation đang Pending không còn cần thiết (gửi nhầm, đổi ý...) → bấm Revoke sau khi confirm → status chuyển Revoked, link mời trong email cũ (nếu invitee đã nhận) không dùng được nữa.

## 5. Definition of Done

- [x] Không cho mời email đã có account active trong hệ thống.
- [x] Token plaintext không bao giờ persist vào DB hoặc log server.
- [x] Accept luôn validate hết hạn + status + email khớp Google account.
- [x] Cùng 1 `GoogleSubjectId` accept nhiều invite không tạo trùng `User`.
- [x] Chỉ user có quyền `InviteMembers` trong repo mới tạo được invite.
- [x] Người mời chọn được Discipline + Role lúc tạo invite; accept dùng đúng lựa chọn đó.
- [x] Accept trả về session đăng nhập hợp lệ (không cần login lại qua form khác).
- [x] Chỉ user có quyền `InviteMembers` xem được danh sách invitation.
- [x] Chỉ revoke được invitation đang Pending; invitee đã nhận link vẫn nhận message hợp lý (không lộ chi tiết) nếu cố accept sau khi bị revoke.

## 6. Edge Cases & Notes

- Lỗi "not found" / "revoked" / "expired" dùng message chung để chống token-enumeration.
- Verify identity diễn ra **sau** khi verify token hợp lệ — bắt buộc login đúng Google account của email được mời.
- Nếu Role được chọn lúc mời bị xoá trước khi user accept, accept sẽ fail thay vì fallback về role khác — tránh gán nhầm quyền ngoài ý muốn của người mời.
- Trang accept (`/invite/accept`) nằm ngoài `authGuard` — truy cập được khi chưa đăng nhập, vì đối tượng là user hoàn toàn mới với hệ thống.
