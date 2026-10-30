# Boards (Kanban Board) — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-10-28 | — | Điều hướng ngang: nút mũi tên + auto-scroll khi kéo thả | Board cuộn ngang nhưng thanh scroll nằm dưới cùng một danh sách cột cao, thường khuất khỏi màn hình → muốn sang cột kế phải cuộn dọc xuống tìm thanh scroll trước. (1) **Nút mũi tên trái/phải** nổi trên cột đầu/cuối, mỗi lần bấm sang ~1 màn (chừa ~15% để mắt có điểm neo), tự ẩn khi hết chỗ cuộn bên đó. (2) **Auto-scroll khi kéo thẻ**: kéo thẻ tới sát mép (trong 110px) thì board tự cuộn về phía đó, tốc độ tăng dần theo độ sâu vào vùng mép (bình phương, để đoạn chậm rộng hơn, dễ kiểm soát), tối đa 22px/frame; chạm hết hai đầu thì dừng. Mục đích: thả được thẻ vào cột mà viewport chưa từng hiện. CDK chỉ tự cuộn danh sách mà con trỏ **đang ở trên**, nên không giải quyết được trường hợp cột đích còn nằm ngoài màn hình. Logic nằm ở `HorizontalPagerDirective` (dùng lại được cho vùng cuộn ngang khác). |
| 2026-10-27 | 23:19 | Chặn drop theo type + tuổi thẻ 3 mức | (1) **Drop giờ check 2 rule độc lập**: ngoài WIP hard-stop, cột còn phải có `MappedState` nằm trong `getAllowedStates(type)` của thẻ đang kéo — vd Test Plan không có "In Review" nên không bao giờ thả vào đó được, dù cột trống. Hai lý do từ chối hiển thị **khác câu chữ** (cột đầy sẽ hết đầy, sai type thì không bao giờ hợp lệ), và rule type được báo trước vì nó là rule không đổi. Cột không hợp type mờ đi 45% ngay khi bắt đầu kéo. (2) **Nhãn tuổi thẻ chia 3 mức** theo bội số `AgingLimitDays`: vàng (1–2×), cam (2–4×), đỏ (≥ 4×) — thay cho 1 cờ "aging" gộp chung thẻ quá hạn 1 ngày với thẻ quá hạn 100 ngày. |
| 2026-07-25 | 17:45 | Dynamic query filter | Nối query builder vào filtering (live); criteria Title/Type/Priority/State/AssignedTo/Remaining/**Labels**, combine AND/OR; card hiển thị chip Labels |
| 2026-06-23 | 23:10 | Khởi tạo document | Tạo document business ban đầu cho feature Boards (Kanban board operational view) |

---

## 1. Purpose

Trang Kanban Board (route `/boards`) là **view vận hành** — hiển thị toàn bộ task của sprint đang chọn dưới dạng card kéo-thả qua các cột, nơi team thực sự làm việc hàng ngày. Khác với [workflow.dod.md](workflow.dod.md) (Workflow page — chỉ cấu hình cột/WIP limit) và [sprint-tasks.dod.md](sprint-tasks.dod.md) (business rule chung của SprintTask) — feature này là lớp **trải nghiệm tương tác** (drag-drop, filter, quick-create) kết hợp dữ liệu từ cả 2 feature đó.

## 2. Key Entities & Relationships

- Không có entity riêng — đây là **read/interaction layer** kết hợp:
  - `SmartBoardColumn` (xem [workflow.dod.md](workflow.dod.md)): định nghĩa cột, state mapping, WIP limit/mode.
  - `SprintTask` (xem [sprint-tasks.dod.md](sprint-tasks.dod.md)): dữ liệu card hiển thị — `BoardTaskDto` gồm WorkItemNumber, Type, Title, Priority, AssignedTo, OriginalEstimate/RemainingWork/CompletedWork, State, StateChangedAt.
- Board load **flat list** toàn bộ SprintTask của 1 Sprint (không phân cấp parent-child), sắp theo Type → State → CreatedAt.
- Mỗi card thuộc đúng 1 cột dựa trên `mappedState` của cột khớp với `SprintTaskState` hiện tại của task.

## 3. Business Rules & Invariants

- Chỉ member của Repository được xem board (`IsMemberOfAsync`); chỉ user có quyền `EditWorkItem` mới được kéo-thả card hoặc tạo work item mới.
- **Drop được chấp nhận khi thỏa CẢ 2 rule** (CDK `enterPredicate`, chặn ngay tại UI, không gọi API):
  1. **Type phải occupy được state của cột**: `getAllowedStates(type)` phải chứa `MappedState` của cột. Đây là thuộc tính của loại work item — Test Plan không có state "In Review", nên cột đó không bao giờ nhận Test Plan dù trống hoàn toàn. Rule này **không phụ thuộc capacity**.
  2. **WIP**: cột mode Hard đã đạt `WipLimit` → chặn. Mode Soft luôn cho drop (chỉ cảnh báo).
- **Hai lý do từ chối phải nói khác nhau**: "cột đầy" là tạm thời (rời 1 thẻ là nhận được), "state không áp dụng cho loại work item này" là vĩnh viễn. Khi cả 2 cùng fail → **báo lý do type trước**, vì đó là lý do còn đúng vào ngày mai.
- **Cột không hợp type mờ đi (45%) ngay khi bắt đầu kéo**, không đợi hover — để người kéo thấy được các đích hợp lệ mà không phải thử từng cột.
- **Nhãn tuổi thẻ chia 3 mức** theo số ngày từ `StateChangedAt`, tính bằng bội số `AgingLimitDays` của cột (cột có `AgingLimitDays = 0` → không hiện nhãn):
  - `warn` (vàng): 1× đến dưới 2× limit — bắt đầu trôi.
  - `late` (cam): 2× đến dưới 4× limit.
  - `stale` (đỏ): từ 4× limit trở lên — gần như đã bị bỏ quên.
  - Lý do chia mức: thẻ quá hạn 1 ngày và thẻ quá hạn 100 ngày là 2 vấn đề khác nhau, mà đó đúng là thứ người lướt board đang tìm. Nhãn luôn ghi cả số ngày thật lẫn ngưỡng (`12d · over 7d`), không chỉ dựa vào màu.
  - Aging vẫn **chỉ cảnh báo, không chặn move** (xem workflow.dod.md).
- **Optimistic update + rollback**: kéo card sang cột khác cập nhật UI ngay (state + `stateChangedAt`), sau đó gửi PATCH lên server; nếu API lỗi → card tự rollback về state cũ.
- **Search/filter**: filter chỉ áp dụng trên list đã load (không query lại server) — match theo title (substring, không phân biệt hoa/thường) hoặc work item number; filter "Assigned to me" kết hợp AND với search text.
- **Dynamic query filter** (panel "Dynamic Query"): nhiều dòng điều kiện, mỗi dòng = field + operation + value; field gồm Title/Type/Priority/State/Assigned To/Remaining Work/**Labels**; operation gồm equals, not-equals, contains, not-contains, starts-with, is-empty, is-not-empty. Các dòng kết hợp theo AND/OR (toán tử chọn ở đầu mỗi dòng, tính trái→phải). Dòng chưa đủ (thiếu field hoặc value) bị bỏ qua. Filter chạy **live** trên list đã load, kết hợp AND với search text + "Assigned to me". Labels match theo any-of trên danh sách label của card.
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
