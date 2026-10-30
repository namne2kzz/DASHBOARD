# Repositories — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-10-27 | 11:00 | Trang quản trị Repository | Thêm **Settings → Repositories** (chỉ Global Admin): list mọi repo trong org kể cả archived, sửa Name/Description, bàn giao Project Manager, archive + **unarchive** (endpoint `POST /{id}/unarchive` mới). Người tạo repo giờ là **Project Manager** thay vì Scrum Master. Archive yêu cầu gõ đúng `Code` của repo để xác nhận. Sửa 2 lỗ: `includeArchived` trước đây bị handler bỏ qua, và archive/update **không filter `OrgId`** nên admin org A thao tác được repo org B. |
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
- Chỉ Global Admin được tạo Repository; tạo Repository tự động gán 1 user chỉ định làm **Project Manager** (sinh `RepositoryMember` đầu tiên).
- Chỉ Global Admin được archive/unarchive Repository (`IsArchived` — soft-hide, không hard-delete). UI bắt gõ đúng `Code` của repo để xác nhận archive.
- Cần quyền `ManageMetadata` để add/update/xoá `RepositoryMetadata`; cần `EditRepository` để sửa thông tin repository (name/description/code).
- `Code` **không sửa được** sau khi tạo: nó là URL segment và prefix của work-item key, đổi sẽ hỏng mọi link đã chia sẻ.
- Bàn giao Project Manager: người mới lên role đó, người cũ **xuống Developer** (vẫn ở lại repo, không bị gỡ).
- Không cho trùng value cho cùng 1 key trong cùng Repository.
- `RepositoryMetadata` hỗ trợ soft-delete (`IsDeleted/DeletedAt/DeletedByUserId`).

## 4. Main Workflows / Use Cases

1. Admin tạo Repository (Name, Code, Description, Project Manager đầu tiên).
2. Admin update Name/Description.
3. Admin archive Repository không còn dùng (member mất quyền truy cập); khôi phục lại từ tab Archived.
3b. Admin bàn giao Project Manager cho member khác trong repo.
4. Admin thêm metadata value vào catalog (vd version mới cho FixedInVersion).
5. Admin soft-delete 1 metadata value (vd version cũ không còn dùng).

## 5. Definition of Done

- [ ] Code luôn được validate unique (case-insensitive) **trong phạm vi `OrgId`** trước khi tạo Repository.
- [ ] Mọi query list repository đều filter theo `OrgId` — không leak repository cross-tenant.
- [ ] Tạo Repository luôn sinh đúng 1 RepositoryMember Project Manager ban đầu.
- [ ] Archive/unarchive/update đều filter theo `OrgId` — admin của tenant này không chạm được repo tenant khác.
- [ ] Archive chỉ thực hiện được bởi Global Admin, không hard-delete data.
- [ ] Add metadata luôn check trùng value trong cùng key + Repository.

## 6. Edge Cases & Notes

- Repository rỗng (chưa có member/backlog) là hợp lệ.
- Archive là soft-hide — có thể khôi phục dữ liệu, không mất data.
- Repository = **workspace của HUB** khi tích hợp chat — xem [hub-integration.dod.md](hub-integration.dod.md).
- Kết nối repository GitHub thật (branch/commit/PR) là feature riêng — xem [git-repositories.dod.md](git-repositories.dod.md).
