# Sprints — Test

> Đọc `../RULES.md` trước khi sửa file này.

## Run Log

| Ngày | Giờ | Kết quả | Bug mới | Ghi chú |
|------|-----|---------|---------|---------|
| 2026-07-04 | 08:52 | ✅ Pass (re-run) | - | Re-run 3 case sau khi BUG-016/BUG-017 fixed (2026-07-03) và user bổ sung thêm test data thật. sprints-32: verify qua API — title rỗng giờ trả đúng 422 `{"errors":{"Title":["'Title' must not be empty."]}}` (BUG-016 fixed). sprints-42: verify qua API — tạo task originalEstimate=8, log work hoursWorked=8/remainingWork=0 → state=Done(5), closedAt được set đúng (BUG-017 fixed), cleanup bằng descope. sprints-47: Skip trước đây do "Product Backlog UI đã removed" — chạy qua API trực tiếp: tìm UserStory Ready đã gán sprint ("User can log in with email and password", Sprint 2 May 2026) → POST `/backlog/{itemId}/promote/{sprintId}` → 200 `{sprintTaskId}`, BacklogItem chuyển Committed, SprintTask mới tạo đúng (Type=UserStory, StoryPoints=5 kế thừa, backlogItemId liên kết, WorkItemNumber=DASH-66) — Pass. Cả 3 case Pass. |
| 2026-07-02 | ~14:00 | Partial ⚠️ | BUG-016, BUG-017 | Chạy F–H (sprints-29–50): BUG-011–015 đã fixed, re-confirm PASS. sprints-32: empty title → 400 (model binding) not 422 (FluentValidation). sprints-42: LogWork auto-Done không set ClosedAt. sprints-45/46 N/A (Product Backlog UI đã removed). sprints-47 Skip (UI path removed). |
| 2026-07-01 | ~16:30 | Partial ⚠️ | BUG-011, BUG-012, BUG-013, BUG-014, BUG-015 | Chạy A–E (sprints-01–28): sprints-08 dialog close silent khi 422, sprints-15 không edit được active sprint, sprints-16/17/18/23/25-28 không có UI (delete/remove/day-off). Error banner thay toast (BUG-014), handler throw exception trực tiếp (BUG-015). |

---

## Test Cases

> **Data hiện có từ backlog test**: repo DASH (`00000000-0000-0000-0002-000000000001`) đã có "Sprint 1 July 2026" với 2 SprintTask (promoted UserStory). Today = 2026-07-01 → sprint này đang active.
> **UI route**: `http://localhost:4200/DASH/sprint-planning`
> **API base**: `http://localhost:5152/api/repositories/00000000-0000-0000-0002-000000000001`

---

## A — Sprint CRUD

### sprints-01 — List sprints: trả về sprint hiện có

- **Business rule**: [sprints.dod.md](../../histories/sprints.dod.md) §4.2 — list sprints của repo; `ListSprintsQueryHandler` trả đủ sprint.
- **Bước thực hiện**: Navigate `http://localhost:4200/DASH/sprint-planning`. Xác nhận page load và hiện sprint list.
- **Kết quả mong đợi**: Page load thành công. "Sprint 1 July 2026" xuất hiện trong UI.

---

### sprints-02 — IsActive = true khi hôm nay trong [StartDate, EndDate]

- **Business rule**: [sprints.dod.md](../../histories/sprints.dod.md) §2 + §3 — "`IsActive` là giá trị tính toán, không lưu DB — true khi hôm nay trong [StartDate, EndDate]."
- **Bước thực hiện**: Gọi `GET /sprints`. Kiểm tra field `isActive` của "Sprint 1 July 2026".
- **Kết quả mong đợi**: `isActive = true` vì 2026-07-01 nằm trong range sprint này.

---

### sprints-03 — IsActive = false với sprint ngoài khoảng hôm nay

- **Business rule**: [sprints.dod.md](../../histories/sprints.dod.md) §3 — "`IsActive` luôn tính lại tại thời điểm query, không cache."
- **Bước thực hiện**: Tạo 1 sprint trong quá khứ (`startDate=2026-05-01`, `endDate=2026-05-14`). Gọi `GET /sprints`, kiểm tra `isActive` của sprint mới.
- **Kết quả mong đợi**: `isActive = false`. "Sprint 1 July 2026" cùng list vẫn `isActive = true`.

---

### sprints-04 — Create sprint hợp lệ (tương lai, không overlap)

- **Business rule**: [sprints.dod.md](../../histories/sprints.dod.md) §4.1 — tạo sprint (name + date range) → validate không overlap.
- **Bước thực hiện**: `POST /sprints` với `name="Sprint 2 August 2026"`, `startDate=2026-08-03`, `endDate=2026-08-14`.
- **Kết quả mong đợi**: 201 Created. Sprint trả về có `id` mới, `isActive = false`.

---

### sprints-05 — Create: EndDate phải sau StartDate

- **Business rule**: `CreateSprintCommandValidator.cs` — `EndDate GreaterThan StartDate`.
- **Bước thực hiện**: `POST /sprints` với `startDate=2026-08-14`, `endDate=2026-08-03`.
- **Kết quả mong đợi**: 422 Validation Error, message "EndDate must be after StartDate."

---

### sprints-06 — Create: EndDate bằng StartDate bị chặn

- **Business rule**: Validator dùng `GreaterThan` (strict) → bằng nhau cũng bị từ chối.
- **Bước thực hiện**: `POST /sprints` với `startDate=2026-09-01`, `endDate=2026-09-01`.
- **Kết quả mong đợi**: 422 Validation Error.

---

### sprints-07 — Create: Name rỗng bị chặn

- **Business rule**: `CreateSprintCommandValidator.cs` — `Name.NotEmpty()`.
- **Bước thực hiện**: `POST /sprints` với `name=""`, dates hợp lệ.
- **Kết quả mong đợi**: 422 Validation Error.

---

### sprints-08 — Create: Name quá 200 ký tự bị chặn

- **Business rule**: `CreateSprintCommandValidator.cs` — `Name.MaximumLength(200)`.
- **Bước thực hiện**: `POST /sprints` với `name` = chuỗi 201 ký tự.
- **Kết quả mong đợi**: 422 Validation Error.

---

### sprints-09 — Create: Dates overlap sprint hiện có → bị chặn

- **Business rule**: [sprints.dod.md](../../histories/sprints.dod.md) §3 + `CreateSprintCommandHandler.cs:28-35` — "Date range không được overlap."
- **Bước thực hiện**: `POST /sprints` với dates giao thoa "Sprint 1 July 2026" (`startDate=2026-06-28`, `endDate=2026-07-07`).
- **Kết quả mong đợi**: 422, message "Sprint dates overlap with an existing sprint in this repository."

---

### sprints-10 — Create: Chạm 1 ngày biên cũng là overlap

- **Business rule**: Overlap check: `s.StartDate <= endDate && s.EndDate >= startDate` — endpoint biên (`=`) cũng bị chặn.
- **Bước thực hiện**: Lấy EndDate của "Sprint 1 July 2026" qua GET. Tạo sprint mới với `startDate = EndDate đó`.
- **Kết quả mong đợi**: 422 — chạm 1 ngày biên = overlap.

---

### sprints-11 — Tên sprint không cần unique

- **Business rule**: [sprints.dod.md](../../histories/sprints.dod.md) §6 — "Tên sprint không cần unique."
- **Bước thực hiện**: `POST /sprints` với `name="Sprint 1 July 2026"` (trùng tên), dates tháng 10 (không overlap).
- **Kết quả mong đợi**: 201 Created — tên trùng không bị chặn.

---

### sprints-12 — Update sprint: đổi tên và dates hợp lệ

- **Business rule**: [sprints.dod.md](../../histories/sprints.dod.md) §4.3 — "Update tên hoặc ngày sprint (re-validate overlap)."
- **Bước thực hiện**: `PUT /sprints/{id}` lên sprint past (sprints-03), đổi tên + dời dates sang tháng 11.
- **Kết quả mong đợi**: 204 No Content. GET list → tên và dates đã cập nhật.

---

### sprints-13 — Update: dates overlap sprint khác → 400

- **Business rule**: `UpdateSprintCommandHandler.cs:30-37` — re-validate overlap khi update.
- **Bước thực hiện**: `PUT /sprints/{id}` đặt dates giao thoa "Sprint 1 July 2026".
- **Kết quả mong đợi**: 400, message "Sprint dates overlap with an existing sprint in this repository."

---

### sprints-14 — Update: không tự overlap bản thân (chỉ đổi tên, giữ dates)

- **Business rule**: `UpdateSprintCommandHandler.cs:32` — exclude self `s.Id != command.SprintId` khi check overlap.
- **Bước thực hiện**: `PUT /sprints/{id}` với cùng dates, chỉ đổi `name`.
- **Kết quả mong đợi**: 204 No Content — không báo overlap với chính nó.

---

### sprints-15 — Update sprint đang active: cho phép rebase boundary

- **Business rule**: [sprints.dod.md](../../histories/sprints.dod.md) §6 — "Đổi date range được cho phép cả khi sprint đang active."
- **Bước thực hiện**: `PUT /sprints/{id}` lên "Sprint 1 July 2026" → mở rộng EndDate thêm 1 tuần (không overlap).
- **Kết quả mong đợi**: 204 No Content — không bị chặn dù đang active. Khôi phục EndDate cũ sau khi test.

---

### sprints-16 — Delete sprint không task → thành công

- **Business rule**: `DeleteSprintCommandHandler.cs` — xoá được khi không active và không có task.
- **Bước thực hiện**: Tạo sprint tháng 12 (không overlap, không task). `DELETE /sprints/{id}`.
- **Kết quả mong đợi**: 204. GET list → sprint biến mất.

---

### sprints-17 — Delete sprint đang active → bị chặn

- **Business rule**: `DeleteSprintCommandHandler.cs:31-33` — "Cannot delete an active sprint."
- **Bước thực hiện**: `DELETE /sprints/{id}` lên "Sprint 1 July 2026" (active hôm nay).
- **Kết quả mong đợi**: 400, message "Cannot delete an active sprint."

---

### sprints-18 — Delete sprint có task → bị chặn

- **Business rule**: `DeleteSprintCommandHandler.cs:35-38` — "Cannot delete sprint with N committed task(s)."
- **Bước thực hiện**: Tạo sprint past (không active), tạo 1 task trong đó, gọi `DELETE /sprints/{id}`.
- **Kết quả mong đợi**: 400, message chứa "Cannot delete sprint with 1 committed task(s)."

---

## B — Capacity Members

### sprints-19 — Thêm capacity member vào sprint

- **Business rule**: [sprints.dod.md](../../histories/sprints.dod.md) §4.4 — "Khai báo capacity từng member (giờ/ngày, overtime, role)." `UpsertCapacityMemberCommandHandler` — upsert (tạo mới nếu chưa có).
- **Bước thực hiện**: Qua UI sprint-planning-page → panel capacity → thêm admin user vào "Sprint 1 July 2026" với `hoursPerDay=8`, `overtimeHoursPerDay=0`, `role="Scrum Master"`.
- **Kết quả mong đợi**: Member xuất hiện trong capacity table. GET /sprints/{id}/detail → `capacityMembers` chứa user này.

---

### sprints-20 — Upsert capacity: cập nhật giờ khi member đã có

- **Business rule**: `UpsertCapacityMemberCommandHandler.cs:40-58` — upsert: nếu đã có row → update, không tạo dòng trùng.
- **Bước thực hiện**: Gọi lại upsert cho cùng user với `hoursPerDay=6`, `overtimeHoursPerDay=1`.
- **Kết quả mong đợi**: Không tạo record mới; giờ đổi thành 6+1. Detail vẫn chỉ có 1 dòng cho user này.

---

### sprints-21 — HoursPerDay tối đa 12, OvertimeHoursPerDay tối đa 6

- **Business rule**: `UpsertCapacityMemberCommandValidator.cs` — `HoursPerDay.InclusiveBetween(0,12)`, `OvertimeHoursPerDay.InclusiveBetween(0,6)`.
- **Bước thực hiện**: Upsert với `hoursPerDay=13`. Sau đó thử `overtimeHoursPerDay=7`.
- **Kết quả mong đợi**: Cả 2 bị 422 Validation Error.

---

### sprints-22 — HoursPerDay = 0 hợp lệ (InclusiveBetween 0–12)

- **Business rule**: Validator dùng `InclusiveBetween(0,12)` → 0 hợp lệ (member tạm không làm việc nhưng vẫn trong capacity).
- **Bước thực hiện**: Upsert với `hoursPerDay=0`, `overtimeHoursPerDay=0`.
- **Kết quả mong đợi**: 200/201 thành công, không bị validation chặn.

---

### sprints-23 — Remove capacity member

- **Business rule**: `RemoveCapacityMemberCommandHandler.cs` — xoá capacity row; yêu cầu ManageCapacity.
- **Bước thực hiện**: Qua UI → nút remove capacity member. Xác nhận xoá.
- **Kết quả mong đợi**: 204. Detail lại → `capacityMembers` không còn user đó.

---

### sprints-24 — Summary: totalCapacity tính đúng từ capacity members

- **Business rule**: `GetSprintSummaryQueryHandler.cs:46-52` — `totalCapacity = (hoursPerDay + overtimeHoursPerDay) × workingDays − daysOffHours`.
- **Bước thực hiện**: Thêm lại capacity member (8h/ngày, 0 overtime). Gọi `GET /sprints/{id}/summary`. Tính tay workingDays của sprint rồi so sánh với `totalCapacity` trong response.
- **Kết quả mong đợi**: `totalCapacity = 8 × workingDays` (nếu chưa có day-off). `workingDays` đúng (bỏ T7/CN).

---

## C — Day-off

### sprints-25 — Thêm day-off cá nhân trong khoảng sprint

- **Business rule**: [sprints.dod.md](../../histories/sprints.dod.md) §3 + `AddDayOffCommandHandler.cs:31-33` — "DayOff phải nằm trong [StartDate, EndDate] của sprint."
- **Bước thực hiện**: Thêm day-off cho admin user vào 1 ngày trong khoảng "Sprint 1 July 2026", `hours=8`, `reason="Public holiday"`.
- **Kết quả mong đợi**: Day-off được tạo. Detail → `daysOff` chứa record này. Summary → `totalCapacity` giảm đúng 8h.

---

### sprints-26 — Day-off ngoài khoảng sprint → bị chặn

- **Business rule**: `AddDayOffCommandHandler.cs:31-33` — `if (command.Date < sprint.StartDate || command.Date > sprint.EndDate) throw InvalidOperationException`.
- **Bước thực hiện**: Thêm day-off với date = 1 ngày trước StartDate hoặc sau EndDate của sprint.
- **Kết quả mong đợi**: 500 / error response (handler dùng throw thay vì Result.Failure — có thể phát hiện bug BUG-pattern mới).

---

### sprints-27 — Day-off toàn team (userId = null)

- **Business rule**: `AddDayOffCommandHandler.cs` — `UserId` nullable; `GetSprintSummaryQueryHandler.cs:48-51` — day-off `userId==null` apply cho mọi member.
- **Bước thực hiện**: Thêm day-off với `userId=null`, `hours=8`, `reason="Company off"`.
- **Kết quả mong đợi**: Tạo thành công. Summary: nếu có 2 capacity members → totalCapacity giảm đúng 2×8h.

---

### sprints-28 — Remove day-off

- **Business rule**: `RemoveDayOffCommandHandler.cs` — xoá day-off entry; yêu cầu ManageCapacity.
- **Bước thực hiện**: Xoá day-off vừa tạo ở sprints-25.
- **Kết quả mong đợi**: 204. Detail → `daysOff` không còn record đó. Summary → `totalCapacity` tăng lại.

---

## D — Sprint Tasks (tạo mới / gán trực tiếp)

### sprints-29 — Tạo sprint task (Task type) trực tiếp trong sprint

- **Business rule**: `CreateSprintTaskCommandHandler.cs:60` — khi `SprintId` được cung cấp, `initialState = SprintTaskState.New`.
- **Bước thực hiện**: Qua UI sprint-planning-page → nút "Add Task" → tạo task type Task, title "Setup CI pipeline", assign cho member có trong capacity.
- **Kết quả mong đợi**: Task được tạo, state = New, `workItemNumber` theo dạng `DASH-{n}`. Xuất hiện trong task list của sprint.

---

### sprints-30 — Tạo sprint task: UserStory không được assign cá nhân

- **Business rule**: `CreateSprintTaskCommandHandler.cs:29-32` — "User stories cannot be assigned to an individual."
- **Bước thực hiện**: `POST /sprint-tasks` với `type=UserStory`, `assignedToId={userId}`, sprint hợp lệ.
- **Kết quả mong đợi**: 422, message "User stories cannot be assigned to an individual. Assign sub-tasks instead."

---

### sprints-31 — Tạo sub-task: parent phải là UserStory

- **Business rule**: `CreateSprintTaskCommandHandler.cs:40-49` — "Parent work item must be a User Story."
- **Bước thực hiện**: Tạo 1 Task (type=Task) trong sprint. Sau đó tạo task mới với `parentId` = task vừa tạo (type=Task, không phải UserStory).
- **Kết quả mong đợi**: 422, message "Parent work item must be a User Story."

---

### sprints-32 — Title bắt buộc, tối đa 500 ký tự

- **Business rule**: `CreateSprintTaskCommandValidator.cs` — `Title.NotEmpty().MaximumLength(500)`.
- **Bước thực hiện**: Tạo task với title rỗng. Sau đó tạo với title 501 ký tự.
- **Kết quả mong đợi**: Cả 2 bị 422 Validation Error.

---

### sprints-33 — RemainingWork = OriginalEstimate khi tạo

- **Business rule**: `CreateSprintTaskCommandHandler.cs:78` — `RemainingWork = command.OriginalEstimate` khi tạo.
- **Bước thực hiện**: Tạo task với `originalEstimate=8`. GET task detail.
- **Kết quả mong đợi**: `remainingWork = 8`, `completedWork = 0`.

---

### sprints-34 — WorkItemNumber tăng dần theo repo (không phải sprint)

- **Business rule**: `CreateSprintTaskCommandHandler.cs:55-58` — đếm max number toàn repo (kể cả deleted items), +1.
- **Bước thực hiện**: Tạo 2 task liên tiếp trong sprint. Kiểm tra `workItemNumber` của task 2.
- **Kết quả mong đợi**: `workItemNumber` của task 2 = task 1 + 1. Tên dạng `DASH-{n}` và `DASH-{n+1}`.

---

## E — De-scope (đưa task về backlog)

### sprints-35 — Descope task được promote từ backlog → backlogItem về Ready

- **Business rule**: `DescodeSprintTaskCommandHandler.cs:41-49` — "xoá sprint task và restore backlog item về Ready."
- **Bước thực hiện**: Descope 1 trong 2 task có sẵn từ promote backlog ("Sprint 1 July 2026").
- **Kết quả mong đợi**: Task biến mất khỏi sprint. Backlog item tương ứng chuyển về state `Ready` (verify qua `GET /backlog`).

---

### sprints-36 — Descope task tạo trực tiếp (không có backlogItemId) → chỉ xoá task

- **Business rule**: `DescodeSprintTaskCommandHandler.cs:41` — chỉ restore backlog item khi `BacklogItemId.HasValue`.
- **Bước thực hiện**: Descope task tạo trực tiếp ở sprints-29 (không có `backlogItemId`).
- **Kết quả mong đợi**: 204. Task biến mất. Không có side effect lên backlog.

---

### sprints-37 — Descope sub-task bị chặn (chỉ root-level được descope)

- **Business rule**: `DescodeSprintTaskCommandHandler.cs:33-34` — "Only root-level tasks (user stories) can be descoped."
- **Bước thực hiện**: Tạo UserStory task + 1 sub-task (Task type). Gọi descope lên sub-task.
- **Kết quả mong đợi**: 400, message "Only root-level tasks (user stories) can be descoped."

---

### sprints-38 — Descope xoá toàn bộ sub-tasks của root task

- **Business rule**: `DescodeSprintTaskCommandHandler.cs:37-38` — `RemoveRange(task.SubTasks)` trước khi xoá root.
- **Bước thực hiện**: Tạo UserStory task + 2 sub-tasks. Descope root task.
- **Kết quả mong đợi**: 204. Cả root lẫn 2 sub-task đều biến mất khỏi sprint.

---

## F — Task State & Workload

### sprints-39 — Đổi state task: New → InProgress → Done

- **Business rule**: `ChangeSprintTaskStateCommandHandler.cs` — state machine tự do (không enforce thứ tự cứng); Done → set `RemainingWork=0` và `ClosedAt`.
- **Bước thực hiện**: Tạo task (state=New). Đổi state InProgress. Đổi tiếp sang Done.
- **Kết quả mong đợi**: Mỗi bước 200/204. State Done → `remainingWork=0`, `closedAt` được set. Summary: `completedTasks` tăng lên.

---

### sprints-40 — Done → reopen (đổi về Active): closedAt bị clear

- **Business rule**: `ChangeSprintTaskStateCommandHandler.cs:45-46` — `else { task.ClosedAt = null; }` khi state khác Done.
- **Bước thực hiện**: Task ở Done, đổi state về Active.
- **Kết quả mong đợi**: 200/204. `closedAt = null`. `remainingWork` vẫn là 0 (không tự phục hồi — đây là expected behavior).

---

### sprints-41 — Log work: CompletedWork tăng, RemainingWork cập nhật

- **Business rule**: `LogWorkCommandHandler.cs:37-38` — `CompletedWork += hoursWorked; RemainingWork = command.RemainingWork`.
- **Bước thực hiện**: Task có `originalEstimate=8`, `remainingWork=8`. Log work `hoursWorked=3`, `remainingWork=5`.
- **Kết quả mong đợi**: `completedWork=3`, `remainingWork=5`. State vẫn chưa Done.

---

### sprints-42 — Log work: remainingWork = 0 → auto Done

- **Business rule**: `LogWorkCommandHandler.cs:40-41` — "auto-transitions to Done if remaining reaches zero."
- **Bước thực hiện**: Log work tiếp với `hoursWorked=5`, `remainingWork=0`.
- **Kết quả mong đợi**: `completedWork=8`, `remainingWork=0`, state tự chuyển thành Done, `closedAt` được set.

---

### sprints-43 — Log work: hoursWorked ≤ 0 bị chặn

- **Business rule**: `LogWorkCommandHandler.cs:27-28` — "HoursWorked must be greater than zero."
- **Bước thực hiện**: Log work với `hoursWorked=0` và `hoursWorked=-1`.
- **Kết quả mong đợi**: Cả 2 trả 400, message "HoursWorked must be greater than zero."

---

### sprints-44 — Log work: remainingWork âm bị chặn

- **Business rule**: `LogWorkCommandHandler.cs:29-30` — "RemainingWork cannot be negative."
- **Bước thực hiện**: Log work với `remainingWork=-1`.
- **Kết quả mong đợi**: 400, message "RemainingWork cannot be negative."

---

## G — Product Backlog trong Sprint Planning

### sprints-45 — Product backlog tab: hiển thị UserStory trạng thái Ready chưa gán sprint

- **Business rule**: [sprints.dod.md](../../histories/sprints.dod.md) §2 — "`BacklogItem` có thể gán tạm `SprintId` trước khi promote chính thức thành `SprintTask`."
- **Bước thực hiện**: Qua UI sprint-planning-page → tab/section "Product Backlog". Xem danh sách item.
- **Kết quả mong đợi**: Hiển thị các UserStory Ready chưa gán sprint (từ backlog đã seed). Không hiển thị item đã promote (Committed).

---

### sprints-46 — Gán sprint cho backlog item (planning, chưa promote)

- **Business rule**: [sprints.dod.md](../../histories/sprints.dod.md) §4.4 — "Gán `SprintId` để planning tạm (không đổi refinement state)." Tham chiếu `MoveToIterationCommandHandler`.
- **Bước thực hiện**: Gán 1 UserStory Ready vào "Sprint 1 July 2026" qua UI (drag hoặc nút assign).
- **Kết quả mong đợi**: Item xuất hiện trong sprint planning. State của BacklogItem vẫn là Ready (không thay đổi về Committed — đó là sau promote).

---

### sprints-47 — Promote backlog item từ planning → tạo SprintTask

- **Business rule**: [backlog.dod.md](../../histories/backlog.dod.md) §5 — "Promote luôn tạo đúng 1 SprintTask và set BacklogItem = Committed." Tham chiếu `PromoteToSprintCommandHandler`.
- **Bước thực hiện**: Promote UserStory ở sprints-46 (đã gán sprint, state Ready).
- **Kết quả mong đợi**: 200/201. Backlog item chuyển Committed. SprintTask mới xuất hiện trong sprint với `backlogItemId` liên kết.

---

## H — UI Sprint Planning (visual + UX)

### sprints-48 — UI: Tạo sprint mới qua dialog

- **Business rule**: [sprints.dod.md](../../histories/sprints.dod.md) §4.1.
- **Bước thực hiện**: Trên sprint-planning-page → mở dialog tạo sprint (tháng 9, không overlap). Submit.
- **Kết quả mong đợi**: Dialog đóng. Sprint mới xuất hiện trong list. Không có lỗi console.

---

### sprints-49 — UI: Overlap error hiển thị cho user (không bị nuốt im lặng)

- **Business rule**: UX — lỗi overlap phải hiển thị lên user (so sánh với BUG-007 đã fix ở roles).
- **Bước thực hiện**: Qua UI dialog → cố tạo sprint với dates overlap "Sprint 1 July 2026".
- **Kết quả mong đợi**: Toast error hoặc inline message "Sprint dates overlap..." — không im lặng.

---

### sprints-50 — UI: Summary widget hiển thị workingDays + capacity đúng

- **Business rule**: [sprints.dod.md](../../histories/sprints.dod.md) §4.6 — "tính member load để hỗ trợ planning."
- **Bước thực hiện**: Xem widget summary trên sprint-planning-page cho "Sprint 1 July 2026" (sau khi đã thêm capacity member ở sprints-19).
- **Kết quả mong đợi**: Widget hiện `workingDays`, `totalCapacity`, `committedPoints`, `completedTasks` khớp với API summary.

---

## Case Status

| Case ID | Trạng thái lần chạy gần nhất | Ngày | Bug liên quan |
|---------|-------------------------------|------|----------------|
| sprints-01 | Pass | 2026-07-01 | - |
| sprints-02 | Pass | 2026-07-01 | - |
| sprints-03 | Pass | 2026-07-01 | - |
| sprints-04 | Pass | 2026-07-01 | - |
| sprints-05 | Pass | 2026-07-01 | - |
| sprints-06 | Pass | 2026-07-01 | - |
| sprints-07 | Pass | 2026-07-01 | - |
| sprints-08 | Pass | 2026-07-02 | BUG-011 (Fixed) |
| sprints-09 | Pass | 2026-07-01 | - |
| sprints-10 | Pass | 2026-07-01 | - |
| sprints-11 | Pass | 2026-07-01 | - |
| sprints-12 | Pass | 2026-07-01 | - |
| sprints-13 | Pass | 2026-07-01 | - |
| sprints-14 | Pass | 2026-07-01 | - |
| sprints-15 | Pass | 2026-07-02 | BUG-012 (Fixed) |
| sprints-16 | Pass | 2026-07-02 | BUG-013 (Fixed) |
| sprints-17 | Pass | 2026-07-02 | BUG-013 (Fixed) |
| sprints-18 | Pass | 2026-07-02 | BUG-013 (Fixed) |
| sprints-19 | Pass | 2026-07-01 | - |
| sprints-20 | Pass | 2026-07-01 | - |
| sprints-21 | Pass | 2026-07-01 | - |
| sprints-22 | Pass | 2026-07-01 | - |
| sprints-23 | Pass | 2026-07-02 | BUG-013 (Fixed) |
| sprints-24 | Pass | 2026-07-01 | - |
| sprints-25 | Pass | 2026-07-02 | BUG-013 (Fixed) |
| sprints-26 | Pass | 2026-07-02 | BUG-013 (Fixed) — 500 expected per test case (handler uses throw) |
| sprints-27 | Pass | 2026-07-02 | BUG-013 (Fixed) |
| sprints-28 | Pass | 2026-07-02 | BUG-013 (Fixed) |
| sprints-29 | Pass | 2026-07-02 | - |
| sprints-30 | Pass | 2026-07-02 | - |
| sprints-31 | Pass | 2026-07-02 | - |
| sprints-32 | Pass | 2026-07-04 | BUG-016 (fixed) |
| sprints-33 | Pass | 2026-07-02 | - |
| sprints-34 | Pass | 2026-07-02 | - |
| sprints-35 | Pass | 2026-07-02 | - |
| sprints-36 | Pass | 2026-07-02 | - |
| sprints-37 | Pass | 2026-07-02 | - |
| sprints-38 | Pass | 2026-07-02 | - |
| sprints-39 | Pass | 2026-07-02 | - |
| sprints-40 | Pass | 2026-07-02 | - |
| sprints-41 | Pass | 2026-07-02 | - |
| sprints-42 | Pass | 2026-07-04 | BUG-017 (fixed) |
| sprints-43 | Pass | 2026-07-02 | - |
| sprints-44 | Pass | 2026-07-02 | - |
| sprints-45 | N/A | 2026-07-02 | - — Product Backlog UI section removed intentionally |
| sprints-46 | N/A | 2026-07-02 | - — Product Backlog UI section removed intentionally |
| sprints-47 | Pass (qua API) | 2026-07-04 | - |
| sprints-48 | Pass | 2026-07-02 | - |
| sprints-49 | Pass | 2026-07-02 | - |
| sprints-50 | Pass | 2026-07-02 | - |
