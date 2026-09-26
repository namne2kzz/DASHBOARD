# Master Test Plan — UI Self-Test

> Đọc `RULES.md` trước khi sửa file này.

## 1. Mục tiêu

Thay thế việc test tay từng chức năng trên UI — dùng Playwright MCP (Edge) để Claude tự lái browser, kiểm tra các flow chính, và tích lũy kết quả/bug/improvement qua nhiều lần chạy.

## 2. Môi trường

- **App chạy native, KHÔNG qua Docker.** Backend: `dotnet run` trong `DASHBOARD/` → `http://localhost:5152`. Frontend: `npm start` (hoặc `ng serve`) trong `DASHBOARD.VIEW/` → `http://localhost:4200`.
- **Không có endpoint `/health`.** Xác nhận app sống bằng cách `browser_navigate` thẳng tới `baseUrl` — nếu lỗi connection refused / DNS thì coi như chưa chạy, dừng và nhắc user start app. Không gọi health-check endpoint nào.
- Backing services (SQL Server, RabbitMQ, Mailpit) qua `docker compose up -d` nếu cần — không bắt buộc nếu DB đã có sẵn data.
- Credential test đọc từ `.claude/self-test.local.json` (gitignored): `baseUrl`, `email`, `password`, `repoCode`.

## 3. Quy trình chung (mỗi module)

1. Đọc `.claude/business/{feature}.dod.md` (nếu có) để biết business rule hiện tại.
2. Đọc `.claude/self-test/modules/{feature}.test.md` — nếu Phần B (Test Cases) chưa có case nào, tự soạn case dựa trên `.dod.md` rồi ghi vào trước khi chạy.
3. Chạy từng case bằng Playwright: `navigate` → `snapshot` → thao tác → assert. Chụp `screenshot` khi Fail.
4. Ghi kết quả: append Run Log của module, cập nhật "Trạng thái lần chạy gần nhất" từng case.
5. Sai behavior so với `.dod.md` → thêm `bugs.md`. Ý tưởng cải tiến (không phải lỗi) → thêm `improvements.md`.

## 4. Danh sách module

| Module | File test | Business doc | Ưu tiên |
|--------|-----------|---------------|---------|
| Auth (login) | [auth.test.md](modules/auth.test.md) | [auth.dod.md](../business/auth.dod.md) | Cao — chặn mọi flow khác |
| Boards (Kanban) | [boards.test.md](modules/boards.test.md) | [boards.dod.md](../business/boards.dod.md) | Cao |
| Backlog | [backlog.test.md](modules/backlog.test.md) | [backlog.dod.md](../business/backlog.dod.md) | Cao |
| Wiki | [wiki.test.md](modules/wiki.test.md) | [wiki.dod.md](../business/wiki.dod.md) | Trung bình |
| Sprints | [sprints.test.md](modules/sprints.test.md) | [sprints.dod.md](../business/sprints.dod.md) | Trung bình |
| Sprint Tasks | [sprint-tasks.test.md](modules/sprint-tasks.test.md) | [sprint-tasks.dod.md](../business/sprint-tasks.dod.md) | Trung bình |
| Board Item Detail (dialog) | [board-detail.test.md](modules/board-detail.test.md) | [sprint-tasks.dod.md](../business/sprint-tasks.dod.md), [discussions.dod.md](../business/discussions.dod.md), [history.dod.md](../business/history.dod.md) | Trung bình |
| Workflow (Smart Board) | [workflow.test.md](modules/workflow.test.md) | [workflow.dod.md](../business/workflow.dod.md) | Trung bình |
| Capacity (Sprint Planning) | [capacity.test.md](modules/capacity.test.md) | [capacity.dod.md](../business/capacity.dod.md) | Trung bình |
| Members | [members.test.md](modules/members.test.md) | [members.dod.md](../business/members.dod.md) | **Cao** — BUG-004/BUG-005 đang Open |
| Roles | [roles.test.md](modules/roles.test.md) | [roles.dod.md](../business/roles.dod.md) | **Cao** — BUG-004/BUG-005 đang Open |
| Invitations | [invitations.test.md](modules/invitations.test.md) | [invitations.dod.md](../business/invitations.dod.md) | Thấp — feature WIP, xem `wip-features.md` |
| Repositories (repos page) | [repositories.test.md](modules/repositories.test.md) | [repositories.dod.md](../business/repositories.dod.md) | Trung bình |
| Discussions | [discussions.test.md](modules/discussions.test.md) | [discussions.dod.md](../business/discussions.dod.md) | Thấp |
| Overview | [overview.test.md](modules/overview.test.md) | [overview.dod.md](../business/overview.dod.md) | Thấp |
| Users (settings/users) | [users.test.md](modules/users.test.md) | [users.dod.md](../business/users.dod.md) | **Cao** — BUG-003 (Critical) đang Open |

Module UI chưa có business doc tương ứng (analytics, pipelines) — chưa tạo file test, sẽ tạo khi cần test theo khám phá UI thuần (ghi rõ "chưa có business doc" trong file).

## 5. Phạm vi KHÔNG cover

- Không test unit/integration backend (đã có ở `unit-testing`/`testcontainers` skill riêng).
- Không test load/performance.
- Không tự sửa code khi phát hiện bug trong lúc self-test — chỉ ghi nhận, để user quyết định fix.
