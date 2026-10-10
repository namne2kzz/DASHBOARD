---
key: guides/search
title: Tìm kiếm nhanh (Ctrl/Cmd + K)
module: search
type: UserGuide
language: vi
suggestions:
  - Tìm nhanh một work item theo mã thế nào?
  - Vì sao tìm kiếm không ra kết quả?
---
# Tìm kiếm nhanh (Ctrl/Cmd + K)

Bấm **Ctrl + K** (Windows) hoặc **Cmd + K** (Mac), hoặc mục **Search** trên thanh bên trái, để mở ô tìm kiếm "Search work items, backlog…". Bấm **Esc** để đóng.

## Tìm được gì

- **Work item** trong sprint (Task, Bug, Test Plan, User Story): khớp theo tiêu đề, mô tả, acceptance criteria hoặc **số của item**.
- **Backlog item** (Epic, Feature, User Story): **chỉ khớp theo tiêu đề**.

Gõ mã như `DASH-34`, `dash-34` hoặc chỉ `34` để tìm đúng item số 34.

Kết quả hiện tối đa **6 work item và 6 backlog item**: work item trước (thay đổi gần nhất lên đầu), backlog sau (ưu tiên cao lên đầu). Chọn một kết quả để mở item đó.

## Vì sao không ra kết quả?

- Từ khoá **dưới 2 ký tự**: hệ thống chưa tìm.
- Tìm kiếm **chỉ trong project đang chọn**, không tìm xuyên project. Đổi project nếu item nằm ở project khác.
- Backlog item chỉ khớp theo tiêu đề, không theo mô tả.
- Ô tìm kiếm chỉ mở được khi bạn đang ở trong một project.

Tìm kiếm không bao gồm người dùng, sprint, bình luận hay metadata.
