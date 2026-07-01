# Boards (Kanban Board) — Test

> Đọc `../RULES.md` trước khi sửa file này.

## Run Log

| Ngày | Giờ | Kết quả | Bug mới | Ghi chú |
|------|-----|---------|---------|---------|

---

## Test Cases

### boards-01 — Load board hiển thị đúng cột theo Workflow config

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §2, §4.1 — card nhóm vào cột theo `mappedState` khớp `SprintTaskState`.
- **Bước thực hiện**: Navigate `/{repoCode}/boards`, chọn 1 sprint đang active.
- **Kết quả mong đợi**: Các cột render đúng theo cấu hình Workflow; card nằm đúng cột tương ứng state hiện tại.

### boards-02 — Search theo title/work-item-number

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §3 — filter chỉ áp dụng trên list đã load, match substring không phân biệt hoa/thường.
- **Bước thực hiện**: Gõ vào ô search 1 chuỗi con của title 1 card đang có.
- **Kết quả mong đợi**: Chỉ card khớp substring hiển thị, các card khác bị ẩn.

### boards-03 — Filter "Assigned to me" kết hợp AND với search

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §3 — "Assigned to me" kết hợp AND với search text.
- **Bước thực hiện**: Bật filter "Assigned to me" + gõ thêm search text.
- **Kết quả mong đợi**: Chỉ card vừa khớp search vừa assigned cho user hiện tại hiển thị.

### boards-04 — Kéo card sang cột khác (trong WIP limit)

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §3 — optimistic update, PATCH state.
- **Bước thực hiện**: Drag 1 card sang cột kế bên (cột còn dưới WIP limit).
- **Kết quả mong đợi**: Card chuyển cột ngay (optimistic), không rollback sau khi API trả thành công.

### boards-05 — Chặn drop khi cột Hard mode đã đạt WIP limit

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §3 — "cột mode Hard đã đạt WipLimit → chặn drop ngay tại UI, không gọi API".
- **Bước thực hiện**: Tìm/tạo tình huống cột Hard mode đầy WIP, thử drag card vào cột đó.
- **Kết quả mong đợi**: Drop bị chặn ngay tại UI (CDK enterPredicate) — quan sát network requests (`browser_network_requests`) để xác nhận **không có** request PATCH nào được gửi.

### boards-06 — Quick-create work item yêu cầu title

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §3 — title bắt buộc, field khác optional.
- **Bước thực hiện**: Mở quick-create, để trống title, submit.
- **Kết quả mong đợi**: Validation chặn submit, hiện lỗi yêu cầu title.

### boards-07 — Assignee picker chỉ liệt kê CapacityMember của sprint

- **Business rule**: [boards.dod.md](../../histories/boards.dod.md) §3 — assignee picker chỉ liệt kê member đã có CapacityMember trong sprint đang chọn.
- **Bước thực hiện**: Mở quick-create/edit task, mở dropdown assignee.
- **Kết quả mong đợi**: Danh sách chỉ gồm member có trong Capacity của sprint hiện tại, không phải toàn bộ Repository member.

---

## Case Status

| Case ID | Trạng thái lần chạy gần nhất | Ngày | Bug liên quan |
|---------|-------------------------------|------|----------------|
| boards-01 | Chưa chạy | - | - |
| boards-02 | Chưa chạy | - | - |
| boards-03 | Chưa chạy | - | - |
| boards-04 | Chưa chạy | - | - |
| boards-05 | Chưa chạy | - | - |
| boards-06 | Chưa chạy | - | - |
| boards-07 | Chưa chạy | - | - |
