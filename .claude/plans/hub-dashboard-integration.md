# HUB ↔ DASHBOARD Integration — Plan & Feature Roadmap

> **Status**: Đang triển khai  
> **Last updated**: 2026-08-22  
> **Author**: Claude (review by Nam)

---

## 1. Architecture Overview

```
DASHBOARD (4201)          HUB (4202)
─────────────────         ────────────────────────────────────
Repositories     ←──────▶ Workspaces  (1 repo = 1 workspace)
Members          ←──────▶ Channel members
Sprints          ←──────▶ Sprint channels (planned)
SprintTasks      ←──────▶ Discussion threads (linked channels)

SSO: DASHBOARD JWT ──────▶ HUB (shared secret, token pass-through)
M2M: HUB services  ──────▶ DASHBOARD /internal/v1/* (X-Internal-Token)
```

### Workspace = Repository (1-to-1)
- Mỗi **KABAN repository** tương ứng với **1 workspace** trong HUB.
- `WorkspaceId` trong `hub_chat.channels` = `RepositoryId` trong `DASHBOARD.Repositories`.
- User chỉ thấy workspace nếu họ là member của repo đó (enforced ở `resolveWorkspace()` trong HUB shell).

### Channel Visibility Rules (đã implement)
| Channel type | Ai thấy |
|---|---|
| Public  | Mọi member của workspace |
| Private | Chỉ member của channel |
| DM / GroupDM | Chỉ người trong cuộc |

---

## 2. Đã làm (Done ✅)

### 2.1 SSO / Auth
- [x] DASHBOARD login → pass JWT qua `?t=&r=` URL params
- [x] HUB APP_INITIALIZER nhận token, lưu vào localStorage (raw string, không JSON.stringify)
- [x] Loop-breaker: counter-based (max 3 handoffs/30s)
- [x] DASHBOARD logout endpoint (`?logout=1&returnUrl=`) để HUB đồng thời logout cả 2

### 2.2 HUB Chat Core
- [x] Channels: CRUD (create, update, archive), public/private
- [x] Channel members: add, remove, leave, list
- [x] Messages: send, edit, soft-delete, cursor pagination
- [x] Reactions: add, remove
- [x] SignalR realtime: message received/edited/deleted, typing indicator, presence
- [x] Typing indicator UI (auto-clear sau 5s)
- [x] Members side panel với presence dots
- [x] Create channel dialog (MS Teams style)
- [x] Unread badge count (local tracking)

### 2.3 Shell Layout (Teams-inspired)
- [x] Sidebar: workspace header, CHANNELS list, DIRECT MESSAGES list, Notifications
- [x] Empty state khi chưa chọn channel
- [x] User footer với presence dot + logout

### 2.4 Internal API (DASHBOARD → HUB)
- [x] `GET /internal/v1/users/{id}` — user profile
- [x] `GET /internal/v1/users?ids=` — bulk user profiles
- [x] `GET /internal/v1/users/{id}/memberships` — repo memberships
- [x] `GET /internal/v1/work-items/{id}` — work item context

### 2.5 Seed Data
- [x] 5 channels (general, dev-chat, product/private, random, announcements)
- [x] 10 DASHBOARD users
- [x] 41 messages với timestamps tự nhiên

---

## 3. Bugs Fixed (Session này)

| Bug | Root Cause | Fix |
|---|---|---|
| SSO "Google Sign-In unavailable" hiện ngay khi mở 4201 | `ngAfterViewInit` set error khi Google SDK init fail | Suppress init error, chỉ show khi user click |
| Channels loading forever ở HUB | `InternalController` trả về `name`/`code` field names nhưng `DashboardGateway.Contracts.RepositoryMembership` expect `repositoryName`/`repositoryCode` → JSON deserialization fail → subscribe.next không fire | Align DTO field names, thêm error handler `loadingChannels.set(false)` |

---

## 4. Đang làm / Cần làm tiếp (Todo 🔄)

### 4.1 Sprint → Channel Integration (Priority: HIGH)
**Business rule**: Khi tạo sprint trong DASHBOARD, có thể tự động tạo một HUB channel cho sprint đó.

#### Backend (DASHBOARD side)
- [ ] **Sprint creation**: Thêm field `CreateHubChannel: bool` vào `CreateSprintCommand`
- [ ] Sau khi sprint saved, nếu `CreateHubChannel = true`:
  - Gọi HUB API `POST /api/v1/channels` với:
    - `workspaceId` = `sprint.RepositoryId`
    - `name` = `sprint-{sprint.Name}` (slugified)
    - `type` = `Private` (chỉ sprint members)
    - `linkType` = `Sprint`, `linkExternalId` = `sprint.Id`
  - Tạo `SprintChannelLink` record để tracking
- [ ] **Member sync**: Khi `AddSprintMember` / `RemoveSprintMember`:
  - Lookup `SprintChannelLink` để tìm channelId
  - Gọi HUB `POST /channels/{id}/members` hoặc `DELETE /channels/{id}/members/me`
  - Resilience: fire-and-forget với retry, không block sprint operation nếu HUB down

#### Frontend (DASHBOARD side)
- [ ] `CreateSprintDialog`: Thêm toggle "🔗 Create HUB channel for this sprint"
- [ ] Sprint detail page: Badge/link "Open in HUB" nếu có linked channel

#### Backend (HUB side)
- [ ] Thêm endpoint `POST /api/v1/channels/linked` (đã có `OpenLinkedThread` logic — hoàn thiện)
- [ ] Thêm `InternalSprintSync` endpoint để DASHBOARD push member changes
- [ ] `GET /api/v1/channels?linkType=Sprint&linkExternalId={sprintId}` — find sprint channel

#### Access control
- [ ] Member của sprint mới được vào channel của sprint đó
- [ ] Owner/Scrum master có thể add/remove manual
- [ ] Channel bị archive khi sprint closed

---

### 4.2 Access Control (Priority: HIGH)
- [ ] **Workspace access check**: Verify user is member of workspace trước khi return channels
  - Hiện tại ListChannels chỉ filter by `workspaceId`, không verify user có quyền vào workspace
  - Cần cross-check với `dir:member:{userId}` cache trong gateway
- [ ] **Private channel**: Đã implement ở ListChannels query (`Members.Any(m => m.UserId == actingUserId)`)
- [ ] **Audit log**: Track ai add/remove member, ai tạo/xóa channel

---

### 4.3 Direct Messages / Group Chat (Priority: MEDIUM)
- [ ] **DM**: `POST /api/v1/channels/dm` — find-or-create DM channel giữa 2 users
  - Type = `Dm`, tên = tên người kia (computed)
  - Không hiện trong public channel list
- [ ] **Group DM**: `POST /api/v1/channels/group-dm` — tạo group chat 3+ người
  - Type = `GroupDm`, cần đặt tên nhóm
- [ ] **DM button** trên member panel → navigate to or create DM channel
- [ ] **People picker** khi tạo Group DM

---

### 4.4 Notifications (Priority: MEDIUM)
- [ ] **@mention**: Parse `@username` trong message body, lưu vào `_mentions` list
  - Tạo `Notification` record khi có @mention
  - Push qua SignalR `NotificationReceived` event
- [ ] **Unread count server-side**: Hiện tại chỉ track local (reset khi refresh)
  - Lưu `LastReadAt` trong `channel_members`
  - Query `SELECT COUNT(*) FROM messages WHERE ChannelId=X AND CreatedAt > LastReadAt`
- [ ] **Push notification** (future): Web Push API khi tab không active

---

### 4.5 Work Item Discussion Thread (Priority: MEDIUM)
- [ ] `POST /api/v1/channels/linked` với `linkType=WorkItem`
- [ ] DASHBOARD work item detail panel: Show/hide HUB thread inline
- [ ] "Discuss in HUB" button ở work item card → open linked channel

---

### 4.6 UX / Polish (Priority: LOW-MEDIUM)
- [ ] **Message reactions** UI: Emoji picker, reaction bar dưới message
- [ ] **Message actions** on hover: Reply, Edit, Delete, Copy link
- [ ] **Thread/reply**: Nested messages (reply to a message)
- [ ] **Search**: Full-text search trong messages của workspace
- [ ] **File upload**: MinIO integration (upload ảnh/file vào message)
- [ ] **Message formatting**: Markdown render (đã có `MessageFormat.Markdown` enum)
- [ ] **Pinned messages**: Pin important messages trong channel
- [ ] **Channel settings**: Edit name/topic, manage members, archive

---

### 4.7 Presence (Priority: LOW)
- [ ] Hiện tại presence chỉ biết Online/Offline qua Redis TTL (heartbeat mỗi 30s)
- [ ] **Custom status**: "In a meeting", "On vacation", emoji status
- [ ] **Away / DND**: Auto-away sau 15 phút inactive

---

## 5. Feature Suggestions (Thực tế cần có)

### 🔥 High Value (nên làm sớm)
1. **Sprint channel auto-create + member sync** ← đang plan
2. **Server-side unread count** — không mất khi refresh
3. **@mention notifications** — critical cho team communication
4. **DM** — không thể thiếu trong team chat

### 💡 Medium Value (roadmap)
5. **Work item discussion thread** — gắn conversation với ticket
6. **Message search** — khó dùng nếu không có search
7. **File sharing** — MinIO đã setup sẵn
8. **Board/sprint event feed** — DASHBOARD actions tự post vào channel ("James moved task X to Done")

### 🎯 Nice to Have (tương lai)
9. **Voice/video meeting link** (không implement, chỉ paste link Zoom/Meet)
10. **Polls** trong channel
11. **Scheduled messages**
12. **Channel templates** — khi tạo sprint channel tự pin message template standup
13. **Activity feed** — log mọi thứ từ DASHBOARD vào một channel "activity" riêng

---

## 6. Technical Debt

| Item | Priority |
|---|---|
| HUB `dashboard-gateway` cần verify workspace access (không chỉ user profile) | HIGH |
| `InternalController` field name mismatch với Gateway Contracts → Fixed session này | ✅ |
| Unread count hiện là local-only (Signal trong shell) → Cần server-side | MEDIUM |
| Redis cache `dir:member:*` TTL 3 phút — có thể stale, cần invalidation event | LOW |
| `TypingIndicatorComponent` dùng `constructor()` thay `ngOnInit()` cho `takeUntilDestroyed` → Fixed | ✅ |

---

## 7. Implementation Order (Đề xuất)

```
Sprint A (Ngay bây giờ):
  1. Fix bugs (done ✅)
  2. Sprint → Channel auto-create (toggle trong CreateSprintDialog)
  3. Member sync (add sprint member → add to channel)
  4. Workspace access check

Sprint B:
  5. DM / Group DM
  6. Server-side unread count
  7. @mention + notifications

Sprint C:
  8. Work item discussion thread
  9. File upload (MinIO)
  10. Message reactions UI
```

---

## 8. API Contracts Reference

### HUB Internal API (called by DASHBOARD)
```
POST /api/v1/channels                        — create channel
POST /api/v1/channels/linked                 — find-or-create linked channel
POST /api/v1/channels/{id}/members           — add member
DELETE /api/v1/channels/{id}/members/{uid}   — remove specific member (admin only)
```

### DASHBOARD Internal API (called by HUB services)
```
GET /internal/v1/users/{id}
GET /internal/v1/users?ids=
GET /internal/v1/users/{id}/memberships   — nay trả thêm orgId/orgAlias/orgName (workspace=org)
GET /internal/v1/work-items/{id}
```

---

## 9. SESSION UPDATE — 2026-08-23 (Org era + Realtime/DM/UX overhaul)

> Cập nhật lớn: HUB chuyển sang mô hình **workspace = Organization**, sửa loạt bug realtime/DM nền tảng, và polish UX theo chuẩn chat hiện đại. Xem chi tiết multi-tenancy ở `org-multitenancy.md`.

## 9.1 Kiến trúc mới — Workspace = Organization

**Trước**: 1 workspace = 1 repository. **Nay**: 1 workspace = 1 **Org** (tenant), channel nhóm theo repo bên trong.

```
DASHBOARD internal /memberships  →  { orgId, orgAlias, orgName, repositories[] }
        │
        ▼ (gateway UserMemberships — thêm org fields)
HUB shell: workspace header = orgName; sidebar group channel theo TỪNG repo (collapse/expand)
        │  user chỉ thấy repo mình là member; public channel của repo đó; private/sprint cần là member
        ▼
Channel.WorkspaceId = repositoryId (giữ nguyên); DM/sprint scope xử lý ở query
```

- **Routing HUB**: URL **chỉ tới `/{orgAlias}/channels`** (KHÔNG có slug/id phía sau — bỏ `:slug`). Channel đang mở là **app state** (`ChannelService.selectedId`, persist localStorage → refresh mở lại đúng channel). `hubOrgRedirectGuard` resolve alias; deep-link cũ `/channels/{id}` (sprint "Open in HUB") vẫn chạy: guard **select** channel đó rồi redirect về URL sạch. Chưa chọn channel → empty state "No channel selected".
- **ListChannels**: trả channel repo (public-or-member) + **tất cả DM của user** (mọi workspace, dedupe ở shell).
- Org info: `InternalController` → gateway `UserMemberships` → FE `directory.model` (thêm orgId/orgAlias/orgName).

## 9.2 Bugs nền tảng đã fix (quan trọng)

| Bug | Root cause | Fix |
|---|---|---|
| **Add/Join member → 500** | `ChannelMember.Id` là Guid client-gen nhưng EF coi key store-gen → add child vào aggregate đã tracked → EF sinh **UPDATE** (0 rows) → `DbUpdateConcurrencyException` | `ValueGeneratedNever()` cho `ChannelMember.Id` |
| **Realtime msg không hiện (phải reload)** | FE `MessageReceivedEvent` model là `{message: MessageDto}` nhưng server gửi **flat** `{messageId, channelId, authorId, preview, sentAt}` → `e.message` undefined | Đổi model → flat; build `MessageDto` từ event (body=preview, dedupe theo id) |
| **Unread/typing sidebar + DM không chạy** | SignalR chỉ broadcast tới **group của channel**; channel-detail gọi `leaveChannel()` khi rời/chuyển → connection rời group → shell không nhận msg channel chưa mở | Bỏ `leaveChannel`; shell `JoinChannel` **tất cả** channel + DM lúc connect (effect) và giữ join |
| **DM một chiều / 2 channel riêng** | `openDm` client-side tạo channel theo tên người kia + addMember (đang hỏng) → mỗi người 1 channel, không chung | Backend **canonical DM** `POST /channels/dm/{userId}`: find-or-create theo cặp member, add cả 2; `ChannelDto.OtherUserId` để hiện tên đúng phía |
| **Typing hiện `User …0001`** | typing-indicator resolve tên qua `authors` map, người typing chưa post nên không có | typing-indicator tự `directory.getUser()` fetch tên |
| **Title workspace lệch "Workspace"** | `resolveWorkspace` chỉ set tên ở nhánh non-cache | Luôn set = `orgName` (workspace=org) |

## 9.3 UX đã cải thiện (chuẩn chat hiện đại)

- **Sidebar group theo repo** + collapse/expand, nút + New Channel per-repo (theo quyền).
- **Section "Online"** (org members đang online, loại người đã có DM — họ lên đầu DM list).
- **Unread**: tên channel/DM **đậm + badge số**; clear khi mở.
- **Typing indicator** ở sidebar (hàng channel/DM) + trong channel (đúng tên).
- **DM**: hiện tên người kia (không còn "dm-guid"); presence key theo user id; canonical 2 chiều.
- **Member count** ở header cập nhật ngay khi add/remove.
- **Message list**: auto-scroll xuống cuối (chỉ khi đang ở đáy), **date separator** (Today/Yesterday/…), **empty state** ("No messages yet"), grouping theo author+5phút, "edited", reactions.
- **Small wins (đợt 2)**: **tab badge** unread ("(N) Nexus HUB"), **âm báo** tin mới (throttle 2s, chỉ channel không active & không phải mình), **nút "jump to latest"** khi cuộn lên, **hover bubble xem giờ đầy đủ**.

## 9.4 Review: Plan (mục 4-5) vs Thực tế

| Roadmap item | Trạng thái |
|---|---|
| 4.1 Sprint → channel auto-create + member sync | ✅ DONE (SprintChannelLink + capacity sync + private channel) |
| 4.3 DM (find-or-create) | ✅ DONE (canonical DM, `POST /channels/dm/{userId}`) |
| 4.3 Group DM (3+) | ⛔ CHƯA (chỉ DM 1-1) |
| 4.6 Empty state, message grouping, date separator, auto-scroll | ✅ DONE (session này) |
| 4.6 Reactions UI (emoji picker) | 🔸 hiển thị reaction có; **picker chưa** |
| 4.6 Message actions hover (edit/delete/reply/copy) | ⛔ CHƯA |
| 4.6 Search trong messages | ✅ DONE (in-channel search panel) |
| 4.6 File upload (MinIO) | ⛔ CHƯA |
| 4.6 Markdown render | ⛔ CHƯA (enum có, render plain) |
| 4.4 Unread count | 🔸 client-side (mất khi refresh) — cần **server-side LastReadAt** |
| 4.4 @mention notifications | 🔸 notification hạ tầng có; **parse @mention chưa** |
| 4.2 Workspace access check (gateway) | 🔸 dựa membership; chưa hard-check ở gateway |

## 9.5 Đề xuất UX/business nên làm tiếp (ưu tiên cho trải nghiệm end-user)

**🔥 High (đáng làm sớm — chuẩn mọi chat trên thị trường):**
1. **Server-side unread + read state** (`LastReadAt` trong `channel_members`): unread không mất khi refresh; badge chuẩn đa thiết bị; "new messages" divider.
2. **Message actions on hover**: Reply, Edit, Delete, React, Copy link — thao tác cơ bản.
3. **Emoji reaction picker** (đã có hiển thị, thiếu picker để thả).
4. **@mention**: autocomplete khi gõ @, highlight, tạo notification.
5. ~~Browser tab badge + notification sound~~ ✅ DONE (đợt 2).

**💡 Medium:**
6. **File / image upload** (MinIO đã sẵn) + preview ảnh inline.
7. **Markdown render** (bold/italic/code/link) — `MessageFormat.Markdown` có sẵn.
8. **Group DM** (3+ người) + đặt tên.
9. **Work item discussion thread** inline ở DASHBOARD.
10. **Message pagination smooth**: giữ vị trí scroll khi load older (hiện prepend hơi nhảy).

**🎯 Nice to have:** custom presence status, pinned messages, activity feed từ DASHBOARD, polls.

## 9.6 API mới (session này)

```
POST /api/v1/channels/dm/{targetUserId}?workspaceId=   — find-or-create canonical DM (2 member)
PUT  /api/v1/channels/{id}/members/{userId}            — add member (đã fix 500)
DELETE /api/v1/channels/{id}/members/{userId}          — remove member (ManageChannels/owner/admin)
```
ChannelDto thêm `OtherUserId` (DM). Login DASHBOARD nay cần `orgAlias`; JWT mang claim `org`.

---

## 10. CHAT REDESIGN & EXTENSIBLE BASE (Plan — 2026-08-24)

> Nâng HUB chat lên chuẩn team-chat hiện đại (tham chiếu Slack/Discord/Teams), **thiết kế base đủ mở** để thêm dần: file/img, view file, markdown/rich, reaction, edit, thread, reply, forward, pin, call/video, calendar/meeting **mà không đập đi xây lại**. Status: Draft — chờ Nam chốt §10.8.

### 10.1 Nguyên tắc kiến trúc (để không phải đập lại)
1. **Message = content model có cấu trúc**, không chỉ 1 string: `body` (Markdown, portable) + `attachments[]` + `reactions[]` + `parentId` (thread) + `replyToId` (quote) + `editedAt` + `forwardedFromId?`. Lưu **Markdown** làm nguồn sự thật; rich toolbar chỉ là cách nhập.
2. **Composer trừu tượng** phát ra `{ body, format, attachmentIds?, replyToId?, parentId? }` → thêm format/nút là additive, không đổi contract.
3. **Right panel = container tab** (About/Members/Files/Pins/…) — thêm tab additive.
4. **Message hover-actions = slot** (react/reply/thread/edit/delete/forward/copy/pin) bật theo quyền.
5. **Realtime event bất biến shape** (flat); thêm event mới (reaction/edit/delete/thread) additive.
6. **Attachment tách khỏi message body** (bảng riêng + MinIO).
7. **Feature-flag mềm**: action mới ẩn sau `can*()` (quyền + config).

> Base hiện đã khá sẵn: `Message` có `ParentId` (thread), `Format` (Plain/Markdown), `Mentions`, `Reactions`, `EditedAt`. Chỉ cần *dùng* + bổ sung `attachments`, `replyToId`, endpoint edit/react/thread.

### 10.2 Research pattern chat hiện đại
| Vùng | Pattern chuẩn |
|---|---|
| Composer | Rich text + markdown shortcut: **B/I/U/S**, link, bullet/numbered, quote, inline code, **code block (```)**, emoji, @mention, /slash, attach. Enter=send, Shift+Enter=newline. |
| Code block | Gõ ``` (hoặc nút `</>`) → khối monospace nhiều dòng, giữ whitespace, optional language (như hình Slack). |
| Message | markdown render, reactions bar, "edited", thread reply count, hover actions. |
| Hover actions | React · Reply-in-thread · Forward · Copy link · More(edit/delete/pin). |
| Right panel ("...") | Tabbed: **About** (topic/description/created/leave/ID), **Members** (add/remove/role), Files, Pins, Settings, Automations. |
| Thread | `parentId`; thread pane phải; reply count + participants; "also send to channel". |

### 10.3 Yêu cầu cụ thể (Nam nêu)
- **Header: icon Members → nút "..."** mở `ChannelInfoPanel` có tab: **Tab1 About** (topic/description edit, created by, leave, channel ID) · **Tab2 Members** (search + list role/presence + Add org-member + Remove theo quyền — tái dùng `channel-members-panel`). Chừa sẵn Files/Pins/Settings.
- **Composer format**: gửi Markdown; toolbar B/I/S/link/list/quote/inline-code/**code block**/emoji/@; markdown shortcut; **gõ ``` → chuyển code-block mode** (monospace nhiều dòng, optional language, Enter=newline, nút Send). Render Markdown **sanitize** (chống XSS) ở message-list.
- **Message hover actions**: Copy · Reply-in-thread · React (emoji picker) · Edit/Delete (của mình; admin xoá của người khác).

### 10.4 Data model & API (đặt chỗ trước, làm dần)
```
Message { Id, ChannelId, AuthorId, Body(Markdown), Format,
          ParentId?(thread,có), ReplyToId?(mới), ForwardedFromId?(mới),
          Mentions[], Reactions[](có), EditedAt?, CreatedAt, DeletedAt?(soft) }
Attachment { Id, MessageId, Kind(Image|File|Video), Url(MinIO), Name, Size, Mime, Width?, Height? }

POST   /channels/{id}/messages  { body, format, parentId?, replyToId?, attachmentIds? }
PATCH  /channels/{id}/messages/{mid}     edit   → realtime messageEdited(có)
DELETE /channels/{id}/messages/{mid}     soft delete → messageDeleted(có)
POST   /channels/{id}/messages/{mid}/reactions {emoji} · DELETE .../{emoji}  → reactionChanged(mới)
GET    /channels/{id}/messages/{mid}/thread
POST   /channels/{id}/messages/{mid}/forward {toChannelId}
POST   /channels/{id}/attachments  (Media service/MinIO) → {id,url}
POST/DELETE /channels/{id}/pins/{mid}
PATCH  /channels/{id} {name,topic,description}  (About edit — có)
```
Realtime additive: `reactionChanged`, `threadReplied`, `pinChanged`.

### 10.5 Frontend component slots
```
ChannelDetailPage
├── ChannelHeader ("..." → ChannelInfoPanel[About|Members|Files|Pins])
├── MessageList → MessageRow (slot: hover-actions, reactions bar, attachments, reply-quote, thread-summary)
├── ThreadPane (mở khi Reply-in-thread; parentId)
└── Composer → { body(md), attachmentIds?, replyToId?, parentId? }
     ├── FormattingToolbar (B/I/S/link/list/quote/code/codeblock/emoji/attach)
     ├── CodeBlockMode (gõ ```)
     ├── MentionAutocomplete (@)  └── (future) SlashCommands (/)
```

### 10.6 Roadmap
- **Phase 1** — ✅ **DONE 2026-08-24** (chốt 10.8: composer=textarea+shortcut, panel=modal center, thread/file/markdown-nâng-cao để sau):
  - ✅ Header icon → **"..." → modal center** `ChannelInfoPanel` tab **About** (topic edit, created, channel ID copy, leave) + **Members** (add/remove, reuse `channel-members-panel`).
  - ✅ Composer rich: toolbar (B/I/S/link/bullet/numbered/quote/inline-code/**code-block**) + markdown shortcut; **gõ ``` → code-block mode** (monospace, language, Ctrl+Enter gửi); emit `ComposerSubmit {body, format=Markdown}` (contract mở sẵn cho attachments/reply).
  - ✅ Render **Markdown sanitize** (subset: code block/inline/bold/italic/strike/link/bullet/numbered/quote/`\n`) ở message-list (`markdown.util.ts`, Angular sanitize khi bind).
  - ⏳ Hover actions (Copy/Reply-thread/React/Edit/Delete) — để đợt sau cùng endpoint edit/delete/reaction.
- **Phase 2**: upload file/ảnh (MinIO) + preview/view; reactions đầy đủ + realtime; forward.
- **Phase 3**: thread pane đầy đủ; pin + tab Pins; @mention autocomplete + notification; unread server-side (`LastReadAt`) + "new messages" divider.
- **Phase 4**: markdown/rich đầy đủ + /slash; call/video/huddle (link trước, WebRTC sau); calendar/meeting + schedule message; server-side search + Files tab.

### 10.7 Base cần làm ngay (để sau đỡ đập) — ✅ **DONE 2026-08-24**
1. ✅ **Migration Postgres** `AddReplyForwardAttachments` (đã apply hub_chat): bảng `attachments` (Kind/Url/Name/Size/Mime/W/H, FK message, cascade) + cột `Message.ReplyToId`, `Message.ForwardedFromId` (DeletedAt đã có sẵn từ trước). Entity `Attachment` + `AttachmentKind` + `Message.AddAttachment()`. **+ convention `ValueGeneratedNever` cho mọi domain Id** (chặn bug UPDATE-instead-of-INSERT khi add reaction/attachment/member vào aggregate tracked).
2. ✅ **DTO/Model**: `MessageDto` thêm `ReplyToId`, `ForwardedFromId`, `Attachments[]` (+`AttachmentDto`); `ParentId`/`Reactions` đã có. Mapping + `ListMessages` include Attachments. FE `message.model` đồng bộ (+ `AttachmentKind`, `AttachmentDto`).
3. ✅ **Composer contract** `ComposerSubmit { body, format, attachmentIds?, replyToId?, parentId? }` (fields reserved, chưa wire).
4. ✅ **Render pipeline** qua sanitizer (`markdown.util` + Angular `[innerHTML]` sanitize).
5. ✅ **Right panel** `ChannelInfoPanel` tab-based (About/Members) — khung nhét Files/Pins/Threads sau.

> Kết quả: schema + DTO + contract đã "đặt chỗ" cho reply/forward/attachments/thread → thêm các feature này về sau **không cần migration/đập model**.

### 10.9 Sidebar icons + User settings hub — ✅ **DONE 2026-08-24**
Theo yêu cầu Nam ("channel private→lock, public→#, group→người, dm→avatar" + "settings user, status, scheduler nhận mng"; **avatar upload để sau**):
1. ✅ **Channel type icons**: sidebar repo-group (Public `#` / Private 🔒 lock / GroupDm nhóm-người) + DM section (Dm avatar chữ-cái, GroupDm icon nhóm) + header channel-detail đồng bộ theo `ch.type`.
2. ✅ **Presence status (Active/Away/Do Not Disturb)** — backend Realtime:
   - `PresenceStatus` thêm `DoNotDisturb=3`; Redis thêm key `manual:{uid}` (override 7 ngày) tách khỏi `presence:{uid}` (TTL 90s theo connection). `AddConnection`/`Heartbeat` honor override; `SetManualStatusAsync` set/clear (Active=clear).
   - `ChatHub.SetStatus(int)` broadcast `presenceChanged` với status effective. Online broadcast dùng status thật thay vì hardcode "Online".
   - FE: `realtime.setStatus()`, `presence.PresenceStatus` +DoNotDisturb, `UserSettingsService` (status persist localStorage, **re-apply mỗi lần (re)connect** qua effect).
3. ✅ **Quiet hours scheduler (client-side)**: `UserSettingsService.quietHours{enabled,start,end}` + computed `muted` (DND **hoặc** trong khung giờ, wrap qua nửa đêm). Shell gate `playBeep()` bằng `!settings.muted()`. Persist localStorage (per-device), tick mỗi phút.
4. ✅ **Settings tab** (sidebar): profile card + dot theo status, status picker 3 preset, toggle quiet-hours + 2 time input, connection, logout. Rail avatar dot dưới cùng theo `ownStatusDot()` (green/yellow/red/grey).

> **Chưa làm** (Nam hoãn): avatar upload thật (cần MinIO + `User.AvatarUrl`), custom status text/emoji (cần persist backend → hiện chỉ 3 preset), server-side DND enforcement cho Notification service (hiện quiet-hours chỉ mute client: sound + popup; unread count vẫn đếm như Slack DND).

### 10.10 Channel membership, visibility & ownership — ✅ **DONE 2026-08-26**
Loạt rule về quyền trong channel (kèm fix bug SprintChannelLink + gỡ Wiki cùng ngày):
1. ✅ **Public non-member gating**: public channel cho **mọi repo member xem** (ListMessages không chặn public), nhưng **gửi/react cần membership** (backend 403). FE: non-member thấy **join-bar** thay composer + nút **Join channel**; info modal About đổi **Leave→Join** theo `channel.isMember`. Endpoint self-join `POST /channels/{id}/members` (đã có).
2. ✅ **Owner rules**: thêm `ChannelDto.MyRole` (GetChannel + ToDto). Owner **không được Leave** (ẩn nút, hiện note "transfer ownership to leave"); Members tab **ẩn dấu X** trên owner (`canManage() && canRemoveRow()`).
3. ✅ **Owner Settings tab** (chỉ owner, non-DM): tab thứ 3 trong info panel. Đầu tiên = **Visibility picker Public↔Private** → `Channel.ChangeVisibility()` + owner-only `ChangeChannelVisibility` command + `PATCH /channels/{id}/visibility`. Header icon (#/lock) update live. Khung tab mở sẵn để thêm setting khác.
4. ✅ **Transfer ownership**: `Channel.TransferOwnership(from,to)` (new owner→Owner, old owner→**Admin**) + owner-only command + `PUT /channels/{id}/owner`. FE: nút 👑 crown trên mỗi member row (chỉ owner thấy, không hiện trên chính mình/owner khác) → confirm → transfer → refetch channel (MyRole=Admin) → Settings tab biến mất, Leave hiện lại.

> Verified live (Playwright): visibility toggle đổi header icon; crown chỉ hiện đúng 1 lần trên non-owner. **Base role model** (Owner/Admin/Member + MyRole trên DTO) sẵn cho các owner-setting tương lai (archive, slow-mode, default-role...).

### 10.8 Cần Nam chốt
1. Composer editor: (a) **textarea + markdown-shortcut** (nhẹ, đúng "gõ ``` ra code block" — *khuyến nghị P1*) hay (b) WYSIWYG (Tiptap/ProseMirror — đẹp nhưng nặng + CSP)?
2. Panel "...": **modal center** (như Slack) hay side panel (hiện tại)? — khuyến nghị modal center.
3. Thread: base ngay P1 (mở pane + reply) hay để P3?
4. Markdown scope P1: bold/italic/strike/inline-code/**code block**/bullet/numbered/quote/link/mention — đủ chưa?
5. Attachments (file/img): kéo lên P1 cùng composer hay giữ P2?
