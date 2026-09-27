# Sprints — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-09-26 | 14:02 | Link kênh chat HUB | Tạo sprint có cờ `CreateHubChannel` — tự tạo channel chat riêng bên HUB (best-effort, timeout 5s, HUB lỗi vẫn tạo sprint thành công) và lưu `SprintChannelLink`. Đóng sprint → archive channel. Chi tiết ở [hub-integration.dod.md](hub-integration.dod.md) |
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document business ban đầu cho feature Sprints |

---

## 1. Purpose

Tạo và quản lý các sprint (time-boxed iteration) với date range, capacity team và theo dõi velocity.

## 2. Key Entities & Relationships

- `Sprint`: Name, StartDate, EndDate (inclusive). `IsActive` là giá trị **tính toán** (không lưu DB) — true khi hôm nay nằm trong [StartDate, EndDate].
- Gom `CapacityMember` (năng lực team), `DayOff` (ngày nghỉ), `SprintTask` (công việc thực thi).
- `BacklogItem` có thể gán tạm `SprintId` trước khi promote chính thức thành `SprintTask`.
- `SprintChannelLink`: 1-1 với Sprint — kênh chat HUB được tạo cho sprint (`HubChannelId` + `HubChannelUrl` deep-link). Không bắt buộc: sprint có thể không có channel.

## 3. Business Rules & Invariants

- Date range của các Sprint trong cùng Repository **không được overlap**; EndDate phải sau StartDate.
- `DayOff` phải nằm trong khoảng [StartDate, EndDate] của sprint — ngoài khoảng sẽ bị reject.
- Tạo sprint mới không tự sinh CapacityMember — team phải khai báo riêng.
- `IsActive` luôn tính lại tại thời điểm query, không cache/lưu cứng.
- **Tạo kênh chat HUB là tuỳ chọn và best-effort**: chỉ provision khi cờ `CreateHubChannel` bật và HUB đã được config. Sprint luôn được commit **trước** khi gọi HUB; HUB lỗi/timeout (5s) → sprint vẫn tạo thành công, chỉ là `HubChannelId = null`. Không bao giờ để lỗi HUB làm fail việc tạo sprint.
- Đóng sprint → archive channel liên kết (best-effort, không chặn việc đóng sprint).

## 4. Main Workflows / Use Cases

1. Tạo sprint (name + date range) → validate không overlap → commit sprint → (nếu bật `CreateHubChannel`) tạo kênh chat HUB riêng cho sprint, người tạo là owner.
2. Lấy list / detail (full snapshot task + capacity + day-off) / summary (capacity vs velocity).
3. Update tên hoặc ngày sprint (re-validate overlap).
4. Khai báo capacity từng member (giờ/ngày, overtime, role) — xem [capacity.dod.md](capacity.dod.md).
5. Ghi nhận day-off (cá nhân hoặc cả team) để giảm capacity hiệu quả.
6. Tính member load (capacity − workload) để hỗ trợ planning.

## 5. Definition of Done

- [ ] Tạo/update sprint luôn validate date range không overlap với sprint khác trong cùng Repository.
- [ ] `IsActive` luôn được tính động theo ngày hiện tại, không lưu field cứng.
- [ ] DayOff luôn được validate nằm trong [StartDate, EndDate] của sprint liên quan.
- [ ] Lỗi/timeout khi tạo kênh HUB không bao giờ làm fail việc tạo sprint.
- [ ] Sprint được persist trước khi gọi sang HUB.

## 6. Edge Cases & Notes

- Tên sprint không cần unique.
- Đổi date range được cho phép cả khi sprint đang active — hệ thống cho rebase boundary.
- Xoá sprint **không cascade** xoá SprintTask/CapacityMember liên quan — các record này sẽ orphan.
- Sprint tạo trước khi có tính năng HUB channel, hoặc tạo lúc HUB đang down → không có `SprintChannelLink`; UI phải xử lý `HubChannelId = null`. Hiện **chưa có** luồng retry/tạo lại channel sau.
