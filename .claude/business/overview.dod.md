# Overview — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-10-28 | — | Dải "Cần chú ý" đổi sang dạng cảnh báo | Dải này trước nằm trên `nx-card` trắng giống mọi panel khác nên đọc như nội dung thường, không ra cảnh báo. Nay là **khối duy nhất trên trang không phải card**: nền nhuốm màu + viền trái 4px. **Tông lấy theo mức nặng nhất đang có**, không mặc định đỏ: toàn `Warning` → vàng, tiêu đề "Worth a look"; có ≥1 `Critical` → đỏ, tiêu đề "Needs attention". Lý do: thẻ kẹt lâu và vi phạm WIP hard-stop không cùng mức độ; banner lúc nào cũng đỏ thì người đọc ngừng để ý, giữ đỏ cho vi phạm thật để khi trang đỏ là nó có nghĩa. Chip bên trong chuyển nền đặc (`--surface`) vì nền tint cũ sẽ chìm vào dải cùng màu — mức độ vẫn đọc được qua màu chữ. Quy tắc nghiệp vụ (3 loại item, tối đa 5, `Critical` trước) **không đổi**. |
| 2026-10-27 | 23:19 | Thêm dải "Cần chú ý" + so sánh sprint trước | Overview trả thêm 2 khối: (1) **Attention** — tối đa 5 vấn đề đang cần xử lý, mỗi item có sẵn câu mô tả và route để bấm tới chỗ sửa: cột vượt WIP (`Critical` nếu hard-stop, `Warning` nếu soft), tối đa 2 người quá tải (> 20 pts), và **1** thẻ kẹt lâu nhất (quá `AgingLimitDays` của cột). (2) **Comparison** — số liệu sprint liền trước (theo StartDate, không theo thứ tự list) để mỗi chỉ số hiện mũi tên tăng/giảm; `HasPrevious=false` ở sprint đầu tiên thì ẩn hết delta. `SprintRefDto` thêm StartDate/EndDate để header hiện khoảng ngày. |
| 2026-10-17 | 11:45 | Thêm Sprint Report card | Bổ sung report card cho sprint đang chọn: task completion rate (done/total), point completion rate (completed/committed), scope change (số item + story point được **tạo sau ngày StartDate** = scope thêm giữa sprint, xấp xỉ vì item move vào sprint vẫn giữ CreatedAt gốc) |
| 2026-07-05 | 13:41 | Thêm health/cycle-time/workload/activity | Bổ sung sprint health indicator, cycle time xấp xỉ, workload theo assignee, recent activity feed, và type-trend 7 sprint vào overview |
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document business ban đầu cho feature Overview |

---

## 1. Purpose

Dashboard tổng hợp/analytics cho 1 Repository (có thể scope theo 1 Sprint cụ thể): đếm work item theo type/status, burndown chart, story-point tracking, velocity trend, sprint health indicator, cycle time xấp xỉ, workload theo assignee, work item trend theo type qua nhiều sprint, và recent activity feed.

## 2. Key Entities & Relationships

- Dữ liệu tổng hợp từ `Sprint` (StartDate/EndDate), `SprintTask` (Type, State, StoryPoints, AssignedToId, ClosedAt, CreatedAt) và `HistoryEntry` (audit-trail entity có sẵn của feature History, denormalized `RepositoryId`) — không có entity/bảng riêng cho Overview, đây hoàn toàn là read-model tính toán tại query time.

## 3. Business Rules & Invariants

- Chỉ member của Repository được xem overview (chỉ cần membership, không cần permission edit).
- Burndown chỉ tính theo ngày làm việc (Mon–Fri) của sprint — bỏ qua weekend và ngày tương lai.
- Ideal burndown: đường thẳng giảm tuyến tính từ tổng story points committed về 0 qua các ngày làm việc.
- Actual burndown: dựa vào `ClosedAt` của task — task chuyển Done mới được tính hoàn thành.
- Burndown dừng tích lũy tại ngày hôm nay (không project tương lai).
- Nếu không chỉ định SprintId: ưu tiên sprint chứa ngày hôm nay; nếu không có thì lấy sprint gần nhất.
- Repository chưa có sprint nào → trả về collection rỗng.
- Velocity trend lấy 5 sprint gần nhất (committed vs completed story points).
- Task soft-delete (`IsDeleted = true`) bị loại khỏi mọi count/metric.
- **Type trend**: lấy sprint đang chọn + tối đa 6 sprint liền trước (theo `StartDate`), sắp xếp lại theo thời gian tăng dần, đếm số item theo từng `SprintTaskType` cho mỗi sprint trong window.
- **Sprint health**: so sánh `actual` remaining points với `ideal` remaining points tại ngày hôm nay (lấy từ burndown data) — `actual ≤ ideal × 1.05` → `OnTrack`; `actual ≤ ideal × 1.25` → `AtRisk`; còn lại → `Behind`. `DaysRemaining` = số ngày làm việc còn lại tính từ hôm nay đến `EndDate` của sprint.
- **Cycle time**: xấp xỉ bằng trung bình `(ClosedAt - CreatedAt)` (đơn vị ngày) trên các task đã Done trong sprint đang chọn. Đây **không phải** cycle time thật (thời gian Active → Done) vì hệ thống chưa lưu lịch sử chuyển trạng thái có cấu trúc (chỉ có `HistoryEntry.Message` dạng free-text) — cần nêu rõ đây là chỉ số xấp xỉ khi hiển thị.
- **Workload theo assignee**: group task trong sprint đang chọn theo `AssignedToId`; task chưa gán (`AssignedToId == null`) gộp vào nhóm "Unassigned".
- **Recent activity**: lấy tối đa 8 `HistoryEntry` mới nhất theo `RepositoryId` (toàn bộ repository, không giới hạn theo 1 task) — tái sử dụng entity/pattern có sẵn của feature History, không tạo bảng mới.
- **Attention (Cần chú ý)**: tối đa 5 item, sắp xếp `Critical` trước. Ba loại:
  - `WipExceeded` — cột có `WipLimit > 0` và số item vượt limit. Severity = `Critical` nếu `WipMode = Hard` (chặn thật), `Warning` nếu Soft (chỉ cảnh báo). `WipLimit = 0` nghĩa là không giới hạn → bỏ qua.
  - `Overloaded` — tối đa 2 người có `StoryPoints > 20` trong sprint, nhiều nhất trước. Ngưỡng **cố định 20 pts**, cố ý không đọc capacity thật: màn Capacity đã mô hình hóa ngày nghỉ/part-time, dải này chỉ cần chỉ ra chỗ lệch rõ ràng chứ không được nói khác màn kia.
  - `StaleItem` — **chỉ 1** item kẹt lâu nhất (chưa Done, `StateChangedAt` quá `AgingLimitDays` của cột tương ứng). Liệt kê 30 thẻ cũ là backlog chứ không còn là cảnh báo.
  - Mỗi item mang sẵn `Route` dạng route tương đối (`boards`, `sprint-planning`) + `TargetId` — server không biết client đánh URL thế nào.
  - Không có vấn đề nào → trả mảng rỗng, UI ẩn hẳn dải. Chính sự xuất hiện của dải là tín hiệu.
- **Comparison (so sánh sprint trước)**: "sprint trước" xác định theo `StartDate` nhỏ hơn sprint đang chọn và gần nhất — **không** theo vị trí trong list, để vẫn đúng khi người dùng chọn 1 sprint cũ bất kỳ. Trả `CompletedPoints`, `CycleTimeDays`, `TotalItems`, `DoneItems`. Sprint đầu tiên của repository → `HasPrevious = false`, UI ẩn toàn bộ mũi tên delta (không hiện "0" giả).
- **Hướng tốt/xấu của delta không giống nhau giữa các chỉ số**: item xong nhiều hơn là tốt, cycle time dài hơn là xấu — UI phải đảo màu cho chỉ số kiểu "càng thấp càng tốt", và luôn kèm mũi tên chứ không chỉ dùng màu (người không phân biệt được đỏ/xanh vẫn phải đọc được).

## 4. Main Workflows / Use Cases

1. Team lead xem dashboard: tổng item, phân bố theo type, phân bố theo status, sprint health badge.
2. Manager xem burndown chart (ideal vs actual) để biết team có đang đúng tiến độ.
3. Team xem velocity trend để dự đoán capacity sprint tương lai.
4. Team xem work item trend theo type qua 7 sprint (sprint hiện tại + 6 sprint trước) để thấy xu hướng tăng/giảm của từng loại work item (Bug tăng bất thường, User Story giảm dần, v.v).
5. Manager xem workload theo assignee để phát hiện thành viên đang quá tải trong sprint hiện tại.
6. Bất kỳ member nào xem recent activity feed để nắm nhanh các thay đổi gần đây trong repository mà không cần vào từng task.
7. Filter theo 1 sprint cụ thể để xem chi tiết toàn bộ các chỉ số trên.
8. Mở Overview thấy ngay dải "Cần chú ý", bấm 1 chip để nhảy thẳng tới màn xử lý (Board cho WIP/thẻ kẹt, Sprint cho người quá tải).
9. Đọc mũi tên bên dưới mỗi con số để biết sprint này tốt hơn hay xấu hơn sprint trước, thay vì phải tự nhớ số cũ.

## 5. Definition of Done

- [ ] Burndown luôn loại trừ weekend khi tính ngày làm việc.
- [ ] Actual burndown luôn dựa trên `ClosedAt`, không phải state hiện tại tại thời điểm query.
- [ ] Mọi metric đều loại trừ task đã soft-delete.
- [ ] Default sprint selection đúng thứ tự ưu tiên: sprint chứa hôm nay → sprint gần nhất → rỗng nếu không có sprint.
- [ ] Type trend window đúng: sprint đang chọn + tối đa 6 sprint liền trước, không bao gồm sprint sau sprint đang chọn.
- [ ] Sprint health status tính đúng ngưỡng (1.05 / 1.25) dựa trên burndown data đã tính sẵn, không tính lại từ đầu.
- [ ] Cycle time hiển thị kèm ghi chú/label cho biết đây là số liệu xấp xỉ, và sample count để người xem biết độ tin cậy.
- [ ] Recent activity chỉ lấy `HistoryEntry` theo đúng `RepositoryId` đang xem (không lộ dữ liệu repository khác).

## 6. Edge Cases & Notes

- Tổng story points = 0 → burndown trả về toàn 0, không lỗi chia 0.
- Sprint chỉ có 1 ngày làm việc → burndown là đường suy biến (không có interpolation tuyến tính thật).
- Member chỉ cần membership (không cần quyền edit) để xem overview.
- Repository có ít hơn 7 sprint → type trend chỉ trả về đúng số sprint hiện có (không pad rỗng).
- Sprint đang chọn chưa có task Done nào → cycle time trả về `AverageDays = 0, SampleCount = 0` (không lỗi chia 0); UI cần hiển thị trạng thái "chưa có dữ liệu" thay vì "0 ngày".
- Repository chưa từng có `HistoryEntry` nào → recent activity trả về danh sách rỗng.
