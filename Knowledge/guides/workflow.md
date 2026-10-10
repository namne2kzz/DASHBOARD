---
key: guides/workflow
title: Workflow (cấu hình cột board)
module: workflow
type: UserGuide
route: /workflow
language: vi
suggestions:
  - WIP limit Soft-warning và Hard-stop khác nhau thế nào?
  - Làm sao thêm hoặc sắp xếp lại cột trên board?
  - Aging limit dùng để làm gì?
---
# Workflow (cấu hình cột board)

Màn hình **Workflow** cấu hình các cột của board cho từng project. Mỗi project có cấu hình riêng.

## Mỗi cột gồm

- **Column**: tên cột.
- **State**: trạng thái work item mà cột này đại diện. Card có trạng thái nào sẽ nằm ở cột tương ứng. Có thể có nhiều cột cùng trạng thái nếu muốn chia nhỏ quy trình.
- **WIP limit**: số card tối đa trong cột; 0 là không giới hạn.
- **Mode**: cách xử lý khi vượt WIP limit:
  - **Soft-warning**: vẫn cho kéo card vào, chỉ cảnh báo.
  - **Hard-stop**: không cho kéo thêm card vào cột đã đầy.
- **Aging limit**: số ngày tối đa một card nên nằm trong cột. Card nằm lâu hơn bị gắn cờ cảnh báo để team chú ý; hệ thống không chặn.

## Thao tác

- Thêm cột mới: cột mới được đặt cuối cùng bên phải.
- Sửa cột (**Edit column**): đổi tên, trạng thái, WIP limit, mode, aging limit, rồi **Save**.
- Sắp xếp lại thứ tự cột từ trái sang phải.

## Lưu ý

- Đổi trạng thái của một cột **không tự đổi trạng thái** các card cũ. Card giữ trạng thái cũ cho tới khi được kéo sang cột khác. Card có trạng thái không còn cột nào sẽ không hiện trên board.
- Board có thể tạm vượt WIP limit nếu trạng thái item bị đổi từ nơi khác (không phải kéo thả trên board).
