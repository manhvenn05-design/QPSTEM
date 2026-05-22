using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Areas.Admin.Models;
using STEM.Web.Data;
using STEM.Web.Services;

namespace STEM.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class PayrollController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly PayrollCalculationService _payrollCalculationService;

    public PayrollController(
        ApplicationDbContext context,
        PayrollCalculationService payrollCalculationService)
    {
        _context = context;
        _payrollCalculationService = payrollCalculationService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? year = null, int? month = null, CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var selectedYear = year ?? today.Year;
        var selectedMonth = month ?? today.Month;

        var records = await _context.PayrollRecords
            .AsNoTracking()
            .Where(x => x.Year == selectedYear && x.Month == selectedMonth)
            .OrderBy(x => x.Teacher.FullName)
            .Select(x => new PayrollRecordItemViewModel
            {
                Id = x.Id,
                TeacherId = x.TeacherId,
                TeacherName = x.Teacher.FullName,
                TeacherUsername = x.Teacher.Username,
                SalaryTier = x.Teacher.TeacherProfile != null ? x.Teacher.TeacherProfile.SalaryTier.ToString() : string.Empty,
                TotalValidSessions = x.TotalValidSessions,
                SessionEarnings = x.SessionEarnings,
                Bonuses = x.Bonuses,
                Deductions = x.Deductions,
                TotalPay = x.TotalPay,
                Status = x.Status,
                ApprovedAt = x.ApprovedAt,
                AdjustmentNotes = x.AdjustmentNotes
            })
            .ToListAsync(cancellationToken);

        var approvedTeachers = records.Count(x =>
            string.Equals(x.Status, AttendanceWorkflowService.PayrollRecordStatusApproved, StringComparison.OrdinalIgnoreCase));

        var readiness = await _payrollCalculationService.GetApprovalReadinessAsync(
            selectedYear,
            selectedMonth,
            records.Select(x => x.TeacherId).ToList(),
            cancellationToken);

        foreach (var record in records)
        {
            var itemReadiness = readiness.GetValueOrDefault(record.TeacherId);
            record.CanApprove = itemReadiness?.CanApprove ?? true;
            record.PendingSessionCount = itemReadiness?.PendingSessionCount ?? 0;
            record.ApprovalHint = itemReadiness?.Message ?? "Đủ điều kiện chốt lương.";
        }

        var model = new PayrollManagementViewModel
        {
            Year = selectedYear,
            Month = selectedMonth,
            PeriodLabel = $"Tháng {selectedMonth:00}/{selectedYear}",
            TotalTeachers = records.Count,
            ApprovedTeachers = approvedTeachers,
            ReadyToApproveTeachers = records.Count(x => !string.Equals(x.Status, AttendanceWorkflowService.PayrollRecordStatusApproved, StringComparison.OrdinalIgnoreCase) && x.CanApprove),
            BlockedTeachers = records.Count(x => !string.Equals(x.Status, AttendanceWorkflowService.PayrollRecordStatusApproved, StringComparison.OrdinalIgnoreCase) && !x.CanApprove),
            TotalPayout = records.Sum(x => x.TotalPay),
            DraftPayout = records
                .Where(x => !string.Equals(x.Status, AttendanceWorkflowService.PayrollRecordStatusApproved, StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.TotalPay),
            Records = records
        };

        ViewData["Title"] = "Bảng lương";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(int year, int month, CancellationToken cancellationToken = default)
    {
        var records = await _payrollCalculationService.GenerateMonthlyPayrollAsync(year, month, cancellationToken);
        TempData["SuccessMessage"] = records.Count == 0
            ? $"Chưa tạo được bản ghi lương nào cho tháng {month:00}/{year}. Hãy kiểm tra dữ liệu giáo viên, buổi học và trạng thái payroll."
            : $"Đã tạo/cập nhật {records.Count} bản ghi lương cho tháng {month:00}/{year}.";
        return RedirectToAction(nameof(Index), new { year, month });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int year, int month, int? teacherId = null, CancellationToken cancellationToken = default)
    {
        var result = await _payrollCalculationService.ApproveMonthlyPayrollAsync(year, month, teacherId, cancellationToken);
        var scopeLabel = teacherId.HasValue ? "giáo viên đã chọn" : $"tháng {month:00}/{year}";

        if (result.ApprovedCount > 0 && result.BlockedItems.Count == 0)
        {
            TempData["SuccessMessage"] = $"Đã chốt {result.ApprovedCount} bản ghi lương cho {scopeLabel}.";
        }
        else if (result.ApprovedCount > 0)
        {
            TempData["SuccessMessage"] = $"Đã chốt {result.ApprovedCount} bản ghi lương cho {scopeLabel}.";
            TempData["ErrorMessage"] = string.Join("; ", result.BlockedItems.Select(x => x.Message));
        }
        else if (result.BlockedItems.Count > 0)
        {
            TempData["ErrorMessage"] = string.Join("; ", result.BlockedItems.Select(x => x.Message));
        }
        else
        {
            TempData["SuccessMessage"] = teacherId.HasValue
                ? "Không tìm thấy bản ghi lương của giáo viên đã chọn."
                : $"Chưa có bảng lương để chốt cho tháng {month:00}/{year}.";
        }

        return RedirectToAction(nameof(Index), new { year, month });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unapprove(int year, int month, int? teacherId = null, CancellationToken cancellationToken = default)
    {
        var unapprovedCount = await _payrollCalculationService.UnapproveMonthlyPayrollAsync(year, month, teacherId, cancellationToken);
        TempData["SuccessMessage"] = unapprovedCount == 0
            ? (teacherId.HasValue
                ? "Giáo viên này hiện chưa ở trạng thái đã chốt."
                : $"Không có bản ghi đã chốt để hủy trong tháng {month:00}/{year}.")
            : (teacherId.HasValue
                ? "Đã hủy chốt bảng lương của giáo viên."
                : $"Đã hủy chốt {unapprovedCount} bản ghi lương trong tháng {month:00}/{year}.");
        return RedirectToAction(nameof(Index), new { year, month });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDraft(UpdateDraftViewModel model, int year, int month, CancellationToken cancellationToken = default)
    {
        var record = await _context.PayrollRecords.FirstOrDefaultAsync(x => x.Id == model.Id, cancellationToken);
        if (record == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy bản ghi lương.";
            return RedirectToAction(nameof(Index), new { year, month });
        }

        if (string.Equals(record.Status, AttendanceWorkflowService.PayrollRecordStatusApproved, StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Không thể chỉnh sửa bản ghi lương đã chốt.";
            return RedirectToAction(nameof(Index), new { year, month });
        }

        record.Bonuses = model.Bonuses;
        record.Deductions = model.Deductions;
        record.AdjustmentNotes = string.IsNullOrWhiteSpace(model.AdjustmentNotes) ? null : model.AdjustmentNotes.Trim();
        record.TotalPay = Math.Max(0m, record.SessionEarnings + record.Bonuses - record.Deductions);

        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = "Đã cập nhật thưởng/phạt cho giáo viên.";
        return RedirectToAction(nameof(Index), new { year, month });
    }
}
