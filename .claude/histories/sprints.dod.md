# Sprints — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document business ban đầu cho feature Sprints |

---

## 1. Purpose

Tạo và quản lý các sprint (time-boxed iteration) với date range, capacity team và theo dõi velocity.

## 2. Key Entities & Relationships

- `Sprint`: Name, StartDate, EndDate (inclusive). `IsActive` là giá trị **tính toán** (không lưu DB) — true khi hôm nay nằm trong [StartDate, EndDate].
- Gom `CapacityMember` (năng lực team), `DayOff` (ngày nghỉ), `SprintTask` (công việc thực thi).
- `BacklogItem` có thể gán tạm `SprintId` trước khi promote chính thức thành `SprintTask`.

## 3. Business Rules & Invariants

- Date range của các Sprint trong cùng Repository **không được overlap**; EndDate phải sau StartDate.
- `DayOff` phải nằm trong khoảng [StartDate, EndDate] của sprint — ngoài khoảng sẽ bị reject.
- Tạo sprint mới không tự sinh CapacityMember — team phải khai báo riêng.
- `IsActive` luôn tính lại tại thời điểm query, không cache/lưu cứng.

## 4. Main Workflows / Use Cases

1. Tạo sprint (name + date range) → validate không overlap.
2. Lấy list / detail (full snapshot task + capacity + day-off) / summary (capacity vs velocity).
3. Update tên hoặc ngày sprint (re-validate overlap).
4. Khai báo capacity từng member (giờ/ngày, overtime, role) — xem [capacity.dod.md](capacity.dod.md).
5. Ghi nhận day-off (cá nhân hoặc cả team) để giảm capacity hiệu quả.
6. Tính member load (capacity − workload) để hỗ trợ planning.

## 5. Definition of Done

- [ ] Tạo/update sprint luôn validate date range không overlap với sprint khác trong cùng Repository.
- [ ] `IsActive` luôn được tính động theo ngày hiện tại, không lưu field cứng.
- [ ] DayOff luôn được validate nằm trong [StartDate, EndDate] của sprint liên quan.

## 6. Edge Cases & Notes

- Tên sprint không cần unique.
- Đổi date range được cho phép cả khi sprint đang active — hệ thống cho rebase boundary.
- Xoá sprint **không cascade** xoá SprintTask/CapacityMember liên quan — các record này sẽ orphan.
