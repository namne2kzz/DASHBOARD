# Quy tắc Document — `.claude/self-test/`

Tài liệu này quy định cách Claude (và bất kỳ ai khác) ghi/đọc/update các file trong `.claude/self-test/`. **Phải đọc file này trước khi tạo hoặc sửa bất kỳ file trong folder.**

## 1. Mục đích thư mục

`.claude/self-test/` lưu **test plan + kết quả + bug + improvement** phát sinh từ việc tự test UI bằng Playwright MCP (xem `/self-test`). Khác với `.claude/histories/` (business doc — mô tả nghiệp vụ "phải đúng như thế nào"), folder này lưu **bằng chứng đã/chưa kiểm chứng đúng nghiệp vụ đó trên app thật**.

## 2. Cấu trúc thư mục

```
.claude/self-test/
├── RULES.md            # file này
├── test-plan.md         # tổng quan: scope, môi trường, quy trình, danh sách module
├── bugs.md               # bug tracker tổng — chỉ lỗi hệ thống (sai so với business doc)
├── improvements.md       # ý tưởng cải tiến UX/nghiệp vụ — KHÔNG phải bug
└── modules/
    └── {feature}.test.md # 1 file / module, tên khớp `.claude/histories/{feature}.dod.md` nếu có
```

## 3. Cấu trúc bên trong `modules/{feature}.test.md`

3 phần, theo đúng thứ tự:

### Phần A — Run Log (luôn ở đầu file)

Append dòng mới lên **đầu bảng**, không xóa dòng cũ. Đây là tổng quan **theo từng lần chạy** (chạy cả module 1 lượt):

```markdown
## Run Log

| Ngày | Giờ | Kết quả | Bug mới | Ghi chú |
|------|-----|---------|---------|---------|
| 2026-06-29 | 21:10 | Pass | - | Login + redirect OK |
```

- **Kết quả**: `Pass` / `Fail` / `Partial`.
- **Bug mới**: liệt kê ID bug nếu phát hiện (vd `BUG-003`), hoặc `-` nếu không có.
- **Ghi chú**: 1 câu ngắn, nêu case nào fail nếu có.

### Phần B — Test Cases (luôn là bản LATEST, ghi đè khi sửa)

Chỉ chứa **định nghĩa case** — KHÔNG có trạng thái pass/fail ở đây (trạng thái nằm riêng ở Phần C). Mỗi case:

```markdown
### {feature}-01 — {Tên case ngắn}

- **Business rule**: trích/link rule liên quan trong [`{feature}.dod.md`](../../histories/{feature}.dod.md) (nếu không có doc, ghi "Chưa có business doc — test theo khám phá UI").
- **Bước thực hiện**: 1, 2, 3...
- **Kết quả mong đợi**: ...
```

Khi business rule trong `.dod.md` đổi → phải rà lại case liên quan, sửa case nếu cần (không tạo case trùng).

### Phần C — Case Status (luôn ở CUỐI file, bảng riêng theo dõi trạng thái mới nhất từng case)

Append/update theo case ID — mỗi case chỉ có **đúng 1 dòng**, ghi đè dòng đó sau mỗi lần chạy (không tạo dòng trùng case ID):

```markdown
## Case Status

| Case ID | Trạng thái lần chạy gần nhất | Ngày | Bug liên quan |
|---------|-------------------------------|------|----------------|
| {feature}-01 | Chưa chạy | - | - |
| {feature}-02 | Pass | 2026-06-29 | - |
| {feature}-03 | Fail | 2026-06-29 | BUG-001 |
```

- Mỗi case mới thêm vào Phần B phải có dòng tương ứng ở đây ngay (mặc định `Chưa chạy`).
- **Trạng thái**: `Pass` / `Fail` / `Chưa chạy`.
- **Bug liên quan**: ID bug nếu case Fail vì sai behavior, hoặc `-`.
- Lịch sử qua nhiều lần chạy của riêng từng case **không** giữ ở đây (chỉ trạng thái mới nhất) — muốn xem case nào fail ở lần chạy nào, tra Run Log (Phần A).

## 4. `bugs.md` — Bug Tracker

Chỉ ghi **lỗi hệ thống thật** — behavior sai so với `.dod.md` hoặc lỗi rõ ràng (crash, exception, mất dữ liệu, sai validation...). Bảng append dòng mới ở **cuối** (ID tăng dần dễ trace):

| ID | Ngày phát hiện | Module | Mức độ | Mô tả + bước repro | Trạng thái | Test case liên quan | Ngày promote lên Nexus |
|----|----------------|--------|--------|---------------------|------------|----------------------|--------------------------|

- **Mức độ**: Critical (mất dữ liệu/crash/security) · High (sai business rule) · Medium (sai UX rõ ràng nhưng không chặn flow) · Low (cosmetic).
- **Trạng thái**: Open · Fixed · WontFix.
- ID dạng `BUG-001`, `BUG-002`... tăng dần, không tái sử dụng số đã xoá.
- **Ngày promote lên Nexus**: mặc định `-`. Xem mục 5b.

## 5. `improvements.md` — Improvement Tracker

Ý tưởng cải tiến phát hiện trong lúc test — **không phải lỗi**, app vẫn chạy đúng nghiệp vụ nhưng có thể tốt hơn (UX, performance, business logic gợi ý thêm). Cùng cấu trúc append cuối bảng:

| ID | Ngày | Module | Loại | Đề xuất | Trạng thái | Ngày promote lên Nexus |
|----|------|--------|------|---------|------------|--------------------------|

- **Loại**: UX · Performance · Business logic suggestion.
- **Trạng thái**: Proposed · Accepted · Rejected · Done.
- ID dạng `IMP-001`, `IMP-002`...
- **Ngày promote lên Nexus**: mặc định `-`. Xem mục 5b.

## 5b. Promote ticket lên Nexus (hệ thống Dashboard đang test)

"Nexus" = chính app Dashboard đang được self-test (dùng tính năng WorkItem/Backlog của nó để track bug/improvement thật).

- **KHÔNG tự động promote.** Mọi dòng trong `bugs.md`/`improvements.md` mặc định cột "Ngày promote lên Nexus" = `-`, dù đã Open/Proposed lâu.
- Chỉ promote khi **user yêu cầu rõ ràng** (vd "promote BUG-003 lên Nexus", "đẩy mấy bug Critical lên ticket thật").
- Khi được yêu cầu: tạo work item thật trong app (qua UI Playwright hoặc API), loại Bug/Task tương ứng, nội dung lấy từ cột "Mô tả + bước repro" (bugs) hoặc "Đề xuất" (improvements). Sau khi tạo thành công → điền ngày thật vào cột "Ngày promote lên Nexus" của dòng đó (không sửa các cột khác).
- 1 dòng chỉ promote 1 lần — nếu đã có ngày ở cột này, không tạo ticket trùng trừ khi user yêu cầu lại rõ ràng.

## 6. Khi nào update

Sau **mỗi lần** chạy `/self-test` (toàn bộ hoặc 1 module):

1. Append Run Log (Phần A) của module vừa test.
2. Update dòng tương ứng của từng case đã chạy trong Case Status (Phần C) — ghi đè trạng thái + ngày.
3. Nếu case nào Fail vì sai behavior → thêm dòng `bugs.md`, điền ID bug đó vào cột "Bug liên quan" của case trong Phần C.
4. Nếu có ý tưởng cải tiến phát sinh → thêm dòng `improvements.md`.
5. Nếu module chưa có case nào (file mới tạo khung rỗng) → đọc `.dod.md` tương ứng, tự soạn case vào Phần B + thêm dòng tương ứng (`Chưa chạy`) vào Phần C trước khi chạy.

## 7. Ngôn ngữ

Viết tiếng Việt, giữ nguyên tên entity/field/route/component bằng tiếng Anh như trong code (đồng bộ với `.claude/histories/RULES.md`).
