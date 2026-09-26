# History — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document business ban đầu cho feature History |

---

## 1. Purpose

Audit trail bất biến (immutable), ghi tự động mọi thay đổi của 1 `SprintTask` — ai đổi gì, lúc nào.

## 2. Key Entities & Relationships

- `HistoryEntry`: `SprintTaskId`, `RepositoryId` (denormalize), `AuthorId` (người gây ra thay đổi), `Message` (mô tả thay đổi dạng human-readable), `CreatedAt` (UTC, không đổi sau khi tạo).
- Many-to-one với `SprintTask` và `User` (author).

## 3. Business Rules & Invariants

- HistoryEntry **chỉ được tạo tự động** bởi application layer — không có API cho user tự insert.
- Chỉ member của Repository được xem history của task trong Repository đó.
- Entry được ghi trong cùng `SaveChangesAsync` với thay đổi gốc (transactional — không thể có thay đổi mà thiếu history).
- Entry append-only: không edit, không xoá — đúng tính chất audit trail thật.
- `Message` là free-form, do application layer generate (vd "State changed from Todo to Active", "Assigned to John Doe").

## 4. Main Workflows / Use Cases

Tự động sinh entry khi:
- Tạo SprintTask mới.
- Đổi state (Todo → Active → InReview → Done...).
- Assign/reassign SprintTask.
- Promote BacklogItem thành SprintTask.

Team xem timeline thay đổi của 1 task (sort mới nhất trước) để troubleshoot hoặc audit.

## 5. Definition of Done

- [ ] Mọi action thay đổi SprintTask (create, state change, assign) đều sinh đúng 1 HistoryEntry tương ứng trong cùng transaction.
- [ ] Không tồn tại endpoint/command cho phép user chỉnh sửa hoặc xoá HistoryEntry.
- [ ] Message luôn đủ rõ nghĩa để người đọc hiểu chuyện gì đã xảy ra (không chỉ là field name kỹ thuật).

## 6. Edge Cases & Notes

- Nếu task bị soft-delete, history liên quan trở thành orphan nhưng vẫn query được.
- Nếu author bị xoá khỏi hệ thống sau đó, entry vẫn giữ `AuthorId` cũ (stale reference) — không tự cập nhật lại tên.
- Format `Message` phụ thuộc hoàn toàn vào application layer generate — cần nhất quán khi thêm action mới sinh history.
