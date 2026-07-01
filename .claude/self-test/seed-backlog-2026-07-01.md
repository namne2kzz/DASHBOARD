# Backlog Seed Data — 2026-07-01

> Tham chiếu nhanh cho seed data backlog tạo trong repo **DASH** (Dashboard Project) ngày 2026-07-01, dùng để test backlog.test.md và làm dữ liệu nền cho việc test các feature khác sau này (Sprint, Board, Overview...). **Không xoá** trừ khi được yêu cầu rõ ràng — xem `.claude/self-test/bugs.md` (BUG-009, BUG-010) và `improvements.md` (IMP-002) được phát hiện từ chính đợt seed/test này.

## Cấu trúc

7 Epic gốc, mỗi Epic chứa Feature, mỗi Feature chứa UserStory — đại diện cho **toàn bộ module thật** của chính app Dashboard (lấy từ `.claude/histories/*.dod.md`), đánh dấu theo trạng thái phát triển thật:

| Epic | Trạng thái | Ghi chú |
|------|------------|---------|
| Identity & Access Management | Done | Authentication, User Account Management, Repository Members & Permission Roles (Done) + Invitations (In Progress) |
| Project & Workspace Management | Done | Repositories (In Progress — settings UI), Repository Metadata Catalog (Done) |
| Agile Planning | Done | Product Backlog, Sprints, Capacity Planning |
| Execution & Delivery | Done | Sprint Tasks, Workflow Board Config, Kanban Board (+ Dynamic Query In Progress), Discussions, History |
| Knowledge & Insights | In Progress | Team Wiki, Repository Overview Dashboard — cả 2 đang WIP (xem `wip-features.md`) |
| DevOps & Delivery Pipeline | Planned | Chưa có business doc, chỉ có nav placeholder `/pipelines` |
| Platform Enhancements (proposed) | Proposed | 6 feature do Claude đề xuất thêm (Notifications, Global Search, Reporting Exports, Security Hardening 2FA/audit log, Roadmap Timeline, Slack/Teams Integration) — chưa nằm trong roadmap thật, chờ product owner quyết định |

## Mapping trạng thái → BacklogItemState

- **Done** → `Ready` (đã refine xong, estimate đầy đủ). 2 UserStory mẫu được Promote thật vào sprint "Sprint 1 July 2026" (active) để minh hoạ workflow đầy đủ → `Committed` + có `SprintTask` thật:
  - "Drag-and-drop reorder backlog items with fractional ranking" (dưới Epic "Agile Planning" → Feature "Product Backlog")
  - "Add an existing user as a repository member with a chosen role" (dưới Epic "Identity & Access Management" → Feature "Repository Members & Permission Roles")
- **In Progress** → `Refining`.
- **Planned** / **Proposed** → `New`, không estimate. AcceptanceCriteria của item Proposed có prefix `[Proposed enhancement by Claude, not yet scheduled]`, item Planned có prefix `[Planned, not started]` để phân biệt rõ với Done/In Progress khi đọc list.

## Lưu ý khi dùng lại

- Item gốc có sẵn từ trước (`Authentication & Authorization` Epic + UserStory "User can log in with email and password", id `00000000-0000-0000-0008-...`) **không bị đụng tới** — vẫn còn nguyên trong backlog, độc lập với cây mới.
- Đã rename 1 item trong lúc test (backlog-32): "Reset forgotten password via emailed link" → "Reset forgotten password via emailed link (renamed)".
- 1 item đã gán `sprintId` = "Sprint 1 July 2026" trong lúc test (backlog-22): "Reset forgotten password via emailed link (renamed)" — state vẫn `Refining`, chỉ là planning tạm, không phải đã promote.
- Dữ liệu test tạm thời (role "ZZZ No Permissions", vài BacklogItem tên "TEMP ...") đã được dọn sạch sau khi test xong — không còn sót trong DB.
- Toàn bộ id thật của từng item nằm trong tool-call log của phiên chat 2026-07-01 (không lưu lại đầy đủ ở đây) — cần lấy lại qua `GET /api/repositories/{DASH}/backlog` nếu cần thao tác trực tiếp theo id.
