---
key: guides/repos-pipelines
title: Repos và Pipelines (GitHub)
module: git-repositories
type: UserGuide
route: /repos
language: vi
suggestions:
  - Vì sao tab Repos báo No GitHub connection?
  - Làm sao kết nối repo GitHub với project?
  - Pipelines hiển thị gì?
---
# Repos và Pipelines

## Repos

Mục **Repos** hiển thị dữ liệu GitHub của các repo gắn với project:

- **Branches**: các branch, branch mặc định và branch cũ (stale).
- **Commits**: commit gần đây.
- **Pull requests**: PR đang mở, đã đóng, đã merge, kèm người review, nhánh nguồn → đích và work item liên kết.
- **API rate limit**: hạn mức gọi GitHub API còn lại.

Một project có thể gắn nhiều repo GitHub (ví dụ frontend và backend); chọn repo cần xem ở đầu trang. Mọi thành viên project đều xem được.

### Vì sao báo "No GitHub connection"?

Project **chưa được cấu hình kết nối GitHub**. Kết nối được cấu hình **phía server bởi người vận hành hệ thống**, không có màn hình nhập token trong app. Hãy liên hệ admin/DevOps của tổ chức.

### Dữ liệu chưa cập nhật?

Dữ liệu GitHub được lưu đệm khoảng **60 giây** để tránh gọi GitHub quá nhiều. Nếu repo đã cài webhook, push hoặc pull request mới sẽ làm mới dữ liệu ngay. Nếu chưa, đợi khoảng một phút rồi tải lại.

## Pipelines

Mục **Pipelines** là nơi hiển thị các lượt build và deploy (CI/CD, ví dụ GitHub Actions) của repo đã kết nối. Hiện màn hình chỉ hiện hướng dẫn thiết lập, **chưa hiển thị dữ liệu chạy pipeline**.
