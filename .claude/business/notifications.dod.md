# Notifications — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-11-07 | 15:35 | Khởi tạo document | Tạo document business ban đầu cho feature Notifications — thông báo in-app (chuông) kèm push realtime qua SSE |

---

## 1. Purpose

Báo cho user biết **việc gì vừa xảy ra liên quan đến họ** mà không cần họ tự đi dò: được giao việc, việc đổi trạng thái, sprint bắt đầu/kết thúc, được thêm vào repository hoặc sprint, bị @mention. Hiển thị ở chuông thông báo, có số chưa đọc, và đẩy realtime xuống trình duyệt đang mở.

## 2. Key Entities & Relationships

- **`Notification`** — thông báo gửi tới **một** user cụ thể (`UserId`). Không có khái niệm thông báo nhóm; mỗi người nhận là một bản ghi riêng.
- Field: `Type` (`NotificationType`), `Title` (dòng ngắn ở dropdown), `Body` (dòng chi tiết — thường là tên work item/sprint), `Url` (deep-link, **null** với loại không điều hướng được), `IsRead`, `ReadAt`, `CreatedAt`.
- Tạo bằng factory `Notification.Create(...)`; đánh dấu đã đọc bằng `MarkRead()` — method này **idempotent**, gọi lại trên bản ghi đã đọc thì không làm gì.
- **`NotificationType`** — 10 loại, chia thành 4 nhóm (category) khi lọc ở UI:

| Category | Loại |
|---|---|
| `workitems` | `WorkItemAssigned` (0) · `WorkItemUnassigned` (1) · `WorkItemStateChanged` (2) |
| `sprints` | `SprintActivated` (3) · `SprintClosed` (4) |
| `membership` | `AddedToRepository` (6) · `RemovedFromRepository` (7) · `AddedToSprint` (8) · `RemovedFromSprint` (9) |
| `mentions` | `MentionedInDiscussion` (5) |

## 3. Business Rules & Invariants

**Quyền xem**

- User **chỉ thấy notification của chính mình** — mọi truy vấn đều lọc `UserId == currentUser`. Không có đường nào xem được của người khác.
- Chưa đăng nhập → `UnauthorizedAccessException`.
- Đánh dấu đã đọc một notification không thuộc về mình → `NotFoundException` (không phải Forbidden — không tiết lộ là bản ghi đó có tồn tại).

**Ai được nhận**

- **Không bao giờ tự thông báo cho chính người gây ra hành động** (`newId != changedById`). Mình tự đổi việc của mình thì không nhận chuông.
- Notification tôn trọng **preference của người nhận** (cờ `pushOn` lấy từ user settings) — tắt thì không tạo bản ghi.
- Việc gửi notification **không được làm hỏng nghiệp vụ chính**: `NotificationService` bọc toàn bộ trong `try/catch`, lỗi chỉ ghi log ở mức Warning. Giao việc vẫn thành công kể cả khi thông báo thất bại.

**Đọc / chưa đọc**

- **Số chưa đọc luôn tính trên toàn bộ hộp thư**, không phụ thuộc filter hay trang đang xem. Đang lọc "mentions" vẫn thấy tổng số chưa đọc thật.
- `MarkRead()` đóng dấu `ReadAt` bằng giờ UTC tại thời điểm đọc.
- "Đánh dấu tất cả đã đọc" chỉ tác động lên notification **chưa đọc** của chính user; không đụng bản ghi đã đọc (giữ nguyên `ReadAt` cũ).

**Phân trang**

- Kích thước trang tối đa **50**; giá trị lớn hơn bị kẹp xuống, nhỏ hơn 1 nâng lên 1. `Skip` âm coi như 0.
- Sắp xếp cố định: **mới nhất trước** (`CreatedAt` giảm dần).
- Cờ `hasMore` cho biết còn trang sau hay không.

**Nguồn sinh notification** — hiện có 11 handler gọi `INotificationService`:

| Nghiệp vụ | Loại sinh ra |
|---|---|
| Gán / bỏ gán sprint task | `WorkItemAssigned` · `WorkItemUnassigned` |
| Đổi trạng thái sprint task | `WorkItemStateChanged` |
| Kích hoạt / đóng sprint | `SprintActivated` · `SprintClosed` |
| Thêm / xóa member repository | `AddedToRepository` · `RemovedFromRepository` |
| Thêm / xóa member khỏi capacity sprint | `AddedToSprint` · `RemovedFromSprint` |
| Có @mention trong discussion | `MentionedInDiscussion` |

## 4. Main Workflows / Use Cases

1. **Nhận realtime** — user mở app, trình duyệt giữ kết nối SSE tới `GET /api/v{version}/notifications/stream`. Có việc liên quan → notification được lưu DB rồi đẩy ngay xuống kết nối đang mở. User offline thì bỏ qua bước đẩy, lần sau đăng nhập vẫn thấy trong danh sách.
2. **Xem hộp thư** — bấm chuông → `GET /notifications` trả về trang notification + số chưa đọc + cờ còn trang sau. Lọc được theo category (workitems / sprints / membership / mentions) hoặc chỉ xem chưa đọc.
3. **Đọc một thông báo** — `POST /notifications/{id}/read`, rồi điều hướng theo `Url` nếu có.
4. **Đọc tất cả** — `POST /notifications/read-all`, dọn sạch badge trong một thao tác.

## 5. Definition of Done

- [ ] User chỉ thấy và chỉ sửa được notification của chính mình.
- [ ] Người gây ra hành động không nhận notification về hành động của chính mình.
- [ ] Tắt preference thì không sinh notification.
- [ ] Lỗi khi gửi notification không làm fail nghiệp vụ gốc.
- [ ] Số chưa đọc tính trên toàn hộp thư, không theo filter hiện tại.
- [ ] Phân trang kẹp đúng giới hạn (1–50) và trả `hasMore` chính xác.
- [ ] SSE đẩy được tới user đang mở app; user offline vẫn nhận khi quay lại.

## 6. Edge Cases & Notes

- `Url` có thể **null** — loại thông báo không điều hướng được thì UI chỉ hiển thị, không cho click.
- Đọc một notification đã đọc rồi: không lỗi, không đổi `ReadAt` (do `MarkRead()` thoát sớm).
- "Đánh dấu tất cả đã đọc" chạy bằng một câu `UPDATE` trực tiếp (`ExecuteUpdate`), **không đi qua `MarkRead()`** của entity. Hệ quả: nếu sau này thêm logic vào `MarkRead()` thì luồng bulk sẽ không chạy logic đó — cần sửa cả hai chỗ.
- Notification **không bị xóa tự động**. Hiện chưa có cơ chế dọn bản ghi cũ; hộp thư của user hoạt động lâu sẽ dài dần.
- Notification đã tạo là **ảnh chụp tại thời điểm đó** — `Title`/`Body` không cập nhật theo nếu work item sau đó đổi tên.
- Xóa work item/sprint không xóa notification trỏ tới nó; click vào `Url` có thể dẫn tới trang không còn tồn tại.
