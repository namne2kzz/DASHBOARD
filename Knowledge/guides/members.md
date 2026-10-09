---
key: guides/members
title: Thành viên và phân quyền
module: members
type: UserGuide
route: /settings/members
language: vi
suggestions:
  - Làm sao thêm thành viên vào project?
  - Vì sao tôi không thấy một nút hay menu nào đó?
  - Chức danh và role khác nhau thế nào?
  - Làm sao tạo role riêng?
---
# Thành viên và phân quyền

Mỗi project có danh sách thành viên riêng. Quyền của mỗi người trong project do **role** được gán quyết định.

## Thêm thành viên

1. Vào **Settings → Members**.
2. Bấm **Add Member** và chọn người dùng (người dùng phải đã có tài khoản trong tổ chức).
3. Chọn **Discipline** (chức danh, ví dụ Developer, Tester).
4. Chọn **Role**. Hệ thống tự gợi ý role khớp với chức danh, bạn có thể đổi.

Muốn mời người chưa có tài khoản thì dùng **Invite by Email**.

Lưu ý:

- Mỗi người chỉ là thành viên **một lần** trong một project.
- Project đã **lưu trữ (archived)** thì không thêm thành viên được.
- Một người có thể ở **nhiều project** với role khác nhau ở mỗi project.
- Cần quyền **quản lý thành viên** mới thêm, sửa, xoá được thành viên.

## Chức danh (Discipline) và Role khác nhau thế nào?

- **Discipline (chức danh)** chỉ dùng để lên kế hoạch capacity trong sprint. **Chức danh không cấp quyền gì.**
- **Role** quyết định bạn được làm gì trong project. Quyền của bạn **đúng bằng** danh sách quyền của role được gán, không cộng thêm từ chức danh.

## 5 role mặc định

Mỗi project tự có 5 role mặc định:

| Role | Được làm gì |
|---|---|
| **Scrum Master** | Toàn bộ quyền |
| **Project Manager** | Toàn bộ quyền |
| **Developer** | Xem project; tạo, sửa, giao work item; xem analytics; quản lý pipeline và repo |
| **Tester** | Xem project; tạo, sửa, giao work item; quản lý backlog; xem analytics |
| **Business Analyst** | Xem project; tạo, sửa, xoá, giao work item; quản lý backlog; promote vào sprint; quản lý và kích hoạt sprint; xem analytics |

Người tạo project trở thành **Scrum Master** của project đó.

## Vì sao tôi không thấy một nút hay menu nào đó?

Các nút và menu chỉ hiện khi role của bạn có quyền tương ứng. Ví dụ:

- Không thấy nút tạo sprint: role chưa có quyền **quản lý sprint**.
- Không đưa được User Story vào sprint: role chưa có quyền **promote vào sprint**.
- Không thêm được thành viên: role chưa có quyền **quản lý thành viên**.

Hãy nhờ Scrum Master (hoặc người có quyền quản lý thành viên) đổi role cho bạn.

## Role tự tạo (custom role)

Nếu 5 role mặc định không phù hợp, người có quyền **quản lý role** có thể tạo role riêng:

- **Tạo mới**: đặt tên (**Role name**), mô tả và chọn danh sách quyền (**Permissions**).
- **Clone**: sao chép một role có sẵn (kể cả role mặc định) thành role mới cùng quyền, rồi chỉnh tiếp.
- **Sửa**: đổi quyền của role tự tạo áp dụng **ngay** cho mọi thành viên đang dùng role đó.

Quy tắc:

- **Role mặc định không sửa và không xoá được.** Muốn khác thì clone ra role mới.
- Tên role tự tạo không được trùng trong cùng project.
- **Không xoá được role còn thành viên đang dùng.** Hãy đổi role cho các thành viên đó trước.

## Không xoá được thành viên hoặc không hạ được quyền?

Hệ thống luôn giữ **ít nhất một thành viên có quyền quản lý thành viên** trong mỗi project. Nếu bạn định xoá hoặc đổi role của người cuối cùng có quyền này, thao tác sẽ bị chặn để project không bị "mất admin".

Xoá thành viên chỉ đưa người đó ra khỏi project, **không xoá tài khoản** của họ.
