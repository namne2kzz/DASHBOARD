# Wiki — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-06-23 | 20:58 | Khởi tạo document | Tạo document business ban đầu cho feature Wiki |

---

## 1. Purpose

Tài liệu hoá theo Repository, dạng cây (parent-child), dùng để lưu spec/document nội bộ team.

## 2. Key Entities & Relationships

- `WikiPage`: scope theo Repository, Title, Content (HTML/Markdown), `LastUpdated`, `ParentId` (cây — null = root page).
- Hỗ trợ soft-delete (`IsDeleted/DeletedAt/DeletedByUserId`).

## 3. Business Rules & Invariants

- Cần quyền `ManageWiki` để create/update/move/delete page; mọi member Repository được xem.
- Title bắt buộc (≤ 500 ký tự), Content ≤ 100,000 ký tự.
- Tạo page với `ParentId` chỉ định phải validate parent tồn tại trong cùng Repository.
- Move page: chống circular reference — parent mới không được là descendant của page đang move (tránh tạo vòng trong cây).
- 1 page không được là parent của chính nó.
- Không cho xoá page còn con — phải xoá hết children trước (bottom-up).
- `LastUpdated` tự stamp mỗi lần đổi content.

## 4. Main Workflows / Use Cases

1. Tạo root page (`ParentId = null`) hoặc child page dưới 1 parent có sẵn.
2. Update title/content → `LastUpdated` tự refresh.
3. Move page sang parent mới (đổi `ParentId`) → check circular reference.
4. Soft-delete page (chỉ khi không còn children).
5. Query toàn bộ page trong Repository để render tree/breadcrumb.

## 5. Definition of Done

- [ ] Move page luôn check circular reference trước khi đổi ParentId.
- [ ] Xoá page luôn check không còn children trước khi cho phép.
- [ ] ParentId khi tạo/move luôn validate parent cùng Repository.

## 6. Edge Cases & Notes

- Check circular reference cần load toàn bộ page trong Repository để dò ancestry.
- Page rỗng content vẫn hợp lệ.
- Move page về `ParentId = null` đồng nghĩa promote page đó thành root.
