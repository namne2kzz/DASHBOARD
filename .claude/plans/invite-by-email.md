# Invite by Email — kế hoạch hoàn thiện end-to-end

> Trạng thái: **draft, chờ review** — 2 handler đã sửa (mục 1, xem "Đã làm" bên dưới), chưa động
> gì tới controller/frontend/DB. Dừng lại theo yêu cầu để review trước khi làm tiếp.

## Context

Đây không phải build feature từ đầu. Phần lớn backend (Domain entity `Invitation`, 2 commands +
handlers, EF config, email template, Google id_token verification, rate-limit policy đăng ký sẵn
trong `Program.cs`, DI) **đã được viết từ trước** nhưng chưa nối hết end-to-end. Frontend cũng có
sẵn UI shell (panel "Invite by Email" trong Settings → Members) với TODO chờ API. Việc còn lại là
nối các mảnh đã có, vá 1 lỗ hổng authorization, và thêm phần thực sự chưa tồn tại.

## Review thay đổi đang pending (working tree lúc bắt đầu)

- **Pipelines page → empty state**: xoá `PipelinesMockService`, thay bằng placeholder chờ tích hợp
  CI/CD thật. Không còn reference nào tới service cũ — ổn.
- **nginx.conf**: thêm cache-control tách theo file hash vs `index.html` — ổn.
- **docker-compose.yml — có bug**: `GitConnections__DASH__0__IsPrimary` và `__1__IsPrimary` đều
  `"true"`. `GetGitRepositoryOverviewQueryHandler.cs:76` chọn primary bằng
  `entries.FirstOrDefault(e => e.IsPrimary)` — nếu 2 entry cùng `true`, entry `1` không bao giờ
  được chọn (entry `0` luôn thắng). Validator không check "tối đa 1 IsPrimary/code" nên không fail
  ở startup, chỉ âm thầm sai logic. **Chưa sửa** — để riêng, chờ ý kiến.
- `resources.json`: không có vấn đề.

## Việc còn thiếu cho Invite-by-Email (xác nhận bằng cách đọc code + query DB thật)

1. **`CreateInvitationCommandHandler` thiếu permission check** — không giống mọi handler khác
   trong repo (vd `AddMemberCommandHandler.cs:24`), nó không gọi
   `user.CanAsync(repositoryId, SystemFunction.InviteMembers, ct)`. `SystemFunction.InviteMembers`
   đã tồn tại, frontend đã có `privilege.canInviteMembers()` — chỉ backend chưa enforce.
2. **`AcceptInvitationCommandHandler` chỉ trả `Guid userId`, không issue JWT** — user tạo qua
   invite là Google-auth (`PasswordHash = string.Empty`), **không thể** login qua
   `POST /api/auth/login` bình thường. Cần tự issue access+refresh token giống hệt
   `LoginCommandHandler.cs`.
3. **Chưa có Controller** — không route HTTP nào cho 2 command. Rate-limit policy
   `"accept-invitation"` đã đăng ký sẵn trong `Program.cs:50`, chờ
   `[EnableRateLimiting("accept-invitation")]`.
4. **Frontend chưa gọi API thật**: `submitInvite()` trong
   `settings-members-page.component.ts:361` có TODO rõ; chưa có `InvitationsService`; chưa có
   Google Identity Services (GIS) tích hợp thật ở đâu (nút Google ở login-page chỉ là SVG trang
   trí); chưa có trang/route `/invite/accept`.

### Điều chỉnh so với lần review đầu — đã tự sửa sai

- **Ban đầu tưởng chưa có EF migration cho bảng `Invitations`** (do tìm file migration theo tên
  chứa chữ "Invitation" và không thấy). **Đã kiểm tra lại bằng MCP SQL trực tiếp trên DB thật**:
  bảng `Invitations` **đã tồn tại** — được tạo từ migration đầu tiên
  `20260523103658_InitialCreated.cs` (nằm ở `DASHBOARD/Migrations/`, không phải
  `Infrastructure/Persistence/Migrations/` — dự án đổi `--output-dir` giữa chừng nên có 2 thư mục
  migration, nhưng vẫn là **1 chain liên tục, cả 13 migration đều đã apply**, không có nhánh rẽ,
  không phải vấn đề thật). → **Không cần migration mới cho Invitations.**
- Lúc chạy thử `dotnet ef migrations add`, phát hiện model hiện tại lệch với snapshot ở
  `Roles.AllowedFunctions` (seed data của 4 role mặc định) — **không liên quan invite-by-email**,
  có vẻ là drift có sẵn từ trước (code seed đổi nhưng chưa migrate). Đã xoá migration nháp, không
  đụng vào. Nêu ra để anh biết, tuỳ anh muốn xử lý riêng hay bỏ qua.

## Đã làm (2 handler, chưa build lại sau lần cuối, chưa chạy migration thật)

- `CreateInvitationCommandHandler.cs`: thêm permission check (`InviteMembers`) + check repo tồn
  tại/chưa archive, mirror `AddMemberCommandHandler`.
- `AcceptInvitationCommand.cs` + `AcceptInvitationCommandHandler.cs`: đổi kiểu trả về từ
  `Result<Guid>` → `Result<LoginResult>` (tái dùng `LoginResult` từ
  `Auth/Commands/Login/LoginCommand.cs`), issue access+refresh token + lưu `UserToken` giống hệt
  `LoginCommandHandler`. Build backend đã pass với 2 thay đổi này (`dotnet build` sạch, 0 lỗi).

## Kế hoạch còn lại

### Backend

**A. `InvitationsController`** (`Controllers/Invitations/InvitationsController.cs`)
- `POST api/repositories/{repoId:guid}/invitations` — `[Authorize]`, gửi `CreateInvitationCommand`,
  response giống `MembersController.Add` (201/400/401/403). Request DTO
  `Controllers/Invitations/Requests/CreateInvitationRequest.cs` (field `Email`).
- `POST api/invitations/accept` — `[AllowAnonymous]`,
  `[EnableRateLimiting("accept-invitation")]`, gửi `AcceptInvitationCommand`, trả 200 với
  `LoginResult` / 400 khi failure. Request DTO
  `Controllers/Invitations/Requests/AcceptInvitationRequest.cs` (`RawToken`, `GoogleIdToken`).

**B. Update `.claude/business/invitations.dod.md`**
Check các mục DoD đã đạt, thêm dòng Update Log ngắn gọn.

### Frontend

**C. Google Identity Services (lần đầu tích hợp thật trong app)**
- Script GIS vào `index.html`.
- `core/services/google-identity.service.ts` — wrap `google.accounts.id.initialize/renderButton`,
  trả `id_token` qua Promise/Observable.
- Thêm `googleClientId` vào `environment.ts` / `.prod.ts` / `.docker.ts` — phải khớp
  `Google:ClientId` bên backend.

**D. `services/invitations.service.ts`**
- `sendInvite(repoId, email)` → `POST /repositories/{repoId}/invitations`.
- `accept(rawToken, googleIdToken)` → `POST /invitations/accept`, trả `LoginResponse`-shaped.

**E. Nối `submitInvite()`** trong `settings-members-page.component.ts:361` — gọi
`InvitationsService.sendInvite`, thêm loading/error signal + toast theo pattern
`submitAddMember()`, bỏ TODO.

**F. Trang `invite-accept-page`** (4 file: `.ts/.html/.scss/.spec.ts`)
- Đọc raw token từ `window.location.hash` (không phải query string).
- Render nút Google Sign-In qua `GoogleIdentityService`.
- Thành công → gọi `AuthService.applySession(res)` (đổi `persistSession` từ `private` →
  `public` để tái dùng) rồi điều hướng vào app; thất bại → hiện message lỗi trả thẳng từ backend.
- Route top-level ngang `login` trong `app.routes.ts` (**ngoài** `authGuard`):
  `{ path: 'invite/accept', loadComponent: ... }`.

**G. i18n** — thêm string mới (nếu có) vào `resources.json`.

## Lưu ý config (cần anh xác nhận, chưa tự đổi)

- `Google:ClientId` trong `appsettings*.json` vẫn là placeholder
  `REPLACE_WITH_GOOGLE_CLIENT_ID.apps.googleusercontent.com` — cần Client ID thật từ Google Cloud
  Console, set cả backend lẫn frontend `environment.googleClientId`.
- `Invitation:TokenTtlMinutes = 5` (ngắn so với mô tả "thường 7 ngày" trong
  `invitations.dod.md`) — giữ nguyên hay tăng lên?
- Bug `docker-compose.yml IsPrimary` trùng — sửa luôn hay để riêng?
- Drift `Roles.AllowedFunctions` phát hiện lúc thử migration — kệ hay tạo migration riêng để đồng
  bộ?

## Kiểm tra sau khi làm xong

- `dotnet build` — xanh (đã pass với phần A/B backend hiện tại).
- Test thủ công: Settings → Members → Invite by Email → email ngoài hệ thống → nhận email (SMTP
  dev) → mở link accept → đăng nhập Google (tài khoản khớp email mời) → redirect vào app ở trạng
  thái đã login, `RepositoryMember` mới có role Developer.
- Case lỗi: token hết hạn, token đã dùng, email Google không khớp — message đúng, không lộ chi
  tiết nội bộ.
- `ng build` + spec hiện có của `settings-members-page` không vỡ do đổi `submitInvite()`.
