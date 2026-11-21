# Work Item Links — Business Document

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-11-07 | 15:42 | Khởi tạo document | Tạo document business ban đầu cho feature Work Item Links — liên kết có hướng giữa các work item |

---

## 1. Purpose

Nối các work item lại với nhau để thấy **quan hệ giữa chúng**: việc này chặn việc kia, bug này gây ra bởi thay đổi nọ, hai phiếu trùng nhau, task này test cho story kia. Giúp người đọc một work item biết ngay nó dính tới cái gì mà không phải tự nhớ.

## 2. Key Entities & Relationships

- **`WorkItemLink`** — một liên kết giữa hai `SprintTask` trong **cùng một repository**.
- Field: `SourceTaskId`, `TargetTaskId`, `LinkType`, `Description` (ghi chú tùy chọn), `CreatedById`, `RepositoryId`.
- **Lưu một chiều, đọc hai chiều.** Chỉ có một bản ghi duy nhất cho một quan hệ; khi hiển thị, nhãn được lật theo phía đang xem.

**8 loại liên kết** (`WorkItemLinkType`), mỗi loại có nhãn xuôi/ngược:

| Loại | Nhìn từ source (outward) | Nhìn từ target (inward) |
|---|---|---|
| `Related` | Related to | Related to *(đối xứng)* |
| `Blocks` | Blocks | Blocked by |
| `Duplicate` | Duplicate of | Duplicated by |
| `DependsOn` | Depends on | Dependency of |
| `Successor` | Successor of | Predecessor of |
| `Affects` | Affects | Affected by |
| `Tests` | Tests | Tested by |
| `CausedBy` | Caused by | Causes |

- `Related` là loại **đối xứng** duy nhất — nhãn giống nhau cả hai chiều, không có quan hệ nghịch.
- Work item hiển thị bằng khóa dạng `{RepoCode}-{WorkItemNumber}` (ví dụ `DASH-142`), dựng bằng `SprintTask.BuildWorkItemNumber`.

## 3. Business Rules & Invariants

**Quyền**

- Mọi thao tác yêu cầu **là member của repository** → không thì `ForbiddenException`.
- Không phân quyền sâu hơn: **mọi member đều tạo và xóa được link**, kể cả link do người khác tạo.

**Ràng buộc khi tạo**

- **Không tự liên kết với chính mình**: `SourceTaskId == TargetTaskId` → `"A work item cannot be linked to itself."`
- **Cả hai work item phải thuộc cùng repository** và **chưa bị xóa mềm** (`!IsDeleted`). Thiếu một trong hai → `"One or both work items were not found in this repository."` Không liên kết được xuyên repository.
- **Không trùng lặp, không phân biệt chiều**: đã có `A → B` loại `Blocks` thì tạo `B → A` cùng loại `Blocks` cũng bị chặn (`"This link already exists."`). Kiểm tra quét cả hai chiều lưu trữ.
- **Hai loại khác nhau giữa cùng cặp item thì được phép** — `A Blocks B` và `A Related to B` tồn tại song song.
- `Description` rỗng hoặc chỉ khoảng trắng được chuẩn hóa thành **null**; có nội dung thì cắt khoảng trắng đầu/cuối.

**Hiển thị**

- Danh sách link của một work item gồm **mọi link chạm tới nó**, bất kể nó là source hay target.
- Nhãn hiển thị **theo góc nhìn của item đang mở**: nó là source → nhãn xuôi; là target → nhãn ngược. Cùng một bản ghi, mở từ hai phía sẽ thấy hai nhãn khác nhau (`Blocks` ↔ `Blocked by`).
- Mỗi dòng kèm thông tin của **item bên kia**: khóa, tiêu đề, loại, trạng thái hiện tại.
- Sắp xếp theo **nhãn**, rồi theo khóa work item.

**Lịch sử**

- Tạo và xóa link đều ghi history **vào cả hai work item**, mỗi bên theo nhãn của phía mình:
  - Tại source: `Linked: Blocks DASH-142.` · tại target: `Linked: Blocked by DASH-87.`
  - Khi xóa: `Removed link: ...` theo cùng quy tắc.
- Nhờ vậy mở lịch sử của bất kỳ phía nào cũng thấy quan hệ này từng được tạo/gỡ.

## 4. Main Workflows / Use Cases

1. **Tạo link** — mở work item → chọn loại quan hệ → tìm work item đích (`SearchLinkTargets`) → thêm ghi chú tùy chọn → tạo. Hệ thống kiểm tra tự-liên-kết, tồn tại, trùng lặp rồi lưu và ghi history hai phía.
2. **Xem quan hệ** — mở work item → thấy danh sách link, nhãn đã lật đúng theo góc nhìn, kèm trạng thái hiện tại của item bên kia (biết ngay cái đang chặn mình đã xong chưa).
3. **Điều hướng** — click một link → sang work item bên kia.
4. **Gỡ link** — xóa bản ghi, ghi history ở cả hai work item.

## 5. Definition of Done

- [ ] Chỉ member repository thao tác được.
- [ ] Không tạo được link tới chính nó.
- [ ] Không liên kết xuyên repository; không liên kết tới item đã xóa mềm.
- [ ] Trùng lặp bị chặn ở **cả hai chiều** cho cùng một loại.
- [ ] Cùng cặp item vẫn tạo được nhiều loại link khác nhau.
- [ ] Nhãn lật đúng theo phía đang xem; `Related` giống nhau hai chiều.
- [ ] History ghi ở cả hai work item khi tạo và khi xóa.
- [ ] Danh sách trả về cả link nó là source lẫn target.

## 6. Edge Cases & Notes

- **Không chống vòng lặp.** `A Blocks B`, `B Blocks C`, `C Blocks A` đều tạo được. Hệ thống không phát hiện chu trình trong quan hệ chặn/phụ thuộc.
- **Không kiểm tra tính nhất quán ngữ nghĩa.** Có thể vừa `A Blocks B` vừa `B Blocks A` (hai bản ghi khác chiều cùng loại **bị chặn**), nhưng `A Blocks B` + `A Depends on B` thì được — dù đọc lên hơi mâu thuẫn.
- **`Description` không hiện khác nhau theo chiều.** Ghi chú viết từ góc nhìn người tạo, người xem ở phía bên kia đọc cùng nội dung đó.
- **Xóa link không cần quyền đặc biệt** — khác với attachment (người đăng hoặc `ManageBoard`). Member nào cũng gỡ được link người khác tạo.
- Khi xóa link, nếu không lấy được `repoCode` thì **bỏ qua phần ghi history**, link vẫn bị xóa. Trường hợp này chỉ xảy ra nếu repository biến mất giữa chừng.
- Xóa mềm một work item **không gỡ các link** trỏ tới nó; link cũ vẫn còn trong DB, nhưng không tạo link mới tới item đã xóa được.
- Truy vấn danh sách chiếu từng field của "item bên kia" riêng lẻ thay vì lấy cả navigation entity — đây là ràng buộc kỹ thuật của EF (ternary trên navigation không materialize), không phải quy tắc nghiệp vụ.
