---
key: guides/metadata
title: Metadata (danh mục giá trị chọn sẵn)
module: metadata
type: UserGuide
route: /settings/metadata
language: vi
suggestions:
  - Metadata dùng để làm gì?
  - Làm sao thêm version mới vào danh sách Fixed In Version?
  - Label và Component lấy từ đâu?
---
# Metadata (danh mục giá trị chọn sẵn)

Metadata là các danh sách giá trị chọn sẵn để gắn vào work item, giúp cả team dùng chung một bộ giá trị thay vì mỗi người gõ một kiểu.

## Các loại (Key) hiện có

- **Fixed In Version**: version sửa lỗi.
- **Implemented In Build**: bản build có thay đổi.
- **Release Notes**, **QA Notes**
- **Design Doc URL**, **External Reference**
- **Components**: thành phần hệ thống.
- **Labels**: nhãn phân loại.
- **Team Role**: danh sách chức danh (Discipline) dùng khi thêm thành viên và khai báo capacity.

## Quản lý giá trị

Vào **Settings → Metadata**. Danh sách nhóm theo **Key**.

- **Thêm**: chọn Key, nhập **Value**, lưu.
- **Sửa**: chỉ đổi được Value (không đổi Key).
- **Xoá**: giá trị bị ẩn khỏi danh sách chọn.

Trong cùng một Key và cùng phạm vi, không được có hai giá trị trùng nhau.

## Giá trị dùng chung và giá trị riêng project

- Giá trị **riêng project**: chỉ dùng trong project đó. Cần quyền **Manage Metadata**.
- Giá trị **Global**: dùng chung cho mọi project trong tổ chức. Chỉ **Global Admin** tạo, sửa, xoá được.

Mỗi project thấy cả giá trị Global lẫn giá trị riêng của mình. Mọi thành viên đều xem được danh sách.

## Gắn metadata vào work item

Trong chi tiết work item, phần **Labels & metadata** cho phép chọn nhiều giá trị. Cần quyền **Edit Work Item**. Trên board, có thể lọc theo **Labels** bằng dynamic query.

## Lưu ý

Đổi tên một giá trị **không tự cập nhật** những chỗ đã lưu giá trị cũ (ví dụ chức danh của thành viên đã chọn trước đó).
