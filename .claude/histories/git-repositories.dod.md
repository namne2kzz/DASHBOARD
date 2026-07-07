# GitRepositories — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-07-07 | 01:00 | Wire Redis thật cho cache overview | Thay `IDistributedCache` từ in-memory (`AddDistributedMemoryCache`, per-instance) sang Redis thật (`docker-compose.yml` service `redis` + `Microsoft.Extensions.Caching.StackExchangeRedis`). Ảnh hưởng business: cache invalidation qua webhook (Phase 3) giờ có hiệu lực trên toàn hệ thống (mọi instance), không còn giới hạn "chỉ instance nhận webhook" như trước. |
| 2026-07-07 | 00:00 | Khởi tạo document + thêm webhook real-time sync | Tạo document business cho feature GitRepositories (Phase 1-2 đã merge trước đó nhưng chưa có doc), đồng thời thêm Phase 3: webhook receiver + HMAC verification + cache invalidation qua MassTransit. |

---

## 1. Purpose

Hiển thị dữ liệu GitHub (branches/commits/pull requests/rate-limit) cho tab "Repos" của 1 Project (`Repository`), dựa trên kết nối GitHub được cấu hình **hoàn toàn ở server-side config** (không có bảng DB, không có admin UI, không nhập token qua web) — mỗi Project có thể liên kết nhiều repo GitHub (vd FE + BE).

## 2. Key Entities & Relationships

- Không có entity DB mới. Kết nối được định nghĩa bởi `GitConnectionsOptions` (`Infrastructure/Settings/GitConnectionsOptions.cs`) — dictionary khoá theo `Repository.Code`, mỗi code map tới danh sách `GitConnectionEntry { RepoUrl, Token, DefaultBranchOverride, IsPrimary, WebhookSecret }`.
- `WebhookSecret` (thêm ở Phase 3): optional, chỉ set khi admin đã đăng ký webhook thật trên GitHub cho repo đó; dùng để xác thực request webhook gửi tới app.
- Liên kết với `Repository` (Domain/Entities) chỉ qua `Code` — không có foreign key DB.
- `GitSyncRequestedEvent` (`Application/Contracts/GitSyncRequestedEvent.cs`): message MassTransit nội bộ, không phải entity nghiệp vụ — chỉ mang `RepositoryId` + `RepoUrl` để trigger invalidate cache.

## 3. Business Rules & Invariants

- Token/WebhookSecret **không bao giờ** được trả về frontend hay ghi log.
- Xem tab Repos: chỉ cần là member của Project (`IsMemberOfAsync`), không cần quyền đặc biệt — vì dữ liệu trả về không chứa secret.
- Chỉnh sửa kết nối (thêm/xoá repo link, đổi token) là hành động **ops/deploy**, nằm ngoài hệ thống permission của app — không có Command nào để mutate config này.
- Dữ liệu overview cache 60s qua `IDistributedCache` (backend thật là Redis) theo key `git:overview:{repositoryId}:{repoUrl}` — tránh gọi GitHub API quá nhiều lần khi nhiều user cùng mở tab.
- **Webhook (Phase 3)**: mỗi request webhook GitHub gửi tới `POST api/webhooks/github/{repositoryId}` phải được xác thực bằng chữ ký HMAC-SHA256 (`X-Hub-Signature-256`), tính trên **raw body** với khoá là `WebhookSecret` của đúng connection khớp `repository.full_name` trong payload — dùng constant-time compare.
- Request webhook không xác thực được (sai chữ ký, không có connection khớp, hoặc connection không cấu hình `WebhookSecret`) → trả **401**, không publish gì cả.
- Request đã xác thực chữ ký nhưng event type không quan tâm (vd `ping` khi mới tạo webhook) → vẫn trả **200/204**, không publish — GitHub tự tắt webhook nếu nhận quá nhiều response không phải 2xx.
- Chỉ event `push` và `pull_request` mới trigger publish `GitSyncRequestedEvent` → consumer xoá cache overview tương ứng, request tiếp theo sẽ gọi GitHub live thay vì đợi hết 60s TTL.
- Không có mirror table nào được ghi khi nhận webhook — webhook chỉ có tác dụng invalidate cache sớm, dữ liệu thật vẫn luôn pull-on-demand từ GitHub.
- Admin phải tự đăng ký URL webhook trên GitHub (Settings → Webhooks) — app không tự động hoá bước này.

## 4. Main Workflows / Use Cases

1. User mở tab Repos của 1 Project → BE resolve `Repository.Code` → tra `GitConnectionsOptions` → nếu chưa cấu hình, trả `HasConnection = false` (empty state).
2. Có cấu hình → chọn connection (theo `RepoUrl` truyền vào, hoặc `IsPrimary`, hoặc entry đầu tiên) → gọi GitHub (Octokit) live, cache 60s.
3. Admin đăng ký webhook trên GitHub trỏ về `api/webhooks/github/{repositoryId}` cho từng repo con của Project, kèm secret nhập vào `GitConnectionEntry.WebhookSecret` trong config.
4. GitHub push/PR mới → gửi webhook → app verify chữ ký → nếu hợp lệ và là `push`/`pull_request` → publish `GitSyncRequestedEvent` → `GitSyncConsumer` xoá cache → lần load overview kế tiếp lấy dữ liệu mới nhất thay vì cache cũ.

## 5. Definition of Done

- [ ] Token và WebhookSecret không xuất hiện trong bất kỳ response/DTO/log nào.
- [ ] Overview trả `HasConnection=false` đúng khi Project chưa cấu hình connection nào.
- [ ] Webhook signature sai/thiếu → luôn 401, không publish event.
- [ ] Webhook event không thuộc `push`/`pull_request` (vd `ping`) → vẫn 200, không publish.
- [ ] Cache key bị xoá bởi `GitSyncConsumer` khớp chính xác format dùng ở `GetGitRepositoryOverviewQueryHandler`.

## 6. Edge Cases & Notes

- 1 Project có nhiều connection (nhiều repo GitHub) → mỗi repo đăng ký 1 webhook riêng, cùng trỏ về 1 route `{repositoryId}` — phân biệt bằng `repository.full_name` trong payload.
- Cache overview dùng Redis thật (`docker-compose.yml` service `redis`, `Microsoft.Extensions.Caching.StackExchangeRedis`) — chia sẻ giữa nhiều instance, nên invalidate qua webhook có hiệu lực toàn hệ thống chứ không chỉ 1 instance. Fallback in-memory (`AddDistributedMemoryCache`) được giữ dạng comment trong `Infrastructure/DependencyInjection.cs` để revert nhanh nếu cần.
- GitHub gửi cả `ping` event khi vừa tạo webhook — app phải trả 200 cho request này dù không làm gì, tránh bị GitHub tự động tắt webhook.
