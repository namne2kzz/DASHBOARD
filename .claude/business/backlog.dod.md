# Backlog — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-09-26 | 16:20 | Filter giữ nguyên cây · thống nhất Skipped | (1) Filter `type`/`state` khi list backlog: item khớp filter được giữ **kèm toàn bộ cha ông** để không có item nào mồ côi — trước đó lọc `type=UserStory` làm Epic/Feature cha bị loại, kéo theo US biến mất khỏi kết quả. Cha chỉ được giữ khi dẫn tới ít nhất 1 match, và không kéo theo các con khác không khớp. (2) `BulkDelete` đổi cách đếm `Skipped` cho khớp `BulkUpdateState`: `Affected + Skipped` luôn bằng số item người dùng chọn |
| 2026-07-25 | 17:06 | Thêm bulk operations | Cho phép multi-select nhiều BacklogItem để bulk-change state và bulk-delete (có skip rule chống orphan) |
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document business ban đầu cho feature Backlog |

---

## 1. Purpose

Quản lý product backlog dạng cây Epic → Feature → UserStory, có refinement lifecycle và ranking để ưu tiên thứ tự.

## 2. Key Entities & Relationships

- `BacklogItem`: `Type` (Epic/Feature/UserStory), `ParentId` (3 cấp lồng nhau), optional `SprintId` (gán tạm vào sprint khi planning).
- Estimate: UserStory dùng Fibonacci story points (1–100); Epic/Feature dùng T-shirt size (XS–XL) — không dùng cả 2 cùng lúc.
- Refinement state (New/Refining/Ready/Committed) độc lập với việc gán SprintId.
- Khi promote, sinh ra `SprintTask` tương ứng (xem [sprint-tasks.dod.md](sprint-tasks.dod.md)).

## 3. Business Rules & Invariants

- Chỉ `UserStory` mới được promote vào sprint, và phải ở state `Ready` trước.
- Rank dùng midpoint: rank mới = `(prev + next) / 2`; khi khoảng cách < 0.001 thì re-normalize toàn bộ sibling về spacing 1000.
- Gán SprintId (planning) **không** đổi refinement state — item có thể ở `Refining` nhưng vẫn được gán tạm vào sprint.
- Acceptance criteria và tài liệu refinement được track theo từng item.
- Không cho tạo quan hệ parent vòng (circular); chỉ UserStory được promote.
- **Bulk operations** (cần quyền `ManageBacklog`):
  - Bulk-change state: áp 1 refinement state (New/Refining/Ready) cho nhiều item cùng lúc. Item đang `Committed` bị **skip** (không revert promotion). Không cho bulk set sang `Committed`.
  - Bulk-delete: item có **con không nằm trong danh sách chọn** thì bị **skip** (chống orphan) — giống rule single-delete "không xoá item còn con". Chọn cả cha lẫn con thì xoá được cả cụm.
  - Summary trả về `Affected + Skipped` = **đúng số item người dùng chọn** (cả 2 bulk operation). Id không khớp row nào (đã bị xoá, hoặc thuộc repo khác) tính là `Skipped` — không biến mất khỏi cả 2 cột.
- **Filter khi list backlog** (`type` / `state`): item khớp filter được trả về **kèm toàn bộ cha ông** của nó, giữ nguyên cấu trúc cây Epic → Feature → UserStory. Không có item nào bị mồ côi. Cha chỉ xuất hiện khi dẫn tới ít nhất 1 item khớp (Epic rỗng không lọt vào), và việc được giữ lại **không** kéo theo các con khác không khớp filter.
  - Kết quả trả về summary `{ affected, skipped }` để UI báo số item đã xử lý / bị bỏ qua.

## 4. Main Workflows / Use Cases

1. Tạo BacklogItem ở bất kỳ cấp, optional gán parent Epic/Feature.
2. Reorder (drag-and-drop) → tính lại rank phân số.
3. Refine qua các state New → Refining → Ready.
4. Gán SprintId để planning tạm (không đổi refinement state).
5. Promote UserStory `Ready` → tạo `SprintTask` (state New) → BacklogItem chuyển `Committed`.
6. Update acceptance criteria, đính kèm/xoá tài liệu refinement.
7. Bulk: bật "Select mode" → tick nhiều item (hoặc "Select all" theo filter hiện tại) → đổi state hàng loạt hoặc xoá hàng loạt (có bước xác nhận).

## 5. Definition of Done

- [ ] Chỉ cho promote khi Type = UserStory và state = Ready.
- [ ] Rank luôn được re-normalize khi gap < 0.001, không để tràn precision.
- [ ] Gán SprintId không làm thay đổi refinement state.
- [ ] Promote luôn tạo đúng 1 SprintTask tương ứng và set BacklogItem = Committed.

## 6. Edge Cases & Notes

- Item chưa estimate (null story points/T-shirt size) vẫn hợp lệ trong backlog — estimate là optional.
- Re-normalize rank là tự động, ngầm định, không cần action riêng từ user.
