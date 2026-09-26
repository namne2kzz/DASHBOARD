# SprintTasks — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-07-25 | 17:35 | Gắn metadata catalog | Work item gắn được value catalog (Labels/Components/Version…) qua bảng join `WorkItemMetadata`; Labels hiện chip trên board card |
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document business ban đầu cho feature SprintTasks |

---

## 1. Purpose

Entity thực thi công việc thống nhất, gồm 4 type: UserStory (từ promote backlog), Task, Bug, TestPlan — track state, estimate, work log.

## 2. Key Entities & Relationships

- `SprintTask`: 4 `Type` (UserStory/Task/Bug/TestPlan), field riêng theo type (Bug: StepsToReproduce/Environment/RootCause/Solution/Impaction; TestPlan: TestSteps/Automated).
- `ParentId`: hỗ trợ sub-task lồng nhau. `BacklogItemId`: link tới backlog item gốc nếu được promote.
- `WorkItemNumber`: tự tăng theo Repository, hiển thị dạng `{Repository.Code}-{number}` (vd `DASH-3`).
- `ClosedAt`, `StateChangedAt`: timestamp lifecycle.
- Estimate: UserStory dùng `StoryPoints`; Task/Bug dùng `OriginalEstimate` (giờ); track riêng `RemainingWork` và `CompletedWork`.
- Mỗi thay đổi sinh `HistoryEntry` (xem [history.dod.md](history.dod.md)) và hiển thị trên [smart-board.dod.md](smart-board.dod.md).

## 3. Business Rules & Invariants

- Chỉ UserStory ở state `Ready` (từ Backlog) mới được promote; tạo SprintTask state = New, kế thừa story points.
- State transition: New → Backlog → Todo → Active → InReview → Done. Mỗi transition ghi history; chuyển sang Done tự động set `RemainingWork = 0` và stamp `ClosedAt`.
- LogWork: số giờ log phải > 0, `RemainingWork` ≥ 0; nếu RemainingWork về 0 → tự động chuyển state Done.
- Assign: assignee phải là member của Repository; unassign (null) được phép.
- Item standalone (`SprintId = null`) hợp lệ — dùng cho Bug/TestPlan không gắn sprint cụ thể.
- **Metadata catalog**: work item gắn N value từ catalog `RepositoryMetadata` (Labels, Components, versions…) qua bảng join `WorkItemMetadata` (1 cơ chế chung cho mọi key, không đẻ entity riêng). Chỉ nhận value thuộc repo hoặc `IsGlobal`; cần quyền `EditWorkItem` để đổi. Xem [metadata.dod.md](metadata.dod.md).

## 4. Main Workflows / Use Cases

1. Tạo task trực tiếp (Task/Bug/TestPlan) hoặc promote UserStory từ Backlog.
2. Assign/unassign member → sinh history record kèm tên member.
3. Chuyển state theo lifecycle New → ... → Done.
4. Log work giờ làm → cộng `CompletedWork`, nhận `RemainingWork` mới.
5. Update detail (title, description, field riêng theo type).
6. Tạo sub-task (`ParentId`); query parent story cho view hierarchy.

## 5. Definition of Done

- [ ] Promote chỉ xảy ra với UserStory state Ready, tạo đúng 1 SprintTask liên kết `BacklogItemId`.
- [ ] Mọi state transition và assignment đều sinh `HistoryEntry` tương ứng.
- [ ] LogWork validate giờ > 0 và RemainingWork ≥ 0, tự đóng task khi Remaining = 0.
- [ ] Assignee luôn được validate là member hợp lệ của Repository trước khi gán.

## 6. Edge Cases & Notes

- Bug/TestPlan standalone (không Sprint) vẫn có đầy đủ lifecycle.
- Bất kỳ task nào cũng có thể tạo sub-task, không giới hạn theo Type.
- Log work có thể tự động đóng task dù chưa chuyển state thủ công.
