---
key: guides/invitations
title: Mời người mới qua email
module: invitations
type: UserGuide
route: /settings/members
language: vi
suggestions:
  - Làm sao mời người chưa có tài khoản vào project?
  - Link mời bị lỗi hoặc hết hạn thì làm sao?
  - Làm sao thu hồi lời mời đã gửi?
---
# Mời người mới qua email

Dùng lời mời khi người đó **chưa có tài khoản** trong tổ chức. Nếu họ đã có tài khoản, hãy dùng **Add Member** (xem phần "Thành viên và phân quyền").

## Gửi lời mời

1. Vào **Settings → Members**, chọn **Invite by Email**.
2. Nhập email (ví dụ `colleague@example.com`).
3. Chọn **Discipline** (chức danh) và **Role** (quyền) mà người đó sẽ có khi tham gia.
4. Gửi. Hệ thống gửi email chứa link mời.

Cần quyền **Invite Members** trong project. Project đã lưu trữ (archived) thì không mời được.

### Vì sao không gửi được lời mời?

- **Email đã có tài khoản** trong hệ thống: dùng **Add Member** thay vì mời.
- Role của bạn chưa có quyền **Invite Members**.
- Project đã bị lưu trữ.

Gửi lời mời mới cho cùng một email và cùng project sẽ **tự huỷ** các lời mời cũ đang chờ.

## Người được mời làm gì

1. Mở link trong email. Trang "You've been invited" hiện ra.
2. Bấm **đăng nhập bằng Google**, và phải dùng **đúng tài khoản Google của email được mời**.
3. Hệ thống tạo tài khoản, thêm vào project với đúng Discipline và Role đã chọn, rồi **vào thẳng app**.

Từ lần sau, người này đăng nhập bằng nút **Google** (tài khoản tạo qua lời mời không có mật khẩu). Người đã từng nhận lời mời ở project khác thì không bị tạo tài khoản mới, chỉ được thêm vào project mới.

### Link mời báo lỗi ("Couldn't accept invite" / "Invalid invite link")

Thường do một trong các lý do sau (hệ thống không nói cụ thể lý do nào, để bảo mật):

- Link **đã hết hạn**. Link mời chỉ có hiệu lực trong một khoảng thời gian giới hạn.
- Lời mời **đã bị thu hồi**, hoặc đã có lời mời mới hơn thay thế.
- Đăng nhập Google bằng **email khác** với email được mời.
- Role được chọn lúc mời **đã bị xoá** trước khi nhận lời mời.

Cách xử lý: nhờ người mời **gửi lại lời mời mới**.

## Xem và thu hồi lời mời

Trong **Settings → Members**, người có quyền Invite Members xem được danh sách lời mời đã gửi (mọi trạng thái: đang chờ, đã nhận, hết hạn, đã thu hồi). Chưa gửi lời mời nào thì hiện "No invitations sent yet."

Lời mời **đang chờ** có thể bị **thu hồi (Revoke)** sau khi xác nhận. Link trong email cũ sẽ không dùng được nữa. Lời mời đã nhận hoặc đã hết hạn thì không thu hồi được.
