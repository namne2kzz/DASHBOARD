---
name: business-doc
description: Use when creating, updating, or auditing a .claude/business/{feature}.dod.md — scans Application/{Feature} code (handlers, validators, entities, authorization) to extract real business rules. Also use to find features that have code but no doc, or docs that drifted from code.
---

# Skill: Business Document từ code

Quét code để viết/cập nhật `.claude/business/{feature}.dod.md`. **Rút rule từ code thật, không đoán.**

## Usage
```
/business-doc [audit|scan|drift] [feature]
```

| Lệnh | Việc |
|---|---|
| `audit` | So `Application/*/` với `.claude/business/*.dod.md` → feature nào thiếu doc, doc nào mồ côi |
| `scan {Feature}` | Đọc code của feature → sinh doc mới hoặc cập nhật doc sẵn có |
| `drift {Feature}` | So doc hiện tại với code hiện tại → chỉ ra chỗ doc nói sai |

---

## Bắt buộc đọc trước

1. **`.claude/business/RULES.md`** — luật ghi doc. Không được làm trái.
2. File `.dod.md` đang có của feature đó (nếu có) — để biết giọng văn và cái gì đã ghi.
3. `.claude/business/domain-business.md` — khái niệm domain chung, đừng định nghĩa lại.

Tóm tắt luật quan trọng nhất của RULES.md (vẫn phải đọc bản đầy đủ):

- Phần A **Update Log**: thêm dòng mới lên **đầu bảng**, không bao giờ xóa dòng cũ. Ngày/giờ lấy **giờ hệ thống thật**, không bịa.
- Phần B **Business Doc**: luôn là bản LATEST, **ghi đè** nội dung cũ, không giữ song song nhiều version.
- 6 mục cố định đúng thứ tự: Purpose · Key Entities & Relationships · Business Rules & Invariants · Main Workflows / Use Cases · Definition of Done · Edge Cases & Notes.
- Refactor kỹ thuật thuần, không đổi business → **không** update.
- Tiếng Việt. Người đọc là PM/BA/dev mới, không đọc code.

---

## Rút business rule từ đâu

Đây là phần cốt lõi. Business rule nằm ở những chỗ cụ thể, không rải đều:

| Nguồn | Rút ra cái gì |
|---|---|
| `{Feature}/Commands/*/\*CommandValidator.cs` | Rule validation: bắt buộc, độ dài, định dạng, khoảng giá trị |
| `{Feature}/Commands/*/\*CommandHandler.cs` | Luồng nghiệp vụ, thứ tự bước, điều kiện rẽ nhánh |
| `throw new ...Exception(...)` trong handler | **Invariant** — điều kiện không được vi phạm. Đọc message để biết rule |
| `Domain/Entities/*.cs` — method, guard clause | Invariant cấp entity, state transition hợp lệ |
| `Domain/Enums/*.cs` | Tập trạng thái hợp lệ; ghi **cả tên lẫn số** (`Done` = 5) |
| Domain event (`AddDomainEvent`) | Tác dụng phụ nghiệp vụ: gửi mail, tạo notification, cập nhật feature khác |
| `[Authorize]`, policy, check role trong handler | Ai được làm gì — mục Business Rules |
| `{Feature}/Queries/*Handler.cs` — mệnh đề `Where` | **Rule hiển thị**: cái gì bị lọc ra và vì sao (`!IsArchived`, `State != Done`) |
| `OrderBy` trong query | Thứ tự mặc định — người dùng thấy được, nên là business |
| `Infrastructure/Persistence/Configurations/*.cs` | Ràng buộc DB: unique, required, cascade delete, độ dài |
| `Controllers/{Feature}/` | Route thật, dùng cho mục Workflows |
| Frontend `pages/{feature}-page/` | Luồng người dùng thấy, empty state, thông báo |

**Luật rút rule:**

- Mọi rule phải **truy được về code**. Không chắc thì không ghi, hoặc ghi `[cần xác nhận]` rồi hỏi.
- Viết kèm **tên field/enum thật** như các doc hiện có: `AssignedToId`, `IsArchived`, `State = 5`. Người đọc không đọc code nhưng dev sẽ đối chiếu được.
- Giải thích **tại sao** khi lý do không hiển nhiên, không chỉ mô tả cái gì.
- Phân biệt rule nghiệp vụ với chi tiết kỹ thuật. "Dùng MediatR pipeline" không phải business. "Chỉ Owner mới xóa được repository" là business.

---

## `audit` — tìm chỗ lệch

```bash
# feature co code
ls DASHBOARD/Application/ | grep -vE '^(Common|Contracts|DependencyInjection.cs)$'
# doc dang co
ls .claude/business/*.dod.md
```

So hai danh sách. Lưu ý **tên không map 1-1**, RULES.md mục 2 đã ghi các ngoại lệ:

- `Application/SmartBoard/` ↔ `workflow.dod.md`
- `Application/Orgs/` ↔ `organizations.dod.md`
- `user-settings`, `hub-integration`, `my-work`, `overview`, `search`: cross-cutting, không có folder `Application/` riêng
- `wiki.dod.md`: DEPRECATED, đã chuyển sang HUB — không phải lỗi

Báo cáo ra 3 nhóm: **thiếu doc** (có code, chưa có `.dod.md`) · **doc mồ côi** (có doc, code đã xóa) · **map đặc biệt** (đã biết, bỏ qua).

## `scan {Feature}` — sinh hoặc cập nhật

1. Liệt kê file của `Application/{Feature}/` — Commands, Queries, DTOs.
2. Đọc **toàn bộ** handler và validator. Không lướt.
3. Tìm entity liên quan trong `Domain/Entities/`, đọc guard clause và method.
4. Xem `Controllers/{Feature}/` lấy route.
5. Xem frontend tương ứng nếu có — lấy empty state, thông báo lỗi người dùng thấy.
6. Viết 6 mục theo RULES.md.
7. Thêm dòng Update Log ở **đầu bảng**, giờ lấy bằng lệnh thật:
   ```bash
   date +"%Y-%m-%d %H:%M"
   ```
8. File mới → Update Log dòng đầu: title `Khởi tạo document`, mô tả `Tạo document business ban đầu cho feature {Tên}`. Và theo RULES mục 5: nếu có entity/khái niệm domain mới thì thêm vào `domain-business.md`, đồng thời thêm file mới vào danh sách ở mục 2 của `RULES.md`.

## `drift {Feature}` — doc có còn đúng không

Đọc doc, đọc code, đối chiếu từng rule ở mục 3 và 4. Báo cáo:

| Loại | Nghĩa |
|---|---|
| **Sai** | Doc ghi rule mà code không còn như vậy |
| **Thiếu** | Code có rule mà doc chưa ghi |
| **Thừa** | Doc mô tả thứ đã bị xóa |

Chỉ báo cáo, **không tự sửa** — trừ khi user bảo sửa. Mỗi phát hiện kèm `file:line` để user kiểm chứng.

---

## Cờ đỏ

- Ghi rule mà không chỉ được nó nằm ở đâu trong code
- Chép mô tả kỹ thuật (tên class, pattern, DI) vào doc nghiệp vụ
- Xóa hoặc sửa dòng Update Log cũ
- Bịa ngày giờ thay vì lấy giờ hệ thống
- Thêm version mới vào phần B thay vì ghi đè
- Viết doc cho một refactor không đổi business
- Doc dài nhưng không có rule nào cụ thể — toàn câu chung chung

## Sau khi code xong một feature

Đây là bước bị quên nhiều nhất. `CLAUDE.md` yêu cầu: thêm/sửa business logic xong **phải** update `.dod.md` tương ứng. Nếu thay đổi chạm domain tổng thể → update thêm `domain-business.md`.

Dùng `drift` để kiểm tra trước khi coi là xong.
