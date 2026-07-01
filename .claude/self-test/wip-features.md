# WIP Features — Tạm loại khỏi Self-Test

> Đọc `RULES.md` trước khi sửa file này.

Liệt kê module/feature **đang code chưa xong** — `/self-test` PHẢI bỏ qua các module này (không navigate, không tạo case, không tính vào báo cáo) cho tới khi bị gỡ khỏi danh sách. Mục đích: tránh tốn lượt test + tránh log bug giả cho code chưa hoàn thiện.

## Quy tắc

- Thêm dòng mới khi 1 module đang code dở, chưa sẵn sàng test UI.
- **Gỡ dòng khỏi bảng** (xoá hẳn, không phải đổi trạng thái) khi module đã hoàn thiện và muốn đưa lại vào self-test.
- Cột **Module** phải khớp tên file trong `modules/` (vd `wiki` → bỏ qua `wiki.test.md`). Nếu feature chưa có module/route rõ ràng (ý tưởng/đang thiết kế), ghi tên tự do ở cột Module và ghi chú ở Lý do.
- `/self-test` (chạy toàn bộ, không argument) phải đọc bảng này TRƯỚC khi lặp qua `test-plan.md` — module nào có tên ở đây thì skip, vẫn liệt kê trong báo cáo cuối với trạng thái `⏭️ Skipped (WIP)`.
- `/self-test {module}` chạy chỉ định đích danh module đang nằm trong bảng này → vẫn DỪNG, báo "module đang WIP, bỏ qua" — không cố chạy.

## Danh sách

| Module | Lý do chưa hoàn thiện | Ngày đánh dấu |
|--------|------------------------|----------------|
| wiki | Đang code dở | 2026-06-29 |
| overview | Đang code dở | 2026-06-29 |
| invitations | Đang code dở | 2026-06-29 |
| repositories | Đang code dở | 2026-06-29 |
| pipelines | Đang code dở (chưa có module test riêng, route UI cũng chưa hoàn thiện) | 2026-06-29 |
| boards / dynamic query | Đang code dở — chưa ổn định, tính năng dynamic query builder chưa xong | 2026-06-29 |
| auth (reset password) | Tính năng reset password (link "Forgot password?" ở trang login) CHƯA code xong — chỉ riêng flow này, không phải toàn bộ module `auth` (login/logout đã test pass, xem `auth.test.md`). Bỏ qua mọi case liên quan reset/forgot password cho tới khi feature này hoàn thiện. | 2026-06-29 |
