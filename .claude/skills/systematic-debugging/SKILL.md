---
name: systematic-debugging
description: Use when hitting any bug, test failure, build error, or unexpected behavior — before proposing a fix. Four-phase root-cause process for the .NET + Angular + EF Core stack.
---

# Skill: Systematic Debugging

**Luật sắt: KHÔNG SỬA KHI CHƯA TÌM RA ROOT CAUSE.** Sửa triệu chứng là thất bại.

Chưa xong Phase 1 thì không được đề xuất fix.

## Khi nào dùng

Mọi vấn đề kỹ thuật: test fail, bug production, hành vi lạ, chậm, build lỗi, lỗi tích hợp.

**Đặc biệt khi:** đang gấp · "sửa nhanh cái này thôi" · đã thử vài fix mà không được · chưa hiểu rõ vấn đề.

**Đừng bỏ qua vì:** "bug đơn giản" (bug đơn giản cũng có root cause) · "đang vội" (làm hệ thống *nhanh hơn* mò mẫm).

---

## Phase 1 — Điều tra root cause

1. **Đọc kỹ error message.** Đọc hết stack trace, ghi lại file/line/mã lỗi. Thường nó nói thẳng nguyên nhân.
   - .NET: đọc cả `InnerException`, không dừng ở message ngoài cùng.
   - Angular: xem console + Network tab, không chỉ terminal `ng serve`.

2. **Tái hiện ổn định.** Bước nào gây ra? Có lặp lại mọi lần không? Không tái hiện được → thu thập thêm dữ liệu, đừng đoán.

3. **Kiểm tra thay đổi gần đây.** `git diff`, commit gần nhất, dependency mới, đổi config, khác biệt môi trường.

4. **Thu bằng chứng ở hệ nhiều tầng.** Trước khi đề xuất fix, thêm log tại **từng ranh giới**:
   ```
   Angular service → HTTP interceptor → Controller → MediatR handler → EF Core → DB
   ```
   Log cái gì **vào** và cái gì **ra** ở mỗi tầng, chạy một lần để biết **tầng nào hỏng**, rồi mới đào tầng đó.

   Ví dụ cho stack này:
   - EF Core: bật `LogTo(Console.WriteLine)` xem SQL thật sinh ra.
   - MediatR: kiểm tra pipeline behavior (validation/logging) có nuốt exception không.
   - Auth: cookie có tới server không (Network tab) → middleware đọc được không → claim có đúng không.

5. **Truy ngược dòng dữ liệu.** Giá trị sai xuất hiện đầu tiên ở đâu? Ai truyền nó vào? Truy ngược tới nguồn, **sửa ở nguồn chứ không ở chỗ phát hiện**.

## Phase 2 — Phân tích pattern

1. **Tìm chỗ tương tự đang chạy đúng** trong chính codebase. Feature nào giống mà không lỗi?
2. **So với bản tham chiếu.** Đang làm theo pattern nào thì đọc **hết** bản mẫu, không lướt.
3. **Liệt kê mọi khác biệt** giữa cái chạy được và cái hỏng. Đừng tự bảo "cái đó không liên quan".
4. **Hiểu dependency:** cần config gì, DI đăng ký đúng scope chưa, migration đã chạy chưa.

## Phase 3 — Giả thuyết và kiểm chứng

1. **Một giả thuyết duy nhất**, viết ra rõ: "Tôi nghĩ X là root cause vì Y."
2. **Test nhỏ nhất có thể.** Một biến một lúc. Không sửa nhiều thứ cùng lúc.
3. **Xác minh rồi mới đi tiếp.** Đúng → Phase 4. Sai → giả thuyết **mới**, không chồng thêm fix.
4. **Không biết thì nói không biết.** Đừng giả vờ hiểu.

## Phase 4 — Triển khai

1. **Viết test fail trước.** Tái hiện đơn giản nhất. Bắt buộc có trước khi sửa. Dùng skill `unit-testing` (xUnit) hoặc `unit-testing-angular` (Jasmine).
2. **Một fix duy nhất**, đúng root cause. Không "tiện tay sửa luôn", không gộp refactor.
3. **Xác minh:** test pass chưa, có làm hỏng test khác không, vấn đề hết thật chưa.
4. **Fix không ăn thua?** DỪNG. Đếm số lần đã thử.
   - < 3 → quay lại Phase 1 với thông tin mới.
   - **≥ 3 → DỪNG HẲN, nghi ngờ kiến trúc** (xem dưới). Không thử fix thứ 4.

5. **3+ fix thất bại = vấn đề kiến trúc, không phải giả thuyết sai.**

   Dấu hiệu: mỗi fix lại lòi ra coupling/shared state ở chỗ khác · fix đòi "refactor lớn" · fix này đẻ ra triệu chứng kia.

   Đặt lại câu hỏi gốc: pattern này có đúng không? Có đang theo nó chỉ vì quán tính? Nên refactor kiến trúc thay vì vá tiếp?

   **Bàn với user trước khi thử thêm.**

---

## Cờ đỏ — thấy là quay lại Phase 1

- "Sửa tạm đã, điều tra sau"
- "Thử đổi X xem có hết không"
- "Sửa mấy chỗ rồi chạy test một lượt"
- "Bỏ test, tự kiểm tra tay cũng được"
- "Chắc là do X, sửa X đi"
- "Chưa hiểu lắm nhưng chắc cái này được"
- Đề xuất giải pháp **trước khi** truy dòng dữ liệu
- "Thử thêm một fix nữa" (khi đã thử 2+)

## Ngụy biện thường gặp

| Lý do | Thực tế |
|---|---|
| "Bug đơn giản, khỏi quy trình" | Bug đơn giản cũng có root cause. Quy trình chạy rất nhanh với bug đơn giản. |
| "Đang gấp, không có thời gian" | Làm hệ thống **nhanh hơn** mò mẫm. |
| "Thử cái này trước đã" | Fix đầu tiên định hình cả hướng. Làm đúng ngay từ đầu. |
| "Sửa xong rồi viết test" | Fix không có test thì không bền. |
| "Sửa nhiều thứ một lúc cho nhanh" | Không biết cái nào có tác dụng. Đẻ bug mới. |
| "Tôi thấy vấn đề rồi" | Thấy triệu chứng ≠ hiểu root cause. |
| "Thử thêm một fix nữa" (sau 2+ lần) | 3+ lần fail = sai kiến trúc. Đừng vá nữa. |

## Tóm tắt

| Phase | Việc chính | Xong khi |
|---|---|---|
| 1. Root cause | Đọc lỗi, tái hiện, xem diff, thu bằng chứng từng tầng | Hiểu **cái gì** và **tại sao** |
| 2. Pattern | Tìm chỗ chạy đúng, so sánh | Xác định được khác biệt |
| 3. Giả thuyết | Nêu một giả thuyết, test tối thiểu | Xác nhận hoặc có giả thuyết mới |
| 4. Triển khai | Viết test fail, sửa, xác minh | Hết bug, test xanh |

## Khi điều tra ra "không có root cause"

Nếu thật sự do môi trường, timing, hay hệ ngoài: ghi lại đã điều tra những gì, xử lý phù hợp (retry, timeout, thông báo lỗi rõ), thêm log/monitoring.

**Nhưng:** 95% ca "không tìm ra root cause" là do điều tra chưa tới.

---

Nguồn: phỏng theo `systematic-debugging` của [obra/superpowers](https://github.com/obra/superpowers) (MIT), chỉnh cho stack .NET + Angular + EF Core của dự án.
