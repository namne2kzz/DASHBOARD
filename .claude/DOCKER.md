# Docker — Cheatsheet cho DASHBOARD

Các backing services (SQL Server, RabbitMQ, Mailpit) chạy trong Docker.
App (.NET API + Angular) vẫn chạy native bằng `dotnet run` / `ng serve`.

> Tất cả lệnh `docker compose` phải chạy trong thư mục chứa `docker-compose.yml` → `C:\DEV\DASHBOARD`.

---

## 1. Cài đặt môi trường (chỉ làm 1 lần)

| Lệnh | Giải thích |
|------|-----------|
| `wsl --install` | Cài Windows Subsystem for Linux (WSL2). Docker Desktop trên Windows cần nó để chạy engine Linux. Chạy trong **PowerShell Admin**, xong **restart máy**. |

---

## 2. Vòng đời services (dùng hằng ngày)

| Lệnh | Giải thích |
|------|-----------|
| `docker compose up -d` | Khởi chạy tất cả container ở chế độ nền (`-d` = detached). Lần đầu tự tải image. |
| `docker compose down` | Dừng và xóa container. **Giữ nguyên data** (volume còn). Dùng khi tắt máy / nghỉ. |
| `docker compose down -v` | Như trên nhưng **xóa luôn data** (`-v` = volumes). Dùng khi muốn DB sạch làm lại từ đầu. |
| `docker compose restart` | Khởi động lại container đang chạy, không đụng data. |
| `docker compose stop` | Tạm dừng container (không xóa). `docker compose start` để bật lại. |

---

## 3. Kiểm tra trạng thái & chẩn đoán lỗi

| Lệnh | Giải thích |
|------|-----------|
| `docker compose ps` | Liệt kê container của project + trạng thái (Up / Restarting / Exited) + port. |
| `docker compose logs sqlserver` | Xem log của 1 service. Dùng để tìm nguyên nhân khi container chết. |
| `docker compose logs sqlserver --tail 30` | Chỉ xem 30 dòng log cuối. |
| `docker compose logs -f sqlserver` | Xem log realtime (`-f` = follow). Ctrl+C để thoát. |
| `docker ps` | Liệt kê **mọi** container đang chạy trên máy (không chỉ project này). |

**Ý nghĩa STATUS thường gặp:**
- `Up (healthy)` → chạy tốt, sẵn sàng kết nối.
- `Restarting (255)` → container chết liên tục, xem `logs` để biết lý do (vd password yếu).
- `Exited` → đã dừng hẳn.

---

## 4. Tạo file cấu hình

| Lệnh | Giải thích |
|------|-----------|
| `copy .env.example .env` | Tạo file `.env` thật từ mẫu. `.env` chứa mật khẩu thật, không commit git. |
| `notepad .env` | Mở `.env` để sửa mật khẩu. |

---

## 5. Kết nối app vào DB (.NET / EF Core)

| Lệnh | Giải thích |
|------|-----------|
| `dotnet tool install --global dotnet-ef` | Cài EF Core CLI (chỉ 1 lần / máy). |
| `dotnet ef database update` | Apply migrations → tạo database + bảng trong container SQL Server. Chạy trong `C:\DEV\DASHBOARD\DASHBOARD`. |
| `dotnet run` | Chạy API. |

---

## 6. Build image cho app (API + Angular) — tùy chọn, thủ công

`api` và `web` nằm chung `docker-compose.yml` nhưng gắn **profile `app`** → lệnh trơn `docker compose up -d` / `down` / `ps` **bỏ qua** 2 service này hoàn toàn, không tự build/chạy. Chỉ khi thêm `--profile app` thì Docker mới đụng tới.

| Lệnh | Giải thích |
|------|-----------|
| `docker compose --profile app build` | Build image `dashboard-api` (.NET, multi-stage SDK→ASP.NET runtime) và `dashboard-web` (Angular production build → nginx). |
| `docker compose --profile app up -d` | Chạy backing services + 2 container app: API ở `localhost:55432`, Web ở `localhost:4201`. |
| `docker compose --profile app up -d --build` | Build lại (nếu Dockerfile đổi) rồi chạy. |
| `docker compose --profile app down` | Dừng và xóa toàn bộ (kể cả backing services). |

Ghi chú:

- API container chạy `ASPNETCORE_ENVIRONMENT=Production` → load `appsettings.json` + `appsettings.Production.json`. File Production trỏ hostname theo **tên service** trong docker network (`sqlserver`, `rabbitmq`, `mailpit`) — chỉ đúng khi chạy trong container, không dùng được nếu `dotnet run` native với `ASPNETCORE_ENVIRONMENT=Production`.
- Password SQL Server + RabbitMQ **không hardcode** trong `appsettings.Production.json` (placeholder `CHANGE_ME_VIA_ENV` không bao giờ được dùng thật) — giá trị thật được `docker-compose.yml` bơm đè qua biến môi trường `ConnectionStrings__Default`, `RabbitMq__Username`, `RabbitMq__Password` (đọc từ `.env`).
- Web container serve static file qua nginx, không phải `ng serve` — dùng để test bản build production, không có hot-reload.
- Web build bằng Angular configuration **`docker`** (không phải `production`) — dùng `environment.docker.ts` với `apiBaseUrl: http://localhost:55432/api` (API expose qua host). Configuration `production` mặc định trỏ `apiBaseUrl` sang Azure thật (`environment.prod.ts`), chỉ dùng khi deploy Azure, không dùng cho test local.
- CORS phía API (`Program.cs`) lấy origin cho phép từ `Invitation:FrontendBaseUrl` — trong `appsettings.Production.json` là `http://localhost:4201`, khớp port container `web`.

---

## 7. Restore file `.bak` vào SQL Server trong Docker

Container mount thêm `./backups:/var/opt/mssql/backup` (bind-mount) — chỉ cần thả file `.bak` vào thư mục `C:\DEV\DASHBOARD\backups\` (Windows Explorer bình thường, không cần `docker cp`), container sẽ thấy ngay ở path `/var/opt/mssql/backup/`. Thư mục này git-ignore, không commit `.bak` lên git.

Restore bằng T-SQL trong SSMS (connect `localhost,1433`, user `sa`, password trong `.env`):

```sql
-- 1. Xem LogicalName thật của data/log file bên trong bản backup
RESTORE FILELISTONLY FROM DISK = '/var/opt/mssql/backup/DASHBOARD.BAK';

-- 2. Nếu database đích đã tồn tại và đang có connection mở, gỡ trước
ALTER DATABASE DASHBOARD SET SINGLE_USER WITH ROLLBACK IMMEDIATE;

-- 3. Restore, MOVE sang path Linux trong container (không dùng path Windows gốc lúc backup)
RESTORE DATABASE DASHBOARD FROM DISK = '/var/opt/mssql/backup/DASHBOARD.BAK'
WITH MOVE 'DASHBOARD'     TO '/var/opt/mssql/data/DASHBOARD.mdf',
     MOVE 'DASHBOARD_log' TO '/var/opt/mssql/data/DASHBOARD_log.ldf',
     REPLACE;

ALTER DATABASE DASHBOARD SET MULTI_USER;
```

Ghi chú:

- Tên trong `MOVE '...'` phải lấy đúng từ cột `LogicalName` ở bước 1 (có thể khác `DASHBOARD`/`DASHBOARD_log` tùy backup gốc).
- Nếu dùng GUI wizard "Restore Database" của SSMS thay vì T-SQL: tab **Files** có cột **"Restore As"** tự điền path Windows gốc lúc backup — phải sửa tay thành `/var/opt/mssql/data/...` trước khi bấm OK, nếu không sẽ lỗi `Directory lookup for the file ... failed`.

---

## Các URL / cổng hữu ích

| Service | Địa chỉ | Ghi chú |
|---------|---------|---------|
| SQL Server | `localhost,1433` | Connect bằng SSMS: user `sa`, password trong `.env`. |
| RabbitMQ UI | http://localhost:15672 | Login `guest` / `guest`. |
| Mailpit (email dev) | http://localhost:8025 | Xem mọi email app gửi ra. |

---

## Quy tắc vàng cần nhớ

1. Đổi mật khẩu trong `.env` → phải đổi luôn trong `appsettings.Development.json` cho **khớp**.
2. Sau khi đổi mật khẩu SQL → phải `docker compose down -v` rồi `up -d` (volume cũ giữ mật khẩu cũ).
3. Mật khẩu SQL Server: ≥8 ký tự, đủ 3/4 nhóm (HOA, thường, số, ký tự đặc biệt).
