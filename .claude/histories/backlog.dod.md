# Backlog — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
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

## 4. Main Workflows / Use Cases

1. Tạo BacklogItem ở bất kỳ cấp, optional gán parent Epic/Feature.
2. Reorder (drag-and-drop) → tính lại rank phân số.
3. Refine qua các state New → Refining → Ready.
4. Gán SprintId để planning tạm (không đổi refinement state).
5. Promote UserStory `Ready` → tạo `SprintTask` (state New) → BacklogItem chuyển `Committed`.
6. Update acceptance criteria, đính kèm/xoá tài liệu refinement.

## 5. Definition of Done

- [ ] Chỉ cho promote khi Type = UserStory và state = Ready.
- [ ] Rank luôn được re-normalize khi gap < 0.001, không để tràn precision.
- [ ] Gán SprintId không làm thay đổi refinement state.
- [ ] Promote luôn tạo đúng 1 SprintTask tương ứng và set BacklogItem = Committed.

## 6. Edge Cases & Notes

- Item chưa estimate (null story points/T-shirt size) vẫn hợp lệ trong backlog — estimate là optional.
- Re-normalize rank là tự động, ngầm định, không cần action riêng từ user.
