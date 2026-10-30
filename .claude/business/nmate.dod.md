# NMate (trợ lý AI hỗ trợ người dùng) — Business Document

> Đọc `RULES.md` trước khi sửa file này.
> Logic AI nằm ở service riêng **SUPPORT** (`C:\DEV\SUPPORT`). DASHBOARD chỉ giữ phần proxy (`Controllers/NMate`, `INMateGateway`) và tài liệu nguồn (`Knowledge/`). Thiết kế đầy đủ: `C:\DEV\docs\NMATE-AI-AGENT.md`.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-10-27 | 23:19 | NMate chủ động gợi ý theo ngữ cảnh | Thêm **nudge**: khi màn hình đang có vấn đề NMate xử lý được (vd item đầu của dải "Cần chú ý" ở Overview), NMate hiện 1 bong bóng tối cạnh nút nổi, nút nổi đồng thời lên viền coral. Bấm nhận → mở panel và **hỏi luôn câu hỏi** chứ không mở ô chat trống. **Mỗi nudge chỉ hiện 1 lần/phiên**, khoá theo `key` gồm cả màn hình lẫn vấn đề cụ thể — cùng màn nhưng vấn đề khác vẫn được hiện, còn cùng vấn đề thì không lặp lại (trợ lý nhắc đi nhắc lại sẽ bị bấm tắt không đọc). Màn Sprint thêm gợi ý cân tải (xem [capacity.dod.md](capacity.dod.md)). Trạng thái bảo trì đổi từ dòng chữ đỏ sang **N-bot đội mũ bảo hộ** — đây là chờ chứ không phải lỗi người dùng gây ra. Dòng disclaimer viết lại ngắn gọn bằng tiếng Anh: "NMate is an AI agent and can be wrong — please double-check." |
| 2026-10-27 | 14:20 | N-bot dùng chung cho header và nút đóng | Logo chữ "N" trong ô vuông xanh ở đầu panel đổi thành **logo NFlow** (`app-brand-logo`, 30px, chỉ phần lục giác, không còn nền). Nút nổi lúc panel đang mở cũng đổi từ dấu **X** sang N-bot **mắt nhắm** (mờ 72%, sáng lại khi hover) thay vì icon khác hẳn — vẫn là một nhân vật, nhắm mắt để báo bấm vào là đóng. Nhãn screen-reader giữ nguyên "Đóng NMate". |
| 2026-10-27 | 12:00 | Nút nổi thành N-bot | Nút nổi đổi từ icon chat tròn xanh sang **N-bot**: đầu lục giác lấy từ logo, hai nét chữ N làm mắt (tím `#8B5CF6` / hồng `#F0506E`, thân `#161B22` — màu logo cố định, không đổi theo theme). 4 trạng thái: **nghỉ** (mắt chớp 4s/lần), **hover** (phóng 1.1×, viền + bóng tím), **đang nghĩ** (mắt nheo, vòng tím xoay quanh đầu khi `isStreaming`), **tin mới** (badge đỏ đếm số câu trả lời hoàn thành lúc panel đóng, xoá khi mở lại). Vùng bấm 52×52px. Tôn trọng `prefers-reduced-motion`. |
| 2026-10-11 | 18:33 | Cho user ẩn/hiện NMate | Settings → General → Appearance có switch "NMate assistant" (setting `ui.nmate-widget`, mặc định bật); tắt thì nút nổi và phím tắt Ctrl+/ biến mất, panel đang mở tự đóng |
| 2026-10-11 | 18:17 | Thêm lịch sử hội thoại gần đây | Nút đồng hồ cạnh "Mới" mở danh sách 3 hội thoại gần nhất của user; chọn một hội thoại để xem lại và hỏi tiếp trong chính hội thoại đó |
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

- Nút nổi góc phải dưới trên mọi màn hình sau đăng nhập (`shell-layout`), hình **N-bot** với 5 trạng thái (nghỉ / hover / đang nghĩ / có tin mới / đang mở — mắt nhắm). Cùng hình N-bot đó làm logo ở đầu panel. Bấm nút hoặc **Ctrl/Cmd + /** để mở/đóng, **Esc** để đóng. Trên điện thoại panel chiếm toàn màn hình.
- Panel trống thì hiện **gợi ý câu hỏi** của màn hình đang đứng, bấm là hỏi luôn.
- **Enter** để gửi, **Shift+Enter** để xuống dòng. Trong lúc đang trả lời thì nút Gửi đổi thành **Dừng**.
- Nguồn hiển thị dưới câu trả lời (gộp các đoạn cùng mục). Bấm để mở đúng màn hình trong org/project hiện tại.
- **Mới**: bắt đầu hội thoại mới. Hội thoại hiện tại được giữ theo tab (sessionStorage): reload trang vẫn xem tiếp được, đóng tab thì bắt đầu lại.
- **Lịch sử (nút đồng hồ cạnh "Mới")**: hiện **3 hội thoại gần nhất** của chính user (theo hoạt động mới nhất, kèm thời gian tương đối), hội thoại đang mở được tô sáng. Chọn một hội thoại → tải lại toàn bộ tin nhắn và **câu hỏi tiếp theo được gửi vào chính hội thoại đó** (NMate dùng 10 tin gần nhất làm ngữ cảnh). Đang có câu trả lời chạy dở thì bị dừng trước khi chuyển. Hội thoại đã bị xoá/hết hạn thì tự biến khỏi danh sách. Esc hoặc bấm ra ngoài để đóng danh sách.
- Backend tắt NMate thì nút nổi không hiện. NMate bảo trì thì panel báo "đang bảo trì" và khoá ô gửi.
- **User tự ẩn/hiện**: Settings → General → Appearance → **NMate assistant** (setting cá nhân `ui.nmate-widget`, mặc định **bật** để user mới biết có trợ lý). Tắt → nút nổi biến mất, Ctrl+/ không còn tác dụng, panel đang mở tự đóng. Chỉ ảnh hưởng giao diện của chính user đó; lịch sử hội thoại vẫn giữ nguyên.

## 6. Edge Cases & Notes

- User đóng widget giữa chừng → generation dừng ở NMate; phần đã sinh vẫn được lưu (đánh dấu bị ngắt).
- Route trong citation là phần **cố định** của route (`/sprint-planning`), không chứa `/:orgAlias/:repoCode` → widget tự ghép link theo org/repo hiện tại.
- Tài liệu nháp đầu tiên (sprints, backlog, members, faq) có câu hỏi chờ owner trả lời trong `Knowledge/REVIEW-NOTES.txt`.
