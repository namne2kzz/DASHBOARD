# Search — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-09-26 | 14:02 | Khởi tạo document | Tạo document business ban đầu cho feature Search (command palette Ctrl/Cmd+K) — đã có trong code từ 2026-08-01 nhưng chưa được document |

---

## 1. Purpose

Command palette (Ctrl/Cmd+K) cho phép tìm nhanh work item và backlog item **trong 1 repository** bằng keyword hoặc work-item number, để user nhảy tới item mà không phải duyệt qua board/backlog.

## 2. Key Entities & Relationships

- Không có entity riêng — query read-only trên `SprintTask` và `BacklogItem`.
- `SearchResultItemDto`: kết quả đã normalize chung cho mọi loại entity.
  - `Kind` — `"task"` | `"backlog"`, UI dùng để group + route.
  - `Id`, `Title`.
  - `Subtitle` — dòng context phụ (vd `DASH-12 · Bug` cho task, `UserStory` cho backlog item).

## 3. Business Rules & Invariants

- **Scope theo repository** — search chỉ trong 1 repository, không phải search xuyên repository.
- Caller **phải là member** của repository đó, nếu không → `UnauthorizedAccessException`. Không phân biệt permission chi tiết, chỉ cần là member.
- Keyword < **2 ký tự** → trả về rỗng (không query DB), tránh quét toàn bảng khi user mới gõ 1 chữ.
- Match **case-insensitive**, term được trim + lowercase.
- Phạm vi match:
  - `SprintTask`: `Title`, `Description`, `AcceptanceCriteria`, hoặc khớp `WorkItemNumber`.
  - `BacklogItem`: **chỉ `Title`**.
- **Nhận dạng work-item number**: lấy phần sau dấu `-` cuối cùng của term và parse số — hỗ trợ `DASH-34`, `dash-34`, `34` đều tìm được task số 34. Số phải > 0.
- Giới hạn **6 kết quả mỗi loại** (task và backlog tính riêng) → tối đa 12 kết quả. Không có phân trang.
- Thứ tự: **task trước, backlog sau**. Task sort theo `StateChangedAt` giảm dần (vừa động gần đây lên trước); backlog sort theo `Rank` tăng dần (ưu tiên cao lên trước).

## 4. Main Workflows / Use Cases

1. User bấm Ctrl/Cmd+K → gõ keyword → nhận tối đa 6 task + 6 backlog item → chọn → điều hướng tới item.
2. User gõ trực tiếp work-item key (`DASH-34`) hoặc chỉ số (`34`) → tìm đúng task theo number.

## 5. Definition of Done

- [ ] Search luôn check membership repository trước khi query.
- [ ] Term < 2 ký tự không bao giờ chạm DB.
- [ ] Kết quả luôn bị cap theo `PerKindLimit`, không trả unbounded list.
- [ ] Match không phân biệt hoa/thường.
- [ ] Parse work-item number xử lý được cả 3 dạng: `CODE-n`, `code-n`, `n`.

## 6. Edge Cases & Notes

- `BacklogItem` **không** search theo Description/AcceptanceCriteria (khác với `SprintTask`) — cố ý, không phải bug.
- Term chứa nhiều dấu `-` (vd `multi-part-34`) → chỉ phần cuối (`34`) được thử parse làm number; phần text vẫn được match bình thường.
- Search dùng `Contains` (`LIKE %term%`) → không dùng được index, có thể chậm khi bảng lớn. Chưa có full-text index.
- Task đã soft-delete: query **không** filter `IsDeleted` tường minh ở đây — phụ thuộc global query filter của `IApplicationDbContext`.
- Không search: Repository, User, Discussion, Sprint, Metadata.
