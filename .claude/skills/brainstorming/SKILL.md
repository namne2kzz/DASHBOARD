---
name: brainstorming
description: Use before any new feature, component, or behavior change — to find out what the user actually wants and why, before writing code or a plan. Ends with an approved design that feeds .claude/plans/.
---

# Skill: Brainstorming

Mình giỏi build. Nhưng cái mình *tin* là user muốn thường mỏng hơn mình tưởng. Skill này để tìm ra họ muốn gì **và tại sao**, trước khi có dòng code nào.

Người ta thường chưa nghĩ xong điều mình muốn. Câu hỏi tốt giúp họ nghĩ tiếp. Khi hiểu được **tại sao**, mình quyết đúng hàng trăm thứ nhỏ mà họ không bao giờ nói ra.

---

## Trước hết: mình đang biết gì?

- **Mọi thứ cần thiết đã có trong yêu cầu** ("đổi icon sang màu xanh") → task rõ, làm luôn, báo đã làm gì. Không hỏi.
- **Thiếu thứ gì đó không nhỏ** → bắt đầu hội thoại.

Trước khi code, theo thứ tự:

1. Nêu các hướng tiếp cận, để user chọn (xem *Nêu các hướng*).
2. Viết design ngắn hoặc đầy đủ (xem *Chọn cỡ*).
3. Được user duyệt design đó.
4. Viết plan vào `.claude/plans/{feature-slug}.md`.

Hai thứ **không tính là code**: spike thăm dò mà user đã đồng ý, và làm thẳng khi user nói khỏi hỏi.

## Đọc trước khi hỏi

Dự án này đã có sẵn rất nhiều ngữ cảnh. **Đọc trước, đừng hỏi cái đã có câu trả lời:**

- `CLAUDE.md` — stack, cấu trúc, hard constraint
- `.claude/business/{feature}.dod.md` — nghiệp vụ feature đó
- `.claude/business/domain-business.md` — nghiệp vụ tổng
- `.claude/plans/` — đã lên kế hoạch gì chưa
- `.claude/memory/patterns.md` và `mistakes.md`
- Code hiện tại của phần sẽ đụng tới

Đọc tới khi giải thích được **hiện tại nó chạy thế nào** và **thay đổi này sẽ đụng vào đâu**. Không hỏi lại thứ dự án đã trả lời, không mở lại thứ user đã chốt.

## Tin nhắn đầu tiên

Đẩy công việc tiến lên, chọn một trong hai:

**User đã nói rõ muốn gì** (spec, plan, yêu cầu chi tiết) — khoảng 200 từ:
- Nhắc lại yêu cầu bằng lời của mình, để họ thấy mình hiểu đúng hình dạng: muốn gì, cho ai, vì sao.
- Thêm cái mình tìm được: phần liên quan hiện chạy ra sao, thay đổi sẽ đụng gì.
- Nếu phát hiện điều họ có thể chưa tính tới mà sẽ đổi cả yêu cầu hoặc cách làm — ràng buộc, xung đột, đường rẻ hơn — nói thẳng.
- Kết bằng một dòng mời sửa.
- Nếu họ **chưa nói tại sao**, hỏi: một câu mở, bằng ngôn ngữ của họ, không đưa lựa chọn sẵn.
- Nếu họ đã nói tại sao, mà vẫn còn một điểm mở sẽ **đổi thứ được build** → hỏi điểm đó, kèm các lựa chọn cụ thể, mỗi cái đánh đổi gì, mình chọn cái nào.
- Nếu không còn gì mở: đưa luôn design ngắn (hướng chọn, một dòng vì sao các hướng kia không hợp, design vài dòng) và xin duyệt.

**User chưa nói rõ:** một câu hỏi mở, về thứ lớn nhất còn bỏ ngỏ. Nói rõ **vì sao hỏi** — câu trả lời sẽ đổi điều gì trong thứ mình build.

## Nêu các hướng

Trước khi build, đưa các hướng ra **trong một message riêng**, để user chọn:

- 2–4 hướng, mỗi hướng tả cụ thể (không chung chung).
- Mỗi hướng: đánh đổi gì, đụng tới đâu trong codebase, tốn bao nhiêu.
- Nói rõ **mình chọn hướng nào và vì sao**. Đừng bày ra rồi im.
- Nếu một hướng vi phạm ràng buộc trong `CLAUDE.md` (Clean Architecture layer, file convention) — loại nó và nói rõ lý do.

## Chọn cỡ

| Cỡ | Khi nào | Sản phẩm |
|---|---|---|
| **Design ngắn** | Thay đổi gọn, một tầng, không đụng schema | Vài dòng trong chat: hướng, lý do, cái sẽ đụng |
| **Design đầy đủ** | Feature mới, đụng nhiều tầng, đổi schema/API contract | File trong `.claude/plans/{feature-slug}.md` |

Design đầy đủ gồm: schema, API contract, thứ tự implement, migration, ảnh hưởng tới business doc nào.

## Sau khi được duyệt

1. Design đầy đủ → ghi `.claude/plans/{feature-slug}.md`.
2. Đổi nghiệp vụ → nhớ sẽ phải update `.claude/business/{feature}.dod.md` sau khi code (theo `RULES.md`).
3. Rồi mới code, theo flow trong `.claude/agents/auto.md`.

## Spike thăm dò

Khi không ai biết cách nào chạy được: xin phép làm một spike nhỏ, **throwaway**, chỉ để trả lời một câu hỏi cụ thể. Nói rõ: hỏi gì, mất bao lâu, xong thì vứt. Spike không phải là bắt đầu implement.

---

## Cờ đỏ

- Bắt đầu code khi chưa biết **tại sao** user muốn thứ đó
- Hỏi thứ `CLAUDE.md` / `.claude/business/` đã trả lời
- Bày ra 3 phương án rồi không khuyến nghị cái nào
- Hỏi dồn 5 câu một lúc (hỏi **một** câu quan trọng nhất)
- Mở lại quyết định user đã chốt
- Viết plan khi user chưa duyệt hướng

## Khi nào bỏ qua skill này

- Task rõ ràng, một bước ("sửa typo", "đổi màu nút")
- User nói thẳng muốn làm luôn, khỏi hỏi
- Đang fix bug → dùng `systematic-debugging` thay vì skill này

---

Nguồn: phỏng theo `brainstorming` của [obra/superpowers](https://github.com/obra/superpowers) (MIT), chỉnh để nối vào `.claude/plans/` và `.claude/business/` của dự án.
