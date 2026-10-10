---
key: guides/users-admin
title: Quản lý người dùng (Global Admin)
module: users
type: UserGuide
route: /settings/users
language: vi
suggestions:
  - Làm sao tạo tài khoản cho nhân viên mới?
  - Global Admin là gì, khác Scrum Master thế nào?
  - Cây tổ chức (quản lý trực tiếp) xem ở đâu?
---
# Quản lý người dùng (Global Admin)

Màn hình **Settings → Users** chỉ hiện với **Global Admin**.

## Global Admin là gì?

Global Admin là quản trị viên của **cả tổ chức**:

- Có **mọi quyền trong mọi project** của tổ chức, không phụ thuộc role.
- Là người duy nhất được **tạo tài khoản**, **tạo project**, **lưu trữ project** và quản lý metadata **Global**.

Scrum Master chỉ có toàn quyền **trong project** của mình. Global Admin không có quyền gì ở tổ chức khác.

## Tạo tài khoản

1. Vào **Settings → Users**, bấm **Create Account**.
2. Nhập **Full name**, **Email address**, **Password** (ít nhất 8 ký tự) và nhập lại mật khẩu.
3. Lưu. Người dùng đăng nhập bằng alias tổ chức + email + mật khẩu này.

Email không được trùng với tài khoản khác trong tổ chức. Tài khoản mới chưa thuộc project nào: hãy thêm họ vào project ở **Settings → Members**.

Ngoài tạo tài khoản trực tiếp, có thể mời người mới qua email (xem "Mời người mới qua email").

## Thông tin trên danh sách

Mỗi người dùng có: **Created** (ngày tạo), **Last login** (lần đăng nhập gần nhất), **Global admin**, **Managed by** (quản lý trực tiếp) và **Repository Memberships** (các project tham gia). Dùng ô "Search name or email…" để tìm.

## Cấp hoặc bỏ quyền Global Admin

Bật/tắt **Global admin** của một người. Chỉ Global Admin khác làm được (không tự đổi cho chính mình).

## Quản lý trực tiếp và cây tổ chức

- **Managed by**: chọn người quản lý trực tiếp; để trống ("No manager") nghĩa là cấp cao nhất.
- Không thể chọn chính mình, hoặc chọn một người đang là cấp dưới của mình, làm quản lý.
- **View tree** (Organisation hierarchy): xem chuỗi cấp trên, đồng cấp và cấp dưới trực tiếp của một người.

## Lưu ý

- Admin **không đổi mật khẩu hộ** người khác được; mỗi người tự đổi ở Settings → General.
- Tài khoản có thể bị vô hiệu hoá (inactive) thay vì xoá.
