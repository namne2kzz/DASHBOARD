# Repositories — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-09-26 | 14:02 | Scope theo Organization | `Code` chỉ unique **trong 1 Organization** (không còn unique toàn hệ thống); repository gắn `OrgId`. `LicenseRepoCapacity` của Organization giới hạn số repository. Xem [organizations.dod.md](organizations.dod.md) |
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document business ban đầu cho feature Repositories |

---

## 1. Purpose

Quản lý "Repository" — container dự án (tương đương Project trong Jira/Azure DevOps). Mỗi Repository có code prefix riêng dùng để đánh số work item (vd `DASH-1`).

## 2. Key Entities & Relationships

- `Repository`: `OrgId` (tenant sở hữu), Name, `Code` (**unique theo từng Organization**, tối đa 10 ký tự, chữ hoa + số), Description, `IsArchived`.
- `RepositoryMember`: many-to-many User ↔ Repository, gắn `DefaultRole` (chức danh) + optional `Role` (quyền) — xem [members.dod.md](members.dod.md) và [roles.dod.md](roles.dod.md).
- `RepositoryMetadata`: catalog key-value chọn sẵn theo Repository (vd key "FixedInVersion" với value "1.4.1", "1.5.0"...).

## 3. Business Rules & Invariants

- `Code` **unique trong phạm vi `OrgId`** (không phải toàn hệ thống — 2 tenant đều có thể dùng code `DASH`), normalize uppercase, chỉ chữ hoa + số, tối đa 10 ký tự.
- Repository mới luôn được gán vào `OrgId` của tenant hiện tại; số repository bị giới hạn bởi `Organization.LicenseRepoCapacity`.
- Chỉ Global Admin được tạo Repository; tạo Repository tự động gán 1 user chỉ định làm ScrumMaster (sinh `RepositoryMember` đầu tiên).
- Chỉ Global Admin được archive Repository (`IsArchived = true` — soft-hide, không hard-delete).
- Cần quyền `ManageMetadata` để add/update/xoá `RepositoryMetadata`; cần `EditRepository` để sửa thông tin repository (name/description/code).
- Không cho trùng value cho cùng 1 key trong cùng Repository.
- `RepositoryMetadata` hỗ trợ soft-delete (`IsDeleted/DeletedAt/DeletedByUserId`).

## 4. Main Workflows / Use Cases

1. Admin tạo Repository (Name, Code, Description, ScrumMaster đầu tiên).
2. Admin update Name/Description.
3. Admin archive Repository không còn dùng (member mất quyền truy cập).
4. Admin thêm metadata value vào catalog (vd version mới cho FixedInVersion).
5. Admin soft-delete 1 metadata value (vd version cũ không còn dùng).

## 5. Definition of Done

- [ ] Code luôn được validate unique (case-insensitive) **trong phạm vi `OrgId`** trước khi tạo Repository.
- [ ] Mọi query list repository đều filter theo `OrgId` — không leak repository cross-tenant.
- [ ] Tạo Repository luôn sinh đúng 1 RepositoryMember ScrumMaster ban đầu.
- [ ] Archive chỉ thực hiện được bởi Global Admin, không hard-delete data.
- [ ] Add metadata luôn check trùng value trong cùng key + Repository.

## 6. Edge Cases & Notes

- Repository rỗng (chưa có member/backlog) là hợp lệ.
- Archive là soft-hide — có thể khôi phục dữ liệu, không mất data.
- Repository = **workspace của HUB** khi tích hợp chat — xem [hub-integration.dod.md](hub-integration.dod.md).
- Kết nối repository GitHub thật (branch/commit/PR) là feature riêng — xem [git-repositories.dod.md](git-repositories.dod.md).
