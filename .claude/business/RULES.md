# Quy tắc Document — `.claude/business/`

Tài liệu này quy định cách Claude (và bất kỳ ai khác) ghi/đọc/update các file business document trong thư mục `.claude/business/`. Đây là rule bắt buộc — **phải đọc file này trước khi tạo hoặc sửa bất kỳ file `.dod.md` hoặc `domain-business.md`.**

## 1. Mục đích thư mục

`.claude/business/` lưu document mô tả **business** của project — không phải document kỹ thuật (kiến trúc, code convention đã có ở `.claude/docs/`). Mục tiêu: người không đọc code (PM, BA, dev mới) vẫn hiểu được mỗi feature làm gì, rule gì, workflow nào.

## 2. Cấu trúc thư mục

```
.claude/business/
├── RULES.md                # file này — quy tắc document
├── domain-business.md      # tổng quan domain/business toàn project
├── auth.dod.md
├── users.dod.md
├── members.dod.md
├── invitations.dod.md
├── roles.dod.md
├── metadata.dod.md
├── boards.dod.md
├── backlog.dod.md
├── sprints.dod.md
├── sprint-tasks.dod.md
├── smart-board.dod.md
├── capacity.dod.md
├── repositories.dod.md
├── git-repositories.dod.md
├── wiki.dod.md
├── discussions.dod.md
├── history.dod.md
└── overview.dod.md
```

- Mỗi file `{feature}.dod.md` tương ứng 1 feature trong `DASHBOARD/Application/{Feature}/`.
- Tên file: kebab-case, số ít theo tên feature (ví dụ feature `Roles` → `roles.dod.md`, `SprintTasks` → `sprint-tasks.dod.md`).
- Nếu sau này có feature mới (folder mới trong `Application/`) → tạo file `.dod.md` mới tương ứng (xem mục 5).

## 3. Cấu trúc bên trong 1 file `.dod.md`

Mỗi file gồm 2 phần, theo đúng thứ tự:

### Phần A — Update Log (luôn ở đầu file)

Bảng ghi lại lịch sử thay đổi business của feature đó. **Không bao giờ xóa dòng cũ** — chỉ thêm dòng mới lên **đầu bảng** (mới nhất trên cùng).

```markdown
## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document business ban đầu cho feature X |
```

- **Ngày/Giờ**: thời điểm thực hiện thay đổi (lấy giờ hệ thống thật, không bịa).
- **Title**: ngắn gọn (≤ 8 từ), dạng "Thêm...", "Sửa...", "Xóa...", "Đổi...". Ví dụ: "Thêm rule duyệt invitation", "Sửa logic tính capacity".
- **Thay đổi**: 1 câu mô tả cụ thể cái gì thay đổi về business (không phải mô tả code/diff kỹ thuật).

### Phần B — Business Doc (luôn là bản LATEST)

Phần này **không phải changelog** — chỉ chứa trạng thái business **hiện tại, mới nhất** của feature. Khi update, **ghi đè nội dung cũ**, không giữ song song nhiều version trong phần này (lịch sử thay đổi đã có ở Update Log).

Bố cục cố định, theo thứ tự:

1. **Purpose** — feature này phục vụ mục đích business gì (1-2 câu).
2. **Key Entities & Relationships** — entity chính, field quan trọng, quan hệ với entity khác.
3. **Business Rules & Invariants** — rule validation, authorization, ràng buộc, state transition... (phần quan trọng nhất).
4. **Main Workflows / Use Cases** — các luồng nghiệp vụ chính.
5. **Definition of Done** — checklist tiêu chí để coi feature này "đã hoàn chỉnh" về business (dùng để review khi thêm/sửa feature).
6. **Edge Cases & Notes** — case đặc biệt, hành vi ngầm định cần lưu ý.

## 4. Khi nào phải update

Bất kỳ lúc nào thêm/sửa/xóa code làm thay đổi **business logic** của 1 feature (rule mới, workflow mới, field mới ảnh hưởng nghiệp vụ, thay đổi permission, v.v.) — **không phải refactor thuần kỹ thuật không đổi business** — thì sau khi code xong PHẢI:

1. Xác định feature bị ảnh hưởng (map theo `Application/{Feature}/`).
2. Mở `.claude/business/{feature}.dod.md` tương ứng.
3. Thêm 1 dòng mới vào **Update Log** (đầu bảng).
4. Sửa lại **Business Doc** (phần B) cho đúng với state mới nhất — xóa/sửa nội dung cũ không còn đúng.
5. Nếu thay đổi ảnh hưởng tới domain tổng thể (entity mới, quan hệ mới giữa các feature, khái niệm business mới) → update thêm `domain-business.md` theo cùng quy tắc (Update Log + nội dung mới nhất).

Nếu thay đổi chỉ là kỹ thuật thuần (đổi tên biến, refactor, sửa bug không đổi behavior business) → **không cần** update file này.

## 5. Khi tạo feature mới

1. Tạo file `.claude/business/{feature-kebab-case}.dod.md` mới theo đúng bố cục mục 3.
2. Update Log dòng đầu tiên: ngày/giờ tạo, title "Khởi tạo document", mô tả "Tạo document business ban đầu cho feature {Tên}".
3. Nếu feature mới giới thiệu entity/khái niệm domain mới → thêm vào `domain-business.md`.
4. Thêm dòng trỏ tới file mới vào danh sách ở mục 2 của file này.

## 6. Ngôn ngữ

Document viết bằng tiếng Việt (xen tên entity/field/enum bằng tiếng Anh giữ nguyên như trong code) để người trong team dễ đọc. Không dịch tên kỹ thuật (ví dụ giữ `SprintTask`, `WipLimit`, không dịch thành "giới hạn công việc đang làm").
