# Board Item Detail Dialog (Sprint Task Detail) — Test

> Đọc `../RULES.md` trước khi sửa file này.

## Run Log

| Ngày | Giờ | Kết quả | Bug mới | Ghi chú |
|------|-----|---------|---------|---------|
| 2026-07-04 | 12:45 | ⚠️ Partial | BUG-023 | Lần chạy đầu tiên — soạn 23 case rồi chạy qua UI thật (Playwright msedge) + API trực tiếp trên card DASH-44 (Bug) và DASH-22 (UserStory). Pass: bd-01,02(ngầm qua tab switch),03,05,07,08,09,12,13,14,15,16,17,18,21,22. Phát hiện phụ ngoài case đã soạn: search theo Work Item Number trên board hoàn toàn không hoạt động (case-sensitivity bug ở `sprint-board.service.ts:101`) — ghi **BUG-023**, đồng thời sửa lại boards-12 (đã ghi Pass sai trước đó) thành Fail trong `boards.test.md`. Cũng phát hiện: sau khi đổi assignee, nút Save tự bật sáng dù không sửa gì (false-positive dirty) — ghi **IMP-004**, không phải bug (không sai dữ liệu). BUG-022 (UpdateSprintTaskCommandHandler thiếu check EditWorkItem) chỉ verify qua code review, chưa demo được qua UI vì hiện không còn role nào thiếu EditWorkItem. Case chưa chạy: bd-04,06,10(cần custom role),11,19,20,23 (ưu tiên thấp hơn, để lần sau). |

---

## Test Cases

> **Component**: `SprintTaskDetailDialogComponent` — mở khi click 1 card trên board (`kanban-board.component.ts` → `dialog.open(SprintTaskDetailDialogComponent, ...)`).
> **Business rule**: [sprint-tasks.dod.md](../../histories/sprint-tasks.dod.md), [discussions.dod.md](../../histories/discussions.dod.md), [history.dod.md](../../histories/history.dod.md).
> **API base**: `http://localhost:5152/api/repositories/{repoId}/sprint-tasks/{taskId}` (detail/discussions/history) + `http://localhost:5152/api/repositories/{repoId}/sprints/{sprintId}/tasks/{taskId}` (state/assign).
> **Data hiện có**: repo DASH, sprint active "Sprint 1 July 2026" (`275c24bc-fc54-426a-bf54-a7a2cf73cc12`).

---

## A — Mở dialog & Load dữ liệu

### bd-01 — Click card mở dialog, load song song detail/discussions/history

- **Business rule**: `SprintTaskDetailService.loadDetail()` dùng `forkJoin` gọi 3 API cùng lúc.
- **Bước thực hiện**: Trên board, click 1 card bất kỳ.
- **Kết quả mong đợi**: Dialog "Sprint Task Detail" mở ra, tab mặc định "Details" active, đủ 3 tab Details/Discussion/History hiển thị. Không lỗi console.

### bd-02 — Chuyển tab Details/Discussion/History giữ đúng nội dung

- **Business rule**: `activeTab` signal — 'details' | 'discussion' | 'history'.
- **Bước thực hiện**: Click lần lượt 3 tab.
- **Kết quả mong đợi**: Mỗi tab hiển thị đúng nội dung tương ứng (form edit / danh sách comment / timeline audit), không mất dữ liệu đã nhập ở tab Details khi chuyển qua lại.

---

## B — Edit Details tab

### bd-03 — Title bắt buộc, Save disable khi title rỗng

- **Business rule**: `canSave = isDirty() && title().trim().length > 0`.
- **Bước thực hiện**: Xoá trắng ô Title.
- **Kết quả mong đợi**: Nút Save bị `disabled`.

### bd-04 — Sửa Title/Description/Priority → Save → board card đồng bộ ngay (patchItem)

- **Business rule**: [sprint-tasks.dod.md](../../histories/sprint-tasks.dod.md) §4.5 — "Update detail"; `updateTask()` gọi `board.patchItem()` cập nhật cục bộ, không reload toàn board.
- **Bước thực hiện**: Đổi Title + Priority, bấm Save.
- **Kết quả mong đợi**: `PUT /sprint-tasks/{id}` trả 204. Đóng dialog (hoặc quan sát board phía sau) → card trên board hiện đúng title/priority mới ngay lập tức, không cần F5.

### bd-05 — Field riêng Bug chỉ hiện khi Type=Bug

- **Business rule**: `isBug` computed — StepsToReproduce/Environment/RootCause/Solution/Impaction chỉ render khi `type === Bug`.
- **Bước thực hiện**: Mở dialog cho 1 card Type=Bug, rồi mở dialog cho 1 card Type=Task.
- **Kết quả mong đợi**: Card Bug thấy đủ 5 field trên; card Task không thấy field nào trong 5 field đó.

### bd-06 — Field riêng TestPlan (TestSteps/Automated) chỉ hiện khi Type=TestPlan

- **Business rule**: `isTestPlan` computed.
- **Bước thực hiện**: Mở dialog cho 1 card Type=TestPlan.
- **Kết quả mong đợi**: Thấy danh sách TestSteps (có thể add/remove row qua `addTestStep()`/`removeTestStep()`) và checkbox Automated. Card Type khác không thấy 2 field này.

### bd-07 — Dev notes (UnitTest/DesignReview) hiện cho cả Task và Bug

- **Business rule**: `showDevNotes = type === Bug || type === Task`.
- **Bước thực hiện**: Mở dialog cho Task, Bug, TestPlan, UserStory lần lượt.
- **Kết quả mong đợi**: Chỉ Task và Bug thấy 2 field Unit test notes / Design review notes; TestPlan và UserStory không thấy.

### bd-08 — Estimate field chỉ hiện đúng loại theo Type

- **Business rule**: `showEstimate = !isTestPlan && !isUserStory`; UserStory dùng StoryPoints riêng (không qua field OriginalEstimate).
- **Bước thực hiện**: So sánh dialog của Task/Bug (có OriginalEstimate) với TestPlan/UserStory (không có).
- **Kết quả mong đợi**: `showEstimate` đúng theo Type — Task/Bug thấy "Estimate (hours)"; TestPlan/UserStory không thấy field này.

### bd-09 — Save chỉ enable khi có thay đổi thật (isDirty)

- **Business rule**: `isDirty = JSON.stringify(payload()) !== snapshot()`.
- **Bước thực hiện**: Mở dialog, không sửa gì, quan sát nút Save.
- **Kết quả mong đợi**: Save `disabled` khi chưa sửa gì (dù title không rỗng). Gõ thêm 1 ký tự rồi xoá lại (về nguyên trạng) → Save vẫn `disabled` (so sánh JSON snapshot, không phải "đã từng gõ").

### bd-10 — ⚠️ UpdateSprintTaskCommandHandler KHÔNG check EditWorkItem (BUG-022)

- **Business rule**: Theo thiết kế phân quyền, sửa detail work item cần `SystemFunction.EditWorkItem` — nhưng code hiện tại (`UpdateSprintTaskCommandHandler.cs:25-26`) chỉ check `IsMemberOfAsync`.
- **Bước thực hiện**: Nếu có role thiếu `EditWorkItem` (hiện tại KHÔNG có role mặc định nào thiếu — cần tạo custom role riêng để demo), đăng nhập user đó, mở dialog, sửa Title, Save.
- **Kết quả mong đợi theo thiết kế**: Nên bị chặn 400 "You do not have permission to edit work items...".
- **Kết quả thực tế theo code**: Save thành công (204) bất kể quyền `EditWorkItem`. Đã ghi **BUG-022** (High), Open — verify qua code review, khuyến nghị tạo custom role không có EditWorkItem để demo qua UI khi cần.

### bd-11 — Update UserStory kèm AssignedToId bị âm thầm null hoá

- **Business rule**: `UpdateSprintTaskCommandHandler.cs:39` — `task.AssignedToId = task.Type == UserStory ? null : command.AssignedToId`.
- **Bước thực hiện**: Mở dialog 1 UserStory, cố set assignee qua field nào đó rồi Save (nếu UI cho phép chọn — cần verify UI có ẩn hẳn field assignee cho UserStory hay không).
- **Kết quả mong đợi**: `AssignedToId` luôn về `null` sau Save nếu Type=UserStory, không có thông báo cảnh báo cho user rằng field bị bỏ qua (chỉ ghi nhận hành vi, không phải bug nghiêm trọng vì không gây sai dữ liệu).

---

## C — State change (inline)

### bd-12 — Đổi state qua dropdown → PATCH ngay, history reload

- **Business rule**: `onStateChange()` gọi `changeState()` → PATCH `.../state` ngay khi chọn, không cần bấm Save riêng.
- **Bước thực hiện**: Đổi state dropdown trong dialog từ state hiện tại sang state khác.
- **Kết quả mong đợi**: `PATCH .../tasks/{id}/state?newState=X` gửi ngay lập tức. Tab History có thêm entry mới "State changed from '...' to '...'." Board card phía sau cũng cập nhật cột tương ứng (`board.patchItem` với `apiState` mới).

### bd-13 — State timeline parse đúng thứ tự từ history

- **Business rule**: `stateTimeline` computed — parse regex `State changed from 'X' to 'Y'.` từ history (đảo ngược để oldest-first), fallback về state hiện tại nếu task chưa từng transition.
- **Bước thực hiện**: Với 1 task đã qua ít nhất 2 lần đổi state, xem timeline hiển thị trong dialog.
- **Kết quả mong đợi**: Timeline hiện đúng chuỗi trạng thái theo thứ tự thời gian tăng dần, kèm tên người đổi + thời điểm cho mỗi bước (trừ bước đầu tiên — chỉ có state, không có author/createdAt).

---

## D — Assignee change (inline)

### bd-14 — Đổi assignee qua picker → PATCH ngay, history reload

- **Business rule**: `selectAssignee()` gọi `changeAssignee()` → PATCH `.../assign` ngay khi chọn.
- **Bước thực hiện**: Mở assignee picker, chọn 1 member khác.
- **Kết quả mong đợi**: PATCH gửi ngay, History có thêm entry "Assigned to '{name}'." hoặc "Unassigned." Board card cập nhật avatar/tên ngay.

### bd-15 — Assignee picker chỉ liệt kê CapacityMember của sprint (giống boards-07)

- **Business rule**: `filteredAssigneeMembers` lấy từ `board.capacityMembers()`.
- **Bước thực hiện**: Mở picker, đếm số member hiển thị.
- **Kết quả mong đợi**: Danh sách khớp đúng capacity members của sprint hiện tại, không phải toàn bộ repo member.

### bd-16 — Search trong assignee picker filter đúng theo tên

- **Business rule**: `filteredAssigneeMembers` filter theo `userName.toLowerCase().includes(q)`.
- **Bước thực hiện**: Gõ 1 phần tên vào ô search trong picker.
- **Kết quả mong đợi**: Chỉ member khớp substring (không phân biệt hoa/thường) hiển thị.

---

## E — Discussion tab

### bd-17 — Post comment → hiện ngay trong list

- **Business rule**: [discussions.dod.md](../../histories/discussions.dod.md) §4.1; `postComment()` append vào cuối list local sau khi POST thành công.
- **Bước thực hiện**: Gõ nội dung vào ô comment, bấm Post/Enter.
- **Kết quả mong đợi**: `POST .../discussions` trả 201. Comment mới xuất hiện ngay cuối danh sách, không cần reload toàn bộ discussion.

### bd-18 — Comment rỗng bị chặn ở FE, không gửi request

- **Business rule**: `postComment()` — `if (!this.commentDraft().trim()) return;`.
- **Bước thực hiện**: Để trống ô comment (hoặc toàn khoảng trắng), bấm Post.
- **Kết quả mong đợi**: Không có request POST nào gửi đi (verify qua `browser_network_requests`).

### bd-19 — Comment > 10.000 ký tự bị chặn

- **Business rule**: `AddDiscussionCommandValidator.cs:13` — `Body.MaximumLength(10_000)`.
- **Bước thực hiện**: Gọi `POST .../discussions` với body 10.001 ký tự.
- **Kết quả mong đợi**: 422 Validation Error. Kiểm tra thêm: FE có giới hạn `maxlength` trên textarea không (nếu không, user gõ dài rồi mới bị BE chặn — trải nghiệm xấu, có thể ghi improvements.md).

### bd-20 — Chỉ member của Repository mới post được comment

- **Business rule**: `AddDiscussionCommandHandler.cs:23-24` — `IsMemberOfAsync`.
- **Bước thực hiện**: Non-member gọi `POST .../discussions`.
- **Kết quả mong đợi**: 401.

---

## F — History tab

### bd-21 — History hiển thị newest-first, message rõ nghĩa

- **Business rule**: [history.dod.md](../../histories/history.dod.md) §4 — "sort mới nhất trước"; `ListHistoryQuery`.
- **Bước thực hiện**: Xem tab History của 1 task đã có nhiều thay đổi (create + state change + assign).
- **Kết quả mong đợi**: Entry mới nhất ở đầu danh sách. Mỗi entry có `Message` dễ hiểu (vd "State changed from 'Todo' to 'Active'.", "Assigned to 'John Doe'.") kèm tên tác giả + thời điểm.

### bd-22 — Mọi thay đổi đều sinh đúng 1 HistoryEntry (transactional)

- **Business rule**: [history.dod.md](../../histories/history.dod.md) §3 — "Entry được ghi trong cùng SaveChangesAsync với thay đổi gốc."
- **Bước thực hiện**: Đổi state 1 lần, đổi assignee 1 lần. Đếm số entry mới trong history trước/sau mỗi action.
- **Kết quả mong đợi**: Mỗi action tương ứng đúng 1 entry mới, không thiếu không thừa, không có action nào thành công mà thiếu history.

---

## G — Permission

### bd-23 — Non-member không load được detail

- **Business rule**: `GetSprintTaskDetailQueryHandler` (suy đoán theo pattern chung — cần verify có check membership không).
- **Bước thực hiện**: Non-member gọi `GET /sprint-tasks/{taskId}`.
- **Kết quả mong đợi**: 401.

---

## Case Status

| Case ID | Trạng thái lần chạy gần nhất | Ngày | Bug liên quan |
|---------|-------------------------------|------|----------------|
| bd-01 | Pass | 2026-07-04 | - |
| bd-02 | Pass (ngầm qua chuyển tab Details/Discussion/History) | 2026-07-04 | - |
| bd-03 | Pass | 2026-07-04 | - |
| bd-04 | Chưa chạy | - | - |
| bd-05 | Pass | 2026-07-04 | - |
| bd-06 | Chưa chạy | - | - |
| bd-07 | Pass | 2026-07-04 | - |
| bd-08 | Pass | 2026-07-04 | - |
| bd-09 | Pass | 2026-07-04 | - |
| bd-10 | Verify qua code review, chưa demo UI (không còn role thiếu EditWorkItem) | 2026-07-04 | BUG-022 |
| bd-11 | Chưa chạy | - | - |
| bd-12 | Pass | 2026-07-04 | - |
| bd-13 | Pass | 2026-07-04 | - |
| bd-14 | Pass | 2026-07-04 | - |
| bd-15 | Pass | 2026-07-04 | - |
| bd-16 | Pass | 2026-07-04 | - |
| bd-17 | Pass | 2026-07-04 | - |
| bd-18 | Pass | 2026-07-04 | - |
| bd-19 | Chưa chạy | - | - |
| bd-20 | Chưa chạy | - | - |
| bd-21 | Pass | 2026-07-04 | - |
| bd-22 | Pass | 2026-07-04 | - |
| bd-23 | Chưa chạy | - | - |
