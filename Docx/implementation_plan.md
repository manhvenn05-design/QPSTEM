# IMPLEMENTATION PLAN - QPSTEM STEM.Web

Trạng thái cập nhật: 2026-05-05

## Hiện trạng

### Đã hoàn thành
- Public site đã có layout và các trang nền chính.
- `Areas/Admin` đã có layout, dashboard và logic thực cho:
  - `Users`
  - `Courses`
  - `Classes`
  - `Sessions`
  - `Attendances`
  - `Inventory`
  - `Finance`
  - `CMS`
- Chuỗi vận hành admin hiện đã đi được theo luồng:
  - tạo người dùng
  - tạo khóa học
  - tạo lớp học
  - thêm học viên vào lớp
  - tạo buổi học
  - mở điểm danh
- `Areas/Teacher` đã có shell riêng và phần `Attendances` theo buổi học.

### Đang làm dở / phụ thuộc phần khác
- `Teacher`
  - `Schedule`
  - `Evidence`
  - `AI Copilot`
  - `Borrow / Return Equipment`
- `StudentPortal`
  - mới ở mức giao diện nền
  - logic còn phụ thuộc thêm vào teacher workflow

### Không nên hiểu sai
- `Settings` hiện là trang điều hướng quản trị, không phải nơi lưu cấu hình DB thật.
- `Admin Attendance` là màn kiểm soát dữ liệu theo buổi.
- `Teacher Attendance` mới là màn thao tác vận hành chính cho giáo viên.

## Ưu tiên tiếp theo

1. Hoàn thiện `Teacher Schedule`
2. Hoàn thiện `Teacher Evidence`
3. Hoàn thiện `Teacher Borrow / Return Equipment`
4. Nối logic cho `StudentPortal`
5. Quay lại tối ưu đồng bộ toàn bộ UI/wording

## Checklist vận hành trước khi test teacher

- Có ít nhất 1 user role `Teacher`
- Có ít nhất 1 user role `Student`
- Có `Course`
- Có `Class` gán cho teacher
- Có `Enrollment` đưa student vào class
- Có `Session` thuộc class đó

Nếu thiếu một mắt xích ở trên thì `Teacher > Điểm danh` sẽ không có dữ liệu để hiển thị.
