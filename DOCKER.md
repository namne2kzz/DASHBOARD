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
