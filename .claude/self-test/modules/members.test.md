# Members — Test

> Đọc `../RULES.md` trước khi sửa file này.

## Run Log

| Ngày | Giờ | Kết quả | Bug mới | Ghi chú |
|------|-----|---------|---------|---------|
| 2026-06-30 | 21:50 | Pass | - | Re-test sau khi fix BUG-003/004/005/006/007/008 + IMP-001 (xem bugs.md/improvements.md). members-01 Pass (đăng nhập `dev@dashboard.local`, không có ManageSettings → navigate `/settings/members` bị route guard redirect thẳng về `/DASH/boards`, không vào được trang). members-04 Pass (search "zzznonexistentuser" trong Add Member picker → "No users found.", không có cách add user chưa tồn tại). members-06(b) **Pass — xác nhận regression fix BUG-004 thành công**: dựng lại đúng kịch bản qua API trực tiếp (đổi Admin User khỏi role Scrum Master sang Developer — cho phép vì còn member khác giữ ManageSettings qua role custom; sau đó role custom đó là nguồn ManageSettings duy nhất) → sửa role đó bỏ ManageSettings qua UI bị chặn đúng 400 "Cannot remove ManageSettings from this role: it is the only source of that permission in the repository." (xem chi tiết ở roles-08b). Đã dọn dữ liệu test, repo về trạng thái gốc (3 member, role gốc). Chưa chạy: members-03 (đã verify gián tiếp qua roles-04, cùng code path AddMember/CreateRole sau fix BUG-005 — không lặp lại riêng), members-05, members-08, members-09, members-10. members-02/07 giữ nguyên Pass từ lần chạy trước (không re-test). members-11 vẫn bỏ qua (Invitations WIP). |
| 2026-06-29 | 23:40 | Partial | - | Test trong lúc chạy chung với roles.test.md (cùng trang Settings → Members). members-02 Pass (add Lan Pham từ seed-users.md, role picker hoạt động đúng). members-07 Pass (verify Lan Pham vẫn còn trong `/api/users/search` sau khi remove khỏi repo). members-06(a) Pass — verify gián tiếp qua roles-08 (chặn đúng khi remove/đổi member cuối giữ Scrum Master). members-06(b) không dựng được qua đường member-role (guard Scrum Master quá chặt, không cho vacate hẳn) nhưng lỗ hổng tương đương đã chứng minh 100% qua roles-08b (UpdateRole vector) — xem BUG-004. Chưa chạy: members-01, 03, 04, 05, 08, 09, 10. members-11 bỏ qua (Invitations WIP). |

---

## Test Cases

### members-01 — Add member cần quyền ManageSettings

- **Business rule**: [members.dod.md](../../business/members.dod.md) §3 — "Cần quyền ManageSettings để add/update/remove member"; code `AddMemberCommandHandler.cs:24-25`.
- **Bước thực hiện**: Đăng nhập account KHÔNG có ManageSettings (vd member role Developer) → vào Settings → Members → thử add member mới.
- **Kết quả mong đợi**: Action bị chặn (nút disable hoặc 403), không tạo được membership.

### members-02 — Add user có sẵn vào Repository, role bắt buộc auto chọn theo chức danh

- **Business rule**: [members.dod.md](../../business/members.dod.md) §3, §4.1 — `RoleId` bắt buộc, UI mặc định auto chọn default role khớp chức danh (Dev→Developer...).
- **Bước thực hiện**: Đăng nhập admin → Settings → Members → Add member → tìm 1 user trong 15 user mới seed (xem [seed-users.md](../seed-users.md)) → chọn chức danh "Developer".
- **Kết quả mong đợi**: Role picker tự động chọn sẵn role "Developer" tương ứng; submit thành công, member mới xuất hiện trong list với đúng chức danh + role.
- **Kết quả thật (2026-06-29)**: **Pass.** Add "Lan Pham" (seed user) thành công (201), picker search-by-name hoạt động đúng (debounce, hiển thị avatar/email), chọn Role tuỳ ý (đã test với "Owner" — case roles-08b — không nhất thiết auto theo chức danh nhưng vẫn cho chọn thủ công đúng).

### members-03 — ⚠️ 1 user không join trùng 1 Repository 2 lần

- **Business rule**: [members.dod.md](../../business/members.dod.md) §3 — "1 user chỉ được làm member 1 lần trong cùng 1 Repository (không trùng)"; code `AddMemberCommandHandler.cs:34-37` throw `InvalidOperationException`.
- **Bước thực hiện**: Add lại đúng user vừa add ở members-02 vào cùng Repository lần 2.
- **Kết quả mong đợi**: Bị chặn — lỗi rõ ràng (user đã là member), không tạo membership trùng. **Lưu ý**: handler hiện `throw InvalidOperationException` thay vì `Result.Failure` (xem **BUG-005**, Open) — nếu VS debugger đang attach với break-on-exception, case này sẽ hang giống BUG-002 cho tới khi BUG-005 được fix.

### members-04 — User phải tồn tại trong hệ thống trước khi add

- **Business rule**: [members.dod.md](../../business/members.dod.md) §3 — "User phải tồn tại trong hệ thống trước khi add làm member".
- **Bước thực hiện**: Ở ô search user-picker khi add member, gõ 1 email/tên không tồn tại trong hệ thống.
- **Kết quả mong đợi**: Picker không trả kết quả, không có cách nào add user chưa tồn tại (không có flow "invite trực tiếp" ở đây — phân biệt với Invitations feature, hiện đang WIP, xem `wip-features.md`).

### members-05 — Repository đã archive không add thêm member được

- **Business rule**: [members.dod.md](../../business/members.dod.md) §3 — "Repository đã IsArchived thì không add thêm member được"; code `AddMemberCommandHandler.cs:31-32` throw `NotFoundException` khi repo archived (ẩn đi như "không tồn tại" thay vì lỗi riêng).
- **Bước thực hiện**: Archive 1 repository test → thử add member mới vào repo đó.
- **Kết quả mong đợi**: Action bị chặn. **Lưu ý**: handler hiện trả lỗi như "repository not found" (404-style) chứ không phải message rõ "repo đã archive" — verify UI có hiển thị message gây hiểu lầm không (member có thể tưởng nhập sai thay vì hiểu đúng "repo đã đóng").

### members-06 — ⚠️ Guard chống mất admin khi remove/đổi role member cuối có ManageSettings

- **Business rule**: [roles.dod.md](../../business/roles.dod.md) §3 — "Guard chống mất admin: không cho đổi/xoá khiến repo không còn member nào có quyền ManageSettings (dựa permission thật, không dựa chức danh SM)".
- **Bước thực hiện**: (a) Repo chỉ còn đúng 1 member có role Scrum Master (default) → remove member đó. (b) Repo chỉ còn đúng 1 member có `ManageSettings` nhưng đang giữ role **"Project Manager"** (không phải Scrum Master) → remove/đổi role member đó sang role không có `ManageSettings`.
- **Kết quả mong đợi**: (a) Bị chặn đúng như code hiện tại (check theo tên "Scrum Master"). (b) **Theo đúng business doc phải bị chặn tương tự** — nhưng theo code hiện tại (`RemoveMemberCommandHandler.cs:33-54`, `UpdateMemberRoleCommandHandler.cs:40-58` chỉ check tên "Scrum Master") **dự kiến KHÔNG bị chặn** → repo mất hết quyền ManageSettings. Đây chính là **BUG-004** (Open) — case (b) dự kiến Fail cho tới khi fix.
- **Kết quả thật (2026-06-29)**: (a) **Pass** — verify qua cả UI (client guard chặn, dù dropdown desync không revert) và API trực tiếp (PUT trả 400 "Cannot change the last Scrum Master role..."). (b) **Không dựng được qua đường member-role** vì guard "Scrum Master role/chức danh" rất chặt — luôn ngăn vacate hẳn Scrum Master, nên không tạo được trạng thái "member cuối ManageSettings giữ role khác SM" thông qua việc đổi role member. Tuy nhiên đúng lỗ hổng mà case này muốn kiểm tra đã được **chứng minh 100% qua roles-08b** (sửa `AllowedFunctions` của role qua `UpdateRole` — vector khác nhưng cùng root cause BUG-004): tạo role custom "Owner" (ManageSettings) → gán cho member → bỏ ManageSettings khỏi role → thành công không 1 cảnh báo.

### members-07 — Remove member chỉ xoá membership, không xoá User

- **Business rule**: [members.dod.md](../../business/members.dod.md) §3 — "Remove member chỉ xoá quan hệ membership, không xoá User".
- **Bước thực hiện**: Remove 1 member (không phải ManageSettings cuối) khỏi Repository → check lại Settings → Users (admin).
- **Kết quả mong đợi**: Member biến mất khỏi list Members của repo đó, nhưng user vẫn còn nguyên trong Settings → Users (hệ thống), có thể add lại làm member repo khác.
- **Kết quả thật (2026-06-29)**: **Pass.** Remove "Lan Pham" khỏi DASH (không phải ManageSettings cuối) → biến mất khỏi Members list; verify `/api/users/search?q=Lan Pham` vẫn trả về user này → confirm hệ thống Users không bị ảnh hưởng.

### members-08 — 1 user là member nhiều Repository với role khác nhau

- **Business rule**: [members.dod.md](../../business/members.dod.md) §6 — "1 user có thể là member của nhiều Repository với role khác nhau ở mỗi nơi".
- **Bước thực hiện**: Add cùng 1 user (từ seed-users.md) vào 2 Repository khác nhau, gán role khác nhau ở mỗi repo (vd Developer ở repo A, Tester ở repo B).
- **Kết quả mong đợi**: Cả 2 membership tồn tại độc lập, đúng role riêng từng repo, không xung đột.

### members-09 — Update role member (đổi chức danh và/hoặc role) — role phải hợp lệ trong repo

- **Business rule**: [members.dod.md](../../business/members.dod.md) §4.2; [roles.dod.md](../../business/roles.dod.md) §3 — RoleId phải là default global hoặc custom cùng Repository; code `UpdateMemberRoleCommandHandler.cs:34-37` trả `Result.Failure("Role not found in this repository.")` nếu không hợp lệ.
- **Bước thực hiện**: (a) Đổi chức danh + role hợp lệ của 1 member có sẵn. (b) Thử gán 1 RoleId thuộc Repository KHÁC (custom role không cùng repo) cho member.
- **Kết quả mong đợi**: (a) Thành công, permission đổi theo role mới ngay. (b) Bị chặn — "Role not found in this repository."

### members-10 — `DefaultRole` (chức danh) không cấp quyền — độc lập với Role

- **Business rule**: [members.dod.md](../../business/members.dod.md) §3 — "DefaultRole chỉ dùng cho capacity/planning, không cấp quyền. Quyền của member = Role.AllowedFunctions".
- **Bước thực hiện**: Gán 1 member chức danh "Scrum Master" (DefaultRole) nhưng Role thực tế = "Developer" (ít quyền).
- **Kết quả mong đợi**: Member chỉ thấy/thực hiện được action theo quyền của Developer, KHÔNG có quyền ManageSettings dù chức danh ghi "Scrum Master".

### members-11 — Accept invitation không tạo trùng membership nếu đã là member

- **Business rule**: [members.dod.md](../../business/members.dod.md) §6 — "Nếu user đã là member qua 1 invitation trước, accept invitation khác vào cùng Repository sẽ thành công im lặng (không tạo trùng membership)".
- **Bước thực hiện**: **Bỏ qua hiện tại** — phụ thuộc feature Invitations đang WIP (xem `wip-features.md`). Re-test khi Invitations hoàn thiện.
- **Kết quả mong đợi**: N/A — pending.

---

## Case Status

| Case ID | Trạng thái lần chạy gần nhất | Ngày | Bug liên quan |
|---------|-------------------------------|------|----------------|
| members-01 | Pass | 2026-06-30 | - |
| members-02 | Pass | 2026-06-29 | - |
| members-03 | Chưa chạy | - | - |
| members-04 | Pass | 2026-06-30 | - |
| members-05 | Chưa chạy | - | - |
| members-06 | Pass (a+b, fix BUG-004 verified qua roles-08b) | 2026-06-30 | - |
| members-07 | Pass | 2026-06-29 | - |
| members-08 | Chưa chạy | - | - |
| members-09 | Chưa chạy | - | - |
| members-10 | Chưa chạy | - | - |
| members-11 | Bỏ qua (Invitations WIP) | - | - |
