---
key: guides/account
title: Đăng nhập và tài khoản
module: account
type: UserGuide
route: /settings/general
language: vi
suggestions:
  - Đăng nhập cần những gì?
  - Tôi quên mật khẩu thì làm sao?
  - Làm sao đổi mật khẩu?
  - Đổi giao diện sáng/tối và ngôn ngữ ở đâu?
---
# Đăng nhập và tài khoản

## Đăng nhập

Màn hình đăng nhập cần 3 thông tin:

1. **Organization**: alias của tổ chức (ví dụ `acme`). Hỏi admin nếu bạn không biết.
2. **Email**
3. **Password**

Email không phân biệt chữ hoa/thường. Cùng một email có thể có tài khoản ở hai tổ chức khác nhau, vì vậy luôn cần nhập đúng alias.

### Vì sao đăng nhập báo sai?

Khi alias tổ chức, email hoặc mật khẩu sai, hệ thống chỉ báo chung một lỗi "Invalid organization, email or password." mà không nói cụ thể sai ở đâu (để bảo mật). Hãy kiểm tra lại cả ba thông tin, đặc biệt là alias tổ chức.

### Đăng nhập bằng Google

Nút đăng nhập Google chỉ dùng được cho tài khoản **đã từng nhận lời mời qua email** và đăng nhập Google lúc nhận lời mời. Đăng nhập Google **không tự tạo tài khoản mới**. Nếu bị từ chối, hãy nhờ admin gửi lời mời.

Tài khoản tạo qua lời mời Google không có mật khẩu, nên luôn đăng nhập bằng nút Google.

## Quên mật khẩu

Hiện NFlow **chưa có chức năng tự đặt lại mật khẩu** qua email (liên kết "Forgot password?" trên màn hình đăng nhập chưa hoạt động). Hãy liên hệ admin của tổ chức để được hỗ trợ.

## Đổi mật khẩu

1. Vào **Settings → General**, phần **Password**.
2. Nhập **Current password**, **New password** và **Confirm new password**.
3. Lưu lại.

Mật khẩu mới phải có **ít nhất 8 ký tự**. Chỉ chính bạn đổi được mật khẩu của mình, và phải nhập đúng mật khẩu hiện tại. Admin không đổi mật khẩu hộ được.

## Hồ sơ cá nhân

Trong **Settings → General**, phần **Your Account**:

- **Display name**: tên hiển thị.
- **Avatar**: bấm **Upload** để tải ảnh đại diện. Chưa có ảnh thì hệ thống dùng avatar màu (**Avatar colour**).
- **Email**: hiển thị để tham khảo.

## Tuỳ chỉnh giao diện và vùng

Cũng trong **Settings → General**:

- **Theme**: giao diện sáng, tối hoặc theo hệ thống.
- **Language**: English hoặc Tiếng Việt.
- **Date format**: DD/MM/YYYY, MM/DD/YYYY hoặc YYYY-MM-DD.
- **Timezone**: múi giờ hiển thị; **Local (device)** dùng múi giờ của máy bạn.
- **Compact mode**: thu hẹp khoảng cách dòng trong các danh sách.
- **Reduce motion**: giảm hiệu ứng chuyển động.

Các tuỳ chỉnh này chỉ áp dụng cho riêng bạn. Định dạng ngày và múi giờ cũng được NHub dùng, nên bạn không phải chỉnh lại ở NHub.

## Thông báo

Phần **Notifications** trong Settings → General gồm: **Email notifications**, **Push notifications**, **Activity digest** (tóm tắt hằng ngày) và **Mentions only** (chỉ báo khi được @nhắc tên).
