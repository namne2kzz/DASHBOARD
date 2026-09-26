# Repositories — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document business ban đầu cho feature Repositories |

---

## 1. Purpose

Quản lý "Repository" — container dự án (tương đương Project trong Jira/Azure DevOps). Mỗi Repository có code prefix riêng dùng để đánh số work item (vd `DASH-1`).

## 2. Key Entities & Relationships

- `Repository`: Name, `Code` (unique, tối đa 10 ký tự, chữ hoa + số), Description, `IsArchived`.
- `RepositoryMember`: many-to-many User ↔ Repository, gắn `DefaultRole` + optional CustomRole (xem [members.dod.md](members.dod.md)).
- `RepositoryMetadata`: catalog key-value chọn sẵn theo Repository (vd key "FixedInVersion" với value "1.4.1", "1.5.0"...).

## 3. Business Rules & Invariants

- `Code` unique toàn hệ thống, normalize uppercase, chỉ chữ hoa + số, tối đa 10 ký tự.
- Chỉ Global Admin được tạo Repository; tạo Repository tự động gán 1 user chỉ định làm ScrumMaster (sinh `RepositoryMember` đầu tiên).
- Chỉ Global Admin được archive Repository (`IsArchived = true` — soft-hide, không hard-delete).
- Cần quyền `ManageSettings` để add/update/xoá `RepositoryMetadata`.
- Không cho trùng value cho cùng 1 key trong cùng Repository.
- `RepositoryMetadata` hỗ trợ soft-delete (`IsDeleted/DeletedAt/DeletedByUserId`).

## 4. Main Workflows / Use Cases

1. Admin tạo Repository (Name, Code, Description, ScrumMaster đầu tiên).
2. Admin update Name/Description.
3. Admin archive Repository không còn dùng (member mất quyền truy cập).
4. Admin thêm metadata value vào catalog (vd version mới cho FixedInVersion).
5. Admin soft-delete 1 metadata value (vd version cũ không còn dùng).

## 5. Definition of Done

- [ ] Code luôn được validate unique (case-insensitive) trước khi tạo Repository.
- [ ] Tạo Repository luôn sinh đúng 1 RepositoryMember ScrumMaster ban đầu.
- [ ] Archive chỉ thực hiện được bởi Global Admin, không hard-delete data.
- [ ] Add metadata luôn check trùng value trong cùng key + Repository.

## 6. Edge Cases & Notes

- Repository rỗng (chưa có member/backlog) là hợp lệ.
- Archive là soft-hide — có thể khôi phục dữ liệu, không mất data.
