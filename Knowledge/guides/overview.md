---
key: guides/overview
title: Overview (tổng quan project)
module: overview
type: UserGuide
route: /overview
language: vi
suggestions:
  - Burndown chart đọc thế nào?
  - Sprint health On track, At risk, Behind nghĩa là gì?
  - Cycle time được tính ra sao?
  - Velocity trend là gì?
---
# Overview (tổng quan project)

**Overview** là dashboard của project. Chọn **Sprint** ở đầu trang để xem số liệu của sprint đó. Nếu không chọn, hệ thống lấy sprint đang chứa ngày hôm nay, không có thì lấy sprint gần nhất.

Mọi thành viên project đều xem được Overview.

## Các phần trên trang

- **Total items**, **Work items by type**, **Type distribution**: số lượng item và tỉ lệ theo loại (User Story, Task, Bug, Test Plan).
- **Status distribution**: tỉ lệ item theo nhóm trạng thái (chưa làm / đang làm / đã xong).
- **Story points** và burndown: so sánh story point còn lại thực tế với đường lý tưởng.
- **Current sprint** và sprint health.
- **Velocity trend**: story point cam kết và hoàn thành của 5 sprint gần nhất.
- **Work item trend by type**: số item theo loại qua tối đa 7 sprint (sprint đang chọn và 6 sprint trước).
- **Avg. cycle time**: thời gian trung bình để hoàn thành item.
- **Workload by assignee**: số việc của từng người trong sprint; việc chưa giao gộp vào "Unassigned".
- **Recent activity**: 8 thay đổi mới nhất trong project.

## Burndown chart

- Chỉ tính **ngày làm việc** (thứ 2 – thứ 6), bỏ cuối tuần.
- **Đường lý tưởng**: giảm đều từ tổng story point cam kết về 0 vào cuối sprint.
- **Đường thực tế**: story point chỉ được tính là hoàn thành khi item chuyển sang trạng thái kết thúc.
- Biểu đồ dừng ở ngày hôm nay, không dự đoán tương lai.

## Sprint health

So sánh story point còn lại thực tế với đường lý tưởng tại ngày hôm nay:

- **On track**: còn lại không quá 5% so với lý tưởng.
- **At risk**: còn lại nhiều hơn lý tưởng từ 5% đến 25%.
- **Behind**: còn lại nhiều hơn lý tưởng trên 25%.

Kèm theo là số ngày làm việc còn lại tới hết sprint.

## Cycle time

**Avg. cycle time** = trung bình số ngày từ lúc **tạo** item tới lúc **hoàn thành**, tính trên các item đã xong trong sprint. Đây là **chỉ số xấp xỉ**, không phải thời gian thực sự làm (từ lúc bắt đầu tới lúc xong). Chưa có item nào xong thì hiện "No completed items yet".
