---
key: guides/backlog
title: Quản lý Backlog
module: backlog
type: UserGuide
route: /backlog
language: vi
suggestions:
  - Promote to Sprint là gì?
  - Vì sao không đưa được item vào sprint?
  - Đổi trạng thái nhiều item cùng lúc thế nào?
  - Story point và T-shirt size khác nhau ra sao?
---
# Quản lý Backlog

Backlog là danh sách tất cả công việc cần làm của project, sắp theo thứ tự ưu tiên. Bạn quản lý backlog ở màn hình **Backlog**.

## Cấu trúc Epic → Feature → User Story

Backlog được tổ chức thành cây 3 cấp:

- **Epic**: mục tiêu lớn, kéo dài nhiều sprint.
- **Feature**: một tính năng thuộc Epic.
- **User Story**: một phần việc cụ thể thuộc Feature, đủ nhỏ để làm trong một sprint.

Bạn có thể tạo item ở bất kỳ cấp nào và chọn item cha (Epic hoặc Feature) nếu muốn. Hệ thống không cho tạo quan hệ vòng (ví dụ A là cha của B trong khi B lại là cha của A).

## Ước lượng: Story point và T-shirt size

- **User Story** ước lượng bằng **story point** theo dãy Fibonacci (1, 2, 3, 5, 8, 13…, tối đa 100).
- **Epic** và **Feature** ước lượng bằng **T-shirt size**: XS, S, M, L, XL.

Mỗi item chỉ dùng một kiểu ước lượng. Ước lượng là **không bắt buộc**, nên item chưa ước lượng vẫn hợp lệ.

## Trạng thái refinement

Item đi qua các trạng thái:

1. **New**: vừa tạo.
2. **Refining**: đang làm rõ yêu cầu, acceptance criteria.
3. **Ready**: đã đủ rõ để đưa vào sprint.
4. **Promoted**: đã được đưa vào sprint thành task.

Bạn có thể ghi acceptance criteria và đính kèm tài liệu refinement cho từng item.

## Sắp xếp thứ tự ưu tiên

Kéo thả item lên xuống để đổi thứ tự ưu tiên. Hệ thống tự lưu thứ tự, bạn không cần thao tác gì thêm.

## Gán tạm item vào sprint

Khi lên kế hoạch, bạn có thể chọn sprint cho item (**Select sprint**) để đánh dấu dự định làm trong sprint đó. Việc gán tạm này **không làm đổi trạng thái** của item: item đang **Refining** vẫn giữ nguyên **Refining**.

## Promote to Sprint (đưa item vào sprint)

Promote to Sprint biến một User Story thành task thực sự trong sprint để team bắt đầu làm.

- **Chỉ User Story** mới promote được, Epic và Feature thì không.
- User Story phải ở trạng thái **Ready**.
- Sau khi promote, User Story xuất hiện trong sprint (kế thừa story point), và item trong backlog chuyển sang **Promoted**. Team tiếp tục chia story thành Task trong sprint.

### Vì sao không đưa được item vào sprint?

- Item không phải User Story.
- Item chưa ở trạng thái **Ready**. Hãy chuyển sang **Ready** trước.
- Role của bạn chưa có quyền **Promote to Sprint**. Hãy nhờ Scrum Master cấp quyền.

### Đưa User Story ra khỏi sprint

Nếu đã promote nhầm hoặc không kịp làm, mở story trong màn hình **Sprint** và bấm **De-scope story**. Story quay lại backlog ở trạng thái **Ready**, còn các task con của nó bị xoá. Xem thêm phần "Quản lý Sprint".

## Thao tác hàng loạt

Bạn có thể xử lý nhiều item cùng lúc (cần quyền quản lý backlog):

1. Bật chế độ chọn. Khi đang chọn, màn hình hiện "Tick rows to select. Dragging is paused." (tạm tắt kéo thả).
2. Tick các item cần xử lý.
3. Chọn **Set state…** để đổi trạng thái, hoặc **Delete** để xoá. Cả hai đều có bước xác nhận.

Quy tắc:

- **Đổi trạng thái hàng loạt** chỉ đổi được sang **New**, **Refining** hoặc **Ready**. Item đã **Promoted** sẽ được **bỏ qua** (không bị kéo ngược lại).
- **Xoá hàng loạt**: item còn item con mà bạn **không** chọn cùng sẽ được **bỏ qua**, để không bỏ lại item con mồ côi. Muốn xoá cả nhánh thì chọn cả cha lẫn con.
- Sau khi xong, hệ thống báo số item **đã xử lý** và số item **bị bỏ qua**. Tổng hai số này luôn bằng số item bạn đã chọn.

## Lọc backlog

Lọc theo loại (Epic / Feature / User Story) hoặc theo trạng thái. Kết quả **giữ nguyên cấu trúc cây**: item khớp bộ lọc được hiển thị kèm các item cha của nó để bạn biết nó nằm ở đâu, còn các item con khác không khớp thì không hiện ra.
