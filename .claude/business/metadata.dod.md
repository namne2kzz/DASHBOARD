# Metadata (RepositoryMetadata) — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|---|---|---|---|
| 2026-06-28 | 16:00 | RepoRole thay enum TeamRole | Member.DefaultRole & CapacityMember.Role giờ lưu **value RepoRole metadata** (string) thay enum `TeamRole` (đã xoá). Dropdown chức danh load từ metadata RepoRole của repo. |
| 2026-06-28 | 15:10 | Thêm IsGlobal + RepoRole + UI | Thêm cột `IsGlobal` (entry dùng chung mọi repo), `RepositoryId` nullable, key `RepoRole` (Team Role), lệnh Update, và trang quản lý Metadata (nav cạnh Members). |
| 2026-06-23 | 20:58 | Khởi tạo document | Document ban đầu cho catalog metadata repo (đính kèm trong repositories). |

---

## 1. Purpose

Quản lý **catalog key/value** cho repo: admin định nghĩa sẵn các giá trị chọn được cho từng key (vd version, build, **chức danh team role**), user pick từ list khi cần. Hỗ trợ entry **global** (dùng chung mọi repo) lẫn entry riêng từng repo.

## 2. Key Entities & Relationships

- `RepositoryMetadata`: `Id`, `RepositoryId` (nullable — null khi global), `IsGlobal` (bool), `Key` (`MetadataKey` lưu string), `Value` (string), soft-delete, `CreatedAt/UpdatedAt`.
- `MetadataKey` (enum, code): mỗi value có `[Display(Name)]` = title hiển thị trên UI. Lấy title qua `MetadataKeyExtensions.GetDisplayName()`. Hiện có: FixedInVersion, ImplementedInBuild, ReleaseNotes, QaNotes, DesignDocUrl, ExternalReference, Components, Labels, **RepoRole** (title "Team Role").
- DB lưu **key + value** (không lưu title — title load từ code).
- Member/Capacity **không FK** tới bảng này; nếu dùng value (vd chức danh) thì chỉ lưu chuỗi value (denormalized).

## 3. Business Rules & Invariants

- **Quyền**: entry `IsGlobal` → chỉ **GlobalAdmin** create/update/delete; entry theo repo → cần **ManageSettings** của repo đó. Mọi member đọc được (List).
- Tạo global → `RepositoryId = null`, `IsGlobal = true`.
- Trùng value: không cho 2 entry cùng `(scope, Key, Value)` đang active (scope = global hoặc 1 repo).
- Update chỉ sửa `Value` (không đổi Key/scope).
- Delete là **soft-delete** (ẩn khỏi list, stamp `DeletedByUserId`).
- `List` của 1 repo trả về **tất cả entry global + entry của repo đó**.
- Title hiển thị luôn lấy từ `[Display]` trong code, không lưu DB.

## 4. Main Workflows / Use Cases

1. Admin mở Settings → Metadata (nav cạnh Members) → xem list nhóm theo key.
2. Tạo entry: chọn Key (load title từ `GET .../metadata/keys`), nhập Value, tick **Global** (chỉ GlobalAdmin thấy) → save key+value.
3. Sửa Value của entry.
4. Soft-delete entry.
5. Khi tạo Repository → tự sinh 5 entry `RepoRole` **theo repo đó** (Developer/Tester/Scrum Master/Project Manager/Business Analyst). Admin thêm/sửa/xoá tiếp qua UI.

## 5. Definition of Done

- [ ] Global entry chỉ GlobalAdmin thao tác; repo entry cần ManageSettings.
- [ ] List trả về global + repo của repo hiện tại.
- [ ] Chặn trùng (scope, Key, Value) active khi create/update.
- [ ] Delete là soft-delete.
- [ ] UI: nav cạnh Members, key dropdown hiển thị title từ code, checkbox Global chỉ hiện với GlobalAdmin.

## 6. Edge Cases & Notes

- Đổi tên 1 value KHÔNG lan sang row member/capacity đang giữ value cũ (do lưu value, không FK) — chấp nhận theo thiết kế.
- Unique index `(RepositoryId, Key, Value)` filter `IsDeleted=0`: với global `RepositoryId` null nên global tách biệt với repo theo từng tuple.
- `RepoRole` là nguồn chức danh duy nhất: `RepositoryMember.DefaultRole` và `CapacityMember.Role` lưu **value** của RepoRole metadata (string), không FK. Enum `TeamRole` đã bị xoá khỏi code (xem [members.dod.md](members.dod.md)).
