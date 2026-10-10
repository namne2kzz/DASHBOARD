---
key: guides/boards
title: Boards (bảng Kanban)
module: boards
type: UserGuide
route: /boards
language: vi
suggestions:
  - Vì sao tôi không kéo được card sang cột khác?
  - Làm sao lọc chỉ những việc của tôi?
  - Dynamic query trên board dùng thế nào?
  - Vì sao card không hiện trên board?
---
# Boards (bảng Kanban)

**Boards** là nơi team làm việc hằng ngày: mỗi work item của sprint là một card, nằm trong cột tương ứng với trạng thái của nó.

## Xem board

1. Mở project, vào **Boards**.
2. Chọn sprint. Board hiển thị toàn bộ work item của sprint đó.

Các cột và trạng thái tương ứng được cấu hình ở màn hình **Workflow**. Nếu thấy "No columns configured", project chưa có cột nào; nhờ người quản lý cấu hình ở Workflow.

## Kéo thả card

Kéo card sang cột khác để đổi trạng thái của item. Board cập nhật ngay; nếu lưu lên server thất bại, card **tự quay về cột cũ**.

### Vì sao không kéo được card?

- **Cột đích đã đầy và đang ở chế độ Hard-stop**: cột có WIP limit và đã đủ số card, nên không nhận thêm. Cột ở chế độ Soft-warning thì vẫn cho kéo, chỉ cảnh báo.
- **Trạng thái của cột không hợp lệ với loại item**: mỗi loại work item có bộ trạng thái riêng (xem phần "Work item"), ví dụ Test Plan không có trạng thái In Review.
- **Role của bạn chưa có quyền sửa work item** (Edit Work Item).
- Hai người kéo cùng một card cùng lúc: thao tác lưu sau cùng sẽ thắng.

## Tìm và lọc

- Ô **Filter title or #ID**: lọc theo tiêu đề hoặc số của item.
- Nút **Me**: chỉ hiện việc được giao cho bạn.
- **Dynamic query**: thêm nhiều điều kiện dạng *trường – phép so sánh – giá trị*. Trường gồm Title, Type, Priority, State, Assigned To, Remaining Work, Labels. Phép so sánh gồm equals, not equals, contains, not contains, starts with, is empty, is not empty. Các điều kiện nối với nhau bằng **AND** hoặc **OR** (bắt đầu bằng **Where**). Dòng điều kiện chưa điền đủ sẽ bị bỏ qua.

Bộ lọc chạy ngay trên dữ liệu đang hiển thị và kết hợp với nhau (ô tìm kiếm + Me + dynamic query).

## Tạo work item mới

Bấm **New work item**, chọn loại (Task, Bug, Test Plan), nhập tiêu đề rồi bấm **Create**. Board tự tải lại để hiện item mới. Nếu không thấy nút này, role của bạn chưa có quyền tạo work item.

## Xem và sửa chi tiết

Bấm vào card để mở chi tiết: sửa nội dung, người được giao, ước lượng, bình luận, xem lịch sử. Thay đổi được cập nhật lại lên board.

## Vì sao card không hiện trên board?

- Item thuộc sprint khác với sprint đang chọn.
- Đang bật bộ lọc (ô tìm kiếm, Me hoặc dynamic query).
- Trạng thái của item **không thuộc cột nào** (ví dụ cột vừa bị xoá hoặc đổi trạng thái ở Workflow). Tải lại trang sau khi cấu hình lại cột.

Board chỉ dành cho thành viên của project.
