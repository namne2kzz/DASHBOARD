# System Info (About) — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-11-28 | 14:05 | Thêm HUB Chat, NMate, GitHub | Bổ sung 3 dòng service vào About: **HUB Chat** và **NMate AI** probe qua liveness endpoint (`/health`, `/health/live`); **GitHub** probe `api.github.com/rate_limit` (endpoint GitHub không tính vào quota) + đếm số Git connection đang cấu hình. Thêm trạng thái thứ 4 **`not-configured`** cho integration tùy chọn chưa khai báo URL — khác hẳn `unreachable`, vì tắt tính năng không phải là lỗi. |
| 2026-11-28 | 11:20 | Khởi tạo document | Tạo feature About: dialog hiển thị **build đang chạy** (version, build number, commit, ngày build) và **trạng thái thật của từng service** API đang kết nối (SQL Server, Redis, RabbitMQ probe thật; MinIO, SMTP chỉ khai báo cấu hình). Version lấy từ assembly, nguồn duy nhất là `DASHBOARD.csproj`. |

---

## 1. Purpose

Trả lời 2 câu hỏi mà trước đây phải vào server mới biết: **"production đang chạy build nào?"** và **"hệ thống đang kết nối được tới những gì?"**. Dùng khi nghi ngờ deploy chưa lên, hoặc khi một chức năng hỏng mà chưa rõ do app hay do hạ tầng (mất Redis, RabbitMQ chết...).

## 2. Key Entities & Relationships

- **Không có entity, không chạm database để lưu gì** — đây là read-only + probe.
- `AboutDto` gồm: thông tin build (`version`, `buildNumber`, `fullVersion`, `commitHash`, `buildDate`), môi trường (`environment`, `runtime`, `serverTimeUtc`), và danh sách `ServiceStatusDto`.
- `ServiceStatusDto`: `name`, `kind`, `status`, `detail`, `latencyMs`.

**Nguồn của version**: `DASHBOARD/DASHBOARD.csproj` → `VersionPrefix` (sửa tay mỗi lần release) và `BuildNumber` (CI truyền vào `-p:BuildNumber=...`, mặc định `0`). SDK stamp vào assembly lúc build; `AssemblyBuildInfoProvider` đọc ngược ra lúc chạy. **Không có chỗ thứ hai nào khai version** — `package.json` của frontend không được dùng cho mục đích này.

## 3. Business Rules & Invariants

1. **Phải đăng nhập mới xem được** (`[Authorize]`). Response có tên host nội bộ — không phát tán cho người lạ.
2. **Không probe nào được ghi dữ liệu.** Tất cả chỉ đọc hoặc chỉ mở kết nối:
   - SQL Server: đọc 1 bản ghi (`Take(1).AnyAsync`).
   - Redis: `GetAsync` 1 key không tồn tại — miss vẫn là câu trả lời hợp lệ, chứng minh round-trip thông.
   - RabbitMQ: `GetSendEndpoint` (resolve + kết nối host), **không publish message nào**.
3. **MinIO và SMTP chỉ báo "configured", không probe.** `IStorageService` chỉ có upload/delete/url — probe thật sẽ phải ghi 1 object. Mở kết nối SMTP mỗi lần mở dialog là một lần đăng nhập vào mail server. Hiển thị cấu hình mà không khẳng định "đang sống" là lựa chọn trung thực hơn.
4. **Mỗi probe timeout 3 giây, và chạy song song.** Ba host chết tốn 3 giây tổng, không phải 9. Dialog không bao giờ treo vô hạn.
5. **Probe hỏng không làm hỏng response.** Mọi exception biến thành 1 dòng `unreachable` kèm lý do; endpoint luôn trả 200.
6. **Chỉ gọi API khi người dùng mở dialog** — không gọi lúc app khởi động. Endpoint này đụng vào hạ tầng thật, không được chạy nền.
7. **`buildNumber = 0` nghĩa là build máy dev**, UI hiển thị "local build" thay vì số 0.

### Ý nghĩa 3 trạng thái

| Status | Nghĩa | Có latency? |
|--------|-------|-------------|
| `healthy` | Đã probe, có trả lời | Có |
| `unreachable` | Đã probe, lỗi hoặc quá 3s | Có (thời gian chờ) |
| `configured` | Có cấu hình, **cố ý không probe** | Không |
| `not-configured` | Integration tùy chọn, **chưa khai báo URL** → tính năng tắt ở deployment này | Không |

## 4. Main Workflows / Use Cases

1. **Xem build đang chạy**: sidebar → About → đọc Version / Build number / Commit / Built.
2. **Chẩn đoán sự cố hạ tầng**: mở About → nhìn dòng nào đỏ → biết ngay Redis hay RabbitMQ chết, kèm lý do và độ trễ.
3. **Kiểm tra lại sau khi hạ tầng được khôi phục**: bấm "Re-check" — probe chạy lại, không cần reload trang.

## 5. Definition of Done

- [x] Version hiển thị đúng giá trị khai trong `DASHBOARD.csproj`, không hardcode ở nơi khác.
- [x] CI truyền được build number qua `-p:BuildNumber=...` (đã verify: ra `FileVersion 1.0.0.482`).
- [x] Mọi service đang thật sự kết nối đều có mặt trong danh sách.
- [x] Không probe nào ghi dữ liệu hay gửi message.
- [x] Một service chết không làm hỏng dialog, chỉ hiện đỏ dòng đó.
- [x] Endpoint yêu cầu đăng nhập.
- [x] Dialog chỉ gọi API khi mở.

## 6. Edge Cases & Notes

- **Commit hash** do SDK tự thêm vào `InformationalVersion` sau dấu `+` (lấy từ git lúc build). CI nên truyền `-p:SourceRevisionId=$(Build.SourceVersion)` cho chắc chắn; không có thì vẫn chạy, chỉ là ẩn dòng Commit.
- **`buildDate` đọc từ last-write-time của file DLL.** Với single-file publish có thể không đọc được → trả `null`, UI ẩn dòng đó. Đây là xấp xỉ, không phải timestamp build chính thức.
- **Không hiển thị host của SQL Server**: `IApplicationDbContext` cố ý chỉ expose `Set<T>()`, connection string thuộc Infrastructure. Bản thân việc round-trip thành công đã là câu trả lời.
- **Namespace là `SystemInfo` chứ không phải `System`** (cả Application lẫn Controllers) — `System` trùng namespace gốc của .NET và gây lỗi resolve kiểu.
- Hai chỗ `shortcut:` trong `GetAboutQueryHandler` đánh dấu MinIO/SMTP chưa probe thật — nâng cấp khi `IStorageService` có thêm thao tác đọc, hoặc khi cần giám sát gửi mail.
