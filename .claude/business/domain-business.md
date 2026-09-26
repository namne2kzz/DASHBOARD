# Domain & Business — Tổng quan Project

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-06-28 | 13:58 | Gộp Role default + custom | `CustomRole` đổi thành `Role` (1 bảng, cờ `IsDefault`); 5 default role global seed từ TeamRole; member dùng `RoleId`. Xem [roles.dod.md](roles.dod.md) |
| 2026-06-23 | 23:10 | Thêm feature Boards | Bổ sung [boards.dod.md](boards.dod.md) — feature Kanban Board (operational view), trước đó bị thiếu trong danh sách |
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document tổng quan domain/business ban đầu cho toàn project |

---

## 1. Project này là gì

Một dashboard quản lý dự án phần mềm theo mô hình Agile/Scrum — tương tự một bản thu nhỏ của Azure DevOps / Jira. Mỗi **Repository** (tương đương 1 "Project") chứa backlog, sprint, board, wiki, member, discussion riêng. Người dùng tạo, refine, lên kế hoạch (planning) và thực thi (execution) công việc qua các sprint, theo dõi tiến độ qua Overview/Burndown.

## 2. Khái niệm domain cốt lõi

| Khái niệm | Entity | Vai trò |
|---|---|---|
| Dự án | `Repository` | Container cao nhất, có `Code` (prefix work-item, vd `DASH`) |
| Thành viên dự án | `RepositoryMember` | Join `User` ↔ `Repository`, gắn `DefaultRole` (chức danh, value RepoRole metadata) + `Role` (quyền) |
| Quyền (role) | `Role` | Tập `SystemFunction` permission; `IsDefault` = role global mặc định, ngược lại = custom role scope theo Repository |
| Backlog | `BacklogItem` | Epic → Feature → UserStory, refine trước khi vào sprint |
| Sprint | `Sprint` | Time-box (StartDate–EndDate), chứa Capacity + Task |
| Công việc thực thi | `SprintTask` | UserStory (promote từ Backlog) / Task / Bug / TestPlan |
| Bảng kanban | `SmartBoardColumn` | Map 1-1 với `SprintTaskState`, có WIP limit |
| Năng lực team | `CapacityMember`, `DayOff` | Giờ làm/sprint, ngày nghỉ, dùng để tính tải |
| Metadata catalog | `RepositoryMetadata` | Key/value chọn được theo key (`MetadataKey`); `IsGlobal` = dùng chung mọi repo, ngược lại theo Repository |
| Tài liệu | `WikiPage` | Cây tài liệu theo Repository |
| Thảo luận | `DiscussionEntry` | Comment gắn vào 1 `SprintTask` |
| Audit trail | `HistoryEntry` | Log tự động mọi thay đổi của `SprintTask` |
| Định danh | `User`, `UserToken` | Tài khoản, JWT/refresh token |
| Mời thành viên | `Invitation` | Mời người ngoài qua email + Google OAuth |

## 3. Luồng nghiệp vụ tổng (end-to-end)

```
User đăng nhập (Auth)
   → vào 1 Repository mà mình là Member (Members / Roles quyết định quyền)
      → tạo/refine BacklogItem (Epic → Feature → UserStory, rank, estimate)
         → khi UserStory ở trạng thái Ready → Promote vào Sprint
            → Sprint có Capacity (CapacityMember + DayOff) để biết team rảnh bao nhiêu
            → BacklogItem được promote thành SprintTask (state New)
               → SprintTask chạy qua SmartBoard (cột = SprintTaskState, có WIP limit)
               → mỗi thay đổi state/assignment/estimate → ghi HistoryEntry tự động
               → team thảo luận qua DiscussionEntry gắn trên SprintTask
            → khi Done → tính vào Velocity/Burndown ở Overview
      → song song: Wiki lưu tài liệu đặc tả, Repositories quản lý metadata (vd FixedInVersion)
```

## 4. Phân quyền (cross-cutting)

- **Global Admin** (`User.IsGlobalAdmin`): tạo Repository, tạo User, archive Repository, toggle admin, xoá comment người khác.
- **Repository-scoped permission**: qua `SystemFunction` (ViewRepository, EditRepository, ManageSettings, CreateWorkItem, EditWorkItem, DeleteWorkItem, ManageSprint, ManageCapacity, ManageWiki, ManageBoard) — quyền lấy **chỉ từ `Role` được gán** (`Role.AllowedFunctions`), GlobalAdmin bypass. `TeamRole` (Dev/Tester/ScrumMaster/ProjectManager) chỉ là chức danh cho capacity, không cấp quyền.
- Mỗi Repository phải còn ít nhất 1 ScrumMaster (không cho remove member cuối cùng có role này).

## 5. Pattern chung xuyên suốt các feature

- **Soft-delete**: hầu hết entity con (WikiPage, DiscussionEntry, RepositoryMetadata, SprintTask...) dùng `IsDeleted/DeletedAt/DeletedByUserId` thay vì xoá cứng. `Repository` dùng riêng `IsArchived`.
- **Audit tự động**: mọi thay đổi `SprintTask` sinh `HistoryEntry` trong cùng transaction (`SaveChangesAsync`), không có API ghi history thủ công.
- **Token/secret không lưu plaintext**: `UserToken` (refresh token) và `Invitation` (invite token) chỉ lưu SHA-256 hash.
- **Identity ổn định qua OAuth**: `User.GoogleSubjectId` là khoá định danh thật khi dùng Google login — email có thể đổi nhưng SubjectId không đổi.
- **Work item numbering**: `SprintTask.WorkItemNumber` tự tăng theo từng Repository, hiển thị dạng `{Repository.Code}-{number}` (vd `DASH-3`).
- **Ranking phân số**: `BacklogItem` dùng midpoint rank (`(prev+next)/2`), tự re-normalize khi khoảng cách quá nhỏ (<0.001) về spacing 1000.

## 6. Danh sách feature document chi tiết

Xem từng file `.dod.md` tương ứng trong cùng thư mục để biết rule/workflow chi tiết:

- [auth.dod.md](auth.dod.md) — đăng nhập, token, logout
- [users.dod.md](users.dod.md) — tài khoản, profile, admin
- [members.dod.md](members.dod.md) — thành viên Repository, role
- [invitations.dod.md](invitations.dod.md) — mời người ngoài qua Google OAuth
- [roles.dod.md](roles.dod.md) — role (default global + custom theo Repository) quyết định permission
- [metadata.dod.md](metadata.dod.md) — catalog key/value (global + repo), gồm key RepoRole (Team Role)
- [boards.dod.md](boards.dod.md) — Kanban board operational view (drag-drop, filter, quick-create)
- [backlog.dod.md](backlog.dod.md) — Epic/Feature/UserStory, refinement
- [sprints.dod.md](sprints.dod.md) — sprint lifecycle, ngày bắt đầu/kết thúc
- [sprint-tasks.dod.md](sprint-tasks.dod.md) — Task/Bug/TestPlan/UserStory execution
- [smart-board.dod.md](smart-board.dod.md) — kanban board, WIP limit
- [capacity.dod.md](capacity.dod.md) — năng lực team, day-off
- [repositories.dod.md](repositories.dod.md) — quản lý Repository, metadata
- [wiki.dod.md](wiki.dod.md) — tài liệu dạng cây
- [discussions.dod.md](discussions.dod.md) — comment trên work item
- [history.dod.md](history.dod.md) — audit trail tự động
- [overview.dod.md](overview.dod.md) — dashboard, burndown, velocity
