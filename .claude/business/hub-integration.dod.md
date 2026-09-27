# HUB Integration — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-09-26 | 14:02 | Khởi tạo document | Tạo document business ban đầu cho feature HUB Integration — sprint channel link + internal API đã có trong code từ 2026-09-05 nhưng chưa được document |

---

## 1. Purpose

Kết nối DASHBOARD với **HUB Chat** để mỗi sprint có 1 kênh chat riêng, và cung cấp internal API cho HUB đọc thông tin user/membership/work-item. Mục tiêu: team thảo luận sprint trong HUB mà không phải tự tạo/quản lý channel bằng tay.

## 2. Key Entities & Relationships

- `SprintChannelLink`: quan hệ **1-1 với `Sprint`** — 1 sprint có tối đa 1 channel liên kết.
  - `SprintId` — sprint được tạo channel cho.
  - `HubChannelId` — ID channel bên HUB (`hub_chat.Channels`).
  - `HubChannelUrl` — deep-link vào HUB frontend (vd `http://localhost:4202/channels/{id}`), lưu sẵn để UI mở HUB mà không cần biết URL pattern.
- Mapping khái niệm: **DASHBOARD `Repository` = HUB workspace**; **DASHBOARD `Organization` = tenant context của HUB**.
- `SprintDto` trả thêm `HubChannelId` + `HubChannelUrl` (null nếu sprint chưa có channel).

## 3. Business Rules & Invariants

- **Toàn bộ call sang HUB là best-effort**: log lỗi và **không bao giờ throw** ra ngoài. HUB chết/timeout thì nghiệp vụ sprint/capacity vẫn thành công.
- Tạo sprint có cờ `CreateHubChannel` — chỉ provision channel khi cờ này bật **và** `HubChatBaseUrl` đã được config. Không config → bỏ qua im lặng.
- Provision channel có **timeout 5 giây**. Quá hạn hoặc lỗi → sprint vẫn được tạo, chỉ là không có channel link (`HubChannelId = null`).
- Sprint được commit **trước** khi gọi HUB — không có trường hợp tạo channel thành công mà sprint không tồn tại.
- Channel tạo ra là **Private**, người tạo sprint trở thành owner.
- **Sync capacity member → channel member** (fire-and-forget, không chờ kết quả):
  - Thêm `CapacityMember` vào sprint có channel → add user vào HUB channel.
  - Xoá `CapacityMember` → remove user khỏi HUB channel.
  - Sprint không có `SprintChannelLink` → không sync gì.
- Đóng sprint → archive channel liên kết (best-effort).
- **Internal API** (`/internal/v1/*`) dành riêng cho machine-to-machine qua internal Docker network:
  - Bảo vệ bằng header `X-Internal-Token` (`InternalApiKeyMiddleware`), **không dùng JWT**.
  - Read-only — không có endpoint nào ghi dữ liệu.
  - Không expose trên public Swagger.

## 4. Main Workflows / Use Cases

1. Tạo sprint với `CreateHubChannel = true` → commit sprint → gọi HUB find-or-create channel (timeout 5s) → lưu `SprintChannelLink` → trả `HubChannelId`/`HubChannelUrl` về UI.
2. Khai báo capacity member cho sprint đã có channel → fire-and-forget add user vào HUB channel.
3. Xoá capacity member → fire-and-forget remove user khỏi HUB channel.
4. Đóng sprint → archive HUB channel.
5. HUB resolve tên user: `GET /internal/v1/users/{id}` hoặc batch `GET /internal/v1/users?ids=...` (id không tồn tại bị bỏ qua im lặng, không lỗi).
6. HUB verify quyền workspace: `GET /internal/v1/users/{id}/memberships` → trả `IsGlobalAdmin`, `OrgId`/`OrgAlias`/`OrgName`, và list repository membership kèm role name + permission.
7. HUB dựng sidebar member của workspace: `GET /internal/v1/repositories/{id}/members`.
8. HUB đọc preference của user: `GET /internal/v1/users/{id}/settings` → key→value map, để HUB honour date format/timezone user đã set ở DASHBOARD. Xem [user-settings.dod.md](user-settings.dod.md).
9. HUB link 1 thread thảo luận vào work item: `GET /internal/v1/work-items/{id}` → trả key (`DASH-3`), title, state, repository.

## 5. Definition of Done

- [ ] Không có code path nào để lỗi HUB làm fail nghiệp vụ DASHBOARD (sprint/capacity luôn thành công).
- [ ] Mọi call sang HUB đều có timeout tường minh, không chờ vô hạn.
- [ ] Sprint được persist trước khi gọi HUB.
- [ ] Internal API luôn yêu cầu `X-Internal-Token`, không có endpoint nào để trống auth.
- [ ] Internal API chỉ read-only, không có endpoint ghi.
- [ ] Internal endpoint trả 404 rõ ràng khi user/work-item không tồn tại (không trả rỗng giả).
- [ ] `SprintChannelLink` luôn giữ đúng 1-1 với Sprint — không tạo trùng link cho cùng 1 sprint.

## 6. Edge Cases & Notes

- Sprint tạo trước khi tính năng này có → không có `SprintChannelLink`, UI phải xử lý `HubChannelId = null`.
- Sprint tạo lúc HUB down → vĩnh viễn không có channel cho tới khi có luồng retry/tạo lại thủ công (hiện **chưa có**).
- Sync member là fire-and-forget → nếu HUB từ chối, member lệch giữa capacity và channel mà không có báo lỗi cho user.
- `GET /internal/v1/users/{id}/memberships` project permission **trong memory** sau khi hydrate, vì `Role.AllowedFunctions` là JSON column (enum `.ToString()` không translate được sang SQL).
- Wiki đã **chuyển sang HUB** — DASHBOARD không còn feature Wiki. Xem [domain-business.md](domain-business.md).
