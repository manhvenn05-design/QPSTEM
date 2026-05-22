# STEM System Improvement Plan

## 1. Mục tiêu

Tài liệu này mô tả kế hoạch cải thiện hệ thống STEM theo góc nhìn:

- kỹ thuật phần mềm
- nghiệp vụ vận hành trung tâm
- nghiệp vụ kế toán và công nợ
- kiểm soát nội bộ và tính nhất quán dữ liệu

Mục tiêu là để AI agent hoặc lập trình viên có thể đọc xong và triển khai tuần tự mà không phải đoán lại ý đồ nghiệp vụ.

## 2. Bối cảnh hiện tại

Hệ thống hiện đã chạy được các luồng chính:

- đăng nhập theo vai trò
- quản lý lớp, khóa học, buổi học
- điểm danh giáo viên
- AI hỗ trợ nhận xét và phân tích video
- quản lý hóa đơn, thanh toán
- quản lý bảng lương giáo viên

Tuy nhiên, sau giai đoạn đổi hướng nhiều lần, hệ thống đang có một số lỗi logic và lỗi nghiệp vụ quan trọng:

- logic chấm trạng thái payroll chưa phản ánh đúng nghiệp vụ thật
- hóa đơn bị hủy chưa được loại đúng khỏi luồng tài chính
- một số quyền truy cập và nhánh nghiệp vụ chưa đồng nhất
- vai trò người dùng đang không chuẩn hóa tuyệt đối
- còn thiếu guard rails để chống sai số tiền và sai báo cáo

## 3. Nguyên tắc triển khai

Khi sửa hệ thống, phải giữ các nguyên tắc sau:

1. Không sửa giao diện trước khi chốt nghiệp vụ lõi.
2. Các thay đổi liên quan tài chính, payroll, điểm danh phải có kiểm tra hồi quy.
3. Không xóa dữ liệu tài chính đã phát sinh nếu có thể thay bằng trạng thái audit-friendly.
4. Mọi enum/trạng thái nghiệp vụ quan trọng phải được chuẩn hóa thành hằng số hoặc enum dùng chung.
5. Mọi rule nghiệp vụ quan trọng phải nằm trong service/domain rule, không cài rải rác trong controller/view.

## 4. Danh sách vấn đề ưu tiên cao

### 4.1. Payroll status đang sai bản chất nghiệp vụ

#### Hiện trạng

`AttendanceIntegrityRules.ComputePayrollStatus(...)` hiện chỉ kiểm tra:

- buổi học trong tương lai thì `Pending`
- lớp không có học sinh thì `Invalid`
- nếu `attendanceCount >= studentCount` thì `Valid`
- ngược lại `Invalid`

Điều này bỏ qua hoàn toàn:

- học sinh có thật sự có mặt hay không
- giáo viên đã nhập nhận xét chưa
- đã có minh chứng video chưa
- đã có AI evaluation chưa

#### Rủi ro nghiệp vụ

- giáo viên có thể được tính lương cho buổi chưa hoàn thành nghiệp vụ
- báo cáo payroll không phản ánh chất lượng điểm danh
- thưởng/phạt downstream bị méo dữ liệu

#### Hướng sửa

Thiết kế lại rule `ComputePayrollStatus` theo nghiệp vụ rõ ràng.

Đề xuất rule:

- `Pending`:
  - buổi học chưa diễn ra
  - hoặc buổi học đã diễn ra nhưng chưa đủ số bản ghi attendance cho toàn bộ học sinh
- `Invalid`:
  - lớp không có học sinh
  - hoặc có học sinh có mặt nhưng thiếu nhận xét bắt buộc
  - hoặc thiếu minh chứng/AI ở mức nghiệp vụ yêu cầu
- `Valid`:
  - đủ attendance cho toàn bộ học sinh trong lớp
  - toàn bộ học sinh có mặt đã có ghi chú theo quy định
  - toàn bộ học sinh có mặt đã có minh chứng hợp lệ theo quy định

#### Việc cần làm

- rà lại định nghĩa nghiệp vụ chính xác với owner
- sửa `AttendanceIntegrityRules.ComputePayrollStatus`
- sửa các chỗ hiển thị label để phản ánh rule mới
- sửa các chỗ gọi `RecomputeSessionPayrollStatusAsync`
- thêm test case cho các tình huống:
  - đủ attendance nhưng thiếu note
  - đủ note nhưng thiếu media
  - tất cả vắng
  - buổi tương lai
  - lớp rỗng

#### Tiêu chí hoàn thành

- không còn trường hợp buổi được `Valid` chỉ vì đủ số record
- payroll estimate và payroll monthly generate ra cùng kết quả trên cùng dữ liệu

---

### 4.2. Void invoice chưa đúng nghiệp vụ kế toán

#### Hiện trạng

`VoidInvoice` chỉ set `Status = 4`, nhưng các logic khác vẫn:

- tính công nợ từ hóa đơn đã hủy
- cho phép ghi nhận thanh toán vào hóa đơn đã hủy
- sync lại trạng thái bằng số tiền đã thu mà không tôn trọng `Status = 4`

#### Rủi ro nghiệp vụ

- công nợ bị sai
- doanh thu và danh sách cần thu bị nhiễu
- thanh toán có thể đi vào chứng từ đã hủy
- audit trail mất ý nghĩa

#### Hướng sửa

Chuẩn hóa rõ các trạng thái hóa đơn:

- `1 = Unpaid`
- `2 = Partial`
- `3 = Paid`
- `4 = Voided`

Rule nghiệp vụ bắt buộc:

- hóa đơn `Voided` không được nhận thanh toán mới
- hóa đơn `Voided` không tính vào outstanding
- hóa đơn `Voided` không hiện trong danh sách hóa đơn cần thu
- nếu đã có payment thì hoặc:
  - không cho void
  - hoặc yêu cầu quy trình hoàn tiền/điều chỉnh riêng

Khuyến nghị hiện tại:

- không cho void nếu hóa đơn đã có payment
- chỉ cho void hóa đơn chưa phát sinh thu

#### Việc cần làm

- thêm helper dùng chung kiểu `IsInvoiceVoided`
- sửa `Index`, `InvoiceDetails`, `CreatePayment`, `EditPayment`, `PopulatePaymentOptionsAsync`, `PopulateInvoiceBalanceAsync`, `SyncInvoiceStatus`, `IsValidInvoiceAsync`
- quyết định rõ rule:
  - hóa đơn void có được edit không
  - hóa đơn void có được restore không
- bổ sung filter riêng cho `Voided`

#### Tiêu chí hoàn thành

- không thể tạo hoặc sửa payment vào hóa đơn đã hủy
- tổng công nợ không tính hóa đơn đã hủy
- trạng thái `Voided` không bị `SyncInvoiceStatus` ghi đè

---

### 4.3. Payroll generation bỏ qua giáo viên thiếu TeacherProfile

#### Hiện trạng

Trong `GenerateMonthlyPayrollAsync`, nếu giáo viên không có `TeacherProfile` thì hệ thống `continue`, tức là không sinh record payroll cho người đó.

#### Rủi ro nghiệp vụ

- giáo viên dạy thật nhưng không có dòng bảng lương
- admin không nhìn thấy lỗi cấu hình
- việc chốt lương có thể bỏ sót người

#### Hướng sửa

Thay vì bỏ qua, phải sinh record hoặc ít nhất sinh cảnh báo rõ ràng.

Khuyến nghị:

- vẫn sinh `PayrollRecord`
- `SessionEarnings = 0`
- `Bonuses = 0`
- `Deductions = 0`
- `Status = Draft`
- kèm `AdjustmentNotes = "Thiếu TeacherProfile hoặc thiếu cấu hình lương"`

Ngoài ra, dashboard/admin payroll nên hiển thị danh sách giáo viên đang thiếu cấu hình.

#### Việc cần làm

- sửa `GenerateMonthlyPayrollAsync`
- sửa `GetTeacherEstimateAsync` để kết quả nhất quán
- bổ sung cảnh báo trong admin payroll screen
- thêm query kiểm tra giáo viên có session nhưng thiếu profile hoặc thiếu pay rate

#### Tiêu chí hoàn thành

- mọi giáo viên có buổi dạy trong kỳ đều xuất hiện trong payroll
- người thiếu cấu hình không bị im lặng bỏ qua

---

### 4.4. Giáo viên dạy thay không dùng được AI đúng quyền

#### Hiện trạng

- `Teacher AttendancesController` cho phép truy cập buổi nếu là giáo viên chính hoặc giáo viên dạy thay
- `AICopilotController.AnalyzeVideo` chỉ cho giáo viên chính

#### Rủi ro nghiệp vụ

- giáo viên dạy thay điểm danh được nhưng không hoàn tất AI/media workflow
- payroll/compliance của buổi dạy thay có thể bị thiếu vì lỗi quyền

#### Hướng sửa

Chuẩn hóa rule quyền cho buổi học:

- giáo viên được thao tác nếu:
  - là giáo viên chính và chưa bị thay
  - hoặc là giáo viên dạy thay được gán vào buổi đó

Rule này phải được gom vào helper/service dùng chung.

#### Việc cần làm

- sửa query trong `AICopilotController.AnalyzeVideo`
- cân nhắc tách helper kiểu `CanTeacherOperateSession(teacherId, session)`
- rà lại các controller Teacher khác xem còn chỗ nào chỉ check `Class.TeacherId`

#### Tiêu chí hoàn thành

- giáo viên dạy thay có quyền giống giáo viên đang phụ trách thực tế của buổi
- giáo viên chính bị thay không còn sửa các tác vụ đã giao cho người khác

---

### 4.5. Role đang không chuẩn hóa tuyệt đối

#### Hiện trạng

Một số nơi dùng:

- `Admin`
- `Teacher`
- `Student`

Một số nơi lại xử lý thêm:

- `Giáo viên`
- `Học sinh`

Điều này tạo ra nguy cơ hệ thống authorize một kiểu nhưng query/filter một kiểu khác.

#### Rủi ro nghiệp vụ

- đăng nhập xong không redirect đúng
- role filter trong admin lệch với role authorize
- dữ liệu user cùng nghĩa nhưng khác tên

#### Hướng sửa

Chốt canonical role names:

- `Admin`
- `Teacher`
- `Student`

Tên hiển thị tiếng Việt phải nằm ở layer display, không nằm ở dữ liệu gốc.

#### Việc cần làm

- tạo constants hoặc enum cho role names
- sửa `AccountController`
- rà toàn bộ repo tìm `Role.Name == "..."` và chuẩn hóa
- viết script dữ liệu/migration để normalize role hiện có trong DB nếu cần

#### Tiêu chí hoàn thành

- toàn hệ thống chỉ dùng 1 bộ role canonical cho logic
- UI vẫn hiển thị tiếng Việt thông qua mapping display

## 5. Danh sách cải thiện ưu tiên trung bình

### 5.1. Chuẩn hóa invoice status và payment method

Tạo constants hoặc enum thay cho số ma thuật:

- invoice status
- equipment status
- maintenance status
- payroll status
- payment method

Mục tiêu:

- giảm bug so sánh sai
- giảm chỗ hard-code
- dễ đọc cho AI agent

### 5.2. Chuẩn hóa policy kiểm tra quyền theo session

Hiện logic quyền buổi học đang lặp ở nhiều controller.

Cần tạo service hoặc helper chung cho:

- teacher có quyền xem session không
- teacher có quyền sửa attendance không
- teacher có quyền chạy AI không
- session có bị lock theo thời gian không
- session có thuộc payroll đã approved không

### 5.3. Tách business rule khỏi controller

Controller hiện còn giữ nhiều rule nghiệp vụ trực tiếp, nhất là:

- FinanceController
- Teacher AttendancesController
- StudentPortalController

Nên tách thành service:

- `InvoiceService`
- `PaymentService`
- `TeacherSessionAccessService`
- `PayrollStatusService` hoặc giữ trong `AttendanceWorkflowService` nhưng làm rõ hơn

### 5.4. Tăng khả năng audit

Nên bổ sung các trường hoặc log nghiệp vụ cho các tác vụ nhạy cảm:

- ai void invoice
- ai approve payroll
- ai sửa payment
- ai xóa payment
- ai chỉnh attendance sau khi buổi kết thúc

Nếu chưa muốn thêm table audit riêng, tối thiểu cần log structured.

## 6. Danh sách cải thiện ưu tiên thấp nhưng nên làm

### 6.1. Bỏ cấu hình và dấu vết công nghệ cũ nếu không còn dùng

Ví dụ:

- cấu hình legacy cloud/AI cũ
- helper cũ không còn được gọi
- file test thủ công

### 6.2. Chuẩn hóa encoding và tiếng Việt

Nhiều file đang có dấu tiếng Việt lỗi encoding.

Việc này không làm vỡ nghiệp vụ ngay, nhưng gây:

- khó review
- khó prompt AI agent
- khó bảo trì

Nên chuẩn hóa toàn bộ source sang UTF-8.

### 6.3. Bổ sung health checklist cho release

Tạo checklist trước khi release:

- login đủ 3 role
- tạo invoice
- void invoice
- thu tiền
- edit payment
- attendance board
- AI note
- AI video
- generate payroll
- approve payroll

## 7. Kế hoạch triển khai theo pha

## Phase 1 - Chặn sai nghiệp vụ nghiêm trọng

Mục tiêu:

- chặn sai số tiền
- chặn sai payroll
- chặn sai quyền AI

Việc làm:

1. Sửa `ComputePayrollStatus`
2. Sửa toàn bộ luồng `VoidInvoice`
3. Sửa `GenerateMonthlyPayrollAsync` với giáo viên thiếu profile
4. Sửa quyền teacher substitute trong AI flow
5. Chuẩn hóa role constants ở các chỗ critical

Kết quả mong muốn:

- không còn sai nghiệp vụ lớn trong payroll và finance

## Phase 2 - Chuẩn hóa domain rules

Mục tiêu:

- giảm trùng lặp
- dễ bảo trì
- dễ giao việc cho AI agent khác

Việc làm:

1. Tạo constants/enums cho status
2. Tạo service kiểm tra quyền theo session
3. Tách finance business logic khỏi controller
4. Tách payroll validation logic rõ hơn

## Phase 3 - Tăng quality và auditability

Mục tiêu:

- dễ kiểm tra
- dễ điều tra lỗi
- dễ nâng cấp sau này

Việc làm:

1. Thêm test
2. Thêm audit logs
3. Chuẩn hóa encoding UTF-8
4. Dọn code cũ và config thừa

## 8. Test cases bắt buộc sau khi sửa

### Payroll

- session tương lai phải là `Pending`
- session đủ attendance nhưng thiếu note phải không được `Valid`
- session đủ note nhưng thiếu media/AI theo rule phải không được `Valid`
- session đủ toàn bộ điều kiện mới được `Valid`
- giáo viên thiếu `TeacherProfile` vẫn xuất hiện trong payroll tháng
- approve payroll xong thì teacher không còn sửa attendance buổi thuộc kỳ đã approved

### Finance

- tạo invoice mới bình thường
- không tạo payment vượt công nợ
- edit payment không làm âm công nợ
- void invoice chưa thu được phép
- void invoice đã thu không được phép nếu chọn policy “không cho void khi đã thu”
- invoice void không tính outstanding
- invoice void không nhận payment mới

### Permission

- giáo viên chính thao tác bình thường khi chưa có substitute
- giáo viên chính bị chặn khi buổi đã giao cho substitute
- substitute được điểm danh và chạy AI
- user role canonical vẫn authorize đúng toàn hệ thống

## 9. Definition of Done

Một hạng mục chỉ được xem là xong khi đáp ứng đủ:

1. Rule nghiệp vụ đã rõ bằng code và không còn hard-code mâu thuẫn ở controller khác.
2. Build pass.
3. Test thủ công cho luồng chính pass.
4. Không còn chỗ khác trong repo dùng logic cũ mâu thuẫn.
5. Nếu thay đổi dữ liệu nghiệp vụ, có ghi chú migration hoặc data-fix cần chạy.

## 10. Gợi ý prompt cho AI agent triển khai

### Prompt cho Phase 1

```text
Hãy triển khai Phase 1 của file system_improvement_plan.md.
Ưu tiên:
1. sửa ComputePayrollStatus cho đúng nghiệp vụ
2. sửa luồng VoidInvoice để hóa đơn hủy không còn tham gia công nợ và không nhận payment
3. đảm bảo giáo viên thiếu TeacherProfile vẫn xuất hiện trong payroll tháng
4. sửa quyền AI cho giáo viên dạy thay
5. chuẩn hóa role canonical ở các chỗ critical

Yêu cầu:
- không thay đổi UI quá mức nếu chưa cần
- ưu tiên sửa service và controller logic
- sau khi sửa phải build lại
- nêu rõ file nào đã sửa và test nào đã chạy
```

### Prompt cho Phase 2

```text
Hãy refactor theo Phase 2 trong system_improvement_plan.md.
Mục tiêu là gom status constants, tách quyền session và giảm logic nghiệp vụ nằm trong controller.
Không đổi behavior đã chốt ở Phase 1.
```

## 11. Ghi chú cuối

Nếu cần chọn đúng thứ tự để vibe coding an toàn, nên làm theo thứ tự:

1. payroll status
2. void invoice
3. payroll generation cho giáo viên thiếu cấu hình
4. quyền substitute teacher
5. role canonical
6. refactor sạch code

Thứ tự này giúp giảm nguy cơ sửa lan man vào UI trước khi chốt xong nghiệp vụ lõi.
