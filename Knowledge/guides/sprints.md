---
key: guides/sprints
title: Quản lý Sprint
module: sprints
type: UserGuide
route: /sprint-planning
language: vi
suggestions:
  - Làm sao tạo sprint mới?
  - Vì sao không tạo được sprint?
  - Làm sao khai báo capacity cho thành viên?
  - Ghi nhận ngày nghỉ của team ở đâu?
---
# Quản lý Sprint

Sprint là một khoảng thời gian cố định (thường 1–4 tuần) để team hoàn thành một nhóm công việc. Mọi thao tác với sprint nằm ở màn hình **Sprint Planning** của từng project.

## Tạo sprint

1. Mở project, vào **Sprint Planning**.
2. Bấm **New sprint**.
3. Nhập **Name**, **Start date** và **End date**.
4. (Tuỳ chọn) bật tạo kênh chat NHub cho sprint, để cả team trao đổi ngay trong sprint.
5. Bấm **Save**.

Tên sprint không cần khác nhau giữa các sprint.

### Vì sao không tạo được sprint?

- **End date phải sau Start date.** Nếu không, form báo "End date must be after start date."
- **Khoảng ngày không được trùng với sprint khác trong cùng project.** Hai sprint của một project không thể chạy chồng lên nhau, kể cả trùng một ngày.
- **Bạn chưa có quyền quản lý sprint.** Nếu không thấy nút **New sprint**, role của bạn trong project chưa có quyền này. Hãy nhờ Scrum Master hoặc người quản lý thành viên của project cấp quyền.

### Kênh chat NHub của sprint

Nếu bật tạo kênh chat, hệ thống tạo sprint trước rồi mới tạo kênh trên NHub. Khi NHub đang lỗi hoặc phản hồi chậm, **sprint vẫn được tạo bình thường**, chỉ là sprint đó không có kênh chat. Hiện chưa có cách tạo lại kênh cho sprint đã tạo.

## Sprint đang diễn ra

Một sprint được coi là **đang diễn ra** khi ngày hôm nay nằm trong khoảng **Start date** đến **End date** (tính cả hai ngày đầu và cuối). Hệ thống tự xác định theo ngày hiện tại.

## Sửa sprint

Bạn có thể đổi tên hoặc đổi ngày của sprint, **kể cả khi sprint đang diễn ra**. Khoảng ngày mới vẫn phải không trùng với sprint khác trong project.

## Khai báo capacity (năng lực của team)

Capacity cho biết mỗi thành viên có bao nhiêu giờ làm việc trong sprint, để team lên kế hoạch vừa sức.

1. Trong **Sprint Planning**, mở phần **Capacity planning**.
2. Bấm **Add member** và chọn thành viên.
3. Nhập **Hours/day** (số giờ làm mỗi ngày) và **OT/day** (giờ làm thêm mỗi ngày, nếu có).

Lưu ý: **tạo sprint mới không tự thêm thành viên vào capacity.** Mỗi sprint cần khai báo lại thành viên tham gia. Nếu thấy "No members yet — use Add member." nghĩa là sprint chưa có ai trong capacity.

## Ghi nhận ngày nghỉ

Ngày nghỉ làm giảm capacity thực tế của sprint.

1. Mở phần **Days off**, bấm **New day off**.
2. Chọn ngày nghỉ và lý do (**Reason**).
3. Chọn nghỉ cho **một thành viên** hoặc **Team-wide** (cả team, ví dụ ngày lễ).

Ngày nghỉ **phải nằm trong khoảng ngày của sprint**. Ngày ngoài sprint sẽ bị từ chối.

## Đọc tải công việc (Load)

Phần **Task workload** so sánh capacity với lượng việc đã giao:

- **Capacity**: tổng giờ thành viên có thể làm trong sprint (đã trừ ngày nghỉ).
- **Workload**: tổng giờ của các task đang giao cho thành viên đó.
- **Load**: capacity trừ workload. Âm nghĩa là thành viên đang bị giao quá sức.
