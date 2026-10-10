---
key: faq
title: Câu hỏi thường gặp
module: general
type: Faq
language: vi
suggestions:
  - Vì sao tôi không thấy một chức năng?
  - Có những phím tắt nào?
  - NMate trả lời dựa trên đâu?
  - Vì sao tôi không thấy project nào?
---
# Câu hỏi thường gặp

## Vì sao tôi không thấy một chức năng mà đồng nghiệp lại thấy?

Mỗi người có **role** riêng trong từng project, và nút hay menu chỉ hiện khi role có quyền tương ứng. Ví dụ Audit log và Settings → Members cần quyền Manage Members; Settings → Users chỉ dành cho Global Admin. Hãy nhờ Scrum Master xem lại role của bạn. Xem bảng quyền trong phần "Thành viên và phân quyền".

## Vì sao tôi không thấy project nào ("No repository access")?

Bạn chưa là thành viên của project nào trong tổ chức. Nhờ Scrum Master thêm bạn vào ở **Settings → Members**, hoặc nhờ admin gửi lời mời.

## Tôi có thể tham gia nhiều project không?

Có. Một người có thể là thành viên của nhiều project, với role khác nhau ở mỗi project. Đổi project ở phần chọn project trên thanh bên trái. **My Work** gom việc được giao cho bạn ở mọi project.

## Project và tổ chức khác nhau thế nào?

**Tổ chức** là công ty của bạn, chứa toàn bộ người dùng và project. **Project** là một dự án cụ thể trong tổ chức, có backlog, sprint, board và thành viên riêng. Dữ liệu giữa các tổ chức hoàn toàn tách biệt.

## Có những phím tắt nào?

- **Ctrl/Cmd + K**: mở tìm kiếm nhanh work item và backlog.
- **Ctrl/Cmd + /**: mở hoặc đóng NMate.
- **Esc**: đóng tìm kiếm hoặc NMate.
- Trong ô hỏi NMate: **Enter** để gửi, **Shift + Enter** để xuống dòng.

## Có thể đổi giao diện sang tiếng Việt hay chế độ tối không?

Có, trong **Settings → General**: **Language** (English / Tiếng Việt) và **Theme** (sáng / tối / theo hệ thống). Các tuỳ chỉnh chỉ áp dụng cho riêng bạn.

## Work item có hạn chót (due date) không?

Hiện NFlow **chưa có** trường hạn chót cho work item. Thời gian được quản lý theo sprint (ngày bắt đầu, ngày kết thúc).

## Mã như DASH-34 nghĩa là gì?

Đó là **key** của work item: code project (`DASH`) và số thứ tự trong project (`34`). Gõ `DASH-34` hoặc `34` vào tìm kiếm (Ctrl/Cmd + K) để mở nhanh.

## Đóng sprint có làm mất các việc chưa xong không?

Không mất. Các việc chưa xong **vẫn nằm trong sprint đã đóng với nguyên trạng thái**. Muốn đưa một User Story về backlog thì dùng **De-scope story** trước khi đóng. Xem phần "Quản lý Sprint".

## Vì sao tôi không tự tạo được tài khoản?

NFlow không có đăng ký tự do. Tài khoản do **Global Admin tạo** hoặc đến từ **lời mời qua email**.

## Chat theo sprint ở đâu?

Mỗi sprint có thể có một kênh chat trên **NHub**, tạo khi tạo sprint (bật **Create NHub channel for this sprint**). Thành viên kênh đi theo capacity của sprint. Xem phần "Chat theo sprint với NHub".

## NMate trả lời dựa trên đâu?

NMate chỉ trả lời dựa trên **tài liệu hướng dẫn sử dụng** của NFlow và luôn ghi nguồn bên dưới câu trả lời. NMate **không xem được dữ liệu** của project bạn (task, sprint, thành viên). Nếu tài liệu chưa có thông tin, NMate sẽ nói là chưa có thay vì đoán; khi đó hãy hỏi admin của tổ chức.

Bạn có thể bấm 👍 hoặc 👎 dưới mỗi câu trả lời để giúp cải thiện tài liệu. Đừng nhập mật khẩu hay thông tin nhạy cảm vào khung chat.

## Vì sao NMate báo "tạm hết lượt" hoặc "hỏi hơi nhanh"?

NMate giới hạn số câu hỏi mỗi phút cho mỗi người, và hệ thống AI phía sau có hạn mức sử dụng chung. Đợi khoảng một phút rồi hỏi lại.
