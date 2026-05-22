# Phase 1 Execution Checklist

## Mục tiêu

Checklist này dùng để triển khai nhanh các sửa lỗi nghiệp vụ mức nghiêm trọng trong hệ thống STEM.

## Checklist

- [ ] Chuẩn hóa rule `PayrollStatus` để không còn `Valid` chỉ vì đủ số record attendance.
- [ ] Đảm bảo buổi chưa đủ attendance hoặc thiếu note/media theo rule không được tính payroll hợp lệ.
- [ ] Đảm bảo giáo viên thiếu `TeacherProfile` vẫn xuất hiện trong payroll tháng với cảnh báo cấu hình.
- [ ] Chặn thanh toán mới vào hóa đơn đã `Voided`.
- [ ] Không tính hóa đơn `Voided` vào công nợ và các chỉ số cần thu.
- [ ] Đảm bảo `SyncInvoiceStatus` không ghi đè hóa đơn `Voided`.
- [ ] Chặn `VoidInvoice` nếu hóa đơn đã có payment.
- [ ] Cho phép giáo viên dạy thay dùng AI/video trên đúng buổi được phân công.
- [ ] Chặn giáo viên chính thao tác AI nếu buổi đã giao cho giáo viên dạy thay khác.
- [ ] Chuẩn hóa role claim khi đăng nhập để authorize và redirect ổn định.
- [ ] Rà các query critical theo role trong finance/auth để hỗ trợ dữ liệu role cũ tiếng Việt.
- [ ] Build lại toàn project.
- [ ] Test tay các luồng payroll, void invoice, create/edit payment, AI video substitute teacher.

## Kết quả mong muốn

- Không còn sai nghiệp vụ lớn ở payroll và finance.
- Không còn mismatch quyền ở luồng teacher substitute.
- Không còn rủi ro role tiếng Việt làm sai authorize sau khi người dùng đăng nhập lại.
