# Users — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-09-26 | 14:02 | Scope theo Organization + settings/avatar | Email chỉ unique **trong 1 Organization** (không còn unique toàn hệ thống); user gắn `OrgId`. Thêm preference (`UserSetting`) và avatar ảnh thật upload MinIO — chi tiết ở [user-settings.dod.md](user-settings.dod.md) |
| 2026-07-25 | 18:20 | Org hierarchy | Expose + sửa được `ManagerId` (quan hệ quản lý); thêm view cây phân cấp (cấp trên/cùng cấp/cấp dưới), chống cycle |
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document business ban đầu cho feature Users |

---

## 1. Purpose

Quản lý tài khoản người dùng hệ thống: tạo account, cập nhật profile, đổi password, toggle quyền Global Admin.

## 2. Key Entities & Relationships

- `User`: `OrgId` (tenant sở hữu), display name, `AvatarClass` (CSS class Tailwind — fallback khi chưa upload ảnh), `IsGlobalAdmin`, `AuthProvider`, quan hệ manager (hierarchical org).
- `SystemUserListItemDto`: profile + metadata hoạt động (CreatedAt, LastLoginAt) dùng cho list admin.
- `UserSetting`: preference key-value của user (date format, timezone, theme, notification, avatar URL...) — xem [user-settings.dod.md](user-settings.dod.md).

## 3. Business Rules & Invariants

- Chỉ Global Admin được tạo user mới.
- **Email unique theo từng `Organization`** (scope `OrgId` + Email), **không** unique toàn hệ thống — 2 tenant khác nhau được phép có cùng email. Check unique vẫn bao gồm user đã soft-delete (`IgnoreQueryFilters`) để tránh trùng khi tạo lại.
- User mới luôn được gán vào `OrgId` của tenant hiện tại.
- Tạo account kiểu System yêu cầu password ≥ 8 ký tự, email/name bắt buộc.
- Avatar class phải là class Tailwind hợp lệ. User có thể upload ảnh avatar thật (lưu MinIO, URL trong `UserSetting`) — có ảnh thì UI dùng ảnh, không có thì fallback về `AvatarClass`.
- Preference/avatar: user chỉ đọc/ghi được setting của **chính mình**.
- Đổi password: chỉ chính chủ được đổi (không delegate cho admin), bắt buộc verify password cũ.
- Cập nhật profile (name, avatar): chỉ chính chủ hoặc Global Admin.
- Toggle Global Admin: chỉ Global Admin khác mới được thực hiện.
- **Org hierarchy (`ManagerId`)**: chỉ Global Admin được set/clear manager của user. Không cho tự làm manager của chính mình; không cho gán manager là **con cháu** của mình (chống cycle — walk up chain kiểm tra). Manager phải là user tồn tại. `ManagerId = null` = root (không có cấp trên).
- **Xem cây phân cấp**: từ 1 user hiển thị chuỗi cấp trên (ancestors → manager), người cùng cấp (cùng `ManagerId`, trừ chính mình), và cấp dưới trực tiếp (report). Chỉ Global Admin xem được.
- User có thể bị toggle active/inactive.

## 4. Main Workflows / Use Cases

1. Admin tạo user mới → hash password → activate với `AuthProvider = System`.
2. User tự cập nhật profile (name/avatar) → `UpdatedAt` refresh.
3. User tự đổi password → verify password cũ → hash password mới với salt mới.
4. Admin toggle quyền Global Admin của user khác.

## 5. Definition of Done

- [ ] Email luôn lưu lowercase và unique-check dùng scope (`OrgId` + Email), bao gồm cả record đã soft-delete.
- [ ] Mọi query list/lookup user đều filter theo `OrgId` — không leak user cross-tenant.
- [ ] Không có path nào cho phép admin đổi password người khác.
- [ ] Validation password ≥ 8 ký tự được áp dụng khi tạo account System.
- [ ] Toggle admin chỉ thực hiện được bởi Global Admin.

## 6. Edge Cases & Notes

- User hỗ trợ soft-delete (`ISoftDelete`) nhưng vẫn tính vào kiểm tra unique email (trong cùng `OrgId`).
- Quan hệ manager (`ManagerId`) đã có rule ràng buộc chống cycle — xem mục 3, không chỉ là lưu trữ thuần.
- Cây phân cấp manager nằm **trong 1 Organization** — không có quan hệ manager xuyên tenant.
- `AvatarClass` vẫn được giữ và vẫn trả trong login result / internal API kể cả khi user đã upload ảnh — không bị thay thế.
