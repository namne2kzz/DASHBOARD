# Members — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-06-28 | 16:00 | Chức danh = RepoRole metadata | `DefaultRole` đổi từ enum `TeamRole` (đã xoá) sang **string value** lấy từ RepoRole metadata. Dropdown chức danh load từ metadata RepoRole của repo. `canActOnMember` đổi sang thuần permission (ManageSettings). |
| 2026-06-28 | 14:30 | RoleId bắt buộc + guard quyền | `RoleId` thành bắt buộc khi add/update (auto chọn default role khớp chức danh). Guard "last ScrumMaster" đổi thành "luôn còn ≥1 member có quyền ManageSettings". TeamRole không còn cấp quyền. |
| 2026-06-28 | 13:58 | Đổi CustomRole → Role | Member trỏ `RoleId` (default hoặc custom role) thay `CustomRoleId`; validate role là default global hoặc custom cùng Repository. |
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document business ban đầu cho feature Members |

---

## 1. Purpose

Quản lý thành viên của 1 Repository và quyền của họ (team role mặc định + role gán thêm).

## 2. Key Entities & Relationships

- `RepositoryMember`: join `User` ↔ `Repository`, có `DefaultRole` (chức danh, **string** = value RepoRole metadata) và `RoleId` (bắt buộc).
- `DefaultRole` lấy từ catalog metadata key `RepoRole` (xem [metadata.dod.md](metadata.dod.md)); lưu value string, không FK. Enum `TeamRole` cũ đã xoá.
- Liên quan `Repository` (container) và `Role` (default hoặc custom, quyết định permission — xem [roles.dod.md](roles.dod.md)).

## 3. Business Rules & Invariants

- Cần quyền `ManageSettings` để add/update/remove member.
- 1 user chỉ được làm member 1 lần trong cùng 1 Repository (không trùng).
- User phải tồn tại trong hệ thống trước khi add làm member.
- `RoleId` **bắt buộc**, phải là default role (global) hoặc custom role thuộc cùng Repository. UI mặc định auto chọn default role khớp chức danh (Dev→Developer...).
- Repository đã `IsArchived` thì không add thêm member được.
- Không cho update/remove khiến repo **không còn member nào có quyền `ManageSettings`** (guard chống mất admin, dựa permission thật).
- Remove member chỉ xoá quan hệ membership, không xoá `User`.
- `DefaultRole` (chức danh, value RepoRole metadata) chỉ dùng cho capacity/planning, **không cấp quyền**. Quyền của member = `Role.AllowedFunctions` (xem [roles.dod.md](roles.dod.md)).

## 4. Main Workflows / Use Cases

1. Admin add user có sẵn vào Repository → chọn chức danh (từ RepoRole metadata) + role (bắt buộc, mặc định khớp chức danh).
2. Admin update role của member (đổi chức danh và/hoặc role).
3. Admin remove member (chặn nếu là ScrumMaster cuối cùng).
4. User được invite qua Google OAuth → tự động join với role Dev (xem [invitations.dod.md](invitations.dod.md)).

## 5. Definition of Done

- [ ] Add member luôn check quyền `ManageSettings` của caller.
- [ ] Add member luôn check Repository chưa `IsArchived`.
- [ ] Update/Remove luôn check repo còn ≥1 member có quyền `ManageSettings`.
- [ ] RoleId bắt buộc + validate là default global hoặc custom cùng Repository trước khi gán.

## 6. Edge Cases & Notes

- 1 user có thể là member của nhiều Repository với role khác nhau ở mỗi nơi.
- Nếu user đã là member qua 1 invitation trước, accept invitation khác vào cùng Repository sẽ thành công "im lặng" (không tạo trùng membership).
