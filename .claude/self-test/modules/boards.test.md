# Boards (Kanban Board) — Test

> Đọc `../RULES.md` trước khi sửa file này.

## Run Log

| Ngày | Giờ | Kết quả | Bug mới | Ghi chú |
|------|-----|---------|---------|---------|
| 2026-07-04 | 09:34 | ⚠️ Partial | BUG-019, BUG-020, BUG-021 | Lần chạy đầu tiên — soạn 22 case chi tiết dựa trên boards.dod.md rồi chạy qua UI thật (Playwright msedge) + API trực tiếp. Case Pass: 01,02,03,06,07,08,09,10,11,12,17. Case Fail: 01b (2 cột cùng map Active(3) → card duplicate, BUG-019). Bug phát hiện thêm ngoài case đã soạn: AssignSprintTaskCommandHandler không chặn assign UserStory cho cá nhân dù CreateSprintTaskCommandHandler có chặn (BUG-020, verify qua boards-10 mở rộng); GetBoardTasksQueryHandler throw thay vì Result (BUG-021, user phát hiện qua debugger lúc test boards-17). Case Not run: 04,05,13,14 (Angular CDK drag-drop dùng pointer events, không simulate được qua `browser_drag` của Playwright MCP — tool đó mô phỏng native HTML5 drag, không tương thích CDK); 15,16,19,20,21 (chưa kịp chạy, ưu tiên thấp hơn). Case N/A: 18 (sau khi user cập nhật lại permission set 2026-07-04, TẤT CẢ role mặc định — kể cả Tester/Business Analyst — đều có EditWorkItem, không còn role nào để test "user thiếu EditWorkItem"; cần tạo custom role riêng nếu muốn test lại). Improvement ghi thêm: non-member navigate thẳng vào route cấp-repo nhận trang trắng hoàn toàn thay vì redirect/thông báo lỗi (IMP-003). |

---

## Test Cases

> **Data hiện có**: repo DASH (`00000000-0000-0000-0002-000000000001`), sprint active "Sprint 1 July 2026" (`275c24bc-fc54-426a-bf54-a7a2cf73cc12`) với 23 SprintTask. Columns hiện tại: Code Review (state=Active/3, WIP=3, Hard), In-Progress (state=Active/3, WIP=3, Hard — **2 cột cùng map Active**, xem boards-01b), In Review (state=InReview/4, WIP=2, Hard), Done (state=Done/5, WIP=10, Soft), Backlog Queue (state=Backlog/1, WIP=0/unlimited, Soft), Code Review 2 (state=New/0, WIP=0/unlimited, Soft). Capacity members: 8 user (Admin, Agasha Hiroshi, Alez Agato, Dev User, Edogawa Conan, James Carter, Jex Jame, Ji Chang Wok).
> **UI route**: `http://localhost:4200/DASH/boards`
> **API base**: `http://localhost:5152/api/repositories/00000000-0000-0000-0002-000000000001`

---

## A — Load & Display

### boards-01 — Load board hiển thị đúng cột theo Workflow config

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §2, §4.1 — card nhóm vào cột theo `mappedState` khớp `SprintTaskState`.
- **Bước thực hiện**: Navigate `/{repoCode}/boards`, chọn 1 sprint đang active.
- **Kết quả mong đợi**: Các cột render đúng theo cấu hình Workflow; card nằm đúng cột tương ứng state hiện tại.

### boards-01b — ⚠️ Edge case: 2 cột cùng map 1 state (Active)

- **Business rule**: Không có rule nào trong doc chặn 2 cột cùng `mappedState` — nhưng cả "Code Review" và "In-Progress" hiện đang cùng map `Active(3)`. Đây là phát hiện qua dữ liệu thật, chưa rõ là bug hay thiết kế cố ý.
- **Bước thực hiện**: Navigate board, quan sát card có state=Active hiển thị ở cột nào (Code Review hay In-Progress, hay bị duplicate ở cả 2).
- **Kết quả mong đợi theo thiết kế an toàn**: Mỗi card chỉ nên xuất hiện ở đúng 1 cột. Nếu FE dùng `find()` lấy cột đầu tiên khớp mappedState thì card sẽ luôn về 1 cột cố định (theo thứ tự Order), cột kia sẽ luôn trống dù có card Active — cần verify thực tế và cân nhắc ghi `improvements.md`.

### boards-08 — Flat list order trong cùng 1 cột: Type → State → CreatedAt

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §2 — "Board load flat list toàn bộ SprintTask của 1 Sprint... sắp theo Type → State → CreatedAt."
- **Bước thực hiện**: Gọi `GET /board/tasks?sprintId={activeSprintId}` (hoặc tương đương query board dùng). So sánh thứ tự trả về với sort thủ công theo Type rồi State rồi CreatedAt.
- **Kết quả mong đợi**: Thứ tự API trả về khớp đúng logic sort 3 tầng.

### boards-09 — Cột rỗng (không có card khớp) vẫn hiển thị bình thường

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §6 — "Cột rỗng do filter/sprint không có task vẫn hiển thị bình thường, không tính là vượt WIP."
- **Bước thực hiện**: Quan sát cột "Code Review 2" (mappedState=New/0) — sprint hiện tại có thể không có task nào ở state New.
- **Kết quả mong đợi**: Cột vẫn render với header + WIP indicator bình thường (0/∞ hoặc 0/limit), không lỗi, không ẩn cột.

### boards-10 — UserStory hiển thị trên board dù không có assignee cá nhân

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §5, §6 — "Board luôn hiển thị đúng 4 loại work item (Task/Bug/TestPlan/UserStory)... UserStory hiển thị trên board nhưng không có assignee theo domain rule."
- **Bước thực hiện**: Tìm 1 card `Type=UserStory` trên board đang active.
- **Kết quả mong đợi**: Card UserStory hiển thị bình thường trong đúng cột theo state, không bị ẩn dù `assignedToId=null`.

---

## B — Search & Filter

### boards-02 — Search theo title/work-item-number

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §3 — filter chỉ áp dụng trên list đã load, match substring không phân biệt hoa/thường.
- **Bước thực hiện**: Gõ vào ô search 1 chuỗi con của title 1 card đang có.
- **Kết quả mong đợi**: Chỉ card khớp substring hiển thị, các card khác bị ẩn.

### boards-11 — Search không phân biệt hoa/thường

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §3 — "match theo title... không phân biệt hoa/thường."
- **Bước thực hiện**: Gõ search với case ngược lại hoàn toàn so với title thật (vd title có "Add", gõ "ADD" hoặc "add").
- **Kết quả mong đợi**: Card vẫn match và hiển thị, không phân biệt hoa/thường.

### boards-12 — Search theo Work Item Number (DASH-xx)

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §3 — "match theo title... hoặc work item number."
- **Bước thực hiện**: Gõ `DASH-{n}` của 1 task đang có vào ô search.
- **Kết quả mong đợi**: Đúng card đó hiển thị, các card khác bị ẩn.

### boards-03 — Filter "Assigned to me" kết hợp AND với search

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §3 — "Assigned to me" kết hợp AND với search text.
- **Bước thực hiện**: Bật filter "Assigned to me" + gõ thêm search text.
- **Kết quả mong đợi**: Chỉ card vừa khớp search vừa assigned cho user hiện tại hiển thị.

---

## C — Drag & Drop / WIP

### boards-04 — Kéo card sang cột khác (trong WIP limit)

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §3 — optimistic update, PATCH state.
- **Bước thực hiện**: Drag 1 card sang cột kế bên (cột còn dưới WIP limit).
- **Kết quả mong đợi**: Card chuyển cột ngay (optimistic), không rollback sau khi API trả thành công.

### boards-05 — Chặn drop khi cột Hard mode đã đạt WIP limit

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §3 — "cột mode Hard đã đạt WipLimit → chặn drop ngay tại UI, không gọi API".
- **Bước thực hiện**: Tìm/tạo tình huống cột Hard mode đầy WIP (vd "In Review" WIP=2 — thêm card cho đủ 2 rồi thử kéo thêm 1 card nữa vào), thử drag card vào cột đó.
- **Kết quả mong đợi**: Drop bị chặn ngay tại UI (CDK enterPredicate) — quan sát network requests (`browser_network_requests`) để xác nhận **không có** request PATCH nào được gửi.

### boards-13 — Cột Soft mode vẫn cho drop dù vượt WIP limit

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §3 — "Mode Soft luôn cho drop" (chỉ cảnh báo, không chặn).
- **Bước thực hiện**: Kéo card vào cột Soft mode (vd "Done", WIP=10) cho tới khi vượt limit.
- **Kết quả mong đợi**: Drop vẫn thành công (PATCH được gửi, card chuyển cột), có thể có cảnh báo visual (màu đỏ/badge) nhưng không chặn action.

### boards-14 — Optimistic update rollback khi PATCH thất bại

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §3 — "nếu API lỗi → card tự rollback về state cũ."
- **Bước thực hiện**: Simulate lỗi PATCH (vd revoke quyền `EditWorkItem` giữa chừng, hoặc kéo card mà backend trả 400/500 — có thể test qua việc gọi PATCH state trực tiếp với state không hợp lệ để xem UI xử lý ra sao nếu tự thao tác qua console, hoặc quan sát code nếu không thể induce lỗi thật qua UI).
- **Kết quả mong đợi**: Card trở về cột/state cũ khi API thất bại, không đứng yên ở cột mới với dữ liệu sai.

---

## D — Quick Create

### boards-06 — Quick-create work item yêu cầu title

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §3 — title bắt buộc, field khác optional.
- **Bước thực hiện**: Mở quick-create, để trống title, submit.
- **Kết quả mong đợi**: Validation chặn submit, hiện lỗi yêu cầu title.

### boards-07 — Assignee picker chỉ liệt kê CapacityMember của sprint

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §3 — assignee picker chỉ liệt kê member đã có CapacityMember trong sprint đang chọn.
- **Bước thực hiện**: Mở quick-create/edit task, mở dropdown assignee.
- **Kết quả mong đợi**: Danh sách chỉ gồm member có trong Capacity của sprint hiện tại (8 member: Admin, Agasha Hiroshi, Alez Agato, Dev User, Edogawa Conan, James Carter, Jex Jame, Ji Chang Wok), không phải toàn bộ Repository member.

### boards-15 — Quick-create tạo đúng Type (Task/Bug/TestPlan) và xuất hiện đúng cột

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §4.4 — "Quick-create Task/Bug/TestPlan từ board (chọn type, điền form...)".
- **Bước thực hiện**: Tạo lần lượt 1 Task, 1 Bug, 1 TestPlan qua quick-create (title hợp lệ, state mặc định New).
- **Kết quả mong đợi**: Cả 3 tạo thành công với đúng `Type`, xuất hiện ở cột mapped với state New (nếu có cột nào map New).

### boards-16 — Quick-create xong board tự reload hiển thị item mới

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §3 — "tạo xong board tự reload để hiển thị item mới."
- **Bước thực hiện**: Tạo 1 work item mới qua quick-create, quan sát board ngay sau khi dialog đóng.
- **Kết quả mong đợi**: Card mới xuất hiện ngay trên board mà không cần user tự F5.

---

## E — Permissions

### boards-17 — Chỉ member của Repository mới xem được board

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §3 — "Chỉ member của Repository được xem board (`IsMemberOfAsync`)".
- **Bước thực hiện**: User không phải member của repo X thử `GET /board/tasks` hoặc navigate `/X/boards`.
- **Kết quả mong đợi**: 401/403, không thấy dữ liệu board của repo X.

### boards-18 — User thiếu EditWorkItem không kéo-thả/tạo được work item

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §3 — "chỉ user có quyền `EditWorkItem` mới được kéo-thả card hoặc tạo work item mới."
- **Bước thực hiện**: Đăng nhập user thiếu `EditWorkItem` (vd `ba` — Business Analyst, xem `self-test.local.json`), thử kéo card và mở quick-create.
- **Kết quả mong đợi**: Drag bị chặn tại UI (hoặc PATCH trả 400 nếu bypass UI) và/hoặc nút quick-create bị ẩn/disable.

---

## F — Task Detail Dialog

### boards-19 — Search Parent Story: tối đa 20 kết quả, sort CreatedAt mới nhất

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §3 — "Search Parent Story... trả tối đa 20 kết quả, match substring theo title/work item number, sort theo CreatedAt mới nhất."
- **Bước thực hiện**: Mở Task Detail Dialog (hoặc quick-create) cho 1 sub-task, mở search Parent Story, gõ 1 từ khoá phổ biến khớp nhiều UserStory.
- **Kết quả mong đợi**: Kết quả trả về tối đa 20 item, sắp xếp mới nhất trước.

### boards-20 — Sửa task từ Task Detail Dialog đồng bộ board qua patchItem

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §3 — "Sửa task từ Task Detail Dialog... đồng bộ lại board qua `patchItem` (cập nhật cục bộ, không reload toàn bộ)."
- **Bước thực hiện**: Click 1 card mở Task Detail Dialog, đổi assignee hoặc estimate, Save.
- **Kết quả mong đợi**: Card trên board cập nhật ngay field vừa đổi mà không thấy toàn bộ board reload/nhấp nháy.

---

## G — Edge Cases

### boards-21 — Cột bị xoá/đổi mapping khi board đang mở → card orphan không hiển thị

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §6 — "Nếu cột bị xoá/đổi mapping ở trang Workflow trong khi board đang mở, card có state không còn khớp cột nào sẽ không hiển thị cho tới khi reload trang."
- **Bước thực hiện**: Mở board ở 1 tab, mở Workflow ở tab khác đổi mappedState của 1 cột đang có card. Quay lại tab board (không F5).
- **Kết quự mong đợi**: Card thuộc state cũ của cột đó biến mất khỏi UI board (không tự động re-fetch), tới khi F5 lại mới hiển thị đúng theo mapping mới.

---

## Case Status

| Case ID | Trạng thái lần chạy gần nhất | Ngày | Bug liên quan |
|---------|-------------------------------|------|----------------|
| boards-01 | Pass | 2026-07-04 | - |
| boards-01b | Fail | 2026-07-04 | BUG-019 |
| boards-02 | Pass | 2026-07-04 | - |
| boards-03 | Pass | 2026-07-04 | - |
| boards-04 | Not run (CDK drag-drop không simulate được qua Playwright MCP) | 2026-07-04 | - |
| boards-05 | Not run (cùng lý do boards-04) | 2026-07-04 | - |
| boards-06 | Pass | 2026-07-04 | - |
| boards-07 | Pass | 2026-07-04 | - |
| boards-08 | Pass | 2026-07-04 | - |
| boards-09 | Pass | 2026-07-04 | - |
| boards-10 | Pass (hiển thị), mở rộng phát hiện BUG-020 (assign) | 2026-07-04 | BUG-020 |
| boards-11 | Pass | 2026-07-04 | - |
| boards-12 | Fail (re-verify 2026-07-04, kết quả Pass trước đó sai) | 2026-07-04 | BUG-023 |
| boards-13 | Not run (cùng lý do boards-04) | 2026-07-04 | - |
| boards-14 | Not run (cùng lý do boards-04) | 2026-07-04 | - |
| boards-15 | Chưa chạy | - | - |
| boards-16 | Chưa chạy | - | - |
| boards-17 | Pass | 2026-07-04 | BUG-021 |
| boards-18 | N/A (không còn role mặc định nào thiếu EditWorkItem sau khi permission redesign 2026-07-04) | 2026-07-04 | - |
| boards-19 | Chưa chạy | - | - |
| boards-20 | Chưa chạy | - | - |
| boards-21 | Chưa chạy | - | - |
