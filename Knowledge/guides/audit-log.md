---
key: guides/audit-log
title: Audit log (lịch sử thay đổi của project)
module: history
type: UserGuide
route: /audit-log
language: vi
suggestions:
  - Xem ai đã thay đổi gì trong project ở đâu?
  - Vì sao tôi không thấy mục Audit log?
---
# Audit log

**Audit log** liệt kê các thay đổi trên work item của **toàn project**: tạo mới, đổi trạng thái, giao việc, promote vào sprint… kèm **người thực hiện** (Author) và **thời gian**.

## Lọc

- **Author**: theo người thực hiện (mặc định "All authors").
- **From** – **To**: theo khoảng ngày.
- Theo loại thao tác (**Action**).
- **Clear** để bỏ lọc.

Danh sách tải thêm khi cuộn xuống ("Loading more…").

## Ai xem được?

Chỉ người có quyền **quản lý thành viên** (Manage Members) trong project mới thấy mục **Audit log** trên thanh bên trái. Không thấy mục này nghĩa là role của bạn không có quyền đó.

Mọi thành viên vẫn xem được **lịch sử của từng work item** trong chi tiết item đó.

## Tính chất

Lịch sử được ghi **tự động** và **không thể sửa hay xoá**. Nếu người thực hiện sau này bị xoá khỏi hệ thống, dòng lịch sử vẫn được giữ lại.
