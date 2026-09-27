# SprintTasks — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-09-26 | 17:40 | Fix badge sub-task luôn hiện 0 | `ListStandaloneItems` đếm `t.SubTasks.Count(...)` **sau** khi `ToListAsync()` nhưng không `Include(t => t.SubTasks)` → navigation rỗng, badge số sub-task ở danh sách standalone **luôn hiện 0** dù story có bao nhiêu sub-task. Đã thêm `Include`. Phát hiện khi viết unit test |
| 2026-09-26 | 16:20 | LogWork yêu cầu quyền EditWorkItem | `LogWork` trước đây chỉ check `IsMemberOfAsync`. Vì log work có thể **tự đóng task** (Remaining = 0 → Done), member thường đóng được work item mà không cần `EditWorkItem` — đi vòng qua đúng lớp quyền mà BUG-022 đã bịt ở `UpdateSprintTask`. Nay `LogWork` yêu cầu `EditWorkItem`. `ChangeSprintTaskState` giữ nguyên chỉ cần là member (kéo card trên board là thao tác thường ngày) |
| 2026-09-26 | 14:02 | Bổ sung field + link HUB | Ghi nhận thêm `Priority`, `UnitTest`, `DesignReview`, `Documents` vào mô tả entity; work item có thể được HUB tham chiếu để gắn thread thảo luận (internal API) — xem [hub-integration.dod.md](hub-integration.dod.md) |
| 2026-07-25 | 17:35 | Gắn metadata catalog | Work item gắn được value catalog (Labels/Components/Version…) qua bảng join `WorkItemMetadata`; Labels hiện chip trên board card |
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document business ban đầu cho feature SprintTasks |

---

## 1. Purpose

Entity thực thi công việc thống nhất, gồm 4 type: UserStory (từ promote backlog), Task, Bug, TestPlan — track state, estimate, work log.

## 2. Key Entities & Relationships

- `SprintTask`: 4 `Type` (UserStory/Task/Bug/TestPlan), `Priority` (`WorkItemPriority`, default Medium), field riêng theo type (Bug: StepsToReproduce/Environment/RootCause/Solution/Impaction; TestPlan: TestSteps/Automated; chất lượng: UnitTest/DesignReview), `Documents` (list link tài liệu).
- `ParentId`: hỗ trợ sub-task lồng nhau. `BacklogItemId`: link tới backlog item gốc nếu được promote.
- `WorkItemNumber`: tự tăng theo Repository, hiển thị dạng `{Repository.Code}-{number}` (vd `DASH-3`).
- `ClosedAt`, `StateChangedAt`: timestamp lifecycle.
- Estimate: UserStory dùng `StoryPoints`; Task/Bug dùng `OriginalEstimate` (giờ); track riêng `RemainingWork` và `CompletedWork`.
- Mỗi thay đổi sinh `HistoryEntry` (xem [history.dod.md](history.dod.md)); card hiển thị trên board (xem [boards.dod.md](boards.dod.md), cấu hình cột ở [workflow.dod.md](workflow.dod.md)).

## 3. Business Rules & Invariants

- Chỉ UserStory ở state `Ready` (từ Backlog) mới được promote; tạo SprintTask state = New, kế thừa story points.
- State transition: New → Backlog → Todo → Active → InReview → Done. Mỗi transition ghi history; chuyển sang Done tự động set `RemainingWork = 0` và stamp `ClosedAt`.
- LogWork: cần quyền `EditWorkItem` (vì có thể tự đóng item); số giờ log phải > 0, `RemainingWork` ≥ 0; nếu RemainingWork về 0 → tự động chuyển state Done.
- Đổi state thủ công (`ChangeSprintTaskState`) chỉ cần **là member** của repo — kéo card trên board không yêu cầu `EditWorkItem`. Mọi đường khác dẫn tới việc sửa/đóng work item (`UpdateSprintTask`, `LogWork`) đều cần `EditWorkItem`.
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
- Work item được HUB tham chiếu khi gắn thread thảo luận: HUB đọc key/title/state/repository qua internal API `GET /internal/v1/work-items/{id}` — xem [hub-integration.dod.md](hub-integration.dod.md).
- Work item tìm được qua command palette Ctrl/Cmd+K theo title, description, acceptance criteria, hoặc số (`DASH-34`) — xem [search.dod.md](search.dod.md).
