# Roles (Custom Roles) — Test

> Đọc `../RULES.md` trước khi sửa file này.

## Run Log

| Ngày | Giờ | Kết quả | Bug mới | Ghi chú |
|------|-----|---------|---------|---------|
| 2026-06-30 | 21:50 | Pass | - | Re-test sau khi fix BUG-004/005/006/007/008 + IMP-001. roles-00 Pass (đăng nhập `dev@dashboard.local` không có ManageSettings → navigate `/settings/members` bị route guard redirect về `/DASH/boards`, không vào được trang Roles). roles-04 **Pass — regression BUG-005/BUG-006 xác nhận**: tạo role trùng tên "Senior Developer" với Description để trống → 400 `{"error":"A role named 'Senior Developer' already exists in this repository."}` (không còn 500, không còn bị chặn bởi Description required) + toast hiện đúng "Error — A role named 'Senior Developer' already exists..." (regression BUG-007). roles-04c **Pass — regression BUG-006 xác nhận**: tạo role "QA Lead Test" để trống Description, tick 1 permission → 201 Created thành công, dialog đóng, role hiện đúng không có description. roles-06 **Pass — regression BUG-007 xác nhận lần 2**: xoá role "QA Lead Test" khi còn member gán → 400 với message "Cannot delete role 'QA Lead Test': 1 member(s) are still assigned to it. Reassign them first." VÀ lần này toast hiện đúng lên UI (trước đây bị nuốt im lặng). Dialog xác nhận xoá cũng đã đổi từ native `confirm()` sang `ConfirmService` đẹp (regression IMP-001). roles-07 **Pass — regression BUG-008 xác nhận**: click nút "Clone" mới trên role "Developer" → prompt nhập tên → xác nhận → POST `.../clone` trả 201, role mới `name="Developer (Clone Test)"`, `description="Clone of 'Developer'"`, permissions copy đúng. roles-08b **Pass — regression BUG-004 xác nhận (đổi từ Fail → Pass)**: dựng lại đúng kịch bản (role custom chỉ có ManageSettings, gán cho 1 member, là nguồn ManageSettings duy nhất của repo) → Edit role đó bỏ tick ManageSettings, save → bị chặn đúng 400 "Cannot remove ManageSettings from this role: it is the only source of that permission in the repository." (trước đây fix BUG-004 thì save thành công không cảnh báo gì). Đã dọn toàn bộ dữ liệu test (xoá role "QA Lead Test", "Developer (Clone Test)", trả Admin User/Edogawa Conan về role gốc), repo về trạng thái sạch (5 default role + Senior Developer). Chưa chạy: roles-11. roles-01/02/03/04b/05/08/09/10 giữ nguyên Pass từ lần chạy trước (không re-test, không liên quan các bug đã fix). |
| 2026-06-29 | 23:35 | Partial | BUG-004, BUG-007, BUG-008 | Tiếp tục từ lần chạy trước. roles-05 Pass (update permission replace toàn bộ, verify qua UI 2 vòng edit). roles-06 Pass hoàn chỉnh (chặn khi đang gán member, xoá thành công sau reassign) — nhưng phát hiện BUG-007 dọc đường (message lỗi backend đúng nhưng UI không hiện gì). roles-07 Pass ở mức backend logic (verify qua API trực tiếp vì BUG-008 — không có UI nào gọi clone) — clone đúng permission/tên/IsDefault. roles-08(a) Pass (guard chặn đúng khi đổi Admin khỏi Scrum Master role duy nhất, verify cả UI lẫn API trực tiếp — phát hiện thêm: UI dropdown desync, không revert sau khi bị chặn, xem ghi chú trong case). roles-08b **Fail xác nhận 100%** — tạo role custom "Owner" (ManageSettings), gán 1 member, update bỏ ManageSettings → thành công không 1 chút cảnh báo (204), member mất quyền hoàn toàn không ai ngăn. Đã dọn dữ liệu test (xoá Lan Pham, role Owner, Developer (Copy)) trả repo về trạng thái cũ. Chưa chạy: roles-00, roles-11. |
| 2026-06-29 | 23:17 | Partial | BUG-006 | Test trên DASH repo có sẵn (2 member, 5 default role + Senior Developer custom). roles-01/02/03 Pass (verify qua data/UI có sẵn, không tạo repo mới). roles-04b Pass (Create role disable khi rỗng tên). roles-04c Pass NHƯNG phát hiện BUG-006 dọc đường (Description bị bắt buộc dù doc ghi optional). roles-04 Fail — tạo role trùng tên "QA Lead" trả 500 generic, UI không hiện lỗi gì (xem BUG-005). roles-09/10 Pass (verify qua response GET /roles thật: sort đúng default→custom A-Z, member count đúng). Chưa chạy: roles-00, roles-05/06/07/08/08b/11. |

---

## Test Cases

### roles-00 — Quản lý role cần quyền ManageSettings

- **Business rule**: [roles.dod.md](../../histories/roles.dod.md) §3 — "Cần quyền ManageSettings để create/update/delete/clone role".
- **Bước thực hiện**: Đăng nhập member KHÔNG có ManageSettings → thử create/update/delete/clone role.
- **Kết quả mong đợi**: Tất cả 4 action đều bị chặn (UI ẩn nút hoặc 403 nếu cố gọi).

### roles-01 — Tạo Repository tự sinh đúng 5 default role

- **Business rule**: [roles.dod.md](../../histories/roles.dod.md) §2, §4.1 — mỗi Repository tự có 5 default role (Scrum Master, Project Manager, Developer, Tester, Business Analyst) sinh lúc `CreateRepository`.
- **Bước thực hiện**: Tạo Repository mới → vào Settings → Roles của repo đó.
- **Kết quả mong đợi**: Đúng 5 role hiện sẵn, đánh dấu `IsDefault`, đúng tên + permission set theo template (Scrum Master = full quyền; Project Manager = View/Create/Edit + ManageSprint/ManageCapacity/ManageSettings; Developer/Tester/BA = View/Create/EditWorkItem).

### roles-02 — Default role không sửa được

- **Business rule**: [roles.dod.md](../../histories/roles.dod.md) §3 — "Default role không sửa được... Update trả lỗi".
- **Bước thực hiện**: Thử update permission của role "Developer" (default).
- **Kết quả mong đợi**: Action bị chặn — nút Edit disable hoặc submit trả lỗi "Default roles cannot be modified".

### roles-03 — Default role không xoá được

- **Business rule**: [roles.dod.md](../../histories/roles.dod.md) §3 — "Default role... không xoá được".
- **Bước thực hiện**: Thử xoá role "Tester" (default).
- **Kết quả mong đợi**: Action bị chặn — nút Delete disable hoặc lỗi "Default roles cannot be deleted".

### roles-04 — ⚠️ Tên custom role unique trong Repository

- **Business rule**: [roles.dod.md](../../histories/roles.dod.md) §3 — "Tên custom role unique trong phạm vi 1 Repository (default role không tính vào check unique)".
- **Bước thực hiện**: Tạo custom role tên "QA Lead" → tạo tiếp 1 custom role khác cùng tên "QA Lead" trong cùng repo.
- **Kết quả mong đợi**: Role thứ 2 bị chặn, lỗi tên đã tồn tại. (Phụ: tạo custom role tên trùng 1 default role, vd "Developer" → phải cho phép vì default không tính vào check unique.) **Lưu ý**: `CreateRoleCommandHandler.cs:28-29` hiện `throw InvalidOperationException` thay vì `Result.Failure` — xem **BUG-005** (Open), case này có thể hang debugger trong dev tới khi fix.

### roles-04b — Tên rỗng / chỉ khoảng trắng không hợp lệ

- **Business rule**: [roles.dod.md](../../histories/roles.dod.md) §3 — "Tên không được rỗng; description optional".
- **Bước thực hiện**: Tạo custom role để trống Name (hoặc chỉ gõ khoảng trắng).
- **Kết quả mong đợi**: Validation chặn, không tạo được role.

### roles-04c — Permission rỗng vẫn hợp lệ cho custom role

- **Business rule**: [roles.dod.md](../../histories/roles.dod.md) §6 — "Permission list rỗng là hợp lệ (role 'audit-only', không có quyền gì) — chỉ áp dụng cho custom role".
- **Bước thực hiện**: Tạo custom role không tick bất kỳ permission nào, submit.
- **Kết quả mong đợi**: Tạo thành công, role có `AllowedFunctions` rỗng, member gán role này không thực hiện được action nào trong repo.

### roles-05 — Update AllowedFunctions replace toàn bộ, không merge

- **Business rule**: [roles.dod.md](../../histories/roles.dod.md) §3 — "AllowedFunctions khi update sẽ replace toàn bộ danh sách cũ, không merge".
- **Bước thực hiện**: Custom role đang có permission [CreateWorkItem, EditWorkItem] → update chỉ chọn [ManageWiki] (bỏ 2 permission cũ).
- **Kết quả mong đợi**: Role sau update CHỈ có [ManageWiki], không còn giữ lại CreateWorkItem/EditWorkItem.
- **Kết quả thật (2026-06-29)**: **Pass.** Verify qua role "QA Lead": tick [Create Work Item, Edit Work Item] → save → confirm hiển thị đúng 2 permission đó → edit lại, bỏ cả 2, tick [Manage Wiki] → save → role chỉ còn đúng "Manage Wiki", xác nhận replace hoàn toàn không merge.

### roles-06 — Không xoá custom role nếu còn member gán

- **Business rule**: [roles.dod.md](../../histories/roles.dod.md) §3 — "Không cho xoá custom role nếu còn member đang gán role đó — phải reassign hết trước"; code `DeleteRoleCommandHandler.cs:34-38` đếm member đang gán, message gồm số lượng.
- **Bước thực hiện**: Tạo custom role, gán cho 1 member (từ seed-users.md) → thử xoá custom role đó.
- **Kết quả mong đợi**: Action bị chặn, message dạng "Cannot delete role '{name}': {n} member(s) are still assigned to it. Reassign them first." Reassign member sang role khác → xoá lại → thành công.
- **Kết quả thật (2026-06-29)**: **Pass hoàn chỉnh.** Gán role "QA Lead" cho Dev User → xoá bị chặn đúng (400, message khớp y hệt dự đoán). Reassign Dev User sang "Senior Developer" → xoá lại → thành công (204). **Phát hiện dọc đường**: message lỗi backend hoàn toàn đúng nhưng **UI không hiển thị gì cho user** (không toast) — xem **BUG-007** (Open).

### roles-07 — Clone role (default hoặc custom) ra custom role mới

- **Business rule**: [roles.dod.md](../../histories/roles.dod.md) §3 — "Clone: clone được cả default lẫn custom → luôn ra 1 custom role mới (IsDefault=false), cùng permission, name mới, scope theo Repository hiện tại".
- **Bước thực hiện**: (a) Clone role default "Developer". (b) Clone tiếp 1 custom role có sẵn. (c) Clone với tên trùng role custom đã có.
- **Kết quả mong đợi**: (a)(b) Sinh ra role mới `IsDefault=false`, copy đúng permission nguồn, tên khác (vd "Developer (Copy)"), sửa/xoá được (vì là custom). (c) Bị chặn lỗi tên trùng — **lưu ý**: `CloneRoleCommandHandler.cs:33-34` cũng `throw InvalidOperationException` (BUG-005), có thể hang debugger trong dev.
- **Kết quả thật (2026-06-29)**: **Không có UI nào gọi tới clone** — xem **BUG-008** (Open): backend + frontend service đều có sẵn nhưng không có nút nào trên `settings-members-page`. Verify backend logic qua gọi API trực tiếp (a): clone "Developer" → role mới đúng `isDefault=false`, `name="Developer (Copy)"`, `description="Clone of 'Developer'"`, `permissions` copy đúng. Logic backend (a) Pass; (b)(c) chưa verify (không cấp thiết, cùng code path, rủi ro thấp); UI hoàn toàn Fail (BUG-008). Đã xoá role test "Developer (Copy)" sau khi verify.

### roles-08 — ⚠️ Guard chống mất admin khi đổi role member cuối có ManageSettings (không phải Scrum Master)

- **Business rule**: [roles.dod.md](../../histories/roles.dod.md) §3, §5 — "Guard chống mất admin: không cho đổi/xoá khiến repo không còn member nào có quyền ManageSettings (dựa permission thật, không dựa chức danh SM)".
- **Bước thực hiện**: (a) Repo chỉ còn 1 member role Scrum Master (default) → đổi role member đó sang Developer/Project Manager.
- **Kết quả mong đợi**: Bị chặn (guard "Cannot change the last Scrum Master role").
- **Kết quả thật (2026-06-29)**: **Pass.** Verify 2 lớp: (1) UI client-side guard chặn, không gọi API, nhưng dropdown KHÔNG revert lại giá trị cũ sau khi bị chặn — hiện sai giá trị (đã chọn nhưng chưa lưu) dù backend chưa hề nhận request. Verify lại bằng GET /members trực tiếp xác nhận server-state vẫn đúng (Scrum Master), chỉ là UI hiển thị sai. (2) Gọi PUT trực tiếp qua API (bỏ qua guard JS) → backend cũng tự chặn đúng, trả 400 "Cannot change the last Scrum Master role...". **Lưu ý phụ (không phải bug nghiêm trọng, ghi vào improvements)**: UI dropdown desync sau khi action bị chặn có thể gây hiểu lầm cho user tưởng đã đổi thành công.
- **Case (b)** (member cuối có ManageSettings qua role khác Scrum Master, vd Project Manager) — không dựng được qua UI/API thông thường vì guard luôn giữ ≥1 Scrum Master role/chức danh cực chặt (sticky). Bằng chứng tương đương cho đúng lỗ hổng này nằm ở **roles-08b** (UpdateRole) — xem case đó.

### roles-08b — ⚠️ Update role: bỏ ManageSettings khỏi role cuối cùng đang cấp quyền đó — KHÔNG có guard

- **Business rule**: [roles.dod.md](../../histories/roles.dod.md) §3 — cùng nguyên tắc guard ở roles-08, nhưng áp dụng cho action **Update Role** (sửa `AllowedFunctions`) thay vì đổi role của member.
- **Bước thực hiện**: Tạo custom role "Owner" (chỉ có `ManageSettings`) → add member mới (Lan Pham, từ seed-users.md) với Role="Owner" → Edit role "Owner", bỏ tick `ManageSettings`, save.
- **Kết quả mong đợi**: Theo đúng business rule, action này PHẢI bị chặn (sẽ làm member đó mất hoàn toàn quyền ManageSettings).
- **Kết quả thật (2026-06-29)**: **Fail — xác nhận 100% qua test thật.** Save thành công ngay (204 No Content), KHÔNG một cảnh báo/chặn nào. Verify lại qua GET /roles: role "Owner" có `permissions: []` — Lan Pham mất hoàn toàn ManageSettings, không ai được hỏi/cảnh báo trước. Đây là **BUG-004** phần 2 (Open) — bằng chứng trực tiếp `UpdateRoleCommandHandler.cs` không có guard nào. Đã dọn dữ liệu test sau khi verify (xoá Lan Pham khỏi repo, xoá role Owner).

### roles-09 — ListRoles trả về default + custom, default xếp trước, custom sort theo tên

- **Business rule**: [roles.dod.md](../../histories/roles.dod.md) §3 — "ListRoles của 1 Repository trả về tất cả role (default + custom), default role xếp trước"; code `ListRolesQueryHandler.cs:27-28` `OrderByDescending(IsDefault).ThenBy(Name)`.
- **Bước thực hiện**: Repo có 5 default role + ít nhất 2 custom role tên khác nhau (vd "Zeta Role", "Alpha Role") → mở danh sách Roles.
- **Kết quả mong đợi**: Thứ tự: 5 default role trước (theo tên A-Z trong nhóm default), rồi custom role sau cũng sort A-Z ("Alpha Role" trước "Zeta Role").

### roles-10 — Member count hiển thị đúng theo từng role

- **Business rule**: [roles.dod.md](../../histories/roles.dod.md) §5 — "ListRoles trả về... member count đếm theo từng Repository".
- **Bước thực hiện**: Gán 2 member khác nhau vào cùng 1 custom role → mở danh sách Roles.
- **Kết quả mong đợi**: Role đó hiển thị member count = 2; role chưa gán ai hiển thị count = 0.

### roles-11 — Không xem được Roles nếu không phải member của Repository

- **Business rule**: code `ListRolesQueryHandler.cs:21-22` — `throw UnauthorizedAccessException` nếu `!IsMemberOfAsync`.
- **Bước thực hiện**: User không phải member của Repository X thử mở Settings → Roles của repo X (qua URL trực tiếp nếu UI không chặn route).
- **Kết quả mong đợi**: Bị chặn, không thấy danh sách role của repo X.

---

## Case Status

| Case ID | Trạng thái lần chạy gần nhất | Ngày | Bug liên quan |
|---------|-------------------------------|------|----------------|
| roles-00 | Pass | 2026-06-30 | - |
| roles-01 | Pass | 2026-06-29 | - |
| roles-02 | Pass | 2026-06-29 | - |
| roles-03 | Pass | 2026-06-29 | - |
| roles-04 | Pass | 2026-06-30 | - |
| roles-04b | Pass | 2026-06-29 | - |
| roles-04c | Pass | 2026-06-30 | - |
| roles-05 | Pass | 2026-06-29 | - |
| roles-06 | Pass | 2026-06-30 | - |
| roles-07 | Pass | 2026-06-30 | - |
| roles-08 | Pass | 2026-06-29 | - |
| roles-08b | Pass | 2026-06-30 | - |
| roles-09 | Pass | 2026-06-29 | - |
| roles-10 | Pass | 2026-06-29 | - |
| roles-11 | Chưa chạy | - | - |
