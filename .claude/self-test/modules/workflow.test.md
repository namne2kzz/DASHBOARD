# Workflow (Smart Board) — Test

> Đọc `../RULES.md` trước khi sửa file này.

## Run Log

| Ngày       | Giờ   | Kết quả    | Bug mới | Ghi chú                                                                                          |
| ---------- | ----- | ---------- | ------- | ------------------------------------------------------------------------------------------------ |
| 2026-07-02 | 16:54 | ⚠️ Partial | BUG-018 | workflow-19 Fail (500 thay 400 khi duplicate ID); workflow-20 Skip (không có non-admin account) |
| 2026-07-04 | 08:01 | ✅ All Pass | -       | workflow-19 Pass (BUG-018 fixed — endpoint returns 400); workflow-20 Pass (BA user controls disabled + action menu hidden) |

---

## Test Cases

> **Route UI**: `http://localhost:4200/DASH/workflow`
> **API base**: `http://localhost:5152/api/repositories/00000000-0000-0000-0002-000000000001/board`
> **Enum refs**:
>
> - `SprintTaskState`: New=0, Backlog=1, Todo=2, Active=3, InReview=4, Done=5
> - `WipMode`: Soft=0, Hard=1

---

## A — Khởi tạo & Hiển thị

### workflow-01 — Load trang: columns hiển thị đúng thứ tự

- **Business rule**: [workflow.dod.md](../../histories/workflow.dod.md) §3 — `Order` quyết định thứ tự trái→phải; `GetBoardQueryHandler` trả columns `OrderBy(c => c.Order)`.
- **Bước thực hiện**: Navigate `http://localhost:4200/DASH/workflow`. Quan sát bảng "Column configuration".
- **Kết quả mong đợi**: Mỗi row hiển thị: tên cột, state badge (tên tiếng Anh), WIP limit input, WIP mode select, aging limit input. Thứ tự row khớp với `Order` tăng dần từ API.

---

### workflow-02 — Stats widget tính đúng

- **Business rule**: [workflow.dod.md](../../histories/workflow.dod.md) §4 — UI tổng hợp từ danh sách cột.
- **Bước thực hiện**: Gọi `GET /board/columns`. Đếm tổng cột, đếm cột có `wipLimit > 0`. So sánh với widget "Columns", "With WIP limit", "States mapped X/6".
- **Kết quả mong đợi**: `Columns = tổng số cột`. `With WIP limit = số cột wipLimit > 0`. `States mapped = số cột / 6`.

---

## B — Tạo cột mới (modal)

### workflow-03 — Tạo cột hợp lệ qua modal

- **Business rule**: [workflow.dod.md](../../histories/workflow.dod.md) §4.1 — "Tạo cột board"; `CreateColumnCommandHandler.cs:39` — `Order = maxOrder + 1`.
- **Bước thực hiện**: Click nút ⋯ cạnh "Column configuration" → chọn "Add column". Điền name = "In Progress", mappedState = Active, WIP limit = 3, mode = Hard-stop, aging = 7 ngày. Click "Add column".
- **Kết quả mong đợi**: Modal đóng. Cột "In Progress" xuất hiện ở cuối bảng (Order lớn nhất). Verify qua `GET /board/columns` → cột mới có `order = maxOrder + 1`, `mappedState = 3 (Active)`, `wipLimit = 3`, `wipMode = 1 (Hard)`, `agingLimitDays = 7`.

---

### workflow-04 — Name rỗng → nút modal bị disable

- **Business rule**: `CreateColumnCommandValidator.cs` — `Name.NotEmpty()`.
- **Bước thực hiện**: Mở "Add column" modal. Để trống trường Name. Quan sát nút "Add column".
- **Kết quả mong đợi**: Nút "Add column" bị `disabled`, không click được. Không có API call nào gửi đi.

---

### workflow-05 — Name quá 100 ký tự → 422

- **Business rule**: `CreateColumnCommandValidator.cs` — `Name.MaximumLength(100)`.
- **Bước thực hiện**: `POST /board/columns?name={101-char-string}&mappedState=0&wipLimit=0&wipMode=0&agingLimitDays=5`.
- **Kết quả mong đợi**: 422 Validation Error.

---

### workflow-06 — WipLimit = 0 hợp lệ (unlimited)

- **Business rule**: `CreateColumnCommandValidator.cs` — `WipLimit.GreaterThanOrEqualTo(0)` → 0 hợp lệ.
- **Bước thực hiện**: Tạo cột qua modal với WIP limit = 0.
- **Kết quả mong đợi**: 201 tạo thành công. Trên bảng, cột mới hiển thị `— / ∞` ở cột "Current" (không có giới hạn).

---

### workflow-07 — WipLimit âm → 422

- **Business rule**: `CreateColumnCommandValidator.cs` — `GreaterThanOrEqualTo(0)`.
- **Bước thực hiện**: `POST /board/columns?name=Test&mappedState=0&wipLimit=-1&wipMode=0&agingLimitDays=5`.
- **Kết quả mong đợi**: 422, message "WIP limit must be 0 (unlimited) or a positive number."

---

### workflow-08 — AgingLimitDays = 0 → 422

- **Business rule**: `CreateColumnCommandValidator.cs` — `AgingLimitDays.GreaterThan(0)`.
- **Bước thực hiện**: `POST /board/columns?name=Test&mappedState=0&wipLimit=0&wipMode=0&agingLimitDays=0`.
- **Kết quả mong đợi**: 422, message "Aging limit must be at least 1 day."

---

## C — Inline edit tên + state mapping

### workflow-09 — Click ⋯ → edit mode inline

- **Business rule**: UX — click ⋯ trên row mở inline edit cho tên và state.
- **Bước thực hiện**: Click nút ⋯ trên 1 row bất kỳ trong bảng.
- **Kết quả mong đợi**: Cell "Column" chuyển thành `<input>` (pre-fill tên cũ), cell "State" chuyển thành `<select>` (pre-fill state hiện tại), 2 nút Save / Cancel xuất hiện ở cuối row. Các row khác không bị ảnh hưởng.

---

### workflow-10 — Lưu tên + state mới

- **Business rule**: [workflow.dod.md](../../histories/workflow.dod.md) §4.3 — "Update config cột (name, mapping state, WIP limit/mode, aging days)."
- **Bước thực hiện**: Mở inline edit (workflow-09). Đổi tên thành "Code Review", state thành "In Review". Click Save.
- **Kết quả mong đợi**: Row về view mode, tên hiện "Code Review", state badge hiện "In Review". `PUT /board/columns/{id}` trả 204. `GET /board/columns` → `mappedState = 4 (InReview)`.

---

### workflow-11 — Tên trống → Save bị disable

- **Business rule**: `UpdateColumnCommandValidator.cs` — `Name.NotEmpty()`.
- **Bước thực hiện**: Mở inline edit. Xoá hết tên trong input. Quan sát nút Save.
- **Kết quả mong đợi**: Nút Save `disabled`, không click được. Không có PUT call.

---

### workflow-12 — Cancel inline edit → không lưu

- **Business rule**: UX.
- **Bước thực hiện**: Mở inline edit (workflow-09). Đổi tên + state. Click Cancel.
- **Kết quả mong đợi**: Row về view mode với tên và state cũ. Không có PUT call nào gửi đi.

---

## D — Inline edit WIP và Aging

### workflow-13 — Đổi WIP limit inline → PUT ngay khi thay đổi giá trị

- **Business rule**: [workflow.dod.md](../../histories/workflow.dod.md) §3 — `WipLimit` lưu trên cột.
- **Bước thực hiện**: Trên bảng, sửa input WIP limit của 1 cột từ 0 → 5 (blur khỏi input hoặc ngModel change).
- **Kết quả mong đợi**: `PUT /board/columns/{id}` được gửi với `wipLimit=5`. 204. `GET /board/columns` → cột đó `wipLimit = 5`. Stats "With WIP limit" tăng lên 1 nếu trước đó là 0.

---

### workflow-14 — Đổi WIP mode Soft → Hard

- **Business rule**: [workflow.dod.md](../../histories/workflow.dod.md) §3 — `WipMode` Soft=cảnh báo, Hard=chặn move.
- **Bước thực hiện**: Đổi select WIP mode của 1 cột từ "Soft-warning" sang "Hard-stop".
- **Kết quả mong đợi**: `PUT /board/columns/{id}` với `wipMode=1 (Hard)`. 204. `GET /board/columns` → `wipMode = 1`.

---

### workflow-15 — Đổi aging limit → PUT

- **Business rule**: [workflow.dod.md](../../histories/workflow.dod.md) §3 — `AgingLimitDays` ngưỡng cảnh báo.
- **Bước thực hiện**: Đổi input aging limit của 1 cột từ 5 → 14 ngày (trigger change event).
- **Kết quả mong đợi**: `PUT /board/columns/{id}` với `agingLimitDays=14`. 204. `GET /board/columns` → cột đó `agingLimitDays = 14`.

---

## E — Order & Reorder (API level)

### workflow-16 — Cột mới tự nhận Order = maxOrder + 1

- **Business rule**: `CreateColumnCommandHandler.cs:26-29` — `maxOrder = Max(c.Order) ?? -1; column.Order = maxOrder + 1`.
- **Bước thực hiện**: `GET /board/columns` → ghi nhận maxOrder hiện tại. Tạo cột mới. `GET /board/columns` lại.
- **Kết quả mong đợi**: Cột mới có `order = maxOrder + 1`. Không có cột nào bị đổi order.

---

### workflow-17 — Reorder đầy đủ IDs → 204, order cập nhật đúng

- **Business rule**: `ReorderColumnsCommandHandler.cs:31-32` — validate list đủ và khớp đúng mọi cột.
- **Bước thực hiện**: `GET /board/columns` → lấy danh sách IDs. Gọi `PUT /board/columns/order` với danh sách IDs đảo ngược thứ tự.
- **Kết quả mong đợi**: 204. `GET /board/columns` → cột đứng cuối giờ ở đầu, thứ tự khớp với request.

---

### workflow-18 — Reorder thiếu 1 ID → 400

- **Business rule**: `ReorderColumnsCommandHandler.cs:31-33` — "The column ID list must include every column exactly once."
- **Bước thực hiện**: `GET /board/columns` → lấy IDs. Gọi `PUT /board/columns/order` với list thiếu 1 ID cuối.
- **Kết quả mong đợi**: 400, body `{error: "The column ID list must include every column exactly once."}`.

---

### workflow-19 — Reorder có ID trùng → 400

- **Business rule**: `ReorderColumnsCommandHandler.cs:32` — `Except(columns.Select(c => c.Id)).Any()` detect sai set.
- **Bước thực hiện**: `GET /board/columns` → lấy IDs. Gọi `PUT /board/columns/order` với list thay ID cuối bằng ID đầu (trùng lặp, tổng count bằng nhau nhưng có ID lạ).
- **Kết quả mong đợi**: 400, message "The column ID list must include every column exactly once."

---

## F — Permission

### workflow-20 — User thiếu ManageBoard → controls read-only

- **Business rule**: `CreateColumnCommandHandler.cs:23` + `UpdateColumnCommandHandler.cs:24` — check `ManageBoard`. UI: `board.canManageBoard()` quyết định disabled/hidden.
- **Bước thực hiện**: Đăng nhập bằng user thành viên không có permission ManageBoard. Navigate tới trang Workflow.
- **Kết quả mong đợi**: Inputs WIP limit, WIP mode, aging limit đều có attribute `disabled`. Nút ⋯ trên mỗi row không hiển thị (Actions column trống). Section menu "⋯" phía header không render (hoặc ẩn "Add column" option).

---

## Case Status

| Case ID | Trạng thái lần chạy gần nhất | Ngày | Bug liên quan |
| ------- | ----------------------------- | ---- | -------------- |
| workflow-01 | Pass | 2026-07-02 | - |
| workflow-02 | Pass | 2026-07-02 | - |
| workflow-03 | Pass | 2026-07-02 | - |
| workflow-04 | Pass | 2026-07-02 | - |
| workflow-05 | Pass | 2026-07-02 | - |
| workflow-06 | Pass | 2026-07-02 | - |
| workflow-07 | Pass | 2026-07-02 | - |
| workflow-08 | Pass | 2026-07-02 | - |
| workflow-09 | Pass | 2026-07-02 | - |
| workflow-10 | Pass | 2026-07-02 | - |
| workflow-11 | Pass | 2026-07-02 | - |
| workflow-12 | Pass | 2026-07-02 | - |
| workflow-13 | Pass | 2026-07-02 | - |
| workflow-14 | Pass | 2026-07-02 | - |
| workflow-15 | Pass | 2026-07-02 | - |
| workflow-16 | Pass | 2026-07-02 | - |
| workflow-17 | Pass | 2026-07-02 | - |
| workflow-18 | Pass | 2026-07-02 | - |
| workflow-19 | Pass | 2026-07-04 | - |
| workflow-20 | Pass | 2026-07-04 | - |
