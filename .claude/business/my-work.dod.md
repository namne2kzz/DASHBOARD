# My Work — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-10-28 | — | Lời chào tách khỏi banner | Banner trên cùng trả về đúng chuẩn các màn khác (`My Work` + nút Refresh, `px-8 py-4`, `text-3xl`). Lời chào + ngày + câu tóm tắt chuyển xuống **1 card riêng** ở đầu cột trái. Lý do: banner định danh màn hình, lời chào là ghi chú theo ngày — hai loại nội dung khác nhau, nhét chung làm banner phình to và lệch với mọi màn còn lại. Nội dung và logic của lời chào không đổi. |
| 2026-10-27 | 23:19 | Lời chào + tóm tắt ngày + empty state N-bot | Thêm lời chào theo giờ ("Good morning, Nam") và **1 câu tóm tắt** hình dạng công việc trong ngày: tổng việc đang mở, trong đó bao nhiêu High/Critical, và có việc nào đang chờ review không. Lý do: hình dạng của ngày đọc bằng câu nhanh hơn đọc bằng 4 ô số (4 ô số vẫn giữ ở panel "Today" cho ai muốn đọc thẳng). Không có việc nào → câu đổi thành "Nothing is assigned to you right now.". **Empty state dùng N-bot**: hết việc → bot vui (đây là tin tốt); filter không khớp gì → bot ngủ (không phải thành tích); không có gì chờ review → bot ngủ ở panel bên phải. |
| 2026-07-25 | 17:06 | Khởi tạo document | Tạo feature "My Work" — view cross-repository các work item được assign cho user hiện tại |

---

## 1. Purpose

Cho mỗi user một màn hình cá nhân tổng hợp **tất cả work item đang mở được gán cho họ** trên **mọi repository họ là member** — thay vì phải mở từng repo/board để kiểm tra. Là điểm khởi đầu tự nhiên sau khi đăng nhập.

## 2. Key Entities & Relationships

- Không có entity mới — là **read model** tổng hợp trên `SprintTask` (đã có `AssignedToId`, `RepositoryId`, `SprintId`, `State`, `Priority`, `Type`).
- Phạm vi hiển thị = các `Repository` mà user có `RepositoryMember` (không tính repo đã `IsArchived`).
- Mỗi dòng kèm `RepositoryCode`/`RepositoryName` và `SprintName` để hiển thị và điều hướng cross-repo.

## 3. Business Rules & Invariants

- Chỉ trả về work item có `AssignedToId == currentUser`.
- Chỉ trong các repository user đang là member; repo `IsArchived` bị loại.
- Chỉ item **đang mở**: loại trạng thái `Done` (State = 5).
- Không phụ thuộc repo context đang chọn — đây là route global (`/my-work`), truy cập được kể cả khi user chưa/không có repo đang active.
- Sắp xếp mặc định: Priority giảm dần → RepositoryCode → State.
- Read-only: feature này chỉ hiển thị + điều hướng, không sửa dữ liệu.

## 4. Main Workflows / Use Cases

1. User mở "My Work" (mục General ở sidebar) → thấy toàn bộ item đang mở gán cho mình, kèm số liệu tổng (Open / Active / In review / số Repositories).
2. Đổi cách nhóm: theo **Sprint** (mặc định), **Priority**, hoặc **State**.
3. Search theo title / work item number / tên repository.
4. Click 1 item → điều hướng sang board của repository chứa item đó (`/{repoCode}/boards`).

## 5. Definition of Done

- [ ] Chỉ hiện item assigned cho chính user, trong repo user là member, không archived, không Done.
- [ ] Truy cập được ở route global, không lệ thuộc repo đang chọn.
- [ ] Group theo Sprint/Priority/State và search hoạt động client-side trên toàn bộ danh sách.
- [ ] Click item điều hướng đúng sang repo tương ứng và set repo context.

## 6. Edge Cases & Notes

- User không có item nào đang mở → hiển thị trạng thái "You're all caught up".
- User không thuộc repo nào → trả về danh sách rỗng (không lỗi).
- "Due state" trong story gốc chưa áp dụng: hệ thống hiện chưa có trường due date cho work item, nên grouping chỉ gồm Sprint/Priority/State.
- UserStory thường không được assign trực tiếp (assignee ở cấp Task/Bug), nên hiếm khi xuất hiện; nếu có assigned thì vẫn hiển thị.
