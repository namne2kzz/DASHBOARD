# Auth — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document business ban đầu cho feature Auth |

---

## 1. Purpose

Xác thực người dùng (đăng nhập/đăng xuất) và quản lý session qua JWT, hỗ trợ 2 provider: System (email/password) và Google OAuth.

## 2. Key Entities & Relationships

- `User`: email (lowercase, unique), password hash/salt (PBKDF2), `IsGlobalAdmin`, `AuthProvider` (System/Google), `GoogleSubjectId` (chỉ có khi đăng nhập Google).
- `UserToken`: mỗi lần issue token sinh 1 record — lưu `JwtId` (access token) và hash SHA-256 của refresh token (không lưu plaintext), kèm trạng thái revoke.

## 3. Business Rules & Invariants

- Email so khớp không phân biệt hoa/thường, luôn normalize về lowercase.
- Đăng nhập sai (user không tồn tại hoặc sai password) trả về **cùng 1 lỗi chung** để chống user-enumeration.
- Refresh token chỉ lưu hash SHA-256, không bao giờ lưu plaintext trong DB.
- Refresh hợp lệ khi đồng thời: chưa revoke + chưa hết hạn + hash khớp.
- Refresh sẽ rotate: phát token pair mới và revoke token pair cũ ngay lập tức.
- Logout revoke `JwtId` hiện tại ở server (access token bị vô hiệu dù chưa hết hạn theo thời gian).
- Đổi password: chỉ chính chủ tài khoản hoặc Global Admin được đổi; phải verify password cũ trước khi đổi.
- Tài khoản Google OAuth không có password hash/salt → không thể login bằng password.

## 4. Main Workflows / Use Cases

1. Login bằng email/password → verify → phát access + refresh token pair, lưu `UserToken`.
2. Access token hết hạn → client gọi refresh → verify refresh token → rotate (phát pair mới, revoke pair cũ).
3. Logout → revoke `JwtId` hiện tại.
4. Admin tạo account mới với password ban đầu → hash + salt trước khi lưu.

## 5. Definition of Done

- [ ] Email luôn được normalize lowercase trước khi so sánh/lưu.
- [ ] Không có code path nào trả lỗi khác nhau giữa "user không tồn tại" và "sai password".
- [ ] Refresh token không bao giờ được log hoặc lưu dạng plaintext.
- [ ] Mọi refresh thành công đều revoke token cũ (không cho dùng lại refresh token đã dùng).
- [ ] Logout revoke đúng `JwtId` của session hiện tại, không ảnh hưởng session khác của cùng user.

## 6. Edge Cases & Notes

- `GoogleSubjectId` không đổi dù user đổi email phía Google → dùng để giữ ổn định định danh.
- User có thể có quan hệ "manager" (hierarchical) — không ảnh hưởng auth nhưng nằm trên cùng entity `User`.
