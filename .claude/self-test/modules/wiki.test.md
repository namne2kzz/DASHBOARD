# Wiki — Test

> Đọc `../RULES.md` trước khi sửa file này.

## Run Log

| Ngày | Giờ | Kết quả | Bug mới | Ghi chú |
|------|-----|---------|---------|---------|

---

## Test Cases

### wiki-01 — Tạo root page

- **Business rule**: [wiki.dod.md](../../business/wiki.dod.md) §4.1 — tạo page với `ParentId = null`.
- **Bước thực hiện**: Navigate `/{repoCode}/wiki` → tạo page mới không chọn parent.
- **Kết quả mong đợi**: Page xuất hiện ở root level của cây wiki.

### wiki-02 — Tạo child page dưới parent có sẵn

- **Business rule**: [wiki.dod.md](../../business/wiki.dod.md) §4.1, §3 — parent phải tồn tại trong cùng Repository.
- **Bước thực hiện**: Tạo page mới, chọn 1 page có sẵn làm parent.
- **Kết quả mong đợi**: Page mới nằm đúng dưới parent trong cây.

### wiki-03 — Title bắt buộc

- **Business rule**: [wiki.dod.md](../../business/wiki.dod.md) §3 — "Title bắt buộc (≤ 500 ký tự)".
- **Bước thực hiện**: Tạo page để trống title, submit.
- **Kết quả mong đợi**: Validation chặn, không tạo được page.

### wiki-04 — Move page chặn circular reference

- **Business rule**: [wiki.dod.md](../../business/wiki.dod.md) §3, §5 — "parent mới không được là descendant của page đang move".
- **Bước thực hiện**: Tạo cây A → B (B là con A), thử move A vào làm con của B.
- **Kết quả mong đợi**: Action bị chặn, hiện lỗi rõ ràng, cây không bị thay đổi.

### wiki-05 — Xoá page còn children bị chặn

- **Business rule**: [wiki.dod.md](../../business/wiki.dod.md) §3, §5 — "Không cho xoá page còn con — phải xoá hết children trước".
- **Bước thực hiện**: Thử xoá 1 page đang có ít nhất 1 child.
- **Kết quả mong đợi**: Action bị chặn/disable, page và children không bị mất.

### wiki-06 — Move page về null = promote thành root

- **Business rule**: [wiki.dod.md](../../business/wiki.dod.md) §6 — "Move page về ParentId = null đồng nghĩa promote page đó thành root".
- **Bước thực hiện**: Move 1 child page ra khỏi parent hiện tại (về root).
- **Kết quả mong đợi**: Page chuyển lên hiển thị ở root level của cây.

### wiki-07 — Update content refresh LastUpdated

- **Business rule**: [wiki.dod.md](../../business/wiki.dod.md) §3 — "LastUpdated tự stamp mỗi lần đổi content".
- **Bước thực hiện**: Sửa content 1 page có sẵn, lưu.
- **Kết quả mong đợi**: Timestamp "last updated" hiển thị trên UI đổi thành thời điểm vừa lưu.

---

## Case Status

| Case ID | Trạng thái lần chạy gần nhất | Ngày | Bug liên quan |
|---------|-------------------------------|------|----------------|
| wiki-01 | Chưa chạy | - | - |
| wiki-02 | Chưa chạy | - | - |
| wiki-03 | Chưa chạy | - | - |
| wiki-04 | Chưa chạy | - | - |
| wiki-05 | Chưa chạy | - | - |
| wiki-06 | Chưa chạy | - | - |
| wiki-07 | Chưa chạy | - | - |
