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
                ApprovedAt = x.ApprovedAt
            })
            .ToListAsync(cancellationToken);

        var model = new PayrollManagementViewModel
        {
            Year = selectedYear,
            Month = selectedMonth,
            PeriodLabel = $"Tháng {selectedMonth:00}/{selectedYear}",
            TotalTeachers = records.Count,
            ApprovedTeachers = records.Count(x => string.Equals(x.Status, AttendanceWorkflowService.PayrollRecordStatusApproved, StringComparison.OrdinalIgnoreCase)),
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
        await _payrollCalculationService.GenerateMonthlyPayrollAsync(year, month, cancellationToken);
        TempData["SuccessMessage"] = $"Đã tạo/cập nhật bảng lương cho tháng {month:00}/{year}.";
        return RedirectToAction(nameof(Index), new { year, month });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int year, int month, CancellationToken cancellationToken = default)
    {
        var approvedCount = await _payrollCalculationService.ApproveMonthlyPayrollAsync(year, month, cancellationToken);
        TempData["SuccessMessage"] = approvedCount == 0
            ? $"Chưa có bảng lương để chốt cho tháng {month:00}/{year}."
            : $"Đã chốt {approvedCount} bản ghi lương cho tháng {month:00}/{year}.";
        return RedirectToAction(nameof(Index), new { year, month });
    }
}
