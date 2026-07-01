# Invitations — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document business ban đầu cho feature Invitations |

---

## 1. Purpose

Onboard người dùng mới (ngoài hệ thống) vào 1 Repository qua email + xác thực Google OAuth, dùng token một-lần.

## 2. Key Entities & Relationships

- `Invitation`: email, Repository đích, người mời, hash SHA-256 của token, ngày hết hạn, status (Pending/Accepted/Expired/Revoked), thời điểm accept.
- Liên quan `Repository`, `User` (người mời + người được tạo sau khi accept), `RepositoryMember` (kết quả accept).

## 3. Business Rules & Invariants

- Email được mời không được trùng email đã có account trong hệ thống.
- Tạo invitation mới cho cùng email + Repository sẽ tự revoke các invitation Pending cũ.
- Token có TTL theo config app (thường 7 ngày), tính theo UTC timestamp.
- Token chỉ lưu hash, plaintext token chỉ nằm trong link mời và **không lưu DB**.
- Token được nhúng vào URL fragment (`#...`) để tránh log lại ở server/proxy/CDN.
- Khi accept: email Google phải khớp chính xác (không phân biệt hoa/thường) với email được mời.
- Nếu `GoogleSubjectId` đã từng accept invite ở Repository khác trước đó → không tạo `User` mới, chỉ tạo thêm `RepositoryMember`.
- User mới từ invitation luôn join với role Dev (mặc định).
- Accept invitation set status + timestamp; invitation hết hạn được phát hiện ngay tại thời điểm accept.
- Khi tạo invitation, publish `InvitationCreatedMessage` (async) để gửi email.

## 4. Main Workflows / Use Cases

1. Admin tạo invitation cho email ngoài → sinh token → publish message gửi email.
2. User ngoài click link → verify token + đăng nhập Google → tạo account (hoặc reuse theo `GoogleSubjectId`) → trở thành `RepositoryMember` role Dev.

## 5. Definition of Done

- [ ] Không cho mời email đã có account active trong hệ thống.
- [ ] Token plaintext không bao giờ persist vào DB hoặc log server.
- [ ] Accept luôn validate hết hạn + status + email khớp Google account.
- [ ] Cùng 1 `GoogleSubjectId` accept nhiều invite không tạo trùng `User`.

## 6. Edge Cases & Notes

- Lỗi "not found" / "revoked" / "expired" dùng message chung để chống token-enumeration.
- Verify identity diễn ra **sau** khi verify token hợp lệ — bắt buộc login đúng Google account của email được mời.
