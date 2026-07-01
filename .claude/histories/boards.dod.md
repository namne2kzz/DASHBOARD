# Boards (Kanban Board) — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-06-23 | 23:10 | Khởi tạo document | Tạo document business ban đầu cho feature Boards (Kanban board operational view) |

---

## 1. Purpose

Trang Kanban Board (route `/boards`) là **view vận hành** — hiển thị toàn bộ task của sprint đang chọn dưới dạng card kéo-thả qua các cột, nơi team thực sự làm việc hàng ngày. Khác với [smart-board.dod.md](smart-board.dod.md) (Workflow page — chỉ cấu hình cột/WIP limit) và [sprint-tasks.dod.md](sprint-tasks.dod.md) (business rule chung của SprintTask) — feature này là lớp **trải nghiệm tương tác** (drag-drop, filter, quick-create) kết hợp dữ liệu từ cả 2 feature đó.

## 2. Key Entities & Relationships

- Không có entity riêng — đây là **read/interaction layer** kết hợp:
  - `SmartBoardColumn` (xem [smart-board.dod.md](smart-board.dod.md)): định nghĩa cột, state mapping, WIP limit/mode.
  - `SprintTask` (xem [sprint-tasks.dod.md](sprint-tasks.dod.md)): dữ liệu card hiển thị — `BoardTaskDto` gồm WorkItemNumber, Type, Title, Priority, AssignedTo, OriginalEstimate/RemainingWork/CompletedWork, State, StateChangedAt.
- Board load **flat list** toàn bộ SprintTask của 1 Sprint (không phân cấp parent-child), sắp theo Type → State → CreatedAt.
- Mỗi card thuộc đúng 1 cột dựa trên `mappedState` của cột khớp với `SprintTaskState` hiện tại của task.

## 3. Business Rules & Invariants

- Chỉ member của Repository được xem board (`IsMemberOfAsync`); chỉ user có quyền `EditWorkItem` mới được kéo-thả card hoặc tạo work item mới.
- **WIP enforcement khi drop**: cột ở mode Hard và đã đạt `WipLimit` → chặn drop ngay tại UI (CDK `enterPredicate`), không gọi API. Mode Soft luôn cho drop.
- **Optimistic update + rollback**: kéo card sang cột khác cập nhật UI ngay (state + `stateChangedAt`), sau đó gửi PATCH lên server; nếu API lỗi → card tự rollback về state cũ.
- **Search/filter**: filter chỉ áp dụng trên list đã load (không query lại server) — match theo title (substring, không phân biệt hoa/thường) hoặc work item number; filter "Assigned to me" kết hợp AND với search text.
- Quick-create work item (Task/Bug/TestPlan) từ board: title bắt buộc, các field còn lại optional; tạo xong board tự reload để hiển thị item mới.
- Assignee picker khi tạo task chỉ liệt kê member đã có `CapacityMember` trong sprint đang chọn — không phải toàn bộ Repository member.
- Search Parent Story (gắn task con vào UserStory): trả tối đa 20 kết quả, match substring theo title/work item number, sort theo CreatedAt mới nhất.
- Sửa task từ Task Detail Dialog (assignee, estimate, description...) đồng bộ lại board qua `patchItem` (cập nhật cục bộ, không reload toàn bộ).

## 4. Main Workflows / Use Cases

1. Chọn sprint → board tự load toàn bộ task của sprint đó, nhóm theo cột (Workflow column config).
2. Search theo tên/work-item-number và/hoặc filter "Assigned to me".
3. Kéo card sang cột khác → optimistic move → PATCH state → rollback nếu lỗi.
4. Quick-create Task/Bug/TestPlan từ board (chọn type, điền form, optional gán Parent Story + Assignee).
5. Click card → mở Task Detail Dialog xem/sửa đầy đủ field, discussion, history.
6. Tìm Parent Story khi tạo/sửa task con.

## 5. Definition of Done

- [ ] Drop vào cột Hard-WIP đã đầy phải bị chặn tại UI trước khi gọi API (không phụ thuộc validate backend).
- [ ] Mọi optimistic move đều có rollback đúng khi PATCH state thất bại.
- [ ] Filter "Assigned to me" + search text luôn kết hợp AND, không loại trừ nhau.
- [ ] Quick-create chỉ cho chọn assignee trong danh sách CapacityMember của sprint hiện tại.
- [ ] Board luôn hiển thị đúng 4 loại work item (Task/Bug/TestPlan/UserStory) ở dạng flat list, không ẩn UserStory.

## 6. Edge Cases & Notes

- Nếu cột bị xoá/đổi mapping ở trang Workflow trong khi board đang mở, card có state không còn khớp cột nào sẽ không hiển thị cho tới khi reload trang.
- Quyền bị revoke giữa lúc xem board và lúc kéo thả → drop fail âm thầm (không toast lỗi), card tự rollback.
- 2 user kéo cùng 1 card đồng thời: API call nào thành công sau cùng sẽ thắng — **không có conflict resolution**.
- UserStory hiển thị trên board nhưng không có assignee theo domain rule (xem [sprint-tasks.dod.md](sprint-tasks.dod.md)), dù UI vẫn cho chọn.
- Cột rỗng do filter/sprint không có task vẫn hiển thị bình thường, không tính là vượt WIP.
