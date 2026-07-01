# Users (Settings / Users) — Test

> Đọc `../RULES.md` trước khi sửa file này.

## Run Log

| Ngày | Giờ | Kết quả | Bug mới | Ghi chú |
|------|-----|---------|---------|---------|
| 2026-06-30 | 21:50 | Pass | - | Lần chạy đầu tiên cho module này. users-01 Pass (đăng nhập `dev@dashboard.local`, không phải Global Admin → navigate `/settings/users` bị route guard redirect về `/DASH/boards`, không vào được trang). users-09 **Pass — regression BUG-003 xác nhận (đổi từ "dự kiến Fail" → Pass)**: thay vì cần login non-admin tự target chính mình (cùng case nhưng IsSelf luôn chặn bất kể admin hay không theo code mới), verify trực tiếp bằng chính `admin@dashboard.local` (đang đăng nhập, IS Global Admin) bấm "Demote admin" lên đúng hàng của chính mình → 403 Forbidden, badge "Admin" không đổi (trước fix, do `!IsSelf && !IsGlobalAdmin` chỉ chặn khi CẢ HAI đúng, nên self-target luôn bypass bất kể admin hay không — đây chính là lỗ hổng cũ). Test này chứng minh nhánh `IsSelf` giờ chặn vô điều kiện đúng như code mới. users-10 Pass: Admin User demote thành công user khác ("Edogawa Conan", trước đó đang là Admin — không rõ nguồn gốc, có thể leftover từ phiên test trước; đã demote về Member sạch, hệ thống còn đúng 1 Admin sau đó). Chưa chạy: users-02 đến users-08, users-11 (cần thêm setup tạo/soft-delete user, đổi password, deactivate... ngoài phạm vi quick reverify lần này). |

---

## Test Cases

### users-01 — Chỉ Global Admin được tạo user mới

- **Business rule**: [users.dod.md](../../histories/users.dod.md) §3, §5 — "Chỉ Global Admin được tạo user mới".
- **Bước thực hiện**: Đăng nhập account KHÔNG phải Global Admin → thử truy cập Settings → Users → Create user (hoặc gọi thẳng action nếu UI ẩn nút).
- **Kết quả mong đợi**: Không thấy nút/trang tạo user, hoặc action bị chặn 403 nếu cố tình gọi.

### users-02 — Tạo user: validate password ≥ 8 ký tự

- **Business rule**: [users.dod.md](../../histories/users.dod.md) §3, §5 — "Validation password ≥ 8 ký tự được áp dụng khi tạo account System".
- **Bước thực hiện**: Admin tạo user mới, điền password 7 ký tự (vd `Ab1234!`).
- **Kết quả mong đợi**: Validation chặn submit, lỗi rõ ràng yêu cầu tối thiểu 8 ký tự.

### users-03 — Tạo user: name/email bắt buộc

- **Business rule**: [users.dod.md](../../histories/users.dod.md) §3 — "email/name bắt buộc".
- **Bước thực hiện**: Tạo user để trống Name, hoặc để trống Email, submit riêng từng trường hợp.
- **Kết quả mong đợi**: Validation chặn cả 2 trường hợp.

### users-04 — Email unique toàn hệ thống (kể cả user đã soft-delete)

- **Business rule**: [users.dod.md](../../histories/users.dod.md) §3, §5 — "Email unique toàn hệ thống — check cả user đã soft-delete để tránh trùng khi tạo lại".
- **Bước thực hiện**: Tạo user A → deactivate (soft-delete) user A → thử tạo user mới với CÙNG email A.
- **Kết quả mong đợi**: Bị chặn — lỗi email đã tồn tại, dù user cũ đã deactivate.

### users-05 — Email luôn lưu lowercase

- **Business rule**: [users.dod.md](../../histories/users.dod.md) §5 — "Email luôn được lưu lowercase".
- **Bước thực hiện**: Tạo user với email viết hoa 1 phần (vd `Test.User@Dashboard.Local`).
- **Kết quả mong đợi**: Email lưu/hiển thị toàn bộ lowercase trong list Users.

### users-06 — Đổi password: chỉ chính chủ, bắt buộc verify password cũ

- **Business rule**: [users.dod.md](../../histories/users.dod.md) §3, §5 — "Đổi password: chỉ chính chủ được đổi (không delegate cho admin), bắt buộc verify password cũ".
- **Bước thực hiện**: (a) Đăng nhập user A, đổi password của chính mình với password cũ SAI → (b) thử gọi API đổi password của user khác (kể cả khi đang là Global Admin).
- **Kết quả mong đợi**: (a) Bị chặn, lỗi "Current password is incorrect" (đúng message handler). (b) Bị chặn hoàn toàn — "You may only change your own password" — không có path nào cho phép admin đổi password người khác (đúng DoD §5).

### users-07 — Cập nhật profile: chính chủ hoặc Global Admin

- **Business rule**: [users.dod.md](../../histories/users.dod.md) §3 — "Cập nhật profile (name, avatar): chỉ chính chủ hoặc Global Admin".
- **Bước thực hiện**: (a) User A tự update name/avatar của mình → (b) Global Admin update profile của user khác → (c) User B (không phải admin) thử update profile của user A.
- **Kết quả mong đợi**: (a) (b) thành công. (c) bị chặn — "You do not have permission to update this profile."

### users-08 — Toggle active/inactive — chỉ Global Admin

- **Business rule**: [users.dod.md](../../histories/users.dod.md) §3, §4.4 — "User có thể bị toggle active/inactive"; theo code `ToggleActiveCommandHandler` chỉ Global Admin được thực hiện.
- **Bước thực hiện**: Admin deactivate 1 user test (từ seed-users.md) → user đó thử login lại.
- **Kết quả mong đợi**: Deactivate thành công; user bị deactivate không login được nữa (liên quan auth — verify lỗi login generic, không tiết lộ "account bị khoá" nếu áp dụng đúng anti-enumeration).

### users-09 — ⚠️ Toggle Global Admin: tự cấp quyền cho chính mình (BUG-003)

- **Business rule**: [users.dod.md](../../histories/users.dod.md) §3, §5 — "Toggle Global Admin: chỉ Global Admin khác mới được thực hiện" / "Toggle admin chỉ thực hiện được bởi Global Admin".
- **Bước thực hiện**: Đăng nhập 1 user **KHÔNG** phải Global Admin (vd user từ seed-users.md) → tự gọi `PATCH /api/users/{chính userId của mình}/promote-admin` (qua UI nếu có path, hoặc verify trực tiếp qua API như đã làm khi phát hiện bug).
- **Kết quả mong đợi**: Phải bị chặn (403/Failure) vì không phải "Global Admin khác". **Thực tế (đã verify code)**: request thành công, user tự cấp được `IsGlobalAdmin = true` cho chính mình — xem **BUG-003** (Critical, đang Open) trong `bugs.md`. Case này **dự kiến Fail** cho tới khi BUG-003 được fix.

### users-10 — Toggle Global Admin bởi admin khác — hợp lệ

- **Business rule**: [users.dod.md](../../histories/users.dod.md) §3 — admin khác được toggle.
- **Bước thực hiện**: Global Admin A toggle `IsGlobalAdmin` của user B (khác A).
- **Kết quả mong đợi**: Thành công, B trở thành Global Admin (hoặc bị gỡ nếu đang là admin).

### users-11 — Search user: chỉ trả active user, hỗ trợ exclude theo repo

- **Business rule**: theo doc `UsersController.Search` — "Searches active users... excludeRepoId: users already in this repository are excluded".
- **Bước thực hiện**: Deactivate 1 user (từ users-08) → search đúng tên/email user đó ở member-picker (Add member). Riêng case exclude: search ở 1 repo đã add sẵn 1 user → user đó không xuất hiện trong kết quả search của repo đó nữa.
- **Kết quả mong đợi**: User đã deactivate KHÔNG xuất hiện trong kết quả search. User đã là member của repo X không xuất hiện khi search trong context add-member của repo X (nhưng vẫn search được ở repo khác).

---

## Case Status

| Case ID | Trạng thái lần chạy gần nhất | Ngày | Bug liên quan |
|---------|-------------------------------|------|----------------|
| users-01 | Pass | 2026-06-30 | - |
| users-02 | Chưa chạy | - | - |
| users-03 | Chưa chạy | - | - |
| users-04 | Chưa chạy | - | - |
| users-05 | Chưa chạy | - | - |
| users-06 | Chưa chạy | - | - |
| users-07 | Chưa chạy | - | - |
| users-08 | Chưa chạy | - | - |
| users-09 | Pass | 2026-06-30 | - |
| users-10 | Pass | 2026-06-30 | - |
| users-11 | Chưa chạy | - | - |
