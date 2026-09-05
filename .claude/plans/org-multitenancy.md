# Organization Multi-Tenancy — Plan (DASHBOARD + HUB)

> **Status**: Confirmed — sẵn sàng impl P1
> **Created**: 2026-08-23 · **Updated**: 2026-08-23 (sau vòng confirm §11)
> **Author**: Claude (review by Nam)
> **Model**: Opus 4.8

Đưa khái niệm **Organization (Org)** làm tenant cấp cao nhất: 1 Org = 1 công ty mà Nexus bán license. Org sở hữu nhiều Repository và nhiều User. HUB workspace = Org. Hệ thống multi-tenant thực thụ: 2 user ở 2 org khác nhau có thể trùng email.

---

## 1. Hiện trạng (facts đã verify trong code)

| Điểm | Thực tế hiện tại |
|---|---|
| User | `User` global — `IX_Users_Email` **unique toàn hệ thống** ([UserConfiguration.cs:40](../../DASHBOARD/Infrastructure/Persistence/Configurations/UserConfiguration.cs#L40)) |
| Login | **Global theo email**, không cần org ([LoginCommandHandler.cs:30](../../DASHBOARD/Application/Auth/Commands/Login/LoginCommandHandler.cs#L30)) |
| Membership | `RepositoryMember` = join User↔Repository (RoleId + DefaultRole) ([RepositoryMember.cs](../../DASHBOARD/Domain/Entities/RepositoryMember.cs)) |
| Repository | Chưa có FK tới Org ([Repository.cs](../../DASHBOARD/Domain/Entities/Repository.cs)) |
| GlobalAdmin | `User.IsGlobalAdmin` bypass mọi repo-permission check |
| Route DASHBOARD | `4201/{repoCode}/...` + global routes `/settings`, `/my-work`, `/profile` ([app.routes.ts:71](../../DASHBOARD.VIEW/src/app/app.routes.ts#L71)) |
| Route HUB | `4202/channels/...`; workspace = 1 repository |
| Channel | `Channel.WorkspaceId` = repositoryId |
| Sprint channel | `SprintChannelLink` + capacity→channel-member sync đã có |

**Kết luận**: model đã tách `User` (identity) + `RepositoryMember` (membership) → thêm Org không đập bỏ, chỉ gắn `OrgId`, đổi email unique thành per-org, và thêm org vào login.

---

## 2. Quyết định đã chốt

1. **User = Org-scoped**: `User.OrgId` required; email unique **(OrgId + Email)**. 1 người ở 2 org = 2 account. "Sync user từ repo khác cùng org" = chọn org-user có sẵn → thêm `RepositoryMember`.
2. **Login phải xác định Org** (multi-tenant): form login = **Org alias + Email + Password** (giống AWS chọn account/region). JWT mang thêm `orgId`.
3. **Không có cross-org super admin**: `IsGlobalAdmin` = admin **của 1 org** (bao trùm mọi quyền trong org đó). Vì mỗi user chỉ thuộc 1 org, việc bypass "toàn bộ" tự nhiên gói trong org của họ. **Quản lý Org (profile/license) chỉ `IsGlobalAdmin`.** → Không thêm `IsOrgAdmin`, không thêm `SystemFunction.ManageOrg`.
4. **Onboarding / license / tạo Org = SERVICE RIÊNG (tương lai)** — giống mô hình HUB. Lý do: chicken-and-egg (chưa có user thì không login được để tạo org). Sẽ có 1 service onboarding riêng quản lý license + tạo org + tạo admin đầu tiên; onboard xong mới qua DASHBOARD login. **DASHBOARD KHÔNG chứa luồng create-org / license.** Giai đoạn hiện tại: chỉ dựa vào **seed default org** cho data sẵn có. (Đã build rồi gỡ lại create-org onboarding trong DASHBOARD — 2026-08-23.)
5. **New Repo dialog KHÔNG có field org**: repo tự thuộc org context hiện tại (từ URL alias / user đang login) + có FK. Dialog chỉ **hiển thị readonly** "Repo này thuộc Org X".
6. **License key = base64 opaque** (không decrypt ra info; info nằm ở appsettings cạnh key). Mỗi entry có `active` (`true`=đang có org dùng, `false`=chưa ai dùng). **Nam tự maintain** flag khi test. Validate tạo org: key tồn tại + chưa hết hạn + `active==false` + chưa bind org nào. Seed migration: pick key đầu tiên → org default dùng → set `active=true`.
7. **Alias** prefix **full route** của mọi function trong org (phân biệt org), cả 2 app. `Org.Alias` **unique toàn hệ thống**.
8. **Repo.Code** unique **per-org** (2 org được trùng code).
9. **Migration = EF backfill giữ data** (default Org gắn toàn bộ repos/users/channels).
10. **Sprint channel** = Private + member sync theo sprint capacity (tái dùng rule private).

---

## 3. Target Architecture

```
                         ┌─────────────────────────────────────┐
                         │            ORG (tenant)             │
                         │  name · alias(uniq) · contactEmail  │
                         │  about · licenseKey(uniq) · dates   │
                         │  repoCapacity                       │
                         └───────────────┬─────────────────────┘
                             1            │            1
                  ┌──────────────────────┼──────────────────────┐
                  │ *                     │ *                     │
            ┌───────────┐          ┌───────────┐                 │
            │   User    │          │Repository │                 │
            │  OrgId FK │          │  OrgId FK │                 │
            │ email uniq│          │ Code uniq │                 │
            │ per (org) │          │ per (org) │                 │
            │ IsGlobal- │          └─────┬─────┘                 │
            │ Admin     │                │                       │
            └─────┬─────┘                │                       │
                  │  *          *        │                       │
                  └──── RepositoryMember ┘                       │
                        (Role, DefaultRole)                      │
   HUB (4202)                                                    │
   ─────────────────────────────────────────────────────────── │
   Workspace = ORG ◀──────────────────────────────────────────  ┘
   Channel.OrgId (workspace) + Channel.RepositoryId (grouping)
```

### URL (alias = full-route prefix)
- DASHBOARD: `4201/{orgAlias}/{repoCode}/...`, `4201/{orgAlias}/settings/...`, `4201/{orgAlias}/my-work`
- HUB:       `4202/{orgAlias}/channels/...`
- Onboarding (không alias): `4201/create-org`, `4201/login` (login có field org alias)

---

## 4. Domain model changes

### 4.1 Org (mới) — DASHBOARD / SQL Server
```csharp
public sealed class Org : BaseEntity
{
    public string Name         { get; set; }
    public string Alias        { get; set; }   // unique toàn hệ thống, url-safe lowercase
    public string ContactEmail { get; set; }
    public string About        { get; set; } = "";

    // License snapshot copy tại lúc activate
    public string   LicenseKey          { get; set; }   // unique — bind 1 key ↔ 1 org
    public DateTime LicenseDueDate      { get; set; }
    public DateTime LicenseExpireDate   { get; set; }
    public int      LicenseRepoCapacity { get; set; }
}
```
Index: `Alias` unique, `LicenseKey` unique.

### 4.2 User — thêm field
```csharp
public Guid OrgId { get; set; }   // FK → Org (required)
// IsGlobalAdmin: giữ nguyên field, nay hiểu là "org admin" (mỗi user thuộc 1 org)
```
- Index: bỏ `IX_Users_Email` global → `IX_Users_Org_Email` unique **(OrgId, Email)**.

### 4.3 Repository — thêm field
```csharp
public Guid OrgId { get; set; }   // FK → Org (required)
```
- Uniqueness `Code`: unique **(OrgId, Code)**.
- Capacity: khi tạo repo, `count(repos of org) < org.LicenseRepoCapacity` (chỉ `IsGlobalAdmin` tạo repo / hoặc quyền `ManageRepo` sẵn có — giữ như hiện tại).

### 4.4 HUB Chat — Channel
```csharp
public Guid OrgId        { get; set; }   // = workspace
public Guid RepositoryId { get; set; }   // grouping trong workspace
```
Migration: `RepositoryId = WorkspaceId` cũ; `OrgId` = org của repo (backfill = default org).

---

## 5. License mechanism

### 5.1 appsettings (dev, Nam tự maintain)
```jsonc
"Licensing": {
  "Licenses": [
    // key = base64 opaque; info nằm ở đây, KHÔNG nhúng trong key
    { "key": "TkVYLTAwMDEtUFJPRA==", "dueDate": "2026-12-31", "expireDate": "2027-12-31", "repoCapacity": 20, "active": true  }, // default org dùng (seed)
    { "key": "TkVYLTAwMDItVEVTVA==", "dueDate": "2026-12-31", "expireDate": "2027-12-31", "repoCapacity": 10, "active": false },
    { "key": "TkVYLTAwMDMtVFJJQUw=", "dueDate": "2026-09-30", "expireDate": "2026-12-31", "repoCapacity": 3,  "active": false }
  ]
}
```

### 5.2 Abstraction (để sau nối license server)
```csharp
public interface ILicenseProvider
{
    Task<LicenseInfo?> ResolveAsync(string key, CancellationToken ct); // null nếu không tồn tại
}
public sealed record LicenseInfo(string Key, DateTime DueDate, DateTime ExpireDate, int RepoCapacity, bool Active);
```
- Impl: `AppSettingsLicenseProvider` (đọc `Licensing:Licenses`).

### 5.3 Validate khi Create Org
1. `ResolveAsync(key)` ≠ null.
2. `ExpireDate >= today`.
3. `active == false` (chưa ai dùng) — ledger do Nam maintain.
4. Chưa có `Org.LicenseKey == key` (hard guard qua unique index).
→ Tạo Org (copy snapshot dates/capacity) + first admin user. App **không** ghi appsettings; Nam tự set `active=true` sau khi test.

---

## 6. HUB visibility rules

| Cấp | Điều kiện thấy |
|---|---|
| Workspace (Org) | `User.OrgId == org.Id` |
| Repo trong workspace | User là `RepositoryMember` của repo |
| Public channel | User là member của repo chứa channel |
| Private channel | User là member của channel |
| Sprint channel | = Private, member sync theo capacity → dùng rule private |

---

## 7. Changes theo layer

### DASHBOARD Backend
- **Domain**: `Org`; thêm field `User.OrgId`, `Repository.OrgId`.
- **Persistence**: `OrgConfiguration`; update `UserConfiguration` (index (OrgId,Email)), `RepositoryConfiguration` (OrgId + (OrgId,Code)); EF migration backfill (§9).
- **Settings**: `LicensingOptions`; `AppSettingsLicenseProvider : ILicenseProvider`.
- **Auth**:
  - `LoginCommand` thêm `OrgAlias`; handler resolve org theo alias → tìm user `(orgId, email)` → verify. Áp cho cả `GoogleLogin`.
  - `TokenService.GenerateToken` thêm claim `orgId`; `IRequestUserContext` expose `OrgId`.
- **Application/Orgs** (mới): `CreateOrganization` (license-gated, tạo org + admin user), `UpdateOrganization`, `GetOrgByAlias`, `ValidateLicenseKey` (preview trước khi tạo).
- **Application/Members**: `ListOrgUsersNotInRepo` + `SyncOrgUserToRepo` (add existing org user vào repo).
- **Application/Repositories**: `CreateRepository` set `OrgId` từ context + enforce capacity.
- **Controllers**: `OrgsController`, update `RepositoriesController` / `MembersController` / `AuthController`.
- **Internal API**: `/internal/v1/users/{id}/memberships` trả thêm org info (orgId, alias, name).

### DASHBOARD Frontend
- **Onboarding**: `/create-org` — paste license → `validate-license` (preview dates/capacity) → nhập org (name/alias/contactEmail/about) + admin (name/email/password) → create → redirect `/{alias}/login`.
- **Login page**: thêm field **Org alias** (pre-fill nếu vào từ `/{alias}/login`); gửi kèm login.
- **Routing**: bọc route dưới `:orgAlias` + `orgContextGuard`; update mọi `routerLink`/navigate.
- **New Repo dialog**: bỏ field org; hiển thị readonly "Thuộc Org X".
- **Members page**: add member 2 mode — "Create new user" | "Add existing org user".
- **Org settings page** (`IsGlobalAdmin`): license info + repo usage (đã dùng/cap).

### HUB Backend (Chat)
- **Channel**: thêm `OrgId` + `RepositoryId`; migration backfill (Postgres).
- **Queries**: `ListChannels` filter theo `OrgId` + repos user là member + visibility; group theo repo.
- **CreateChannel**: nhận `orgId` + `repositoryId`.
- **Gateway/DirectoryService**: memberships trả org info.

### HUB Frontend
- **Shell `resolveWorkspace()`**: workspace = org của user; load repos user là member → group; load channels theo repo.
- **Sidebar**: group channel theo Repository.
- **Routing**: `/{orgAlias}/channels/...` + guard.
- **channel.model**: `workspaceId` → `orgId` + `repositoryId`.

---

## 8. Sequences

### 8.1 Create Org (onboarding)
```
Visitor  → POST /api/v1/orgs/validate-license { key }
         ← 200 { dueDate, expireDate, repoCapacity }  (nếu tồn tại, chưa hết hạn, active=false, chưa bind)
Visitor  nhập org info + admin credentials
         → POST /api/v1/orgs { key, name, alias, contactEmail, about, admin{name,email,password} }
         ← re-validate → tạo Org + User(IsGlobalAdmin, OrgId) → 201
         → redirect /{alias}/login
(Nam set active=true cho key trong appsettings)
```

### 8.2 Login (multi-tenant)
```
User → POST /api/v1/auth/login { orgAlias, email, password }
     → resolve Org theo alias → find User (orgId, email) → verify PBKDF2
     → JWT { sub, email, orgId } → 200
```

---

## 9. Migration (EF backfill, giữ data)

### DASHBOARD (SQL Server)
1. Tạo bảng `Orgs`; insert **default Org** (alias `default`, licenseKey = key#1 trong appsettings, copy dates/capacity). Set key#1 `active=true` trong appsettings.
2. `User.OrgId`, `Repository.OrgId`: add nullable → UPDATE = default org → alter NOT NULL.
3. Drop `IX_Users_Email` → create unique `(OrgId, Email)`.
4. `Repository`: unique `(OrgId, Code)`.
5. Existing global admin giữ `IsGlobalAdmin=true` (nay là admin của default org).

### HUB Chat (Postgres)
1. `channels.RepositoryId` = `WorkspaceId` cũ.
2. `channels.OrgId` = default org id.
3. Rename/deprecate `WorkspaceId`; update code.

---

## 10. Rollout phases

- [x] **P0** Chốt plan.
- [x] **P1** DASHBOARD Org **data model** + migration backfill + seed default org. ✅ 2026-08-23
      - Entity `Organization` (đổi từ `Org` để tránh xung đột namespace), `User.OrgId` + `Repository.OrgId`, email unique (OrgId,Email), Code unique (OrgId,Code).
      - Migration `20260823061730_AddOrganizations` đã apply vào DB dev (backfill 15 users + 2 repos về default org, 0 sót).
      - Seed **default org** (alias `default`) cho toàn bộ data sẵn có.
      - ❌ **Đã GỠ** create-org onboarding (FE `/create-org`, `OrgsController`, license provider, appsettings Licensing) — chuyển sang service onboarding riêng (xem §2 mục 4). DASHBOARD chỉ dựa seed.
- [x] **P2** Login multi-tenant (org alias + JWT orgId) + auth context OrgId. ✅ 2026-08-23
      - JWT thêm claim `org` (AppConstants.OrgIdClaim); `ITokenService.GenerateToken(..., orgId)` — cập nhật 4 caller (Login/Refresh/Google/AcceptInvitation).
      - `LoginCommand`/`LoginRequest` thêm `OrgAlias`; handler resolve org theo alias → tìm user (OrgId,Email) → verify. `LoginResult` trả thêm `OrgId`+`OrgAlias`.
      - `ICurrentUserService.OrgId` + `IRequestUserContext.OrgId` (đọc claim `org`).
      - **AcceptInvitation**: user mời nay set `OrgId = org của repo` (fix FK do OrgId required).
      - FE: login page thêm field Organization (pre-fill từ `?org=`), `LoginRequest.orgAlias`, `UserProfile.orgId/orgAlias` lưu session.
      - Lưu ý: token cũ (trước deploy) không có claim org → `CurrentUserService.OrgId` = null; chưa handler nào bắt buộc OrgId nên session cũ vẫn chạy, refresh sẽ có claim.
- [x] **P3** User redesign: sync existing org user + org-scoping toàn bộ query/command. ✅ 2026-08-23
      - `SearchUsers` scope theo org của caller → member picker & Scrum Master picker chỉ hiện user cùng org (= "sync existing org user", không cần UI mới).
      - `CreateUser` set `OrgId` + email unique per-org; `CreateRepository` set `OrgId` + Code unique per-org + **enforce license repo capacity**; `AddMember` guard user cùng org với repo.
      - `ListUsers` + `ListRepositories` scope theo org (chặn leak cross-org cho global admin).
      - FE: New Repo dialog hiện readonly "Organization: {alias}".
- [x] **P4** Routing alias DASHBOARD (`:orgAlias` + guard + links). ✅ 2026-08-23 (user chọn làm ngay)
      - Route tree bọc dưới `:orgAlias` (shell + children); thêm `wiki/:pageId`; bare `/` → `orgHomeRedirectGuard` → `/{alias}`.
      - Guards mới `orgContextGuard` (validate alias == org của user) + `orgHomeRedirectGuard`; `repoContextGuard`/`rootRedirectGuard` build URL kèm alias.
      - Shell: `orgAlias` computed, `repoLink()`/`globalLink()` prefix alias, `selectRepo`/`isGlobalRoute` theo shape `/{alias}/{code}/...`; 6 global link → `globalLink()`.
      - Nav lẻ: global-search, wiki-page, work-item-detail prefix alias+code. Login/invite dùng `/` → auto-redirect.
      - ⚠️ Cần test UI kỹ điều hướng (khó verify tự động hết). HUB routing alias là P5/P6.
- [~] **P5** HUB workspace=org. **Nền tảng xong** (2026-08-23): org info (OrgId/OrgAlias/OrgName) chảy DASHBOARD internal `/memberships` → gateway `UserMemberships` → HUB FE `UserMemberships` model. DASHBOARD compiles.
      - **Đổi hướng (giảm rủi ro)**: KHÔNG đổi schema `Channel`/migration Postgres. Endpoint `list(repoId)` hiện tại **đã** enforce "public OR member" → workspace=org làm THUẦN FRONTEND: shell lặp các repo user là member → list channel mỗi repo → group theo repo; DM để riêng. Sprint/private tự đúng.
      - **Sidebar rewrite XONG** (2026-08-23): `resolveWorkspace` load channel + member qua TẤT CẢ repo user là member (forkJoin) rồi merge; sidebar **group theo repo, collapse/expand**; header = org name; `canCreateChannelForRepo` per-repo (+ nút New Channel mỗi group). Thêm: **unread bold + badge** per channel, **typing indicator** ở sidebar (realtime typingStarted/Stopped, auto-expire 6s), **section Online** (org members đang online). DM mới gắn repo active đầu. Cần test UI.
- [x] **P6** HUB routing alias `/{orgAlias}/channels/...`. ✅ 2026-08-23
      - Route tree bọc dưới `:orgAlias`; `hubOrgRedirectGuard` (path-preserving) resolve alias từ memberships → forward `/` và deep-link legacy `/channels/{id}` (sprint) sang `/{alias}/…`; nav shell + notifications-page prefix alias.
      - Sprint channel deep-link cũ tự forward qua guard (không cần sửa CreateSprint generator). Sprint visibility đã đúng (private+capacity sync). Org settings/license UI bỏ (onboarding = service riêng).
      - **Title fix**: workspace header = org name (`orgName`) nhất quán mọi nhánh (fix bug cache hiện "Workspace").
      - ⏳ **Còn lại của P5**: sidebar group channel theo TỪNG repo (hiện vẫn hiện channel của 1 repo active). Quan trọng khi user thuộc nhiều repo trong org.

P1–P4 (DASHBOARD) làm trước; HUB (P5–P6) theo sau (phụ thuộc org info từ internal API).

---

## 11. Đã resolve (vòng confirm 2026-08-23)

1. Login phải chọn org (alias) — multi-tenant, email trùng giữa org được. ✅
2. License key base64 opaque + flag `active` (Nam maintain); seed pick key đầu → active=true. ✅
3. Global admin = admin của 1 org (không có cross-org super admin). ✅
4. New Repo dialog bỏ org, chỉ readonly hiển thị org. ✅
5. Alias = full-route prefix. ✅
6. Quản lý org chỉ `IsGlobalAdmin` (không thêm ManageOrg / IsOrgAdmin). ✅
7. Repo.Code unique per-org; Org.Alias unique global. ✅
8. Sprint channel = Private + capacity sync. ✅
