---
key: glossary
title: Thuật ngữ
module: general
type: Glossary
language: vi
suggestions:
  - WIP limit là gì?
  - Story point khác giờ ước lượng thế nào?
  - Velocity và burndown là gì?
---
# Thuật ngữ

## Tổ chức (Organization) và alias
Công ty của bạn trong NFlow. **Alias** là tên ngắn của tổ chức (ví dụ `acme`), phải nhập khi đăng nhập và xuất hiện ở đầu đường dẫn trong app.

## Project (Repository)
Một dự án trong tổ chức, có **code** riêng (tối đa 10 ký tự, chữ hoa và số, ví dụ `DASH`). Trong một số màn hình kỹ thuật, project còn được gọi là "repository".

## Work item key
Mã định danh của work item, gồm code project và số thứ tự: `DASH-34`. Có thể gõ `DASH-34` hoặc chỉ `34` vào ô tìm kiếm (Ctrl/Cmd + K) để tìm nhanh.

## Epic, Feature, User Story
Ba cấp yêu cầu trong backlog. Epic là mục tiêu lớn, Feature là một tính năng thuộc Epic, User Story là phần việc đủ nhỏ để làm trong một sprint.

## Task, Bug, Test Plan
Ba loại việc thực thi. Task là việc kỹ thuật, Bug là lỗi cần sửa, Test Plan là kế hoạch kiểm thử.

## Sprint
Khoảng thời gian cố định để team hoàn thành một nhóm User Story. Sprint có 3 trạng thái: **Planning** (đang lên kế hoạch), **Active** (đang chạy) và **Closed** (đã đóng). Mỗi project chỉ có tối đa một sprint Active.

## Refinement
Quá trình làm rõ một User Story (acceptance criteria, ước lượng) trước khi đưa vào sprint. Trạng thái refinement trong backlog: New → Refining → Ready.

## Promote to Sprint
Đưa một User Story đã **Ready** từ backlog vào sprint để team bắt đầu làm.

## De-scope
Đưa một User Story **ra khỏi** sprint và trả về backlog (trạng thái Ready). Các task con của nó bị xoá theo.

## Acceptance criteria
Các điều kiện để coi User Story là hoàn thành, mỗi dòng một điều kiện, thường viết dạng "Given … When … Then …".

## Story point
Đơn vị ước lượng độ lớn của User Story theo dãy Fibonacci (1, 2, 3, 5, 8, 13…). Story point đo độ phức tạp tương đối, không phải số giờ.

## T-shirt size
Cách ước lượng thô cho Epic và Feature: XS, S, M, L, XL.

## Original estimate, Remaining, Completed
Ba con số giờ của Task/Bug: **Original estimate** là ước lượng ban đầu, **Remaining** là số giờ còn lại, **Completed** là số giờ đã làm (cộng dồn khi log work).

## Capacity
Số giờ mỗi thành viên có thể làm trong sprint, tính từ giờ làm mỗi ngày (Hours/day), giờ làm thêm (OT/day) và trừ đi ngày nghỉ.

## Load (tải công việc)
Capacity trừ đi tổng giờ việc đang được giao. Load âm nghĩa là người đó đang bị giao quá sức.

## Velocity
Số story point team hoàn thành mỗi sprint. Màn hình Overview so sánh committed (cam kết) với completed (hoàn thành) của 5 sprint gần nhất.

## Burndown
Biểu đồ story point còn lại theo từng ngày làm việc của sprint, so với đường lý tưởng giảm đều về 0.

## Sprint health
Tình trạng sprint trên Overview: **On track**, **At risk** hoặc **Behind**, dựa trên việc story point còn lại thực tế so với đường lý tưởng.

## Cycle time
Thời gian trung bình (ngày) từ lúc tạo tới lúc xong của các item đã hoàn thành trong sprint. Đây là chỉ số xấp xỉ.

## WIP limit
Giới hạn số card được nằm cùng lúc trong một cột của board. **Soft-warning** chỉ cảnh báo khi vượt; **Hard-stop** không cho kéo thêm card vào cột đã đầy.

## Aging limit
Số ngày tối đa một card nên nằm trong một cột. Card nằm lâu hơn sẽ bị đánh dấu cảnh báo (không bị chặn).

## Role và Discipline
**Role** quyết định bạn được làm gì trong project (quyền). **Discipline** (chức danh, ví dụ Developer, Tester) chỉ dùng để lên kế hoạch capacity, không cấp quyền.

## Global Admin
Quản trị viên của cả tổ chức: có mọi quyền trong mọi project của tổ chức, tạo tài khoản và tạo project.

## Metadata
Danh mục giá trị chọn sẵn cho work item như Fixed In Version, Labels, Components.
