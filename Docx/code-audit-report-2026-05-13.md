# Code Audit Report

Ngày quét: `2026-05-13`

Phạm vi đã rà soát:

- `STEM.Web/Areas/Teacher`
- `STEM.Web/Areas/Admin`
- `STEM.Web/Controllers`
- `STEM.Web/Program.cs`
- `STEM.Web/Data/ApplicationDbContext.cs`
- `TestGemini`

Build verification:

- `dotnet build STEM.sln`: thành công, `0 warnings`, `0 errors`

## 1. Critical

### 1.1. Lộ API key Google AI trong source

Mức độ: `Critical`

Vị trí:

- `STEM.Web/appsettings.json:8`
- `TestGemini/Program.cs:11`

Mô tả:

- API key đang được commit trực tiếp vào source code.
- Điều này cho phép người khác dùng key để gọi dịch vụ AI, làm phát sinh chi phí hoặc lạm dụng hệ thống.

Rủi ro:

- Lộ bí mật hệ thống
- Tăng chi phí ngoài kiểm soát
- Có thể bị dùng để tấn công danh tiếng hoặc spam

Khuyến nghị:

- Thu hồi key hiện tại ngay.
- Chuyển sang `User Secrets`, biến môi trường, hoặc secret store.
- Không commit key thật vào repo.

### 1.2. Có endpoint công khai tạo admin mặc định

Mức độ: `Critical`

Vị trí:

- `STEM.Web/Controllers/AccountController.cs:120`

Mô tả:

- `SeedAdmin()` là `GET` công khai.
- Endpoint này có thể tạo tài khoản `admin` với mật khẩu cứng `123456aA@`.

Rủi ro:

- Chiếm quyền quản trị hệ thống
- Toàn bộ Admin area bị compromise

Khuyến nghị:

- Xóa endpoint này khỏi production code.
- Nếu cần seed dữ liệu, chuyển sang migration/seed nội bộ hoặc script vận hành riêng.

### 1.3. AI video flow có nguy cơ path traversal và exfiltration file local

Mức độ: `Critical`

Vị trí:

- `STEM.Web/Areas/Teacher/Controllers/AICopilotController.cs:137`

Mô tả:

- `VideoUrl` từ client được ghép thành path local bằng `Path.Combine(...)`.
- Không có bước xác thực rằng file phải nằm đúng trong thư mục upload dự kiến.
- Nếu đầu vào bị lách, hệ thống có thể đọc file local ngoài phạm vi video rồi gửi tiếp lên Google AI.

Rủi ro:

- Rò rỉ dữ liệu nội bộ từ server
- Đọc file trái phép

Khuyến nghị:

- Chỉ cho phép đọc file nằm trong thư mục `wwwroot/uploads/videos`.
- Chuẩn hóa path và kiểm tra path cuối cùng có nằm trong root cho phép hay không.
- Không tin dữ liệu `VideoUrl` từ client.

### 1.4. Upload file video lên thư mục public mà thiếu kiểm soát chặt

Mức độ: `Critical`

Vị trí:

- `STEM.Web/Areas/Teacher/Controllers/AttendancesController.cs:413`

Mô tả:

- Upload chỉ kiểm kích thước, chưa kiểm MIME type thật phía server, chưa whitelist extension chặt chẽ.
- File được lưu trực tiếp dưới `wwwroot/uploads/videos`.

Rủi ro:

- Upload file không mong muốn
- Public exposure của file vừa upload
- Tăng bề mặt tấn công

Khuyến nghị:

- Kiểm tra MIME type và extension ở server.
- Đổi tên file an toàn.
- Cân nhắc lưu ngoài `wwwroot`, chỉ public qua endpoint kiểm soát.

## 2. High

### 2.1. API AI và upload thiếu CSRF protection

Mức độ: `High`

Vị trí:

- `STEM.Web/Areas/Teacher/Controllers/AICopilotController.cs:25`
- `STEM.Web/Areas/Teacher/Controllers/AICopilotController.cs:121`
- `STEM.Web/Areas/Teacher/Controllers/AttendancesController.cs:413`
- `STEM.Web/Areas/Teacher/Views/Attendances/Board.cshtml:334`
- `STEM.Web/Areas/Teacher/Views/Attendances/Board.cshtml:387`

Mô tả:

- Các endpoint POST AI/upload không có `[ValidateAntiForgeryToken]`.
- JS gọi `fetch` trực tiếp nhưng không gửi anti-forgery token.

Rủi ro:

- CSRF trên tài khoản giáo viên
- Gọi AI ngoài ý muốn
- Upload file trái phép dưới ngữ cảnh user đã đăng nhập

Khuyến nghị:

- Thêm anti-forgery validation cho các endpoint POST.
- Gửi token trong `fetch`.

### 2.2. Màn điểm danh giáo viên mặc định coi học sinh có mặt dù chưa điểm danh

Mức độ: `High`

Vị trí:

- `STEM.Web/Areas/Teacher/Controllers/AttendancesController.cs:304`
- `STEM.Web/Areas/Teacher/Controllers/AttendancesController.cs:354`
- `STEM.Web/Areas/Teacher/Views/Attendances/Board.cshtml:132`

Mô tả:

- Khi chưa có bản ghi attendance, `IsPresent` mặc định là `true`.
- Giao diện render checkbox đã tick sẵn.
- `PresentCount` cũng tính theo dữ liệu mặc định này.

Tác động nghiệp vụ:

- Gây ảo giác rằng cả lớp đã có mặt.
- Giáo viên dễ bấm lưu và vô tình tạo dữ liệu sai.
- Thống kê hiện tại không phản ánh trạng thái thật.

Khuyến nghị:

- Tách rõ 3 trạng thái: `chưa ghi nhận`, `có mặt`, `vắng`.
- Không mặc định tick `có mặt` khi chưa có bản ghi.

### 2.3. Nghiệp vụ vắng có phép/vắng không phép đang suy diễn sai

Mức độ: `High`

Vị trí:

- `STEM.Web/Areas/Admin/Controllers/AttendancesController.cs:229`
- `STEM.Web/Areas/Admin/Controllers/AttendancesController.cs:642`

Mô tả:

- Hệ thống đang map `excused` bằng cách: `IsPresent = false` và `TeacherRawNote` có nội dung.
- Điều này đồng nghĩa mọi trường hợp vắng mà có ghi chú đều bị hiểu là "có phép".

Tác động nghiệp vụ:

- Sai bản chất điểm danh
- Báo cáo chuyên cần sai
- Không thể phân biệt lý do nghỉ thật với ghi chú nội bộ

Khuyến nghị:

- Thêm cột trạng thái attendance riêng, không suy diễn từ note.

### 2.4. Tài chính không khóa quan hệ học sinh - lớp

Mức độ: `High`

Vị trí:

- `STEM.Web/Areas/Admin/Controllers/FinanceController.cs:183`
- `STEM.Web/Areas/Admin/Controllers/FinanceController.cs:264`

Mô tả:

- Tạo/sửa invoice chỉ kiểm học sinh hợp lệ và lớp hợp lệ.
- Không kiểm học sinh đó có thuộc lớp được chọn hay không.

Tác động nghiệp vụ:

- Lập công nợ sai người/sai lớp
- Báo cáo tài chính và vận hành bị lệch

Khuyến nghị:

- Bắt buộc validate `StudentId` thuộc `ClassId` nếu invoice gắn lớp.

### 2.5. Chuyển lead sang học viên chưa validate lớp đích đúng nghiệp vụ

Mức độ: `High`

Vị trí:

- `STEM.Web/Areas/Admin/Controllers/LeadsController.cs:159`

Mô tả:

- Cho phép ghi danh vào `SelectedClassId` nhưng không validate lớp có đúng khóa học lead quan tâm hay không.
- Không thấy chặn lớp đã kết thúc hoặc không phù hợp trạng thái tuyển sinh.

Tác động nghiệp vụ:

- Tuyển sinh sai lớp
- Dữ liệu lead và enrollment lệch nhau

Khuyến nghị:

- Validate lớp đích theo khóa học quan tâm và trạng thái lớp.

## 3. Medium

### 3.1. Có XSS risk khi render kết quả AI bằng `innerHTML`

Mức độ: `Medium`

Vị trí:

- `STEM.Web/Areas/Teacher/Views/Attendances/Board.cshtml:346`
- `STEM.Web/Areas/Teacher/Views/Attendances/Board.cshtml:465`
- `STEM.Web/Areas/Teacher/Views/Attendances/Board.cshtml:468`

Mô tả:

- Nội dung AI được đổ vào DOM bằng `innerHTML`.
- Nếu response chứa HTML/script không mong muốn, browser có thể render hoặc thực thi.

Khuyến nghị:

- Dùng `textContent` hoặc escape dữ liệu trước khi render.

### 3.2. Layout Admin/Teacher query DB trực tiếp trong Razor view

Mức độ: `Medium`

Vị trí:

- `STEM.Web/Areas/Teacher/Views/Shared/_TeacherLayout.cshtml:656`
- `STEM.Web/Areas/Admin/Views/Shared/_AdminLayout.cshtml:851`

Mô tả:

- Layout đang tự truy vấn profile và số liệu tổng hợp.
- Admin layout còn chạy nhiều `CountAsync` ngay trong view.

Hệ quả:

- Tăng số query nền trên mọi page
- Khó test
- Khó tối ưu cache
- Làm view gánh luôn business/data access

Khuyến nghị:

- Chuyển sang view component, base controller, hoặc dedicated layout view model.

### 3.3. UI kéo thả video là giả lập, progress bar không phản ánh thật

Mức độ: `Medium`

Vị trí:

- `STEM.Web/Areas/Teacher/Views/Attendances/Board.cshtml:153`
- `STEM.Web/Areas/Teacher/Views/Attendances/Board.cshtml:176`

Mô tả:

- Giao diện nói hỗ trợ kéo thả nhưng không thấy xử lý drag/drop thật.
- Progress bar luôn hiển thị hoàn tất, không có phần trăm upload thực.

Tác động:

- Gây hiểu nhầm cho người dùng
- Tạo cảm giác UI "làm màu"

Khuyến nghị:

- Hoặc triển khai drag/drop thật với progress thật, hoặc bỏ phần mô phỏng.

### 3.4. Layout chưa tối ưu mobile/responsive

Mức độ: `Medium`

Vị trí:

- `STEM.Web/Areas/Teacher/Views/Shared/_TeacherLayout.cshtml:117`
- `STEM.Web/Areas/Admin/Views/Shared/_AdminLayout.cshtml:103`

Mô tả:

- Sidebar fixed kết hợp `margin-left` cứng.
- Chưa thấy cơ chế collapse/hamburger/menu mobile tương ứng.

Tác động:

- Trên màn nhỏ sẽ khó thao tác
- Có nguy cơ vỡ layout hoặc chiếm không gian nội dung

Khuyến nghị:

- Thiết kế lại shell cho tablet/mobile.

### 3.5. Trang Settings thực chất là dashboard shortcut, không phải cài đặt

Mức độ: `Medium`

Vị trí:

- `STEM.Web/Areas/Admin/Controllers/SettingsController.cs:20`
- `STEM.Web/Areas/Admin/Views/Settings/Index.cshtml:7`

Mô tả:

- Trang `Settings` chỉ hiển thị số liệu tổng hợp và link điều hướng.
- Không có cấu hình hệ thống thực thụ.

Tác động:

- Lệch kỳ vọng người dùng
- Naming không đúng bản chất

Khuyến nghị:

- Đổi tên menu/trang hoặc phát triển đúng tính năng cài đặt.

### 3.6. Trả lỗi thô từ AI service ra client

Mức độ: `Medium`

Vị trí:

- `STEM.Web/Areas/Teacher/Controllers/AICopilotController.cs:93`
- `STEM.Web/Areas/Teacher/Controllers/AICopilotController.cs:117`
- `STEM.Web/Areas/Teacher/Controllers/AICopilotController.cs:163`
- `STEM.Web/Areas/Teacher/Controllers/AICopilotController.cs:239`
- `STEM.Web/Areas/Teacher/Controllers/AICopilotController.cs:268`

Mô tả:

- Hệ thống trả nguyên `responseString` hoặc `ex.Message` về client.

Tác động:

- Lộ chi tiết nội bộ
- Gây khó kiểm soát trải nghiệm lỗi

Khuyến nghị:

- Log chi tiết ở server, chỉ trả message an toàn cho người dùng.

## 4. Low / Technical Debt

### 4.1. Tailwind dùng CDN trong layout

Mức độ: `Low`

Vị trí:

- `STEM.Web/Areas/Admin/Views/Shared/_AdminLayout.cshtml:12`
- `STEM.Web/Areas/Teacher/Views/Shared/_TeacherLayout.cshtml:12`
- `STEM.Web/Views/Shared/_StudentLayout.cshtml:12`

Mô tả:

- CSS framework được load từ CDN runtime.

Tác động:

- Khó quản lý CSP
- Khó build ổn định cho production
- Không tối ưu bundle/cache nội bộ

Khuyến nghị:

- Chuyển sang asset build nội bộ nếu dự án tiếp tục mở rộng.

### 4.2. Có dấu hiệu mã thử nghiệm còn nằm trong repo production

Mức độ: `Low`

Vị trí:

- `TestGemini/Program.cs`
- `STEM.Web/error.html`
- `STEM.Web/error2.html`
- `STEM.Web/error_page.html`
- `stdout.log`
- `stderr.log`

Mô tả:

- Repo đang chứa project test AI, file lỗi HTML, log runtime.

Tác động:

- Tăng nhiễu repo
- Khó phân biệt code chính thức và code test

Khuyến nghị:

- Dọn hoặc chuyển sang thư mục `sandbox`, `archive`, hoặc `.gitignore` phù hợp.

## 5. Ưu tiên xử lý đề xuất

### Ưu tiên 1: Bảo mật

- Gỡ `SeedAdmin`
- Thu hồi và đổi `GoogleAI.ApiKey`
- Vá anti-forgery cho AI/upload
- Chặn path traversal và siết upload

### Ưu tiên 2: Đúng nghiệp vụ

- Sửa mô hình điểm danh Teacher để không mặc định `có mặt`
- Thiết kế lại trạng thái attendance có `pending/present/absent/excused` thật
- Validate chặt quan hệ `student-class` ở Finance và Leads

### Ưu tiên 3: UX/UI

- Sửa shell Admin/Teacher cho mobile
- Bỏ UI giả progress/drag-drop hoặc triển khai thật
- Đổi tên/triển khai lại `Settings`

### Ưu tiên 4: Kiến trúc và maintainability

- Loại query DB khỏi layout
- Chuẩn hóa asset pipeline
- Dọn code test và file rác khỏi repo

