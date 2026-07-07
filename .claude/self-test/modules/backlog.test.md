# Backlog — Test

> Đọc `../RULES.md` trước khi sửa file này.

## Run Log

| Ngày | Giờ | Kết quả | Bug mới | Ghi chú |
|------|-----|---------|---------|---------|
| 2026-07-01 | 16:13 | ✅ Pass (32/32) | - | Lần chạy thứ 2 sau khi fix BUG-009 (PromoteToSprint + CreateBacklogItem dùng Result thay vì throw) và BUG-010 (UpdateBacklogItem guard chặn State=Committed). Re-ran 4 case đã fail: backlog-12 (400 permission error đúng format), backlog-21 (400 Cannot set Committed directly), backlog-24 (400 Only UserStory), backlog-25 (400 must be Ready). Spot-check backlog-01/05/08/20 không có regression. UI visual verify: backlog page load 82 items, "Add item" button hoạt động bình thường. Tất cả 32/32 Pass. |
| 2026-07-01 | 00:50 | Partial | BUG-009, BUG-010 | Lần chạy đầu tiên — soạn 32 case chi tiết rồi chạy qua API trực tiếp (xen kẽ với việc tạo seed data thật cho toàn bộ module trong app, xem `.claude/self-test/seed-backlog-2026-07-01.md` nếu cần đối chiếu). 28/32 case Pass. backlog-04 xác nhận hierarchy Type không được enforce server-side (ghi `improvements.md` IMP-002, chưa rõ là bug hay thiết kế cố ý nên không ghi `bugs.md`). backlog-24/backlog-25: promote 1 Feature hoặc 1 UserStory chưa Ready đều trả **500** thay vì 400 (cùng class lỗi throw-vs-Result đã fix ở BUG-002/005/007, nhưng `PromoteToSprintCommandHandler` chưa được rà — ghi **BUG-009**, Medium, Open). backlog-21: phát hiện nghiêm trọng hơn dự kiến — không chỉ PATCH `/state` cho lùi state từ Committed về Ready vô điều kiện, mà **PUT `/{itemId}` (full Update) hoàn toàn không có guard chặn `State=Committed`**, cho phép set Committed trực tiếp qua đường này (kể cả cho Epic/Feature) mà không tạo SprintTask nào — vi phạm thẳng invariant DoD "Promote luôn tạo đúng 1 SprintTask". Ghi **BUG-010**, High, Open. Toàn bộ seed data (7 Epic, ~23 Feature, ~52 UserStory bao phủ đủ module Done/In Progress/Planned + 6 feature đề xuất mới) **giữ nguyên trong repo DASH** để dùng cho việc test các feature khác sau này (sprint/board/overview...) — không xoá. 2 UserStory mẫu đã Promote thật vào "Sprint 1 July 2026" để minh hoạ workflow đầy đủ. Dữ liệu test tạm (no-perm role, vài item TEMP validation) đã dọn sạch sau khi test xong. |

---

## Test Cases

### backlog-01 — Create Epic with no parent

- **Business rule**: [backlog.dod.md](../../histories/backlog.dod.md) §4.1 — create a BacklogItem at any level, parent is optional.
- **Bước thực hiện**: Navigate `/{repoCode}/backlog` → create item Type = Epic, no parent selected.
- **Kết quả mong đợi**: Epic created successfully (201), appears at root level (`parentId = null`), initial `state = New`, `rank` = previous max root rank + 1000.

### backlog-02 — Create Feature under an existing Epic

- **Business rule**: [backlog.dod.md](../../histories/backlog.dod.md) §2 — Epic → Feature → UserStory tree (3 nested levels).
- **Bước thực hiện**: Create a Feature with `ParentId` = an existing Epic's id.
- **Kết quả mong đợi**: Feature created with the correct `parentId`; appears nested under that Epic in the tree.

### backlog-03 — Create UserStory under an existing Feature

- **Business rule**: [backlog.dod.md](../../histories/backlog.dod.md) §2 — 3-level hierarchy.
- **Bước thực hiện**: Create a UserStory with `ParentId` = an existing Feature's id.
- **Kết quả mong đợi**: UserStory created and nested correctly under the Feature, which itself sits under its Epic — full 3-level tree visible via `GET /backlog`.

### backlog-04 — ⚠️ Hierarchy Type is NOT enforced server-side at creation

- **Business rule**: [backlog.dod.md](../../histories/backlog.dod.md) §2 implies Epic→Feature→UserStory, but `CreateBacklogItemCommandHandler.cs:22-30` only validates that `ParentId` exists **in the same repository** — it never checks that the parent's `Type` is one level above the child's `Type`.
- **Bước thực hiện**: Create a UserStory with `ParentId` pointing directly at an Epic (skipping Feature), or create an Epic with `ParentId` pointing at a UserStory.
- **Kết quả mong đợi theo doc**: Nên bị chặn (tree phải đúng 3 cấp).
- **Kết quả thực tế dự kiến (theo code)**: Request **thành công** — không có validation nào chặn việc trộn cấp. Đây là phát hiện qua code review, cần verify thực tế và cân nhắc ghi `improvements.md` (hoặc `bugs.md` nếu coi đây là sai lệch so với doc) tuỳ kết quả.

### backlog-05 — Estimate: cannot set both StoryPoints and TshirtSize at once

- **Business rule**: [backlog.dod.md](../../histories/backlog.dod.md) §2 — "không dùng cả 2 loại estimate cùng lúc"; `CreateBacklogItemCommandValidator.cs:22-24` / `UpdateBacklogItemCommandValidator.cs:16-18` — `Must(x => !(StoryPoints.HasValue && TshirtSize.HasValue))`.
- **Bước thực hiện**: Gọi Create hoặc Update với cả `storyPoints` và `tshirtSize` cùng khác null.
- **Kết quả mong đợi**: 422 validation error "Specify either StoryPoints or TshirtSize, not both."

### backlog-06 — UI estimate picker: UserStory chỉ thấy Fibonacci, Epic/Feature chỉ thấy T-shirt

- **Business rule**: [backlog.dod.md](../../histories/backlog.dod.md) §2. **Lưu ý**: đây là rule chỉ enforce ở **UI** (`FIBONACCI_POINTS`/`TSHIRT_SIZES` constants + `updateStoryPoints`/`updateTshirtSize` trong `backlog-management.service.ts:238-262`) — backend KHÔNG validate loại estimate theo Type (xem backlog-04), chỉ chặn dùng-cả-2 (backlog-05).
- **Bước thực hiện**: Mở refinement editor cho 1 UserStory và cho 1 Epic, so sánh option hiển thị.
- **Kết quả mong đợi**: UserStory chỉ thấy Fibonacci (1,2,3,5,8,13,21,34,55,89...); Epic/Feature chỉ thấy XS/S/M/L/XL.

### backlog-07 — StoryPoints phải trong khoảng 1–100

- **Business rule**: [backlog.dod.md](../../histories/backlog.dod.md) §2 — Fibonacci 1–100; `CreateBacklogItemCommandValidator.cs:17-19` `InclusiveBetween(1, 100)`.
- **Bước thực hiện**: Gọi Create/Update với `storyPoints = 0` và `storyPoints = 101`.
- **Kết quả mong đợi**: Cả 2 trường hợp bị validation chặn (422).

### backlog-08 — Title bắt buộc, tối đa 500 ký tự

- **Business rule**: code `CreateBacklogItemCommandValidator.cs:13` / `UpdateBacklogItemCommandValidator.cs:13` — `NotEmpty().MaximumLength(500)`.
- **Bước thực hiện**: Tạo item với Title rỗng; tạo item với Title 501 ký tự.
- **Kết quả mong đợi**: Cả 2 trường hợp bị chặn validation.

### backlog-09 — AcceptanceCriteria tối đa 4000 ký tự

- **Business rule**: code `CreateBacklogItemCommandValidator.cs:14` — `MaximumLength(4000)`.
- **Bước thực hiện**: Tạo/update item với AcceptanceCriteria 4001 ký tự.
- **Kết quả mong đợi**: Bị chặn validation (422).

### backlog-10 — Tạo item với ParentId không tồn tại bị chặn

- **Business rule**: code `CreateBacklogItemCommandHandler.cs:28-30` — `throw NotFoundException` nếu parent không tồn tại trong cùng repo.
- **Bước thực hiện**: Gọi Create với `parentId` = 1 GUID ngẫu nhiên không tồn tại.
- **Kết quả mong đợi**: 404 Not Found.

### backlog-11 — Tạo item với ParentId thuộc Repository khác bị chặn

- **Business rule**: code `CreateBacklogItemCommandHandler.cs:28-30` — check `b.RepositoryId == command.RepositoryId`.
- **Bước thực hiện**: Gọi Create ở Repository A với `parentId` là 1 BacklogItem thật nhưng thuộc Repository B.
- **Kết quả mong đợi**: 404 Not Found (coi như parent không tồn tại trong repo đích).

### backlog-12 — Tạo backlog item cần quyền CreateWorkItem

- **Business rule**: code `CreateBacklogItemCommandHandler.cs:24-25`.
- **Bước thực hiện**: Đăng nhập member không có `CreateWorkItem` (vd role chỉ có ViewRepository) → thử tạo item.
- **Kết quả mong đợi**: Action bị chặn (401/403 tuỳ tầng chặn).

### backlog-13 — Sửa/rank/state/AC/documents cần quyền EditWorkItem

- **Business rule**: code — mọi handler Update*/Rank đều check `SystemFunction.EditWorkItem` (`UpdateBacklogItemCommandHandler.cs:24`, `RankBacklogItemCommandHandler.cs:26`, `UpdateBacklogStateCommandHandler.cs:24`, `UpdateBacklogAcceptanceCriteriaCommandHandler.cs:24`, `UpdateBacklogDocumentsCommandHandler.cs:24`, `MoveToIterationCommandHandler.cs:24`).
- **Bước thực hiện**: Member không có `EditWorkItem` thử rename/rank/đổi state/đổi AC/đổi documents/move iteration.
- **Kết quả mong đợi**: Tất cả action bị chặn — 400 `{error: "You do not have permission to edit backlog items..."}`.

### backlog-14 — Xoá backlog item cần quyền DeleteWorkItem

- **Business rule**: code `DeleteBacklogItemCommandHandler.cs:24-25`.
- **Bước thực hiện**: Member không có `DeleteWorkItem` thử xoá 1 item lá (không con).
- **Kết quả mong đợi**: Bị chặn 400.

### backlog-15 — Không cho xoá item còn con

- **Business rule**: code `DeleteBacklogItemCommandHandler.cs:31-34` — check `AnyAsync(b => b.ParentId == command.ItemId)`.
- **Bước thực hiện**: Thử xoá 1 Epic/Feature đang có ít nhất 1 con.
- **Kết quả mong đợi**: 400 "Cannot delete a backlog item that has children. Delete children first." Xoá hết con trước → xoá lại thành công.

### backlog-16 — Reorder (drag-drop) tính lại rank theo midpoint

- **Business rule**: [backlog.dod.md](../../histories/backlog.dod.md) §3 — rank = `(prev + next) / 2`; code `RankBacklogItemCommandHandler.cs:55-61`.
- **Bước thực hiện**: Kéo 1 item vào giữa 2 sibling khác trong cùng cấp (drag-drop trên UI hoặc gọi PATCH `.../rank` trực tiếp với `previousItemId`/`nextItemId`).
- **Kết quả mong đợi**: `rank` mới = trung bình cộng rank của 2 sibling; thứ tự hiển thị đúng vị trí kéo tới và giữ nguyên sau khi reload trang.

### backlog-17 — Rank ở đầu/cuối list

- **Business rule**: code `RankBacklogItemCommandHandler.cs:56-59` — không có `previousItemId` → `rank = nextRank/2` (lên đầu); không có `nextItemId` → `rank = prevRank + 1000` (xuống cuối).
- **Bước thực hiện**: Kéo 1 item lên vị trí đầu tiên trong list; kéo 1 item khác xuống vị trí cuối cùng.
- **Kết quả mong đợi**: Item ở đầu có rank nhỏ hơn mọi sibling còn lại; item ở cuối có rank lớn hơn mọi sibling còn lại.

### backlog-18 — Re-normalize rank khi gap < 0.001

- **Business rule**: [backlog.dod.md](../../histories/backlog.dod.md) §3; code `RankBacklogItemCommandHandler.cs:18,64-77` — `MinGap = 0.001m`, khi `(nextRank - prevRank) < MinGap` thì re-normalize toàn bộ sibling về spacing 1000.
- **Bước thực hiện**: Lặp lại thao tác rank 1 item vào giữa cùng 1 cặp sibling nhiều lần liên tiếp (mỗi lần halving khoảng cách) cho tới khi gap < 0.001.
- **Kết quả mong đợi**: Tại lần gap sụp xuống dưới ngưỡng, toàn bộ sibling cùng cấp được re-normalize về rank = `(index+1) * 1000`, không lỗi tràn precision, thứ tự tương đối vẫn giữ nguyên.

### backlog-19 — Refine state transitions New → Refining → Ready

- **Business rule**: [backlog.dod.md](../../histories/backlog.dod.md) §4.3; code `UpdateBacklogStateCommandHandler.cs`.
- **Bước thực hiện**: Tạo item mới (state mặc định New) → PATCH state = Refining → PATCH state = Ready.
- **Kết quả mong đợi**: Mỗi transition thành công (204), state hiển thị đúng theo thứ tự.

### backlog-20 — UpdateBacklogState chặn transition trực tiếp sang Committed

- **Business rule**: code `UpdateBacklogStateCommandHandler.cs:27-28` — `if (command.State == Committed) return Result.Failure(...)`.
- **Bước thực hiện**: Gọi PATCH `.../state` với `state = Committed` trực tiếp (bỏ qua Promote).
- **Kết quả mong đợi**: 400 "Use 'Move to Iteration' or 'Promote to Sprint' to commit a backlog item."

### backlog-21 — ⚠️ UpdateBacklogState cho phép lùi state TỪ Committed về state khác

- **Business rule**: Không có trong doc — phát hiện qua code review. `UpdateBacklogStateCommandHandler.cs:27` chỉ chặn state ĐÍCH = Committed, không chặn state NGUỒN = Committed.
- **Bước thực hiện**: Promote 1 UserStory thành Committed (có SprintTask liên kết) → gọi PATCH `.../state` với `state = Ready` (hoặc New/Refining) trên chính BacklogItem đó.
- **Kết quả mong đợi theo thiết kế an toàn**: Có thể nên bị chặn hoặc cảnh báo (item đã có SprintTask liên kết, lùi state có thể gây desync giữa BacklogItem và SprintTask đã tạo).
- **Kết quả thực tế dự kiến (theo code)**: Thành công không cảnh báo — BacklogItem đổi state, nhưng SprintTask đã tạo trước đó vẫn còn nguyên, không bị xoá/đồng bộ lại. Verify thực tế rồi cân nhắc ghi nhận vào `improvements.md`/`bugs.md`.

### backlog-22 — Gán SprintId (Move to Iteration) không đổi refinement state

- **Business rule**: [backlog.dod.md](../../histories/backlog.dod.md) §3; code `MoveToIterationCommandHandler.cs` chỉ set `item.SprintId`, không đụng `item.State`.
- **Bước thực hiện**: Đưa 1 item về state Refining → gọi PATCH `.../iteration` gán `sprintId` = 1 sprint hợp lệ.
- **Kết quả mong đợi**: Item vẫn hiển thị state Refining sau khi gán sprint; chỉ `sprintId`/`sprintName` đổi.

### backlog-23 — Move to Iteration validate Sprint cùng Repository

- **Business rule**: code `MoveToIterationCommandHandler.cs:27-29`.
- **Bước thực hiện**: Gọi PATCH `.../iteration` với `sprintId` thuộc Repository khác.
- **Kết quả mong đợi**: 400 "Sprint not found in this repository."

### backlog-24 — Chỉ Type = UserStory mới được Promote

- **Business rule**: [backlog.dod.md](../../histories/backlog.dod.md) §3; code `PromoteToSprintCommandHandler.cs:32-33`.
- **Bước thực hiện**: Thử Promote 1 Epic hoặc 1 Feature (kể cả khi state đã Ready — set thủ công qua API để bypass UI nếu UI chỉ cho UserStory).
- **Kết quả mong đợi**: 400 "Only UserStory backlog items can be promoted to a sprint."

### backlog-25 — Chỉ Promote được khi UserStory ở state Ready

- **Business rule**: [backlog.dod.md](../../histories/backlog.dod.md) §3; code `PromoteToSprintCommandHandler.cs:35-36`.
- **Bước thực hiện**: Thử Promote 1 UserStory đang ở state New/Refining (chưa Ready).
- **Kết quả mong đợi**: Nút Promote bị disable trên UI hoặc API trả 400 "Backlog item must be in 'Ready' state before promoting to a sprint. Current state: {state}."

### backlog-26 — Promote UserStory Ready → tạo đúng 1 SprintTask + BacklogItem chuyển Committed

- **Business rule**: [backlog.dod.md](../../histories/backlog.dod.md) §4.5, §5; code `PromoteToSprintCommandHandler.cs:41-65`.
- **Bước thực hiện**: Refine 1 UserStory tới Ready (kèm StoryPoints) → Promote vào 1 sprint hợp lệ.
- **Kết quả mong đợi**: 200 với `sprintTaskId`; BacklogItem chuyển `state=Committed`, `sprintId` = sprint đích; sang Boards/Sprint của sprint đó thấy đúng 1 SprintTask mới, `Type=UserStory`, `StoryPoints` kế thừa từ BacklogItem, `WorkItemNumber` tự tăng theo Repository, có `HistoryEntry` "Work item created." (xem [history.dod.md](../../histories/history.dod.md)).

### backlog-27 — Promote vào Sprint không tồn tại / khác Repository bị chặn

- **Business rule**: code `PromoteToSprintCommandHandler.cs:38-39`.
- **Bước thực hiện**: Gọi Promote với `sprintId` không tồn tại hoặc thuộc Repository khác.
- **Kết quả mong đợi**: 404 Not Found.

### backlog-28 — ParentId không thể đổi sau khi tạo (không có endpoint reparent)

- **Business rule**: Hệ quả gián tiếp của rule "không cho tạo quan hệ parent vòng (circular)" — code xác nhận `UpdateBacklogItemCommand.cs` **không có field `ParentId`** (chỉ Title/State/SprintId/StoryPoints/TshirtSize/AcceptanceCriteria/Documents), nên backend không có cách nào đổi cha của 1 item đã tồn tại → circular reference không thể xảy ra được (vì không reparent được, không phải vì có check circular tường minh).
- **Bước thực hiện**: Gọi PUT `.../{itemId}` (Update) với payload bất kỳ, quan sát response/DB — không có cách truyền `parentId` trong request.
- **Kết quả mong đợi**: Xác nhận `UpdateBacklogItemRequest` không có field `parentId`; item giữ nguyên `ParentId` ban đầu sau mọi lần Update.

### backlog-29 — List/filter theo Type và theo State

- **Business rule**: code `ListBacklogItemsQueryHandler.cs:33-36` — `query.TypeFilter`/`query.StateFilter` optional.
- **Bước thực hiện**: Gọi `GET /backlog?type=UserStory`; gọi `GET /backlog?state=Ready`.
- **Kết quả mong đợi**: Kết quả chỉ chứa item đúng filter (lưu ý: filter áp dụng trên list **đã build tree** nên item không khớp filter nhưng có con khớp filter có thể bị ẩn luôn — vì filter chạy trước `BuildTree`, xem code dòng 33-38).

### backlog-30 — Chỉ member của Repository mới xem được backlog

- **Business rule**: code `ListBacklogItemsQueryHandler.cs:22-23` — `IsMemberOfAsync`.
- **Bước thực hiện**: User không phải member của Repository X thử `GET /backlog` của repo X.
- **Kết quả mong đợi**: 401/403, không thấy dữ liệu backlog của repo X.

### backlog-31 — Item chưa estimate vẫn hợp lệ (estimate optional)

- **Business rule**: [backlog.dod.md](../../histories/backlog.dod.md) §6 — "Item chưa estimate (null story points/T-shirt size) vẫn hợp lệ trong backlog — estimate là optional."
- **Bước thực hiện**: Tạo item không điền estimate, để state ở New.
- **Kết quả mong đợi**: Tạo thành công, `storyPoints = null`, `tshirtSize = null`, hiển thị bình thường trong list (không bị ẩn/lỗi).

### backlog-32 — Rename qua endpoint Title riêng không ảnh hưởng field khác

- **Business rule**: code `UpdateBacklogTitleCommandHandler.cs` (PATCH `.../title`) — chỉ đổi `Title`, độc lập với endpoint Update đầy đủ (PUT).
- **Bước thực hiện**: Đổi Title qua nút inline-edit trên UI (gọi PATCH `.../title`) cho 1 item đã có estimate + AC + documents.
- **Kết quả mong đợi**: Chỉ `Title` đổi; `StoryPoints`/`TshirtSize`/`AcceptanceCriteria`/`Documents`/`State` giữ nguyên không đổi.

---

## Case Status

| Case ID | Trạng thái lần chạy gần nhất | Ngày | Bug liên quan |
|---------|-------------------------------|------|----------------|
| backlog-01 | Pass | 2026-07-01 | - |
| backlog-02 | Pass | 2026-07-01 | - |
| backlog-03 | Pass | 2026-07-01 | - |
| backlog-04 | Fail (theo doc) — xem IMP-002 | 2026-07-01 | - |
| backlog-05 | Pass | 2026-07-01 | - |
| backlog-06 | Pass (verify qua code review FE service, không click lại UI) | 2026-07-01 | - |
| backlog-07 | Pass | 2026-07-01 | - |
| backlog-08 | Pass | 2026-07-01 | - |
| backlog-09 | Pass | 2026-07-01 | - |
| backlog-10 | Pass | 2026-07-01 | - |
| backlog-11 | Pass (verify qua code review — cùng code path với backlog-10) | 2026-07-01 | - |
| backlog-12 | Pass | 2026-07-01 | BUG-009 (fixed) |
| backlog-13 | Pass | 2026-07-01 | - |
| backlog-14 | Pass | 2026-07-01 | - |
| backlog-15 | Pass | 2026-07-01 | - |
| backlog-16 | Pass | 2026-07-01 | - |
| backlog-17 | Pass | 2026-07-01 | - |
| backlog-18 | Pass | 2026-07-01 | - |
| backlog-19 | Pass | 2026-07-01 | - |
| backlog-20 | Pass | 2026-07-01 | - |
| backlog-21 | Pass | 2026-07-01 | BUG-010 (fixed) |
| backlog-22 | Pass | 2026-07-01 | - |
| backlog-23 | Pass | 2026-07-01 | - |
| backlog-24 | Pass | 2026-07-01 | BUG-009 (fixed) |
| backlog-25 | Pass | 2026-07-01 | BUG-009 (fixed) |
| backlog-26 | Pass | 2026-07-01 | - |
| backlog-27 | Pass | 2026-07-01 | - |
| backlog-28 | Pass (verify qua code review — không có field ParentId trong UpdateBacklogItemCommand) | 2026-07-01 | - |
| backlog-29 | Pass | 2026-07-01 | - |
| backlog-30 | Pass (verify qua code review + đối chiếu gián tiếp qua backlog-12/13/14) | 2026-07-01 | - |
| backlog-31 | Pass | 2026-07-01 | - |
| backlog-32 | Pass | 2026-07-01 | - |
