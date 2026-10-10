---
key: guides/nhub-chat
title: Chat theo sprint với NHub
module: hub-integration
type: UserGuide
language: vi
suggestions:
  - Làm sao tạo kênh chat cho sprint?
  - Vì sao tôi không có trong kênh chat của sprint?
  - Sprint của tôi không có kênh chat?
---
# Chat theo sprint với NHub

**NHub** là ứng dụng chat đi kèm NFlow. Mỗi sprint có thể có một **kênh chat riêng** trên NHub để team trao đổi trong suốt sprint.

## Tạo kênh chat cho sprint

Khi tạo sprint, bật **Create NHub channel for this sprint**. Kênh được tạo ở chế độ **riêng tư (Private)**, người tạo sprint là chủ kênh. Kênh được gắn với sprint để mở nhanh từ NFlow.

## Ai có trong kênh?

Thành viên kênh đi theo **capacity của sprint**:

- Thêm người vào **Capacity planning** của sprint → người đó được thêm vào kênh.
- Xoá người khỏi capacity → người đó bị xoá khỏi kênh.

Vì vậy nếu bạn không có trong kênh, hãy nhờ người quản lý sprint thêm bạn vào capacity.

## Vì sao sprint không có kênh chat?

- Lúc tạo sprint **không bật** tạo kênh NHub.
- NHub **đang lỗi hoặc phản hồi chậm** lúc tạo sprint. Sprint vẫn được tạo bình thường nhưng không có kênh.
- Sprint được tạo trước khi có tính năng này.

Hiện **chưa có cách tạo kênh bổ sung** cho sprint đã tạo.

## Lưu ý

Việc thêm/xoá thành viên kênh chạy ngầm. Nếu NHub lỗi đúng lúc đó, danh sách thành viên kênh có thể lệch với capacity mà không có thông báo. NHub cũng dùng định dạng ngày và múi giờ bạn đã chọn trong Settings → General.
