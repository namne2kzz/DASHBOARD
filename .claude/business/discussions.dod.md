# Discussions — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document business ban đầu cho feature Discussions |
| 2026-11-20 | 21:10 | Reply + edit/delete qua API | Thêm `ParentId` (thread 2 tầng, flat), endpoint PUT/DELETE/replies, phân trang reply, và dọn ảnh MinIO khi edit/delete |

---

## 1. Purpose

Comment thread gắn trên 1 `SprintTask` (work item) để team thảo luận implementation, blocker, review feedback.

## 2. Key Entities & Relationships

- `DiscussionEntry`: `AuthorId`, `Body` (Markdown, ≤ 10,000 ký tự), `SprintTaskId`, `RepositoryId` (denormalize để query nhanh theo Repository), `ParentId` (nullable).
- Many-to-one với `SprintTask` và `User` (author). Hỗ trợ soft-delete.
- Self-reference qua `ParentId`: `null` = comment gốc, có giá trị = reply. FK dùng `Restrict` (SQL Server không cho self-referencing cascade).

## 3. Business Rules & Invariants

- Chỉ member của Repository được post comment.
- Tạo comment phải validate `SprintTaskId` tồn tại trong cùng Repository.
- `Body` không được rỗng, tối đa 10,000 ký tự.
- Chỉ author gốc được edit comment của mình.
- Xoá comment: author gốc **hoặc** Global Admin.
- **Thread tối đa 2 tầng.** Reply vào 1 reply sẽ được re-parent về đúng comment gốc của reply đó, không bao giờ lồng sâu hơn. Lý do: UI chỉ thụt 1 cấp, nên data không được sâu hơn thứ hiển thị.
- `ParentId` phải trỏ tới comment thuộc **cùng SprintTask + Repository**; id lạ → `NotFoundException` (chặn ghép comment sang work item khác).
- Xoá comment gốc thì xoá luôn toàn bộ reply của nó.
- **Ảnh dán trong comment không có record riêng** — body là nơi duy nhất ghi nhận ảnh đang được dùng. Nên khi edit gỡ ảnh, hoặc xoá comment, object tương ứng trên MinIO bị xoá theo (gồm cả ảnh trong các reply bị xoá kèm).
- Chỉ xoá ảnh thuộc bucket attachment của hệ thống; ảnh host ngoài (URL lạ) không đụng tới.
- Việc xoá ảnh chạy **sau khi commit** và nuốt lỗi: mất object store không được làm hỏng thao tác edit/delete của user.
- Soft-delete giữ lại record để audit, không hard-delete.
- Author và thời điểm tạo (`CreatedAt`) không đổi sau khi post (immutable).

## 4. Main Workflows / Use Cases

1. Member post comment trên 1 SprintTask.
2. Author edit lại comment của mình.
3. Author hoặc Global Admin soft-delete comment.
4. Query comment của 1 task, sort theo thời gian tạo, loại trừ comment đã soft-delete.
5. Member reply vào 1 comment (gốc hoặc reply) — hệ thống tự gắn về comment gốc.
6. Đọc thêm reply: list chỉ trả **1 reply đầu** mỗi comment gốc kèm `ReplyCount`; phần còn lại lấy qua `GET .../discussions/{id}/replies?skip&take` (tối đa 50/lần). Tránh tải hết thread dài ngay khi mở task.

## 5. Definition of Done

- [ ] Validate SprintTaskId tồn tại + cùng Repository trước khi tạo comment.
- [ ] Edit chỉ cho phép đúng author gốc, không cho author khác hoặc non-admin sửa.
- [ ] Delete chỉ cho phép author gốc hoặc Global Admin.
- [ ] Body validate non-empty + giới hạn 10,000 ký tự ở tầng command validation (cả khi tạo lẫn khi edit).
- [ ] Reply vào reply phải nằm cùng cấp với reply đó, không tạo cây sâu dần.
- [ ] `ParentId` khác task/repository bị từ chối.
- [ ] Xoá comment gốc kéo theo reply của nó.
- [ ] Edit gỡ ảnh / xoá comment thì ảnh trên MinIO bị xoá; ảnh giữ lại hoặc ảnh host ngoài thì không.
- [ ] Object store lỗi không được làm fail thao tác edit/delete.

## 6. Edge Cases & Notes

- Nếu author bị xoá khỏi hệ thống sau đó, comment vẫn còn với `AuthorId` cũ (stale reference).
- Comment soft-delete có thể vẫn reference 1 work item đã bị xoá riêng (orphan).
