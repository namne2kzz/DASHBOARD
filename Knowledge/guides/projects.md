---
key: guides/projects
title: Project (tạo, chuyển, lưu trữ)
module: repositories
type: UserGuide
language: vi
suggestions:
  - Ai được tạo project mới?
  - Code của project có quy tắc gì?
  - Lưu trữ (archive) project thì sao?
---
# Project

Project (trong một số màn hình gọi là "repository") là một dự án trong tổ chức, có backlog, sprint, board, thành viên và phân quyền riêng.

## Chuyển project đang làm việc

Dùng phần chọn project ở đầu thanh bên trái. Các mục trong nhóm **Workspace** (Overview, Boards, Backlog, Sprint…) luôn làm việc với project đang chọn. Chỉ thấy những project mà bạn là thành viên; thấy "No repository access" nghĩa là bạn chưa thuộc project nào, hãy nhờ admin thêm vào.

## Tạo project mới

Chỉ **Global Admin** tạo được project. Cần nhập:

- **Name**: tên project.
- **Code**: mã project, **tối đa 10 ký tự, chỉ gồm chữ hoa và số** (ví dụ `MYPRJ`). Code là tiền tố của mọi work item (`MYPRJ-1`) và phải **không trùng với project khác trong tổ chức**. Form báo "Available." hoặc "Code is not available." ngay khi gõ.
- **Description**: mô tả (tuỳ chọn).
- **Scrum Master**: người quản lý đầu tiên của project; người này có toàn quyền trong project.

Khi tạo project, hệ thống tự sinh **5 role mặc định** (Scrum Master, Project Manager, Developer, Tester, Business Analyst) và 5 giá trị chức danh tương ứng.

Số project tối đa của tổ chức bị giới hạn theo **license** đã mua.

## Sửa thông tin project

Đổi tên, mô tả hoặc code cần quyền **Edit Repository**.

## Lưu trữ project (archive)

Chỉ **Global Admin** lưu trữ được project. Project lưu trữ bị **ẩn đi** chứ không bị xoá dữ liệu. Sau khi lưu trữ:

- Không thêm thành viên hay gửi lời mời mới được.
- Việc trong project đó không còn hiện trong **My Work**.
