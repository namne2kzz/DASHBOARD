# Roles — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-09-26 | 14:02 | Cập nhật lại danh sách SystemFunction | `SystemFunction` giờ có 20 quyền (thêm ManageMembers/ManageRoles/InviteMembers/ManageMetadata/AssignWorkItem/ManageBacklog/PromoteToSprint/ActivateSprint/ViewAnalytics/ManagePipeline/ManageRepo/ManageChannels); **bỏ `ManageSettings` và `ManageWiki`**. Cập nhật lại bộ quyền của 5 default role cho khớp `DefaultRoleDefinitions` |
| 2026-06-28 | 17:00 | Default role per-repo | Bỏ default role global + hardcode GUID (`DefaultRoleIds`). Mỗi repo tự sinh 5 default role + 5 RepoRole discipline lúc `CreateRepository` (template `DefaultRoleDefinitions`). `Role.RepositoryId` luôn set. |
| 2026-06-28 | 16:00 | Chức danh = RepoRole metadata | Chức danh (`DefaultRole`) đổi từ enum `TeamRole` (đã xoá) sang value RepoRole metadata (string). "TeamRole" trong doc dưới đây = chức danh lấy từ metadata. |
| 2026-06-28 | 14:30 | Role là nguồn quyền duy nhất | Bỏ `RolePrivilegeService`: permission = chỉ `Role.AllowedFunctions` (GlobalAdmin bypass). `TeamRole` thành chức danh thuần (không còn quyền). `RepositoryMember.RoleId` thành **bắt buộc** (NOT NULL). |
| 2026-06-28 | 13:58 | Gộp default + custom role | Đổi `CustomRole` → `Role` (1 bảng), thêm `IsDefault`; seed 5 default role global từ TeamRole (SM/PM/Dev/Test/BA); default role không sửa/xoá được; member trỏ `RoleId` thay `CustomRoleId`. |
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document business ban đầu cho feature CustomRoles |

---

## 1. Purpose

Định nghĩa quyền chi tiết (granular permission) gán cho member của Repository. Mỗi Repository có bộ **default role** riêng (tạo tự động lúc tạo repo, không sửa được) và **custom role** (do admin tự tạo). Tất cả đều scope theo Repository.

## 2. Key Entities & Relationships

- `Role`: có `Name`, `Description`, danh sách `SystemFunction` được phép (lưu JSON), và 2 field phân loại:
  - `IsDefault`: `true` = default role (tạo tự động lúc tạo repo, không sửa được); `false` = custom role.
  - `RepositoryId`: **luôn set** (mọi role thuộc 1 Repository — không còn role global).
- `SystemFunction` (enum) — 20 quyền, nhóm theo chức năng:
  - **Repository**: `ViewRepository`, `EditRepository`
  - **Members & Access**: `ManageMembers`, `ManageRoles`, `InviteMembers`, `ManageMetadata`
  - **Work Item**: `CreateWorkItem`, `EditWorkItem`, `DeleteWorkItem`, `AssignWorkItem`
  - **Backlog**: `ManageBacklog`, `PromoteToSprint`
  - **Sprint**: `ManageSprint`, `ActivateSprint`
  - **Capacity**: `ManageCapacity`
  - **Board**: `ManageBoard`
  - **Analytics & Integration**: `ViewAnalytics`, `ManagePipeline`, `ManageRepo`
  - **Collaboration (HUB)**: `ManageChannels`

  > `ManageSettings` và `ManageWiki` **đã bị bỏ**: quyền settings tách thành `ManageMembers`/`ManageRoles`/`ManageMetadata`/`ManageRepo`; Wiki chuyển sang HUB.

- **Mỗi Repository tự có 5 default role** (sinh lúc `CreateRepository` từ template `DefaultRoleDefinitions`, không hardcode GUID):

  | Default role | Quyền |
  |---|---|
  | **Scrum Master** | **Tất cả** `SystemFunction` |
  | **Project Manager** | **Tất cả** `SystemFunction` (hiện giống Scrum Master) |
  | **Developer** | View + Create/Edit/AssignWorkItem + ViewAnalytics + ManagePipeline + ManageRepo |
  | **Tester** | View + Create/Edit/AssignWorkItem + ManageBacklog + ViewAnalytics |
  | **Business Analyst** | View + Create/Edit/Delete/AssignWorkItem + ManageBacklog + PromoteToSprint + ManageSprint + ActivateSprint + ViewAnalytics |

  > Tên 5 default role cũng được dùng làm value chức danh (`RepoRole` metadata) — xem [metadata.dod.md](metadata.dod.md).
- `RepositoryMember.RoleId`: **bắt buộc (NOT NULL)** — trỏ tới đúng 1 `Role` (default hoặc custom). Đây là **nguồn quyền duy nhất** của member.
- `RepositoryMember.DefaultRole` (string, value RepoRole metadata): chỉ là **chức danh** (discipline) cho capacity/planning, **không cấp quyền gì**.

## 3. Business Rules & Invariants

- Cần quyền `ManageRoles` để create/update/delete/clone role.
- **Default role không sửa được, không xoá được** (`Update`/`Delete` trả lỗi "Default roles cannot be modified/deleted"). Chỉ custom role mới sửa/xoá được.
- Tên custom role unique trong phạm vi 1 Repository (default role không tính vào check unique).
- Tên không được rỗng; description optional.
- Không cho xoá custom role nếu còn member đang gán role đó — phải reassign hết trước.
- `AllowedFunctions` khi update sẽ **replace toàn bộ** danh sách cũ, không merge.
- Clone: clone được **cả default lẫn custom** role → luôn ra 1 **custom role mới** (`IsDefault = false`) cùng permission, name mới, scope theo Repository hiện tại.
- **Permission của member = đúng `AllowedFunctions` của role được gán** (GlobalAdmin bypass có hết quyền). Chức danh (`DefaultRole`) KHÔNG cộng quyền. Không còn merge "default ∪ custom".
- `RoleId` bắt buộc: khi add member phải gán 1 role (mặc định auto chọn default role khớp chức danh: Dev→Developer...). Không có trạng thái "no role".
- Guard chống mất admin: không cho đổi/xoá khiến repo **không còn member nào có quyền `ManageMembers`** (dựa permission thật, không dựa chức danh SM) — `ManageSettingsGuard`.
- `ListRoles` của 1 Repository trả về tất cả role của Repository đó (default + custom), default role xếp trước.

## 4. Main Workflows / Use Cases

1. Tạo Repository → tự sinh 5 default role + 5 RepoRole discipline cho repo đó; creator thành Scrum Master.
2. Admin tạo custom role (name, description, list permission) cho Repository.
3. Admin clone 1 role (default hoặc custom) → custom role mới cùng permission, tên khác.
4. Admin update custom role → áp dụng ngay cho mọi member đang gán role đó (default role bị chặn).
5. Admin gán/bỏ gán role cho member (chọn trong danh sách default + custom).
6. Admin xoá custom role (chỉ khi không còn member nào gán; default role không xoá được).

## 5. Definition of Done

- [ ] Default role bị chặn update/delete; chỉ custom role mới sửa/xoá được.
- [ ] Validate tên custom role unique trong Repository (bỏ qua default) trước khi create/update.
- [ ] Update `AllowedFunctions` luôn replace toàn bộ, không append.
- [ ] Xoá custom role luôn check còn member gán hay không trước khi cho phép.
- [ ] Clone luôn copy đúng permission gốc, sinh role mới `IsDefault = false`, gán đúng Repository.
- [ ] `ListRoles` trả về default + custom của repo, member count đếm theo từng Repository.
- [ ] Tạo repo tự sinh 5 default role + 5 RepoRole discipline; không hardcode GUID trong business logic.

## 6. Edge Cases & Notes

- Chức danh (`DefaultRole`, từ RepoRole metadata) hoàn toàn độc lập với `Role` (quyền) — đổi chức danh không đổi quyền và ngược lại. Muốn "Dev1 khác quyền Dev2" thì tạo custom role riêng rồi gán, chức danh vẫn để Developer.
- Default role giờ scope theo Repository (mỗi repo 1 bộ riêng), sinh lúc tạo repo từ template `DefaultRoleDefinitions` (không reference GUID seed). `Role.RepositoryId` về mặt cột vẫn nullable nhưng thực tế luôn được set.
- Permission list rỗng là hợp lệ (role "audit-only", không có quyền gì) — chỉ áp dụng cho custom role.
- Nhiều member (kể cả khác Repository với default role) có thể share cùng 1 role.
- Khi migrate dữ liệu cũ: member chưa có role được tự động link tới default role tương ứng với `TeamRole` hiện tại (Dev→Developer, Tester→Tester, ScrumMaster→Scrum Master, ProjectManager→Project Manager).
