# Users — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document business ban đầu cho feature Users |

---

## 1. Purpose

Quản lý tài khoản người dùng hệ thống: tạo account, cập nhật profile, đổi password, toggle quyền Global Admin.

## 2. Key Entities & Relationships

- `User`: display name, avatar (CSS class Tailwind), `IsGlobalAdmin`, `AuthProvider`, quan hệ manager (hierarchical org).
- `SystemUserListItemDto`: profile + metadata hoạt động (CreatedAt, LastLoginAt) dùng cho list admin.

## 3. Business Rules & Invariants

- Chỉ Global Admin được tạo user mới.
- Email unique toàn hệ thống — check cả user đã soft-delete (`IgnoreQueryFilters`) để tránh trùng khi tạo lại.
- Tạo account kiểu System yêu cầu password ≥ 8 ký tự, email/name bắt buộc.
- Avatar class phải là class Tailwind hợp lệ.
- Đổi password: chỉ chính chủ được đổi (không delegate cho admin), bắt buộc verify password cũ.
- Cập nhật profile (name, avatar): chỉ chính chủ hoặc Global Admin.
- Toggle Global Admin: chỉ Global Admin khác mới được thực hiện.
- User có thể bị toggle active/inactive.

## 4. Main Workflows / Use Cases

1. Admin tạo user mới → hash password → activate với `AuthProvider = System`.
2. User tự cập nhật profile (name/avatar) → `UpdatedAt` refresh.
3. User tự đổi password → verify password cũ → hash password mới với salt mới.
4. Admin toggle quyền Global Admin của user khác.

## 5. Definition of Done

- [ ] Email luôn lưu lowercase và unique-check bao gồm cả record đã soft-delete.
- [ ] Không có path nào cho phép admin đổi password người khác.
- [ ] Validation password ≥ 8 ký tự được áp dụng khi tạo account System.
- [ ] Toggle admin chỉ thực hiện được bởi Global Admin.

## 6. Edge Cases & Notes

- User hỗ trợ soft-delete (`ISoftDelete`) nhưng vẫn tính vào kiểm tra unique email.
- Quan hệ manager hỗ trợ cây tổ chức (org tree), hiện chưa có rule nghiệp vụ ràng buộc thêm ngoài lưu trữ.
