---
name: c4-architecture
description: Use when documenting system architecture for other people — onboarding doc, interview prep, ADR context, or explaining how DASHBOARD/HUB fits together. Generates C4 Context/Container/Component views from the actual code.
---

# Skill: C4 Architecture Documentation

Sinh tài liệu kiến trúc theo [C4 model](https://c4model.com/diagrams) từ code thật, không từ trí nhớ.

## Usage
```
/c4-architecture [context|container|component|all] [target]
```

Mặc định: **chỉ Context + Container**. Theo chính C4 model: *"the system context and container diagrams are sufficient for most software development teams."* Component chỉ làm khi được yêu cầu, Code level gần như không bao giờ cần (code tự nói rồi).

---

## 4 tầng — và tầng nào thực sự cần

| Tầng | Trả lời câu hỏi | Người đọc | Khi nào làm |
|---|---|---|---|
| **1. Context** | Hệ thống này phục vụ ai, nói chuyện với hệ nào bên ngoài | PM, người mới, phỏng vấn | Gần như luôn |
| **2. Container** | Gồm những app/service/DB nào, giao tiếp bằng gì | Dev, DevOps | Gần như luôn |
| **3. Component** | Bên trong một container có những khối gì | Dev làm trong container đó | Khi onboard người mới vào 1 service |
| **4. Code** | Class, method cụ thể | — | Hiếm khi. Code + XML doc đã đủ |

---

## Quy trình

### Đọc trước, đừng đoán

Nguồn sự thật, theo thứ tự:

1. `CLAUDE.md` — stack, cấu trúc thư mục, ràng buộc layer
2. `.claude/business/domain-business.md` — nghiệp vụ tổng
3. `*.sln`, `*.csproj` — project nào tồn tại, tham chiếu nhau ra sao
4. `Program.cs` — service nào được đăng ký, middleware nào chạy
5. `appsettings.json` — DB, Redis, Service Bus, external endpoint
6. `docker-compose.yml` / `deployment/` — container thật khi chạy
7. `angular.json`, `package.json` — phía frontend

**Không bịa.** Thành phần nào không tìm thấy trong code hoặc config thì không đưa vào sơ đồ. Thiếu thông tin thì ghi `[cần xác nhận]` và hỏi.

### Context (tầng 1)

Một sơ đồ, một hệ thống ở giữa.

- **Người dùng** (persona): ai dùng, để làm gì. Lấy từ role/permission trong code, không tự nghĩ.
- **Hệ thống bên ngoài**: DB, identity provider, email, object storage (MinIO), message broker, API bên thứ ba. Lấy từ `appsettings.json` + DI registration.
- **Không** vẽ công nghệ bên trong ở tầng này. Người đọc là người không rành kỹ thuật.

### Container (tầng 2)

Mỗi thứ **chạy riêng được** là một container: web app, API, worker, database, cache, broker.

Với mỗi container ghi: tên · công nghệ · trách nhiệm một dòng · giao tiếp với container khác bằng gì (HTTP/JSON, SignalR, AMQP, TCP).

Ví dụ hình dạng cho DASHBOARD: `Angular SPA` → `ASP.NET Core API` → `SQL Server` + `Redis` + `MinIO`.
Với HUB còn có microservice + RabbitMQ + YARP gateway.

### Component (tầng 3) — chỉ khi cần

Trong một Clean Architecture codebase, **component map thẳng vào layer và feature folder**, đừng quét mù từng thư mục:

| Component | Gồm | Phụ thuộc |
|---|---|---|
| Domain | `Domain/Entities`, `Enums`, `Interfaces` | không gì |
| Application / {Feature} | `Application/{Feature}/Commands`, `Queries`, `DTOs` | Domain |
| Infrastructure | `Persistence`, `Auth`, `Messaging`, `Services` | Application + Domain |
| API | `Controllers/{Feature}` | Application |

Một feature (Backlog, Sprints, Wiki…) là một component ngang, cắt qua Application + Controllers. Mô tả theo feature dễ hiểu hơn theo thư mục.

**Kiểm tra luôn chiều phụ thuộc** khi dựng tầng này: thấy Domain tham chiếu Infrastructure là **vi phạm**, ghi vào tài liệu như một phát hiện, không im lặng vẽ cho đẹp.

---

## Sơ đồ

- Có skill/plugin `diagram-design` → dùng nó, sơ đồ sẽ theo token màu Nexus.
- Không có → Mermaid C4 (`C4Context`, `C4Container`, `C4Component`), vì nó render được thẳng trên GitHub.

Giữ mỗi sơ đồ dưới ~12 node. Nhiều hơn thì tách, đừng nhồi.

## Đầu ra đặt ở đâu

Theo quy ước của dự án, **tài liệu người đọc → `C:\DEV\docs\`**, không phải `.claude/`:

```
C:\DEV\docs\ARCHITECTURE-{project}.md
```

Một file, các tầng là các mục `##`. Chỉ tách file khi quá dài.

Không tạo thư mục `C4-Documentation/` trong repo — nó làm rối repo và trùng với `.claude/business/`.

---

## Cờ đỏ

- Vẽ thành phần không có trong code (vẽ cái "nên có" thay vì cái "đang có")
- Làm cả 4 tầng khi chỉ cần 2
- Component level liệt kê từng thư mục thay vì nhóm theo feature/layer
- Sơ đồ 30 node không ai đọc nổi
- Tài liệu không ghi ngày và commit hash → vài tuần sau không biết còn đúng không

Luôn mở đầu file bằng: `> Dựng từ commit <sha> ngày <date>. Code đổi thì tài liệu này cũ.`

## Khi nào dùng skill này

Hợp: onboarding người mới · chuẩn bị phỏng vấn (`C:\DEV\docs\INTERVIEW.md`) · viết ADR cần phần context · giải thích hệ thống cho người ngoài team.

Không hợp: tự mình hiểu code (đọc thẳng nhanh hơn) · tài liệu nghiệp vụ (dùng `.claude/business/`) · kế hoạch làm feature (dùng `.claude/plans/`).

---

Nguồn: phỏng theo workflow `c4-architecture` của [wshobson/agents](https://github.com/wshobson/agents) (MIT), rút gọn còn 2 tầng mặc định và map thẳng vào Clean Architecture của dự án.
