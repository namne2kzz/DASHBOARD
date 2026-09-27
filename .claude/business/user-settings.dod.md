# User Settings & Avatar Storage — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-09-26 | 14:02 | Khởi tạo document | Tạo document business ban đầu cho feature User Settings + avatar upload qua MinIO — đã có trong code từ 2026-09-20 nhưng chưa được document |

---

## 1. Purpose

Lưu preference hiển thị/thông báo của từng user (date format, timezone, theme, language, notification...) và cho user upload ảnh avatar thật thay cho avatar màu. Preference được share sang HUB để user không phải cấu hình lại 2 lần.

## 2. Key Entities & Relationships

- `UserSetting`: bảng **key-value schema-free** theo (`UserId`, `Key`).
  - `Key` — token dot-separated ổn định (vd `ui.date-format`), khai báo trong `UserSettingKeys`.
  - `Value` — string đã serialize. `null` = chưa set → app dùng default.
  - Thiết kế key-value có chủ đích: thêm setting mới **không cần migration DB**, chỉ thêm 1 string key.
- Các key hiện có (`UserSettingKeys`):
  - **UI**: `ui.date-format` (`dmy`/`mdy`/`ymd`), `ui.timezone` (rỗng = browser local, hoặc `utc7`/`utc0`/`utc-5`/`utc9`), `ui.theme` (`dark`/`light`/`system`), `ui.language` (`en`/`vi`/`ja`), `ui.compact-mode`.
  - **Notification**: `notify.email`, `notify.push`, `notify.digest`, `notify.mentions-only`.
  - **Profile**: `profile.avatar-url`.
  - **Accessibility**: `a11y.reduce-motion`.
- Avatar: `User.AvatarClass` (class Tailwind màu — fallback) vs `profile.avatar-url` (ảnh thật upload lên MinIO). Có avatar-url → dùng ảnh; rỗng/không có → dùng `AvatarClass`.

## 3. Business Rules & Invariants

- Setting luôn scope theo user — user chỉ đọc/ghi setting của chính mình.
- Upsert semantics: ghi 1 key đã tồn tại sẽ **update in-place**, không tạo dòng trùng.
- `Value = null` nghĩa là unset, app phải fallback về default — không coi null là giá trị hợp lệ.
- Key mới **phải** khai báo trong `UserSettingKeys`, không hardcode string rải rác.
- **Avatar upload 2 bước (presigned URL)** — binary **không đi qua API server**:
  1. **Request URL**: server tự sinh object key `avatars/{userId}/{unixMs}.jpg` (client **không** được tự chọn key), trả presigned PUT URL TTL **5 phút**. Timestamp suffix để cache-bust khi user thay avatar.
  2. Browser PUT file trực tiếp lên MinIO.
  3. **Confirm**: server verify rồi mới lưu URL.
- Rule bảo mật khi confirm:
  - **Path-traversal guard**: object key phải bắt đầu bằng đúng `avatars/{callerUserId}/` — không cho confirm key của user khác.
  - **Verify tồn tại thật**: HeadObject lên MinIO (chỉ metadata, zero data transfer) — browser **không thể** fake upload thành công.
  - Object không tồn tại → reject, yêu cầu upload lại.
- Thay avatar → **xoá file cũ** khỏi storage (nếu key cũ khác key mới) để tránh orphan object tích tụ.
- File ≥ 5 MB dùng multipart upload; mỗi part trừ part cuối phải ≥ 5 MB (ràng buộc S3/MinIO). Client cancel giữa chừng → abort để không đọng orphan part.
- Bucket avatar được ensure tồn tại (public-read) lúc startup.

## 4. Main Workflows / Use Cases

1. User mở Settings → `GET` settings → nhận key→value map → UI apply (thiếu key thì dùng default).
2. User đổi preference → upsert 1 hoặc nhiều key cùng lúc.
3. User upload avatar → request presigned URL → browser PUT lên MinIO → confirm → server verify (ownership + tồn tại) → xoá avatar cũ → lưu `profile.avatar-url` → trả public URL.
4. HUB đọc preference của user qua internal API `GET /internal/v1/users/{id}/settings` để honour date format/timezone. Xem [hub-integration.dod.md](hub-integration.dod.md).

## 5. Definition of Done

- [ ] Object key luôn do **server** sinh, client không bao giờ được chỉ định key.
- [ ] Confirm luôn check prefix `avatars/{callerUserId}/` trước khi lưu.
- [ ] Confirm luôn verify object tồn tại thật trong storage (không tin client).
- [ ] Thay avatar luôn xoá file cũ, không để orphan object.
- [ ] Presigned URL có TTL giới hạn (5 phút), không phát URL vĩnh viễn.
- [ ] Thêm setting mới không cần migration DB.
- [ ] Mọi key đọc/ghi đều qua constant trong `UserSettingKeys`.
- [ ] User không đọc/ghi được setting của user khác.

## 6. Edge Cases & Notes

- Object key format cố định `.jpg` — không phân biệt content type thật của file upload.
- Presigned URL hết hạn giữa lúc user chọn file xong mới upload → phải request URL mới.
- Xoá avatar cũ là best-effort; delete không throw nếu file không tồn tại.
- Bảng key-value đánh đổi: linh hoạt (không migration) nhưng **không có type safety ở tầng DB** — validate value phải làm ở application layer.
- `User.AvatarClass` vẫn được giữ và vẫn trả trong login result/internal API như fallback — không bị thay thế bởi avatar-url.
