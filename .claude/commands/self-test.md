---
description: Drive the running app in Microsoft Edge via Playwright MCP to self-test UI flows against business rules in .claude/business/, tracking results/bugs/improvements in .claude/self-test/.
argument-hint: [optional: module name e.g. "auth" | "boards" | "backlog" | "all"]
---

# Workflow: UI Self-Test (Playwright Edge MCP)

Tự lái Microsoft Edge qua các luồng UI chính, đối chiếu với business rule trong `.claude/business/`, và ghi kết quả/bug/improvement vào `.claude/self-test/` — thay cho việc test tay từng chức năng.

## Usage
```
/self-test            # chạy toàn bộ module theo .claude/self-test/test-plan.md
/self-test boards     # chỉ chạy module boards
```

---

## Pre-flight (xác nhận app đang sống TRƯỚC khi test)

App chạy **native**, KHÔNG qua Docker, và **không có endpoint `/health`**. KHÔNG gọi health-check endpoint, KHÔNG tự chạy `docker compose`/migration/seed.

1. Đọc credential từ file gitignored **`.claude/self-test.local.json`**:

   ```json
   { "baseUrl": "http://localhost:4200", "email": "...", "password": "...", "repoCode": "..." }
   ```

   Nếu file không tồn tại hoặc thiếu field → hỏi user rồi mới chạy tiếp.
2. `browser_navigate` tới `{baseUrl}`. Nếu lỗi connection refused / không load được → DỪNG, báo user chạy `dotnet run` (trong `DASHBOARD/`, API tại `:5152`) và `npm start` (trong `DASHBOARD.VIEW/`, UI tại `:4200`). Không tự đoán app đã sống nếu chưa thấy trang load được.
3. Đọc **`.claude/self-test/wip-features.md`** — danh sách module đang code dở. Module nào nằm trong bảng đó thì **bỏ qua hoàn toàn** (không navigate, không tạo case) cho tới khi bị gỡ khỏi file.

## Quy trình cho MỖI module được test

> Trước khi vào module bất kỳ: nếu module đó có tên trong `.claude/self-test/wip-features.md` → skip ngay, ghi vào báo cáo cuối là `⏭️ Skipped (WIP)`, không thực hiện 4 bước dưới. Nếu user gọi `/self-test {module}` đích danh 1 module đang WIP → vẫn dừng, báo lý do, không cố chạy.

1. **Đọc business doc**: `.claude/business/{feature}.dod.md` (nếu tồn tại) — nắm rule/invariant hiện tại.
2. **Đọc file test**: `.claude/self-test/modules/{feature}.test.md` (3 phần: Run Log đầu file, Test Cases giữa, Case Status cuối file).
   - Nếu Phần "Test Cases" đã có case → dùng case đó.
   - Nếu chưa có case nào (file khung rỗng) → tự soạn case dựa trên business doc vừa đọc, ghi vào Phần "Test Cases" + thêm dòng tương ứng (`Chưa chạy`) vào bảng "Case Status" cuối file, TRƯỚC khi chạy.
3. **Chạy từng case** bằng Playwright (browser = msedge): `browser_navigate` → `browser_snapshot` (xác định element qua accessibility tree) → thao tác (`browser_click`/`browser_type`/`browser_drag`...) → assert kết quả. Dùng `browser_network_requests`/`browser_console_messages` khi cần xác nhận không có request gửi đi hoặc bắt lỗi runtime. **KHÔNG dùng `browser_take_screenshot`** — tốn token, mô tả lại bằng text (state cụ thể, message lỗi, request/response liên quan) là đủ.
4. **Ghi kết quả**:
   - Append 1 dòng vào **Run Log** (đầu file) của `.claude/self-test/modules/{feature}.test.md` (ngày/giờ/kết quả tổng/case fail nếu có/bug mới).
   - Update dòng tương ứng của từng case đã chạy trong bảng **Case Status** (cuối file) — ghi đè Pass/Fail + ngày (không tạo dòng trùng case ID).
   - Case Fail vì sai behavior so với business doc → thêm dòng vào `.claude/self-test/bugs.md` (mức độ theo rule trong `self-test/RULES.md`), điền ID bug đó vào cột "Bug liên quan" của case trong Case Status.
   - Phát hiện điểm có thể cải tiến (không phải lỗi) → thêm dòng vào `.claude/self-test/improvements.md`.

Danh sách module + độ ưu tiên: xem `.claude/self-test/test-plan.md`. Nếu chạy `/self-test` không kèm argument → chạy theo thứ tự ưu tiên trong file đó (Cao trước). Nếu chạy `/self-test {module}` → chỉ chạy đúng module đó.

**KHÔNG sửa code app trong lúc self-test** — chỉ test, ghi nhận bug/improvement, để user quyết định fix.

---

## Report cuối cùng

In bảng tổng kết cho lần chạy này:

| Module | Kết quả | Case fail | Bug mới | Chi tiết |
|--------|---------|-----------|---------|----------|
| auth | ✅/❌/⚠️ | ... | BUG-00x hoặc - | `.claude/self-test/modules/auth.test.md` |
| wiki | ⏭️ Skipped (WIP) | - | - | xem `.claude/self-test/wip-features.md` |

- ⚠️ = Partial (có case pass, có case fail). ⏭️ = Skipped vì đang nằm trong `wip-features.md`.
- Với mỗi ❌/⚠️: nêu case nào fail, lý do bằng text (state/message lỗi/request liên quan) — không kèm screenshot.
- Cuối bảng: tổng số bug mới ghi vào `bugs.md`, tổng số improvement mới ghi vào `improvements.md` (nếu có).
