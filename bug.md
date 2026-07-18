# Bug Tracker

Bug phát hiện trong quá trình implement/review, chưa có tracker chính thức (không có Jira/Linear) nên ghi tạm ở đây. Review khi rảnh.

---

## OPEN

### 1. `new-repo-dialog.component.spec.ts` — spec lỗi thời, test sai API hiện tại

- **File:** `DASHBOARD.VIEW/src/app/components/new-repo-dialog/new-repo-dialog.component.spec.ts`
- **Phát hiện:** 2026-07-15
- **Mô tả:** Spec viết cho version cũ của `NewRepoDialogComponent` — giả định có `ngOnInit()` tự gọi `usersSvc.load()` khi list rỗng, và field `scrumMasterId` gán trực tiếp (`component.scrumMasterId = 'user-1'`). Component hiện tại đã refactor sang UI search-autocomplete (`selectedUser`/`selectUser()`/`onUserSearch()` gọi `usersSvc.searchAllUsers(term)`), không còn implement `OnInit`, không còn field `scrumMasterId`.
- **Impact:** ~5/7 test trong file compile lỗi (`ngOnInit does not exist`, `scrumMasterId does not exist`) → toàn bộ `ng test` suite của project không chạy được (Angular test builder compile chung tất cả spec file).
- **Fix cần làm:** Viết lại các test để dùng đúng API hiện tại (`selectUser(mockPickerItem)` thay vì set `scrumMasterId` trực tiếp; bỏ test `ngOnInit`/load-if-empty vì logic đó không còn tồn tại, hoặc verify lại xem load-users có nên xảy ra ở đâu đó khác không).

---

## FIXED

### 2. `SettingsUsersPageComponent` thiếu guard chặn tự thao tác lên chính mình

- **File:** `DASHBOARD.VIEW/src/app/pages/settings-users-page/settings-users-page.component.ts` (+ `.html`)
- **Phát hiện & fix:** 2026-07-15
- **Mô tả:** Spec đã có sẵn 2 test kỳ vọng method `canActOnUser(user)` (chặn Global Admin tự demote/deactivate chính tài khoản mình), nhưng method này chưa từng được implement trong component thật — nút Promote/Demote/Activate/Deactivate render không điều kiện cho mọi hàng, kể cả hàng của chính user đang đăng nhập.
- **Rủi ro trước khi fix:** Global Admin có thể tự bấm Deactivate hoặc Demote chính mình, dẫn tới tự khoá quyền truy cập (self-lockout) nếu không còn admin nào khác active.
- **Fix đã áp dụng:** Thêm `canActOnUser(user): boolean` (so `user.userId` với `auth.currentUser()?.userId`), gate cả trong `toggleAdmin()`/`toggleActive()` (defensive check) lẫn trong template (ẩn nút, hiện badge "You" cho hàng của chính mình).

### 3. `new-repo-dialog.component.spec.ts` — mock `SystemUserDto` thiếu field `authProvider`

- **File:** `DASHBOARD.VIEW/src/app/components/new-repo-dialog/new-repo-dialog.component.spec.ts`
- **Phát hiện & fix:** 2026-07-15
- **Mô tả:** Do thêm field `authProvider` mới vào interface `SystemUserDto` (phục vụ hiện badge "Google" ở Settings → Users), mock user trong file này thiếu field bắt buộc → lỗi TS2741 compile.
- **Fix đã áp dụng:** Thêm `authProvider: AuthProvider.System` vào mock.

---

## Ghi chú khác (không phải bug, chỉ để nhớ)

- `EmailSettings` SMTP flow đã fix 2 lần trong session này: `SecureSocketOptions.StartTls` → `StartTlsWhenAvailable` (mailpit không hỗ trợ STARTTLS), và bỏ `AuthenticateAsync` vô điều kiện (mailpit không advertise SASL mechanism nào) — xem `DASHBOARD/Infrastructure/Email/EmailService.cs`.
- Lúc thử tạo EF migration cho Invitations, phát hiện model hiện tại lệch với snapshot ở `Roles.AllowedFunctions` (seed data 4 role mặc định) — không liên quan tới bất kỳ feature nào đang làm, có vẻ là drift có sẵn từ trước (code seed đổi nhưng chưa tạo migration tương ứng). Chưa xử lý, để riêng.
