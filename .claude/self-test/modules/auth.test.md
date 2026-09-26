# Auth (Login) — Test

> Đọc `../RULES.md` trước khi sửa file này.

## Run Log

| Ngày | Giờ | Kết quả | Bug mới | Ghi chú |
|------|-----|---------|---------|---------|
| 2026-06-29 | 22:48 | Pass | - | Re-verify sau khi user restart BE (rebuild VS). auth-02/auth-03 giờ trả 401 ngay, message generic giống nhau, không còn debugger break/hang. auth-01 re-check vẫn redirect đúng /DASH/boards. BUG-002 đóng. |
| 2026-06-29 | 22:40 | Partial | BUG-002 | Phát hiện thêm qua quan sát VS debugger: auth-02/auth-03 trước đó "Pass" là sai — LoginCommandHandler throw exception cho credential sai khiến VS debugger break (user phải tự continue), không phải app tự xử lý đúng. Đánh lại Fail, đã fix sang Result<T> + Unauthorized(...). Cần restart BE (rebuild VS) để verify lại — chưa re-run được trong lần này. |
| 2026-06-29 | 22:26 | Pass | - | Chạy lại sau khi fix BUG-001. Toàn bộ 6/6 case pass: auth-01 redirect đúng /DASH/boards, auth-02/03 message generic giống nhau, auth-04 email hoa thường OK, auth-05/06 logout + route guard redirect đúng /login. |
| 2026-06-29 | 21:57 | Fail | BUG-001 | auth-01 fail: returnUrl bị lồng đè cấp số nhân khi đứng yên ở /login (lỗi 401 lặp), login sau đó vào app không deterministic. Dừng demo ở đây để báo bug trước khi chạy tiếp auth-02..06. |

---

## Test Cases

### auth-01 — Login thành công với credential đúng

- **Business rule**: [auth.dod.md](../../business/auth.dod.md) §3, §4.1 — login email/password verify đúng → phát token, redirect vào app.
- **Bước thực hiện**: Navigate `/login` → fill email + password đúng (từ `self-test.local.json`) → submit.
- **Kết quả mong đợi**: Redirect khỏi `/login`, shell layout (nav) render, không có lỗi console.

### auth-02 — Login sai password trả lỗi chung (anti-enumeration)

- **Business rule**: [auth.dod.md](../../business/auth.dod.md) §3 — "Đăng nhập sai trả về cùng 1 lỗi chung để chống user-enumeration".
- **Bước thực hiện**: Navigate `/login` → fill email đúng + password sai → submit.
- **Kết quả mong đợi**: Hiện lỗi generic (không nói rõ "sai password"), vẫn ở `/login`.

### auth-03 — Login user không tồn tại trả lỗi giống auth-02

- **Business rule**: [auth.dod.md](../../business/auth.dod.md) §3 — cùng message lỗi như sai password.
- **Bước thực hiện**: Navigate `/login` → fill email không tồn tại + password bất kỳ → submit.
- **Kết quả mong đợi**: Message lỗi **giống y hệt** auth-02 (so sánh text), không tiết lộ "email không tồn tại".

### auth-04 — Email không phân biệt hoa/thường

- **Business rule**: [auth.dod.md](../../business/auth.dod.md) §3 — "Email so khớp không phân biệt hoa/thường, luôn normalize lowercase".
- **Bước thực hiện**: Login với email viết HOA toàn bộ (hoặc đổi case ngẫu nhiên) + password đúng.
- **Kết quả mong đợi**: Login thành công như auth-01.

### auth-05 — Logout vô hiệu session

- **Business rule**: [auth.dod.md](../../business/auth.dod.md) §3, §4.3 — logout revoke JwtId hiện tại.
- **Bước thực hiện**: Login thành công → tìm nút logout (thường ở nav/profile menu) → click → thử quay lại 1 route protected (vd back button hoặc navigate thẳng `/{repoCode}/boards`).
- **Kết quả mong đợi**: Sau logout bị redirect về `/login`; quay lại route protected cũng bị redirect về `/login` (không cache session).

### auth-06 — Truy cập route protected khi chưa login

- **Business rule**: `authGuard` trong `app.routes.ts` — mọi route con của shell layout yêu cầu auth.
- **Bước thực hiện**: Chưa login, navigate thẳng tới `/{repoCode}/boards`.
- **Kết quả mong đợi**: Redirect về `/login`, không render được nội dung board.

---

## Case Status

| Case ID | Trạng thái lần chạy gần nhất | Ngày | Bug liên quan |
|---------|-------------------------------|------|----------------|
| auth-01 | Pass | 2026-06-29 | BUG-001 (Fixed) |
| auth-02 | Pass | 2026-06-29 | BUG-002 (Fixed) |
| auth-03 | Pass | 2026-06-29 | BUG-002 (Fixed) |
| auth-04 | Pass | 2026-06-29 | - |
| auth-05 | Pass | 2026-06-29 | - |
| auth-06 | Pass | 2026-06-29 | - |
