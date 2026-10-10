---
key: product/nflow-overview
title: Giới thiệu NFlow
module: general
type: UserGuide
language: vi
suggestions:
  - NFlow là gì, dùng để làm gì?
  - Các màn hình chính trong NFlow là gì?
  - Tổ chức và project khác nhau thế nào?
  - Có những loại work item nào?
---
# Giới thiệu NFlow

NFlow là công cụ quản lý dự án phần mềm theo Agile/Scrum, tương tự một bản gọn của Azure DevOps hay Jira. Team dùng NFlow để quản lý backlog, lên kế hoạch sprint, theo dõi công việc hằng ngày trên board và xem tiến độ qua biểu đồ.

## Tổ chức và project

- **Tổ chức (Organization)** là công ty của bạn. Mọi người dùng và project đều thuộc đúng một tổ chức, và dữ liệu của tổ chức này không nhìn thấy được từ tổ chức khác. Khi đăng nhập, bạn nhập **alias của tổ chức** (ví dụ `acme`).
- **Project** là một dự án cụ thể trong tổ chức. Mỗi project có **mã (code)** riêng, ví dụ `DASH`, dùng để đánh số work item: `DASH-1`, `DASH-2`… Mỗi project có backlog, sprint, board, thành viên và phân quyền riêng.
- Một người có thể tham gia nhiều project, với role khác nhau ở mỗi project. Đổi project đang làm việc ở phần chọn project trên thanh bên trái.

## Các màn hình chính

Thanh bên trái chia làm 3 nhóm.

**Workspace** (làm việc trong project đang chọn):

- **Overview**: tổng quan project — số lượng item, burndown, velocity, tình trạng sprint, workload từng người, hoạt động gần đây.
- **Boards**: bảng Kanban của sprint, kéo thả card qua các cột.
- **Workflow**: cấu hình cột của board, giới hạn WIP, cảnh báo item nằm lâu.
- **Backlog**: danh sách công việc theo cây Epic → Feature → User Story.
- **Sprint**: lên kế hoạch sprint — tạo, kích hoạt, đóng sprint, capacity và ngày nghỉ.
- **Repos**: branch, commit, pull request của repo GitHub đã kết nối.
- **Pipelines**: nơi hiển thị lượt build/deploy (CI/CD).
- **Audit log**: lịch sử thay đổi trong project (chỉ người có quyền quản lý thành viên thấy).

**Personal**:

- **My Work**: mọi work item đang mở được giao cho bạn, trên tất cả project.

**Settings**:

- **General**: hồ sơ, mật khẩu, giao diện, ngôn ngữ, thông báo của riêng bạn.
- **Members**: thành viên và role của project (cần quyền quản lý thành viên).
- **Metadata**: danh mục giá trị chọn sẵn như version, label, component (cần quyền quản lý thành viên).
- **Users**: tài khoản người dùng của tổ chức (chỉ Global Admin).

## Các loại work item

- **Epic**, **Feature**, **User Story**: nằm trong backlog, dùng để chia nhỏ yêu cầu. Chỉ **User Story** được đưa vào sprint.
- **Task**, **Bug**, **Test Plan**: việc thực thi, nằm trong sprint (Bug và Test Plan có thể không thuộc sprint nào).

Xem chi tiết ở phần "Work item" và "Quản lý Backlog".

## Luồng làm việc điển hình

1. Product Owner/BA tạo Epic → Feature → User Story trong **Backlog**, làm rõ yêu cầu cho tới khi User Story ở trạng thái **Ready**.
2. Team tạo sprint, khai báo **capacity** và ngày nghỉ, rồi **Promote to Sprint** các User Story đã Ready.
3. Scrum Master bấm **Activate** để bắt đầu sprint.
4. Team chia User Story thành Task, làm việc trên **Boards**, log giờ làm.
5. Theo dõi tiến độ ở **Overview**. Cuối sprint bấm **Close sprint**.

## Sản phẩm đi kèm

- **NHub**: ứng dụng chat của bộ sản phẩm. Mỗi sprint có thể có một kênh chat NHub riêng.
- **NMate**: trợ lý trả lời câu hỏi cách dùng NFlow (chính là khung chat này), dựa trên tài liệu hướng dẫn.
