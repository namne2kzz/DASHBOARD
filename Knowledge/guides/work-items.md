---
key: guides/work-items
title: Work item (Task, Bug, Test Plan, User Story)
module: work-items
type: UserGuide
route: /boards
language: vi
suggestions:
  - Mỗi loại work item có những trạng thái nào?
  - Làm sao log giờ làm việc?
  - Làm sao tạo task con cho một User Story?
  - Ai được sửa hay xoá comment?
---
# Work item

Work item là đơn vị công việc trong sprint. Mỗi work item có **key** dạng `{code project}-{số}`, ví dụ `DASH-34`, đánh số tăng dần trong từng project.

## Các loại work item

- **User Story**: yêu cầu từ backlog, được đưa vào sprint bằng **Promote to Sprint**. Ước lượng bằng **Story points**.
- **Task**: việc kỹ thuật cụ thể, thường là con của một User Story. Ước lượng bằng giờ.
- **Bug**: lỗi cần sửa, có thêm phần **Bug details**: **Steps to reproduce**, **Environment**, **Impact**, **Root cause**, **Solution**.
- **Test Plan**: kế hoạch kiểm thử, có danh sách **Test steps**.

Bug và Test Plan có thể tồn tại **không thuộc sprint nào**. Mọi loại work item đều có thể có work item con (sub-task).

## Trạng thái theo từng loại

Mỗi loại có bộ trạng thái riêng. Chọn trạng thái không thuộc loại đó sẽ bị từ chối.

| Loại | Trạng thái |
|---|---|
| User Story | Open → In Progress → Done, hoặc Closed |
| Task | To Do → In Progress → In Review → Done, hoặc Closed |
| Bug | Open → In Progress → In Review → Verified → Done, hoặc Closed |
| Test Plan | Open → Running → Passed hoặc Failed, hoặc Closed |

Trạng thái được gom thành 3 nhóm, dùng ở board và báo cáo:

- **Chưa làm**: Open, To Do.
- **Đang làm**: In Progress, In Review, Verified, Running.
- **Đã kết thúc**: Done, Passed, Failed, Closed.

Khi chuyển sang một trạng thái kết thúc, hệ thống tự đặt **Remaining = 0** và ghi lại thời điểm hoàn thành. Mọi lần đổi trạng thái đều được ghi vào lịch sử của item.

## Tạo work item

- Trên **Boards**, bấm **New work item**, chọn loại (Task, Bug, Test Plan), nhập tiêu đề (bắt buộc) và các trường tuỳ chọn: **Description**, **Priority**, **Assignee**, **Parent story**…
- User Story không tạo trực tiếp trên board mà đến từ backlog qua **Promote to Sprint**.

### Gắn task con vào User Story

Trong form tạo hoặc chi tiết task, ô **Parent story** cho phép tìm User Story theo key hoặc tiêu đề ("Search Story by ID or title…"). Kết quả hiện tối đa 20 story mới nhất khớp từ khoá.

## Độ ưu tiên

**Priority** gồm: Low, Medium (mặc định), High, Critical.

## Giao việc

- Người được giao (**Assignee**) phải là thành viên của project. Có thể bỏ giao (Unassigned).
- Khi tạo task từ board, danh sách người được giao chỉ gồm **những người đã có trong capacity** của sprint đang chọn.
- User Story thường không giao cho một người; người làm được giao ở cấp Task/Bug.

## Ước lượng và log giờ làm

- **Story points**: cho User Story.
- **Estimate** (Original estimate): số giờ ước lượng ban đầu cho Task/Bug.
- **Remaining**: số giờ còn lại.
- **Completed**: số giờ đã làm, cộng dồn mỗi lần log work.

Khi log giờ làm, nhập số giờ đã làm (phải lớn hơn 0) và số giờ còn lại (không âm). **Nếu số giờ còn lại về 0, item tự chuyển sang Done.** Log work cần quyền **Edit Work Item**.

## Chi tiết work item

Bấm vào một card trên board để mở chi tiết. Tại đây bạn có:

- **Description**, **Acceptance criteria** (với User Story), **State**, **Assignee**, **Priority**, các số giờ.
- **Labels & metadata**: gắn các giá trị chọn sẵn như Labels, Components, Fixed In Version (xem phần "Metadata").
- **Documents**: danh sách link tài liệu liên quan.
- **Work (children)**: các work item con.
- Lịch sử thay đổi và phần bình luận.

Bấm **Save changes** để lưu. Sửa nội dung work item cần quyền **Edit Work Item**; đổi trạng thái chỉ cần là thành viên của project.

## Bình luận (Discussion)

- Mọi thành viên project có thể bình luận: nhập vào ô "Write a comment…" rồi bấm **Post comment**.
- Bình luận hỗ trợ Markdown, tối đa 10.000 ký tự.
- **Chỉ người viết** được sửa bình luận của mình.
- **Người viết hoặc Global Admin** được xoá bình luận.

## Lịch sử thay đổi

Mỗi work item có lịch sử ghi tự động: tạo mới, đổi trạng thái, giao việc, promote… kèm người thực hiện và thời gian. Lịch sử **không sửa và không xoá được**. Chưa có gì thì hiện "No changes recorded yet."

## Mở work item từ NHub

Thread thảo luận trên NHub có thể gắn với một work item; NHub hiển thị key, tiêu đề và trạng thái của item đó.
