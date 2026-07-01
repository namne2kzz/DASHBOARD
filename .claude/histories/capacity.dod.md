# Capacity — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document business ban đầu cho feature Capacity |

---

## 1. Purpose

Mô hình hoá năng lực team theo sprint (giờ làm khả dụng mỗi member) và ngày nghỉ, hỗ trợ planning sprint sát thực tế.

## 2. Key Entities & Relationships

- `CapacityMember`: gán theo (`SprintId`, `UserId`), `HoursPerDay` (0–12), `OvertimeHoursPerDay` (0–6), `TeamRole` (Dev/Tester/ScrumMaster/ProjectManager — chỉ phân loại, không giới hạn việc gán task).
- `DayOff`: ngày nghỉ cụ thể, số giờ trừ, lý do (vd "Nghỉ lễ", "Nghỉ bệnh"); có thể gán riêng 1 user hoặc cả team (`UserId = null`).
- Upsert semantics: tạo CapacityMember với (SprintId, UserId) đã tồn tại sẽ **update** chứ không tạo trùng.

## 3. Business Rules & Invariants

- Ngày DayOff phải nằm trong [StartDate, EndDate] của Sprint liên quan, ngoài khoảng → reject với lỗi rõ nghĩa.
- Upsert CapacityMember yêu cầu: sprint tồn tại, user là member của Repository, caller có quyền `ManageCapacity`.
- Giờ DayOff trừ vào capacity member (nếu có UserId) hoặc capacity cả team (nếu UserId null); không giới hạn range giờ.
- Capacity chủ yếu phục vụ mục đích hiển thị planning (`MemberLoad = Capacity − Workload`) — **không tự động chặn** việc gán task khi overload.

## 4. Main Workflows / Use Cases

1. Lấy snapshot capacity của sprint (CapacityMember, DayOff, MemberLoad tính sẵn).
2. Add/update capacity member (upsert theo Sprint+User).
3. Ghi nhận DayOff (cá nhân hoặc cả team).
4. Xoá capacity member hoặc DayOff.
5. Tính MemberLoad = (HoursPerDay × số ngày làm) + (OvertimeHoursPerDay × số ngày làm) − DayOffHours; Workload từ giờ task được gán.
6. Tính load percent (Workload/Capacity × 100) để cảnh báo over/under-allocation.

## 5. Definition of Done

- [ ] DayOff luôn được validate nằm trong khoảng ngày của Sprint trước khi lưu.
- [ ] Upsert CapacityMember không tạo duplicate khi (SprintId, UserId) đã tồn tại.
- [ ] MemberLoad luôn tính lại tại thời điểm đọc (không cache cứng), phản ánh đúng workload hiện tại.

## 6. Edge Cases & Notes

- DayOff cả team (`UserId = null`) được trả về riêng, không tự gộp vào DayOff cá nhân.
- Xoá CapacityMember không cascade — task đã assign vẫn giữ `AssignedToId` (orphan reference).
- Overtime hours là optional, coi như budget capacity bổ sung, không tách riêng rule enforce.
