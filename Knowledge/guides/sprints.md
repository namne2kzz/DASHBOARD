---
key: guides/sprints
title: Quản lý Sprint
module: sprints
type: UserGuide
route: /sprint-planning
language: vi
suggestions:
  - Làm sao tạo và bắt đầu một sprint?
  - Đóng sprint thì các item chưa xong đi đâu?
  - Vì sao không tạo được sprint?
  - Làm sao khai báo capacity cho thành viên?
---
# Quản lý Sprint

Sprint là một khoảng thời gian cố định (thường 1–4 tuần) để team hoàn thành một nhóm User Story. Mọi thao tác với sprint nằm ở mục **Sprint** (màn hình Sprint Planning) của từng project.

## Vòng đời của sprint

Mỗi sprint có 3 trạng thái:

1. **Planning**: vừa tạo, đang lên kế hoạch (đưa User Story vào, khai báo capacity).
2. **Active**: đang chạy. **Mỗi project chỉ có tối đa một sprint Active** tại một thời điểm.
3. **Closed**: đã kết thúc.

Chuyển trạng thái bằng menu **⋯** của sprint trong Sprint Planning: **Activate** và **Close sprint**.

## Tạo sprint

1. Mở project, vào **Sprint**.
2. Bấm **New sprint**.
3. Nhập **Name**, **Start date** và **End date**.
4. (Tuỳ chọn) bật **Create NHub channel for this sprint** để tạo kênh chat NHub riêng cho sprint.
5. Lưu lại. Sprint mới ở trạng thái **Planning**.

Tên sprint không cần khác nhau giữa các sprint.

### Vì sao không tạo được sprint?

- **End date phải sau Start date.** Nếu không, form báo "End date must be after start date."
- **Khoảng ngày không được trùng với sprint khác trong cùng project.** Lỗi: "Sprint dates overlap with an existing sprint in this repository."
- **Bạn chưa có quyền quản lý sprint (Manage Sprint).** Hãy nhờ Scrum Master hoặc người quản lý thành viên của project cấp quyền.

### Kênh chat NHub của sprint

Nếu bật tạo kênh chat, hệ thống tạo sprint trước rồi mới tạo kênh trên NHub. Khi NHub đang lỗi hoặc phản hồi chậm, **sprint vẫn được tạo bình thường**, chỉ là sprint đó không có kênh chat. Hiện chưa có cách tạo lại kênh cho sprint đã tạo.

Thành viên được thêm vào **capacity** của sprint sẽ tự được thêm vào kênh chat; xoá khỏi capacity thì bị xoá khỏi kênh.

## Bắt đầu sprint (Activate)

1. Chọn sprint đang ở trạng thái **Planning**.
2. Mở menu **⋯**, bấm **Activate** và xác nhận.

### Vì sao không Activate được?

- **Đang có sprint khác Active** trong project: "Another sprint is already active in this repository. Close it before activating a new one." Hãy đóng sprint đó trước.
- Sprint đã **Closed** thì không kích hoạt lại được.
- Role của bạn chưa có quyền **Manage Sprint**.

## Đóng sprint (Close sprint)

1. Chọn sprint đang **Active**.
2. Mở menu **⋯**, bấm **Close sprint**.

Nếu sprint còn item chưa hoàn thành, hệ thống hiện cảnh báo **"Incomplete items remaining"** kèm số lượng (ví dụ "3 items still in progress. Close anyway?"). Bạn có thể huỷ để xử lý tiếp, hoặc xác nhận để vẫn đóng.

### Đóng sprint thì các item chưa xong đi đâu?

**Các item chưa xong vẫn nằm lại trong sprint đã đóng với nguyên trạng thái hiện tại.** Hệ thống không tự chuyển chúng về backlog hay sang sprint sau. Item được coi là "chưa xong" khi chưa ở trạng thái kết thúc (Done, Passed, Failed hoặc Closed); hệ thống chỉ đếm item cấp gốc, không đếm sub-task.

Muốn đưa một User Story chưa xong về lại backlog trước khi đóng sprint, dùng **De-scope story** (xem dưới).

Lưu ý:
- Chỉ đóng được sprint đang **Active**; sprint còn ở Planning thì báo "Cannot close a sprint that has not been activated yet."
- Đóng sprint cần quyền **Manage Sprint**.

## Đưa User Story ra khỏi sprint (De-scope)

1. Mở User Story trong sprint (bấm vào story trong danh sách **Sprint stories**).
2. Bấm **De-scope story** và xác nhận.

Kết quả:
- User Story **quay về backlog ở trạng thái Ready**, sẵn sàng đưa vào sprint khác.
- **Các task con của story bị xoá** khỏi sprint.

Chỉ de-scope được User Story cấp gốc (không áp dụng cho sub-task). Cần quyền **Promote to Sprint**.

## Sửa sprint

Trong menu **⋯** của sprint còn có mục sửa tên và ngày. Khoảng ngày mới vẫn phải không trùng với sprint khác trong project.

## Khai báo capacity (năng lực của team)

Capacity cho biết mỗi thành viên có bao nhiêu giờ làm việc trong sprint, để team lên kế hoạch vừa sức.

1. Trong **Sprint**, mở phần **Capacity planning**.
2. Bấm **Add member** và chọn thành viên (phải là thành viên của project).
3. Nhập **Hours/day** (0–12 giờ làm mỗi ngày) và **OT/day** (0–6 giờ làm thêm mỗi ngày, nếu có).

Lưu ý:
- **Tạo sprint mới không tự thêm thành viên vào capacity.** Mỗi sprint cần khai báo lại. Thấy "No members yet — use Add member." nghĩa là sprint chưa có ai trong capacity.
- Thêm lại một người đã có trong capacity sẽ **cập nhật** số giờ, không tạo dòng trùng.
- Khai báo capacity cần quyền **Manage Capacity**.
- Khi tạo Task từ board, ô chọn người được giao **chỉ liệt kê người đã có trong capacity** của sprint đó.

## Ghi nhận ngày nghỉ

Ngày nghỉ làm giảm capacity thực tế của sprint.

1. Mở phần **Days off**, bấm **New day off**.
2. Chọn ngày, số giờ nghỉ và lý do (**Reason**).
3. Chọn nghỉ cho **một thành viên** hoặc **Team-wide** (cả team, ví dụ ngày lễ).

Quy tắc:
- Ngày nghỉ **phải nằm trong khoảng ngày của sprint**, ngoài khoảng sẽ bị từ chối.
- Ngày nghỉ **Team-wide** chỉ trừ tối đa số giờ mà mỗi người thực sự làm trong một ngày. Ví dụ ngày lễ khai 8 giờ, người làm part-time 4 giờ/ngày chỉ bị trừ 4 giờ.
- Capacity của mỗi người không bao giờ âm.

## Đọc tải công việc (Load)

Phần **Task workload** so sánh capacity với lượng việc đã giao:

- **Capacity**: tổng giờ thành viên có thể làm trong sprint (Hours/day + OT/day nhân số ngày làm, trừ ngày nghỉ).
- **Workload**: tổng giờ của các task đang giao cho thành viên đó.
- **Load**: capacity trừ workload. Âm nghĩa là thành viên đang bị giao quá sức.

Capacity chỉ để tham khảo khi lên kế hoạch: hệ thống **không chặn** việc giao thêm task khi một người đã quá tải.
