# NMate (trợ lý AI hỗ trợ người dùng) — Business Document

> Đọc `RULES.md` trước khi sửa file này.
> Logic AI nằm ở service riêng **SUPPORT** (`C:\DEV\SUPPORT`). DASHBOARD chỉ giữ phần proxy (`Controllers/NMate`, `INMateGateway`) và tài liệu nguồn (`Knowledge/`). Thiết kế đầy đủ: `C:\DEV\docs\NMATE-AI-AGENT.md`.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-10-09 | 22:20 | Thêm widget chat NMate | Nút nổi góc phải dưới trên mọi màn hình trong app (Ctrl+/ mở/đóng), gợi ý câu hỏi theo màn hình, stream câu trả lời, nút Dừng, nguồn bấm được, 👍/👎; hội thoại tiếp tục sau khi reload trong cùng tab |
| 2026-10-09 | 21:40 | Khởi tạo document | Tạo document business ban đầu cho feature NMate (proxy `/api/v1/nmate/*` sang service SUPPORT, rate limit theo user, knowledge từ `Knowledge/`) |

---

## 1. Purpose

Cho người dùng hỏi cách dùng hệ thống ngay trong app (widget chat nổi) và nhận câu trả lời **chỉ dựa trên tài liệu hướng dẫn** (`Knowledge/*.md`), có ghi nguồn. Mục tiêu là giảm số câu hỏi "làm sao…/vì sao không…" gửi cho admin.

## 2. Key Entities & Relationships

- DASHBOARD **không lưu entity nào** cho NMate. Hội thoại, câu trả lời, feedback và vector tài liệu nằm trong DB riêng của service SUPPORT (PostgreSQL + pgvector).
- Liên kết sang DASHBOARD chỉ qua định danh: `UserId` và `OrgId` của người hỏi (lấy từ JWT, không có FK).
- `Knowledge/*.md` (ở gốc repo DASHBOARD): tài liệu cho **end-user**, mỗi file có front-matter `key`, `title`, `module`, `route`, `suggestions`. Đây là nguồn duy nhất NMate được dùng để trả lời.
- Product key của DASHBOARD phía SUPPORT là `dashboard`, do internal token quyết định chứ không do request.

## 3. Business Rules & Invariants

- **Chỉ user đã đăng nhập** mới gọi được `/api/v1/nmate/*` (`[Authorize]`). DASHBOARD tự gắn `X-User-Id`/`X-Org-Id` từ JWT; client không tự khai được danh tính.
- **Mỗi user chỉ thấy hội thoại của chính mình** trong tổ chức của mình. Hội thoại của người khác trả về 404 (không phân biệt "không tồn tại" và "không phải của bạn").
- **Rate limit hỏi**: mặc định 5 câu / 1 phút / user (sliding window, theo user chứ không theo IP). Vượt thì trả 429. Lý do: quota Gemini free tính theo request/phút.
- Mỗi user chỉ có **1 câu trả lời đang sinh tại một thời điểm**. Câu thứ hai trong lúc đang trả lời bị từ chối (409 `NMATE_STREAM_IN_PROGRESS`).
- **Không có tài liệu liên quan thì không trả lời**: NMate trả "chưa có tài liệu" và **không gọi model** (không bịa, không tốn quota).
- NMate **không đọc dữ liệu nghiệp vụ** (task, sprint, member…) của user. Chỉ dùng `Knowledge/`.
- Câu hỏi tối đa 1000 ký tự.
- Endpoint bảo trì của NMate (re-index tài liệu, xoá dữ liệu user) **không được expose** qua DASHBOARD. Proxy chỉ chuyển tiếp một danh sách endpoint cố định.
- NMate chưa cấu hình (`NMate:BaseUrl` rỗng) → mọi `/api/v1/nmate/*` trả 404, widget ẩn. NMate không truy cập được hoặc timeout → 503 `NMATE_UNAVAILABLE`, widget hiện "đang bảo trì". **Lỗi NMate không bao giờ ảnh hưởng chức năng khác của DASHBOARD.**
- Hội thoại không hoạt động quá 90 ngày bị xoá tự động (phía SUPPORT).

## 4. Main Workflows / Use Cases

1. **Hỏi**: user mở widget → gõ câu hỏi (kèm route màn hình đang đứng) → `POST /api/v1/nmate/chat` → câu trả lời stream về dạng SSE (`meta` → `delta`… → `citations` → `done`, hoặc `error`) → hiển thị nguồn (tài liệu › mục).
2. **Tiếp tục hội thoại**: gửi kèm `conversationId`; NMate dùng tối đa 10 tin gần nhất làm ngữ cảnh.
3. **Gợi ý câu hỏi**: `GET /api/v1/nmate/suggestions?route=…` → câu gợi ý của tài liệu khớp màn hình hiện tại (fallback: tài liệu chung). Không tốn quota.
4. **Lịch sử**: liệt kê, xem lại, xoá hội thoại của chính mình.
5. **Đánh giá**: 👍/👎 (kèm comment tuỳ chọn) cho từng câu trả lời; gửi lại thì ghi đè. Câu bị 👎 nhiều là dấu hiệu tài liệu còn thiếu.
6. **Cập nhật tài liệu** (team): sửa `Knowledge/*.md` cùng PR với thay đổi business → khi deploy, tài liệu được sync và re-index (chỉ file đổi mới bị embed lại).

## 5. Definition of Done

- [ ] Mọi endpoint `/api/v1/nmate/*` yêu cầu đăng nhập; danh tính gửi sang NMate lấy từ JWT, không lấy từ body/query.
- [ ] Chỉ các endpoint trong danh sách (chat, conversations, messages, delete, feedback, suggestions, status) được chuyển tiếp.
- [ ] `POST /chat` có rate limit theo user và stream từng phần về browser (không buffer).
- [ ] NMate tắt → 404; NMate lỗi/timeout → 503 `NMATE_UNAVAILABLE`; không request nào của DASHBOARD fail vì NMate.
- [ ] Không retry tự động khi gọi `/chat` (tránh trả lời 2 lần và tốn quota 2 lần).
- [ ] Thay đổi business của feature X → cập nhật cả `.claude/business/X.dod.md` lẫn `Knowledge/` tương ứng.

### Widget (DASHBOARD.VIEW)

- Nút nổi góc phải dưới trên mọi màn hình sau đăng nhập (`shell-layout`). Bấm nút hoặc **Ctrl/Cmd + /** để mở/đóng, **Esc** để đóng. Trên điện thoại panel chiếm toàn màn hình.
- Panel trống thì hiện **gợi ý câu hỏi** của màn hình đang đứng, bấm là hỏi luôn.
- **Enter** để gửi, **Shift+Enter** để xuống dòng. Trong lúc đang trả lời thì nút Gửi đổi thành **Dừng**.
- Nguồn hiển thị dưới câu trả lời (gộp các đoạn cùng mục). Bấm để mở đúng màn hình trong org/project hiện tại.
- **Mới**: bắt đầu hội thoại mới. Hội thoại hiện tại được giữ theo tab (sessionStorage): reload trang vẫn xem tiếp được, đóng tab thì bắt đầu lại.
- Backend tắt NMate thì nút nổi không hiện. NMate bảo trì thì panel báo "đang bảo trì" và khoá ô gửi.

## 6. Edge Cases & Notes

- User đóng widget giữa chừng → generation dừng ở NMate; phần đã sinh vẫn được lưu (đánh dấu bị ngắt).
- Route trong citation là phần **cố định** của route (`/sprint-planning`), không chứa `/:orgAlias/:repoCode` → widget tự ghép link theo org/repo hiện tại.
- Tài liệu nháp đầu tiên (sprints, backlog, members, faq) có câu hỏi chờ owner trả lời trong `Knowledge/REVIEW-NOTES.txt`.
