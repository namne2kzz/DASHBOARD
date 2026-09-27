# Capacity — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-09-26 | 17:05 | Fire-and-forget HUB thực sự không được fail thao tác | Sync member sang kênh HUB dùng `_ = hub.XxxAsync(...)` — cách này chỉ nuốt được **faulted Task**, không nuốt được exception ném **đồng bộ** trước khi Task được tạo (HttpClient đã dispose, argument check…). Khi đó capacity **đã commit** nhưng client vẫn nhận 500 → user tưởng thất bại dù thao tác đã thành công. Bọc `try/catch` quanh cả lời gọi ở `UpsertCapacityMember` và `RemoveCapacityMember` |
| 2026-09-26 | 16:20 | DayOff cả team cap theo giờ làm của member | Làm rõ rule: DayOff `UserId = null` trừ **tối đa bằng số giờ member thực làm trong ngày** (`HoursPerDay + OvertimeHoursPerDay`), không trừ nguyên số giờ khai báo. Member 4h/ngày gặp ngày nghỉ chung 8h chỉ bị trừ 4h. Sửa `GetSprintSummary` cho khớp `GetSprintDetail` (trước đó 2 màn hình ra số khác nhau). Capacity cũng clamp ≥ 0 **theo từng member**, không clamp trên tổng |
| 2026-09-26 | 14:02 | Sync member sang kênh chat HUB | Thêm/xoá `CapacityMember` của sprint có kênh HUB → tự add/remove user khỏi kênh đó (fire-and-forget). Chi tiết ở [hub-integration.dod.md](hub-integration.dod.md) |
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document business ban đầu cho feature Capacity |

---

## 1. Purpose

Mô hình hoá năng lực team theo sprint (giờ làm khả dụng mỗi member) và ngày nghỉ, hỗ trợ planning sprint sát thực tế.

## 2. Key Entities & Relationships

- `CapacityMember`: gán theo (`SprintId`, `UserId`), `HoursPerDay` (0–12), `OvertimeHoursPerDay` (0–6), `Role` — **chức danh dạng string** lấy từ value `RepoRole` metadata (enum `TeamRole` đã bị xoá khỏi code), chỉ để phân loại, không giới hạn việc gán task. Xem [metadata.dod.md](metadata.dod.md).
- `DayOff`: ngày nghỉ cụ thể, số giờ trừ, lý do (vd "Nghỉ lễ", "Nghỉ bệnh"); có thể gán riêng 1 user hoặc cả team (`UserId = null`).
- Upsert semantics: tạo CapacityMember với (SprintId, UserId) đã tồn tại sẽ **update** chứ không tạo trùng.

## 3. Business Rules & Invariants

- Ngày DayOff phải nằm trong [StartDate, EndDate] của Sprint liên quan, ngoài khoảng → reject với lỗi rõ nghĩa.
- Upsert CapacityMember yêu cầu: sprint tồn tại, user là member của Repository, caller có quyền `ManageCapacity`.
- Giờ DayOff trừ vào capacity member (nếu có UserId) hoặc capacity cả team (nếu UserId null); không giới hạn range giờ.
- **DayOff cả team trừ tối đa bằng số giờ member thực làm trong 1 ngày** (`HoursPerDay + OvertimeHoursPerDay`), không trừ nguyên số giờ khai báo. Ví dụ: ngày nghỉ chung khai 8h, member part-time 4h/ngày chỉ bị trừ 4h — không thể "nghỉ" nhiều hơn số giờ đáng lẽ làm. DayOff cá nhân thì trừ đúng số giờ khai.
- **Capacity không bao giờ âm, clamp theo từng member** (`max(0, …)` áp cho mỗi người rồi mới cộng tổng) — một người nghỉ vượt quá capacity của họ không được ăn lẹm vào capacity người khác.
- Capacity chủ yếu phục vụ mục đích hiển thị planning (`MemberLoad = Capacity − Workload`) — **không tự động chặn** việc gán task khi overload.
- **Capacity member = thành viên kênh chat sprint**: sprint có `SprintChannelLink` thì thêm CapacityMember sẽ add user vào kênh HUB, xoá CapacityMember sẽ remove khỏi kênh. Đây là **fire-and-forget** — không chờ kết quả, HUB lỗi không làm fail thao tác capacity. Sprint không có channel → không sync gì. Lỗi từ HUB client phải được nuốt **cả khi ném đồng bộ** (bọc `try/catch`), không chỉ dựa vào việc bỏ Task — thao tác capacity đã commit trước đó nên không được phép trả lỗi về client.

## 4. Main Workflows / Use Cases

1. Lấy snapshot capacity của sprint (CapacityMember, DayOff, MemberLoad tính sẵn).
2. Add/update capacity member (upsert theo Sprint+User) → nếu sprint có kênh HUB, add user vào kênh đó.
3. Ghi nhận DayOff (cá nhân hoặc cả team).
4. Xoá capacity member (→ remove khỏi kênh HUB nếu có) hoặc DayOff.
5. Tính MemberLoad = `max(0, (HoursPerDay + OvertimeHoursPerDay) × số ngày làm − DayOff cá nhân − Σ min(DayOff team, HoursPerDay + OvertimeHoursPerDay))`; Workload từ giờ task được gán. Công thức này dùng chung cho cả màn hình Sprint Summary lẫn Sprint Detail.
6. Tính load percent (Workload/Capacity × 100) để cảnh báo over/under-allocation.

## 5. Definition of Done

- [ ] DayOff luôn được validate nằm trong khoảng ngày của Sprint trước khi lưu.
- [ ] Upsert CapacityMember không tạo duplicate khi (SprintId, UserId) đã tồn tại.
- [ ] MemberLoad luôn tính lại tại thời điểm đọc (không cache cứng), phản ánh đúng workload hiện tại.
- [ ] Lỗi sync sang HUB không bao giờ làm fail thao tác thêm/xoá capacity member.

## 6. Edge Cases & Notes

- DayOff cả team (`UserId = null`) được trả về riêng, không tự gộp vào DayOff cá nhân.
- Xoá CapacityMember không cascade — task đã assign vẫn giữ `AssignedToId` (orphan reference).
- Overtime hours là optional, coi như budget capacity bổ sung, không tách riêng rule enforce.
- Sync sang HUB là fire-and-forget → nếu HUB từ chối, thành viên kênh chat lệch so với capacity member mà **không có thông báo lỗi** cho user. Hiện chưa có cơ chế reconcile.
