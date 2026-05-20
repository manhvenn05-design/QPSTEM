# Bản Mô Tả Yêu Cầu Tối Ưu Hệ Thống Tính Lương (Payroll Optimization Spec)

**Dự án:** STEM.Web (Education Center ERP)
**Công nghệ:** ASP.NET Core MVC 10, Entity Framework Core, SQL Server, Tailwind CSS (Vanilla).
**Mục tiêu:** Tối ưu hóa module tính lương (`PayrollController`, `PayrollCalculationService`) để giải quyết triệt để 3 bài toán nghiệp vụ phát sinh (Edge cases): Dạy thay, Điều chỉnh thủ công (Thưởng/Phạt), và Cập nhật bậc lương giữa tháng.

Bản spec này được thiết kế để một AI Agent có thể đọc hiểu bối cảnh và tự động triển khai (Vibe Coding).

---

## 🚀 Tính Năng 1: Xử lý "Dạy Thay" (Substitute Teaching)

**Vấn đề:** Lớp học được gán cố định cho 1 `TeacherId` trong bảng `Class`. Khi giáo viên này nghỉ, người khác dạy thay, hệ thống vẫn tính tiền cho giáo viên cũ (vì truy vấn qua `Class.TeacherId`).

**Yêu cầu triển khai:**
1. **Database Schema:** 
   - Thêm cột `SubstituteTeacherId` (nullable, foreign key to `Users`) vào bảng `Sessions`.
2. **Backend Logic (`PayrollCalculationService.cs`):** 
   - Cập nhật hàm tính lương (`GetPayrollEstimateAsync` và `GenerateMonthlyPayrollAsync`).
   - *Logic mới:* Khi tính lương cho một giáo viên X, hệ thống phải quét tất cả các `Sessions` thỏa mãn điều kiện:
     `(Class.TeacherId == X AND SubstituteTeacherId IS NULL) OR (SubstituteTeacherId == X)`.
   - Nếu buổi học do người dạy thay thực hiện, mức tiền ca đó sẽ áp dụng `TeacherProfile` (SalaryTier / CustomSessionRate) của người dạy thay, không phải của giáo viên chủ nhiệm.
3. **UI / Controller:** 
   - Thêm dropdown "Giáo viên dạy thay" trong Modal Cập nhật Buổi học (`SessionsController` -> `Edit`).

---

## 🚀 Tính Năng 2: Bảng Nháp Lương & Điều Chỉnh Thủ Công (Draft & Manual Adjustments)

**Vấn đề:** Hiện tại Admin bấm "Chốt lương" là hệ thống insert/update trực tiếp và khóa sổ. Kế toán không có giao diện để nhập thủ công các khoản tiền Phạt (đi trễ) hoặc Thưởng (Lễ/Tết).

**Yêu cầu triển khai:**
1. **Database Schema:** 
   - Cột `Bonuses` và `Deductions` đã có sẵn trong bảng `PayrollRecords`. 
   - Thêm cột `AdjustmentNotes` (NVARCHAR, nullable) vào `PayrollRecords` để ghi chú lý do thưởng/phạt.
2. **Workflow (Luồng hoạt động mới):**
   - **Bước 1 (Tính toán nháp):** Nút "Tạo bảng lương" ở `Admin/Payroll/Index` sẽ tạo ra `PayrollRecords` với trạng thái `Status = "Draft"`.
   - **Bước 2 (Giao diện Kế toán):** Trong màn hình `Payroll/Index`, nếu bản ghi đang là "Draft", hiển thị UI (hoặc Modal) cho phép Kế toán chỉnh sửa cột `Bonuses`, `Deductions`, và `AdjustmentNotes`.
   - Tổng thực nhận (`TotalPay`) = `SessionEarnings` + `Bonuses` - `Deductions`.
   - **Bước 3 (Chốt sổ):** Admin bấm nút "Duyệt" (Approve) -> chuyển Status thành `"Approved"`. Sau khi Approve, khóa (Disable) toàn bộ input, không cho sửa nữa.
3. **UI Cập nhật:** 
   - Chỉnh sửa `Admin/Views/Payroll/Index.cshtml` để các ô `Bonuses` và `Deductions` biến thành thẻ `<input type="number">` có thể nhập được (nếu Status là Draft), sử dụng AJAX (hoặc submit form) để lưu dữ liệu.

---

## 🚀 Tính Năng 3: Lịch Sử Bậc Lương (Tăng lương giữa tháng)

**Vấn đề:** Giáo viên tăng `SalaryTier` vào ngày 15. Nếu cuối tháng bấm chốt lương, toàn bộ buổi học ngày 1 đến ngày 14 cũng bị tính theo giá Tier mới (làm công ty bị lỗ).

**Yêu cầu triển khai (Chọn 1 trong 2 phương án tùy độ phức tạp):**

- **Phương án A (Đơn giản nhất - Chốt cứng giá trị tại thời điểm dạy):** 
  - Thêm cột `SessionRateApplied` (DECIMAL) vào bảng `Sessions`.
  - Hàng ngày, khi Job/AI chạy kiểm tra hoàn thành buổi học (Valid), hệ thống sẽ tính ngay ra số tiền của ca đó dựa vào Tier hiện tại và điền chết vào cột `SessionRateApplied`.
  - Cuối tháng, Kế toán tính lương chỉ việc `SUM(SessionRateApplied)` thay vì tính toán lại. Tăng lương hôm nào thì giá mới áp dụng từ hôm đó trở đi.

- **Phương án B (Quản lý Version Bậc lương):**
  - Tạo bảng mới `TeacherSalaryTierHistories`: `Id`, `TeacherId`, `SalaryTier`, `CustomSessionRate`, `EffectiveDate`.
  - Khi `PayrollCalculationService` tính tiền cho 1 Session, nó phải lookup (tra cứu) giá trị Tier trong bảng History có `EffectiveDate` gần nhất trước ngày của Session đó.

*(AI Agent khi thực hiện hãy phân tích và ưu tiên chọn **Phương án A** vì nó mang lại Hiệu năng truy vấn (Performance) tốt nhất khi Database phình to).*

---

## Hướng dẫn cho AI Agent (System Prompt Context)
- **Kiến trúc:** Bám sát tư duy "Fat Service, Skinny Controller". Đặt toàn bộ Business Logic vào `PayrollCalculationService`.
- **An toàn Dữ liệu:** Sử dụng Transaction `await using var transaction = await _context.Database.BeginTransactionAsync()` trong các hàm xử lý chốt sổ lương.
- **Frontend:** Giữ nguyên phong cách UI/UX "Forest Green" đang sử dụng trong toàn bộ Area Admin. Tái sử dụng các class như `.admin-card`, `.admin-btn-primary`, `.admin-input`.
