# Overview — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document business ban đầu cho feature Overview |

---

## 1. Purpose

Dashboard tổng hợp/analytics cho 1 Repository (có thể scope theo 1 Sprint cụ thể): đếm work item theo type/status, burndown chart, story-point tracking, velocity trend.

## 2. Key Entities & Relationships

- Dữ liệu tổng hợp từ `Sprint` (StartDate/EndDate) và `SprintTask` (Type, State, StoryPoints) — không có entity riêng, đây là read-model tính toán.

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

## 4. Main Workflows / Use Cases

1. Team lead xem dashboard: tổng item, phân bố theo type, phân bố theo status.
2. Manager xem burndown chart (ideal vs actual) để biết team có đang đúng tiến độ.
3. Team xem velocity trend để dự đoán capacity sprint tương lai.
4. Filter theo 1 sprint cụ thể để xem chi tiết.

## 5. Definition of Done

- [ ] Burndown luôn loại trừ weekend khi tính ngày làm việc.
- [ ] Actual burndown luôn dựa trên `ClosedAt`, không phải state hiện tại tại thời điểm query.
- [ ] Mọi metric đều loại trừ task đã soft-delete.
- [ ] Default sprint selection đúng thứ tự ưu tiên: sprint chứa hôm nay → sprint gần nhất → rỗng nếu không có sprint.

## 6. Edge Cases & Notes

- Tổng story points = 0 → burndown trả về toàn 0, không lỗi chia 0.
- Sprint chỉ có 1 ngày làm việc → burndown là đường suy biến (không có interpolation tuyến tính thật).
- Member chỉ cần membership (không cần quyền edit) để xem overview.
