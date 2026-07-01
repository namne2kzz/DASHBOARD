# SmartBoard — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-06-23 | 22:57 | Đổi tên component FE | Frontend (Angular) đổi tên component/service `SmartBoard` → `Workflow` để khớp route path `workflow`; backend giữ nguyên tên `SmartBoard` |
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document business ban đầu cho feature SmartBoard |

---

## 1. Purpose

Bảng kanban có giới hạn WIP (work-in-progress) bằng cách map `SprintTaskState` vào các cột, hỗ trợ aging indicator để giữ kỷ luật làm việc. Feature này chỉ lo phần **cấu hình** cột/WIP (trang Workflow) — trải nghiệm kéo-thả card thực tế nằm ở [boards.dod.md](boards.dod.md).

## 2. Key Entities & Relationships

> **Naming note**: Backend (Domain/Application) dùng tên `SmartBoard` (`SmartBoardColumn`, `Application/SmartBoard/...`). Frontend (Angular) dùng tên `Workflow` (`WorkflowPageComponent`, `WorkflowService`, route path `/workflow`) để khớp với router — cùng 1 feature business, chỉ khác naming giữa 2 layer.

- `SmartBoardColumn`: mỗi cột map đúng 1 `SprintTaskState` (New/Backlog/Todo/Active/InReview/Done); có thể có nhiều cột map cùng 1 state nếu cần chia nhỏ workflow.
- `WipLimit` (0 = không giới hạn) + `WipMode` (Soft/Hard): Soft chỉ warning, Hard chặn move khi vượt limit.
- `Order`: thứ tự hiển thị trái → phải. `AgingLimitDays`: ngưỡng cảnh báo item nằm quá lâu trong cột.
- Scope theo Repository — mỗi Repository tự cấu hình board riêng.

## 3. Business Rules & Invariants

- 1 state chỉ map 1-1 vào 1 cột tại một thời điểm xác định trong cấu hình của 1 board, nhưng cấu hình cho phép remap.
- WIP Hard: từ chối move (kéo task vào cột) khi sẽ vượt limit. WIP Soft: cho phép nhưng có warning.
- Aging chỉ cảnh báo, không chặn move.
- `Order` là số nguyên 0-based, quản lý tường minh; cột mới thêm tự nhận order kế tiếp.

## 4. Main Workflows / Use Cases

1. Tạo cột board (name, state mapping, WIP setting, aging threshold).
2. Reorder cột để đổi thứ tự hiển thị.
3. Update config cột (name, mapping state, WIP limit/mode, aging days).
4. Hiển thị task theo cột kèm trạng thái WIP và aging flag.
5. Drag-and-drop task giữa cột → enforce WIP + state transition rule.

## 5. Definition of Done

- [ ] Move task vào cột Hard-WIP vượt limit phải bị chặn ở backend, không chỉ ở UI.
- [ ] Đổi mapping state của cột không tự động đổi state của task đang nằm trong cột đó.
- [ ] Reorder cột cập nhật đúng `Order` cho toàn bộ cột liên quan, không để trùng order.

## 6. Edge Cases & Notes

- Sau khi remap cột sang state khác, task cũ vẫn giữ state cũ cho tới khi được move thủ công.
- Board có thể tạm thời vượt WIP nếu state thay đổi từ nơi khác (không qua move) rồi giảm dần khi xử lý tiếp.
