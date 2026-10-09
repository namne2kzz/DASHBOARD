# Domain & Business — Tổng quan Project

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-10-09 | 21:40 | Thêm trợ lý AI NMate | Thêm [nmate.dod.md](nmate.dod.md): widget hỏi đáp cách dùng hệ thống, trả lời chỉ dựa trên tài liệu end-user ở `Knowledge/` (gốc repo). Logic AI ở service riêng SUPPORT; DASHBOARD chỉ proxy `/api/v1/nmate/*` (đăng nhập + rate limit theo user). Không thêm entity nào vào DASHBOARD |
| 2026-09-26 | 14:02 | Multi-tenancy + tích hợp HUB, bỏ Wiki | Thêm `Organization` làm tenant cấp cao nhất (User/Repository đều thuộc 1 org; email/code chỉ unique trong org; login cần org alias). Tích hợp HUB Chat (kênh chat theo sprint, internal API). Thêm `UserSetting` (preference + avatar MinIO). **Feature Wiki đã bỏ khỏi DASHBOARD** (chuyển sang HUB) — `WikiPage` không còn tồn tại. Cập nhật lại danh sách document cho khớp thực tế |
| 2026-06-28 | 13:58 | Gộp Role default + custom | `CustomRole` đổi thành `Role` (1 bảng, cờ `IsDefault`); 5 default role global seed từ TeamRole; member dùng `RoleId`. Xem [roles.dod.md](roles.dod.md) |
| 2026-06-23 | 23:10 | Thêm feature Boards | Bổ sung [boards.dod.md](boards.dod.md) — feature Kanban Board (operational view), trước đó bị thiếu trong danh sách |
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document tổng quan domain/business ban đầu cho toàn project |

---

## 1. Project này là gì

Một dashboard quản lý dự án phần mềm theo mô hình Agile/Scrum — tương tự một bản thu nhỏ của Azure DevOps / Jira. Sản phẩm bán theo license cho từng công ty, mỗi công ty là 1 **Organization** (tenant) độc lập. Trong 1 Organization, mỗi **Repository** (tương đương 1 "Project") chứa backlog, sprint, board, member, discussion riêng. Người dùng tạo, refine, lên kế hoạch (planning) và thực thi (execution) công việc qua các sprint, theo dõi tiến độ qua Overview/Burndown.

DASHBOARD đi kèm **HUB** (sản phẩm chat riêng): mỗi sprint có thể có 1 kênh chat HUB tự tạo, và tài liệu (Wiki) đã được chuyển hẳn sang HUB — DASHBOARD không còn quản lý Wiki.

## 2. Khái niệm domain cốt lõi

| Khái niệm | Entity | Vai trò |
|---|---|---|
| Tenant (công ty) | `Organization` | Container **cao nhất** — 1 license = 1 org; `Alias` là tenant prefix trong route |
| Dự án | `Repository` | Container dự án trong 1 org, có `Code` (prefix work-item, vd `DASH`) — unique theo org |
| Thành viên dự án | `RepositoryMember` | Join `User` ↔ `Repository`, gắn `DefaultRole` (chức danh, value RepoRole metadata) + `Role` (quyền) |
| Quyền (role) | `Role` | Tập `SystemFunction` permission; `IsDefault` = 1 trong 5 role sinh tự động cho repo (không sửa/xoá được), ngược lại = custom role. Cả 2 đều scope theo Repository (`RepositoryId` luôn set) |
| Backlog | `BacklogItem` | Epic → Feature → UserStory, refine trước khi vào sprint |
| Sprint | `Sprint` | Time-box (StartDate–EndDate), chứa Capacity + Task |
| Công việc thực thi | `SprintTask` | UserStory (promote từ Backlog) / Task / Bug / TestPlan |
| Bảng kanban | `SmartBoardColumn` | Map 1-1 với `SprintTaskState`, có WIP limit |
| Năng lực team | `CapacityMember`, `DayOff` | Giờ làm/sprint, ngày nghỉ, dùng để tính tải |
| Metadata catalog | `RepositoryMetadata` | Key/value chọn được theo key (`MetadataKey`); `IsGlobal` = dùng chung mọi repo, ngược lại theo Repository |
| Thảo luận | `DiscussionEntry` | Comment gắn vào 1 `SprintTask` |
| Audit trail | `HistoryEntry` | Log tự động mọi thay đổi của `SprintTask` |
| Định danh | `User`, `UserToken` | Tài khoản (thuộc 1 org), JWT/refresh token |
| Preference | `UserSetting` | Key-value setting của user (date format, timezone, theme, avatar URL...) |
| Mời thành viên | `Invitation` | Mời người ngoài qua email + Google OAuth |
| Kênh chat sprint | `SprintChannelLink` | Link 1-1 sprint ↔ kênh chat bên HUB |
| Catalog work item | `WorkItemMetadata` | Label/component/version gắn vào work item |

> **Đã bỏ**: `WikiPage` (feature Wiki chuyển sang HUB từ 2026-09-05) — DASHBOARD không còn entity/API Wiki.

## 3. Luồng nghiệp vụ tổng (end-to-end)

```
User đăng nhập (Auth: org alias + email + password) → gắn vào 1 Organization (tenant)
   → vào 1 Repository mà mình là Member (Members / Roles quyết định quyền)
      → tạo/refine BacklogItem (Epic → Feature → UserStory, rank, estimate)
         → khi UserStory ở trạng thái Ready → Promote vào Sprint
            → Sprint có thể tự tạo 1 kênh chat bên HUB (SprintChannelLink) để team thảo luận
            → Sprint có Capacity (CapacityMember + DayOff) để biết team rảnh bao nhiêu
               (capacity member cũng được sync thành thành viên kênh chat HUB)
            → BacklogItem được promote thành SprintTask (state New)
               → SprintTask chạy qua SmartBoard (cột = SprintTaskState, có WIP limit)
               → mỗi thay đổi state/assignment/estimate → ghi HistoryEntry tự động
               → team thảo luận qua DiscussionEntry gắn trên SprintTask
            → khi Done → tính vào Velocity/Burndown ở Overview
      → song song: Repositories quản lý metadata (vd FixedInVersion); tài liệu đặc tả nằm ở HUB
```

## 4. Phân quyền (cross-cutting)

- **Organization** (`Organization`): lớp isolation ngoài cùng — dữ liệu không bao giờ nhìn xuyên tenant. Xem [organizations.dod.md](organizations.dod.md).
- **Global Admin** (`User.IsGlobalAdmin`): tạo Repository, tạo User, archive Repository, toggle admin, set manager, xoá comment người khác. Bypass permission **trong phạm vi Organization của mình** — không phải super-admin xuyên tenant.
- **Repository-scoped permission**: qua `SystemFunction` — quyền lấy **chỉ từ `Role` được gán** (`Role.AllowedFunctions`), GlobalAdmin bypass. Chức danh (`DefaultRole` — value `RepoRole` metadata, thay cho enum `TeamRole` đã xoá) chỉ để phân loại/capacity, **không** cấp quyền. Danh sách `SystemFunction` hiện tại:

  | Nhóm | Function |
  |---|---|
  | Repository | `ViewRepository`, `EditRepository` |
  | Members & Access | `ManageMembers`, `ManageRoles`, `InviteMembers`, `ManageMetadata` |
  | Work Item | `CreateWorkItem`, `EditWorkItem`, `DeleteWorkItem`, `AssignWorkItem` |
  | Backlog | `ManageBacklog`, `PromoteToSprint` |
  | Sprint | `ManageSprint`, `ActivateSprint` |
  | Capacity | `ManageCapacity` |
  | Board | `ManageBoard` |
  | Analytics & Integration | `ViewAnalytics`, `ManagePipeline`, `ManageRepo` |
  | Collaboration (HUB) | `ManageChannels` |

  > Lưu ý: `ManageWiki` **đã bị bỏ** (feature Wiki chuyển sang HUB). `ManageSettings` không còn là `SystemFunction` — quyền settings đã tách thành `ManageMembers`/`ManageRoles`/`ManageMetadata`/`ManageRepo`.
- Mỗi Repository phải luôn còn **ít nhất 1 member có quyền `ManageMembers`** (`ManageSettingsGuard`) — guard dựa trên permission thật, **không** dựa chức danh ScrumMaster.

## 5. Pattern chung xuyên suốt các feature

- **Soft-delete**: hầu hết entity con (DiscussionEntry, RepositoryMetadata, SprintTask, User...) dùng `IsDeleted/DeletedAt/DeletedByUserId` thay vì xoá cứng. `Repository` dùng riêng `IsArchived`.
- **Tenant scoping**: `User` và `Repository` đều mang `OrgId`. Mọi uniqueness đều scope theo org (`OrgId`+Email, `OrgId`+Code) — **không** unique toàn hệ thống.
- **Audit tự động**: mọi thay đổi `SprintTask` sinh `HistoryEntry` trong cùng transaction (`SaveChangesAsync`), không có API ghi history thủ công.
- **Token/secret không lưu plaintext**: `UserToken` (refresh token) và `Invitation` (invite token) chỉ lưu SHA-256 hash.
- **Identity ổn định qua OAuth**: `User.GoogleSubjectId` là khoá định danh thật khi dùng Google login — email có thể đổi nhưng SubjectId không đổi.
- **Work item numbering**: `SprintTask.WorkItemNumber` tự tăng theo từng Repository, hiển thị dạng `{Repository.Code}-{number}` (vd `DASH-3`).
- **Ranking phân số**: `BacklogItem` dùng midpoint rank (`(prev+next)/2`), tự re-normalize khi khoảng cách quá nhỏ (<0.001) về spacing 1000.
- **Tích hợp ngoài là best-effort**: mọi call sang HUB (tạo channel, sync member, archive) đều log lỗi và **không throw** — HUB down không bao giờ làm fail nghiệp vụ DASHBOARD. Xem [hub-integration.dod.md](hub-integration.dod.md).
- **Setting key-value không cần migration**: `UserSetting` lưu preference dạng (key, value) — thêm setting mới chỉ cần thêm string key trong `UserSettingKeys`, không đổi schema.
- **Upload file không qua API server**: avatar dùng presigned URL 2 bước (request URL → browser PUT thẳng lên MinIO → confirm), server verify ownership + sự tồn tại của object trước khi lưu. Xem [user-settings.dod.md](user-settings.dod.md).
- **Internal API cho service-to-service**: `/internal/v1/*` dùng header `X-Internal-Token` (không JWT), read-only, không expose Swagger public.

## 6. Danh sách feature document chi tiết

Xem từng file `.dod.md` tương ứng trong cùng thư mục để biết rule/workflow chi tiết:

**Tenant & định danh**

- [organizations.dod.md](organizations.dod.md) — Organization (tenant), license, scope dữ liệu
- [auth.dod.md](auth.dod.md) — đăng nhập (org alias + email/password, Google), token, logout
- [users.dod.md](users.dod.md) — tài khoản, profile, admin, org hierarchy
- [user-settings.dod.md](user-settings.dod.md) — preference key-value, upload avatar MinIO
- [invitations.dod.md](invitations.dod.md) — mời người ngoài qua email + Google OAuth

**Repository & phân quyền**

- [repositories.dod.md](repositories.dod.md) — quản lý Repository, metadata
- [members.dod.md](members.dod.md) — thành viên Repository, role
- [roles.dod.md](roles.dod.md) — role (default global + custom theo Repository) quyết định permission
- [metadata.dod.md](metadata.dod.md) — catalog key/value (global + repo), gồm key RepoRole (Team Role)
- [git-repositories.dod.md](git-repositories.dod.md) — kết nối GitHub thật (branch/commit/PR, webhook, cache)

**Planning & execution**

- [backlog.dod.md](backlog.dod.md) — Epic/Feature/UserStory, refinement, bulk operation
- [sprints.dod.md](sprints.dod.md) — sprint lifecycle, ngày bắt đầu/kết thúc, link kênh chat HUB
- [sprint-tasks.dod.md](sprint-tasks.dod.md) — Task/Bug/TestPlan/UserStory execution
- [capacity.dod.md](capacity.dod.md) — năng lực team, day-off, sync member kênh chat
- [workflow.dod.md](workflow.dod.md) — cấu hình cột board + WIP limit (backend tên `SmartBoard`)
- [boards.dod.md](boards.dod.md) — Kanban board operational view (drag-drop, filter, quick-create)

**Theo dõi & tra cứu**

- [overview.dod.md](overview.dod.md) — dashboard, burndown, velocity
- [my-work.dod.md](my-work.dod.md) — dashboard cá nhân xuyên repository
- [search.dod.md](search.dod.md) — command palette Ctrl/Cmd+K
- [discussions.dod.md](discussions.dod.md) — comment trên work item
- [history.dod.md](history.dod.md) — audit trail tự động

**Tích hợp**

- [hub-integration.dod.md](hub-integration.dod.md) — kênh chat HUB theo sprint, internal API cho HUB
- [nmate.dod.md](nmate.dod.md) — trợ lý AI NMate: proxy sang service SUPPORT, trả lời dựa trên `Knowledge/`

> **Đã bỏ**: Wiki (`wiki.dod.md`) — feature chuyển sang HUB từ 2026-09-05, DASHBOARD không còn code Wiki.
