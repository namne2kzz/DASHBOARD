# Attachments — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-11-07 | 15:39 | Khởi tạo document | Tạo document business ban đầu cho feature Attachments — đính kèm file vào work item qua MinIO presigned upload |

---

## 1. Purpose

Cho phép thành viên repository **đính file vào work item** (ảnh chụp lỗi, tài liệu đặc tả, log, bảng tính) và **dán ảnh trực tiếp vào mô tả**. File đi thẳng từ trình duyệt lên object storage (MinIO), backend chỉ giữ metadata và kiểm soát quyền — server không làm trung chuyển dữ liệu file.

## 2. Key Entities & Relationships

- **`WorkItemAttachment`** — một file đính kèm vào **một** `SprintTask`, trong **một** `Repository`.
- Field: `FileName` (tên gốc người dùng thấy), `ContentType`, `SizeBytes`, `ObjectKey` (vị trí thật trong storage), `PublicUrl` (link hiển thị), `UploadedById`, `CreatedAt`.
- Quan hệ: `SprintTaskId` → work item · `RepositoryId` → repository · `UploadedById` → `User` (người đăng, dùng để hiển thị tên + avatar và để xét quyền xóa).
- **Object key do server sinh**, người dùng không đặt được: `workitems/{RepositoryId}/{SprintTaskId}/{GUID}{ext}`. Phần mở rộng suy ra từ content type, không lấy từ tên file người dùng gửi lên.
- Bucket lấy từ cấu hình `MinioAttachmentBucket`.

## 3. Business Rules & Invariants

**Quyền**

- Mọi thao tác (xem, xin link upload, ghi nhận, xóa) đều yêu cầu **là member của repository** → không phải thì `ForbiddenException`.
- Work item phải tồn tại, thuộc đúng repository đó, và **chưa bị xóa mềm** (`!IsDeleted`) → không thì `NotFoundException`.
- **Xóa**: người đăng **luôn** xóa được file của mình; người khác cần quyền `ManageBoard`.

**File được chấp nhận**

Hai mức khác nhau, tùy cách đưa file vào:

| | Ảnh dán inline | File đính kèm thường |
|---|---|---|
| Giới hạn | **10 MB** | **50 MB** |
| Loại | PNG · JPEG · GIF · WebP | Thêm SVG, PDF, Word, Excel, PowerPoint, TXT, CSV, Markdown, JSON, XML, ZIP |

- Kích thước phải **lớn hơn 0**; file rỗng bị từ chối.
- Content type so khớp **không phân biệt hoa thường**, theo **danh sách cho phép** (allowlist) — loại không có trong danh sách thì bị chặn, kể cả khi vô hại.
- Vi phạm trả về `Result.Failure` kèm thông báo cho người dùng, **không ném exception** — đây là lỗi nhập liệu bình thường chứ không phải sự cố.

**Chống lạm dụng khi ghi nhận file**

- `ObjectKey` client gửi lên **bắt buộc bắt đầu bằng** `workitems/{RepositoryId}/{SprintTaskId}/`. Sai tiền tố → `"Invalid object key."` Rule này chặn path traversal và chặn việc gán file của work item khác sang work item này.
- Trước khi lưu, server **kiểm tra object thật sự tồn tại** trong storage (`HeadObjectAsync`). Không có → `"Upload not found. Please try uploading again."` Nhờ vậy không sinh ra bản ghi trỏ tới file không tồn tại.
- Chính sách file được **kiểm tra hai lần**: lúc xin link upload và lúc ghi nhận. Lần hai là lần quyết định, vì client có thể gọi thẳng bước ghi nhận.

**Link upload**

- Presigned URL sống **10 phút**. Hết hạn phải xin lại.

**Hiển thị**

- Danh sách sắp xếp **mới nhất trước** (`CreatedAt` giảm dần).
- Mỗi dòng kèm tên và `AvatarClass` của người đăng.

**Lịch sử**

- Thêm và xóa file đều ghi vào history của work item: `Attached file '{tên}'.` / `Removed file '{tên}'.`

## 4. Main Workflows / Use Cases

1. **Đính file** — người dùng chọn file → client xin presigned URL (`POST .../attachments/upload-url`) → client **PUT thẳng lên MinIO** → gọi tiếp để ghi nhận (`POST .../attachments`) → server xác minh object có thật rồi lưu metadata, ghi history.
2. **Dán ảnh vào mô tả** — cùng luồng nhưng cờ `InlineImage`, áp giới hạn chặt hơn (10 MB, chỉ 4 định dạng ảnh). Ảnh dán sinh ra `PublicUrl` để nhúng thẳng vào nội dung.
3. **Xem danh sách** — mở work item → thấy các file đã đính, kèm tên người đăng và thời điểm.
4. **Xóa** — người đăng hoặc người có `ManageBoard` xóa → xóa bản ghi, ghi history, rồi xóa object trong storage.

## 5. Definition of Done

- [ ] Chỉ member của repository thao tác được; work item phải tồn tại, đúng repo, chưa xóa mềm.
- [ ] Giới hạn đúng hai mức: ảnh inline 10 MB / file thường 50 MB; chỉ loại trong allowlist.
- [ ] File rỗng bị từ chối.
- [ ] `ObjectKey` sai tiền tố bị từ chối.
- [ ] Không tạo được bản ghi khi object chưa thực sự tồn tại trên storage.
- [ ] Người đăng xóa được file của mình; người khác cần `ManageBoard`.
- [ ] Thêm/xóa đều ghi history.
- [ ] File đi thẳng browser → MinIO, không qua server.

## 6. Edge Cases & Notes

- **Object mồ côi**: xin được presigned URL, upload lên, nhưng không gọi bước ghi nhận (đóng tab, mất mạng) → file nằm lại trong storage mà không có bản ghi nào trỏ tới. Hiện **chưa có cơ chế dọn**.
- **Xóa object là best-effort**: bản ghi xóa trước, xóa file trong storage bọc `try/catch` rỗng. Storage lỗi thì lệnh vẫn thành công và để lại object mồ côi — chấp nhận được vì bản ghi mới là nguồn sự thật.
- **`ExtensionFor` trả chuỗi rỗng** với loại không nằm trong danh sách map (Word, Excel, ZIP…). Object key của những file này **không có phần mở rộng** — không ảnh hưởng nghiệp vụ vì `FileName` gốc vẫn được giữ để hiển thị và tải về.
- **`PublicUrl` lưu cứng trong DB** tại thời điểm tạo. Đổi endpoint MinIO sau này thì các link cũ trỏ sai, cần migration dữ liệu.
- **Content type do client khai báo**, server không kiểm tra nội dung file thật. Người dùng cố tình có thể khai sai để lách allowlist — rủi ro được giới hạn bởi việc file nằm ở bucket riêng và object key do server sinh.
- Xóa work item **không xóa attachment** của nó theo cơ chế nghiệp vụ ở đây; phụ thuộc cấu hình cascade ở tầng DB.
- `image/svg+xml` nằm trong allowlist của file đính kèm nhưng **không** nằm trong allowlist ảnh inline — SVG đính kèm được, dán vào mô tả thì không.
