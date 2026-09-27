# Organizations (Multi-tenancy) — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-09-26 | 14:02 | Khởi tạo document | Tạo document business ban đầu cho feature Organizations (multi-tenancy) — entity `Organization` đã có trong code từ 2026-09-05 nhưng chưa được document |

---

## 1. Purpose

`Organization` là **tenant cấp cao nhất** — 1 Organization = 1 công ty mà Nexus đã bán license. Toàn bộ dữ liệu của hệ thống (User, Repository) đều thuộc về đúng 1 Organization. Đây là lớp isolation ngoài cùng: dữ liệu của tenant này không được nhìn thấy dữ liệu của tenant khác.

## 2. Key Entities & Relationships

- `Organization`:
  - `Name` — tên hiển thị của công ty.
  - `Alias` — chuỗi URL-safe, **unique toàn hệ thống**, dùng làm tenant prefix trong route của cả DASHBOARD và HUB (vd `nexus`).
  - `ContactEmail` — email liên hệ chính.
  - `About` — mô tả tuỳ chọn.
  - `LicenseKey` — key license đã activate (base64, opaque). **Unique** — 1 key chỉ bind vào tối đa 1 Organization.
  - `LicenseDueDate`, `LicenseExpireDate`, `LicenseRepoCapacity` — **snapshot** copy lại tại thời điểm activate license (không phải live reference sang hệ LICENSE).
- `User.OrgId` → Organization. **Email unique theo từng Organization**, không phải unique toàn hệ thống — 2 tenant khác nhau được phép có cùng email.
- `Repository.OrgId` → Organization. **`Code` unique theo từng Organization** — 2 tenant đều có thể có repository code `DASH`.
- Quan hệ với HUB: `OrgId`/`OrgAlias` được trả về trong login result và qua internal API để HUB dùng làm workspace context.

## 3. Business Rules & Invariants

- `Alias` unique toàn hệ thống, lowercase, URL-safe — là định danh tenant dùng trong route.
- `LicenseKey` unique — không cho 2 Organization dùng chung 1 license key.
- **Login phải kèm `OrgAlias`**: xác thực là (OrgAlias + Email + Password). Xem [auth.dod.md](auth.dod.md).
- Alias không tồn tại → trả **cùng 1 lỗi chung** với sai email/sai password để chống enumeration tenant.
- Scope uniqueness: `User.Email` unique trong phạm vi `OrgId`; `Repository.Code` unique trong phạm vi `OrgId`.
- Thông tin license (`LicenseDueDate`, `LicenseExpireDate`, `LicenseRepoCapacity`) là snapshot lúc activate — đổi license bên hệ LICENSE **không** tự sync lại vào Organization.
- `LicenseRepoCapacity` là số repository tối đa được phép dưới license này.

## 4. Main Workflows / Use Cases

1. Activate license → tạo Organization (Name, Alias, ContactEmail) + copy snapshot license (key, due date, expire date, repo capacity).
2. User đăng nhập → nhập/chọn `OrgAlias` → resolve Organization theo alias → tìm User theo (`OrgId` + Email) → phát token kèm `OrgId` trong claim.
3. HUB gọi internal API `GET /internal/v1/users/{id}/memberships` → nhận về `OrgId`, `OrgAlias`, `OrgName` kèm danh sách repository membership để dựng workspace context.
4. Tạo User / tạo Repository → luôn gán vào `OrgId` của tenant hiện tại.

## 5. Definition of Done

- [ ] Mọi truy vấn User/Repository đều filter theo `OrgId` — không có path nào leak dữ liệu cross-tenant.
- [ ] `Alias` và `LicenseKey` được enforce unique ở tầng DB (unique index), không chỉ ở application code.
- [ ] Unique check email dùng scope (`OrgId` + Email), không dùng email đơn lẻ.
- [ ] Unique check repository code dùng scope (`OrgId` + Code), không dùng code đơn lẻ.
- [ ] Login sai alias trả cùng message với sai email/password.
- [ ] JWT chứa `OrgId` để downstream request không phải resolve lại tenant.

## 6. Edge Cases & Notes

- Global Admin (`User.IsGlobalAdmin`) bypass permission **trong phạm vi Organization của mình** — không phải super-admin xuyên tenant.
- License snapshot có thể lệch so với hệ LICENSE nếu license được gia hạn/thu hồi bên đó — hiện chưa có luồng re-sync.
- Feature này giới thiệu khái niệm domain mới → xem thêm [domain-business.md](domain-business.md).
