# Discussions — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document business ban đầu cho feature Discussions |

---

## 1. Purpose

Comment thread gắn trên 1 `SprintTask` (work item) để team thảo luận implementation, blocker, review feedback.

## 2. Key Entities & Relationships

- `DiscussionEntry`: `AuthorId`, `Body` (Markdown, ≤ 10,000 ký tự), `SprintTaskId`, `RepositoryId` (denormalize để query nhanh theo Repository).
- Many-to-one với `SprintTask` và `User` (author). Hỗ trợ soft-delete.

## 3. Business Rules & Invariants

- Chỉ member của Repository được post comment.
- Tạo comment phải validate `SprintTaskId` tồn tại trong cùng Repository.
- `Body` không được rỗng, tối đa 10,000 ký tự.
- Chỉ author gốc được edit comment của mình.
- Xoá comment: author gốc **hoặc** Global Admin.
- Soft-delete giữ lại record để audit, không hard-delete.
- Author và thời điểm tạo (`CreatedAt`) không đổi sau khi post (immutable).

## 4. Main Workflows / Use Cases

1. Member post comment trên 1 SprintTask.
2. Author edit lại comment của mình.
3. Author hoặc Global Admin soft-delete comment.
4. Query comment của 1 task, sort theo thời gian tạo, loại trừ comment đã soft-delete.

## 5. Definition of Done

- [ ] Validate SprintTaskId tồn tại + cùng Repository trước khi tạo comment.
- [ ] Edit chỉ cho phép đúng author gốc, không cho author khác hoặc non-admin sửa.
- [ ] Delete chỉ cho phép author gốc hoặc Global Admin.
- [ ] Body validate non-empty + giới hạn 10,000 ký tự ở tầng command validation.

## 6. Edge Cases & Notes

- Nếu author bị xoá khỏi hệ thống sau đó, comment vẫn còn với `AuthorId` cũ (stale reference).
- Comment soft-delete có thể vẫn reference 1 work item đã bị xoá riêng (orphan).
