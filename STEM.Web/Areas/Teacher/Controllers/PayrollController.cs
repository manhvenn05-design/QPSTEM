using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Areas.Teacher.Models;
using STEM.Web.Data;
using STEM.Web.Models;
using STEM.Web.Services;

namespace STEM.Web.Areas.Teacher.Controllers;

[Area("Teacher")]
[Authorize(Roles = "Teacher")]
public class PayrollController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly PayrollCalculationService _payrollService;

    public PayrollController(
        ApplicationDbContext context,
        PayrollCalculationService payrollService)
    {
        _context = context;
        _payrollService = payrollService;
    }

    // GET /Teacher/Payroll
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var teacherId = GetCurrentTeacherId();
        if (!teacherId.HasValue)
        {
            return Challenge();
        }

        var teacher = await _context.Users
            .AsNoTracking()
            .Where(x => x.Id == teacherId.Value)
            .Select(x => new { x.FullName })
            .FirstOrDefaultAsync(ct);

        var teacherProfile = await _context.Set<TeacherProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == teacherId.Value, ct);

        var history = await _context.PayrollRecords
            .AsNoTracking()
            .Where(x => x.TeacherId == teacherId.Value)
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .Select(x => new TeacherPayrollHistoryItemViewModel
            {
                RecordId = x.Id,
                Year = x.Year,
                Month = x.Month,
                PeriodLabel = $"Tháng {x.Month:00}/{x.Year}",
                TotalValidSessions = x.TotalValidSessions,
                SessionEarnings = x.SessionEarnings,
                Bonuses = x.Bonuses,
                Deductions = x.Deductions,
                TotalPay = x.TotalPay,
                Status = x.Status,
                IsApproved = x.Status == AttendanceWorkflowService.PayrollRecordStatusApproved,
                CreatedAt = x.CreatedAt,
                ApprovedAt = x.ApprovedAt
            })
            .ToListAsync(ct);

        // Ước tính tháng hiện tại
        var now = DateTime.UtcNow;
        var estimate = await _payrollService.GetTeacherEstimateAsync(teacherId.Value, now.Year, now.Month, ct);

        // Kiểm tra xem tháng hiện tại đã có official record chưa
        var currentOfficialRecord = history
            .FirstOrDefault(x => x.Year == now.Year && x.Month == now.Month);

        var (tier, tierLabel) = GetTierInfo(teacherProfile);

        var model = new TeacherPayrollIndexViewModel
        {
            TeacherName = teacher?.FullName ?? string.Empty,
            SalaryTier = tier,
            SalaryTierLabel = tierLabel,
            HasSalaryTierConfigured = teacherProfile?.SalaryTier > 0 || teacherProfile?.CustomSessionRate.HasValue == true,
            CurrentMonthEstimate = new TeacherPayrollEstimateViewModel
            {
                Year = now.Year,
                Month = now.Month,
                PeriodLabel = $"Tháng {now.Month:00}/{now.Year}",
                TotalSessions = estimate.TotalSessions,
                ValidSessions = estimate.ValidSessions,
                PendingSessions = estimate.PendingSessions,
                InvalidSessions = estimate.InvalidSessions,
                EstimatedSessionEarnings = estimate.EstimatedSessionEarnings,
                EstimatedBonuses = estimate.EstimatedBonuses,
                EstimatedDeductions = estimate.EstimatedDeductions,
                EstimatedTotalPay = estimate.EstimatedTotalPay,
                HasOfficialRecord = currentOfficialRecord != null,
                OfficialRecordStatus = currentOfficialRecord?.Status
            },
            History = history
        };

        ViewData["Title"] = "Phiếu lương";
        return View(model);
    }

    // GET /Teacher/Payroll/Period?year=&month=
    [HttpGet]
    public async Task<IActionResult> Period(int year, int month, CancellationToken ct)
    {
        var teacherId = GetCurrentTeacherId();
        if (!teacherId.HasValue)
        {
            return Challenge();
        }

        if (year < 2020 || year > 2100 || month < 1 || month > 12)
        {
            return BadRequest("Kỳ lương không hợp lệ.");
        }

        var record = await _context.PayrollRecords
            .AsNoTracking()
            .Where(x => x.TeacherId == teacherId.Value && x.Year == year && x.Month == month)
            .Select(x => new
            {
                x.Id,
                x.Year,
                x.Month,
                x.TotalValidSessions,
                x.SessionEarnings,
                x.Bonuses,
                x.Deductions,
                x.TotalPay,
                x.Status,
                x.CreatedAt,
                x.ApprovedAt,
                TeacherName = x.Teacher.FullName
            })
            .FirstOrDefaultAsync(ct);

        if (record == null)
        {
            return NotFound("Không tìm thấy bảng lương cho kỳ này.");
        }

        var teacherProfile = await _context.Set<TeacherProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == teacherId.Value, ct);

        var (tier, tierLabel) = GetTierInfo(teacherProfile);

        var periodStart = new DateOnly(year, month, 1);
        var periodEnd = periodStart.AddMonths(1);

        var sessions = await _context.Sessions
            .AsNoTracking()
            .Where(x => x.Class.TeacherId == teacherId.Value &&
                        x.Date >= periodStart && x.Date < periodEnd)
            .OrderBy(x => x.Date)
            .ThenBy(x => x.StartTime)
            .Select(x => new
            {
                x.Id,
                x.SessionNo,
                x.Date,
                x.StartTime,
                x.EndTime,
                x.PayrollStatus,
                ClassCode = x.Class.ClassCode,
                CourseName = x.Class.Course.Name,
                CourseDifficulty = x.Class.Course.DifficultyLevel
            })
            .ToListAsync(ct);

        var totalSessions = sessions.Count;
        var validCount = sessions.Count(s => string.Equals(s.PayrollStatus, AttendanceIntegrityRules.PayrollStatusValid, StringComparison.OrdinalIgnoreCase));
        var invalidCount = sessions.Count(s => string.Equals(s.PayrollStatus, AttendanceIntegrityRules.PayrollStatusInvalid, StringComparison.OrdinalIgnoreCase));
        var pendingCount = sessions.Count(s => string.Equals(s.PayrollStatus, AttendanceIntegrityRules.PayrollStatusPending, StringComparison.OrdinalIgnoreCase));

        var bonusItems = record.Bonuses > 0
            ? new List<TeacherPayrollBreakdownItem> { new() { Description = "Thưởng chuyên cần và chất lượng", Amount = record.Bonuses, Icon = "star", IsBonus = true } }
            : new List<TeacherPayrollBreakdownItem>();

        var deductionItems = record.Deductions > 0
            ? new List<TeacherPayrollBreakdownItem> { new() { Description = "Khấu trừ vi phạm quy định", Amount = record.Deductions, Icon = "remove_circle", IsBonus = false } }
            : new List<TeacherPayrollBreakdownItem>();

        var model = new TeacherPayrollPeriodViewModel
        {
            RecordId = record.Id,
            Year = record.Year,
            Month = record.Month,
            PeriodLabel = $"Tháng {record.Month:00}/{record.Year}",
            TeacherName = record.TeacherName,
            SalaryTier = tier,
            SalaryTierLabel = tierLabel,
            TotalSessions = totalSessions,
            ValidSessions = validCount,
            InvalidSessions = invalidCount,
            PendingSessions = pendingCount,
            SessionEarnings = record.SessionEarnings,
            Bonuses = record.Bonuses,
            Deductions = record.Deductions,
            TotalPay = record.TotalPay,
            Status = record.Status,
            IsApproved = record.Status == AttendanceWorkflowService.PayrollRecordStatusApproved,
            CreatedAt = record.CreatedAt,
            ApprovedAt = record.ApprovedAt,
            BonusItems = bonusItems,
            DeductionItems = deductionItems,
            Sessions = sessions.Select(s => new TeacherPayrollSessionRowViewModel
            {
                SessionId = s.Id,
                SessionLabel = $"Buổi {s.SessionNo:00}",
                ClassCode = s.ClassCode,
                CourseName = s.CourseName,
                CourseDifficulty = s.CourseDifficulty,
                CourseDifficultyLabel = GetDifficultyLabel(s.CourseDifficulty),
                DateText = s.Date.ToString("dd/MM/yyyy"),
                TimeText = $"{s.StartTime:HH\\:mm} – {s.EndTime:HH\\:mm}",
                PayrollStatus = s.PayrollStatus ?? string.Empty,
                PayrollStatusLabel = GetPayrollStatusLabel(s.PayrollStatus),
                PayrollStatusBadgeClass = GetPayrollStatusBadge(s.PayrollStatus)
            }).ToList()
        };

        ViewData["Title"] = $"Chi tiết lương {model.PeriodLabel}";
        return View(model);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private int? GetCurrentTeacherId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userId, out var id) ? id : null;
    }

    private static (int tier, string label) GetTierInfo(TeacherProfile? profile)
    {
        if (profile == null) return (0, "Chưa cấu hình");
        if (profile.CustomSessionRate.HasValue)
            return (0, $"Theo thỏa thuận ({profile.CustomSessionRate.Value:N0} đ/buổi)");
        return profile.SalaryTier > 0
            ? (profile.SalaryTier, $"Bậc {profile.SalaryTier}")
            : (0, "Chưa cấu hình");
    }

    private static string GetDifficultyLabel(int difficulty) => difficulty switch
    {
        1 => "Cơ bản",
        2 => "Nâng cao",
        3 => "Chuyên sâu",
        _ => $"Cấp {difficulty}"
    };

    private static string GetPayrollStatusLabel(string? status) => status switch
    {
        AttendanceIntegrityRules.PayrollStatusValid => "Hợp lệ",
        AttendanceIntegrityRules.PayrollStatusInvalid => "Không hợp lệ",
        AttendanceIntegrityRules.PayrollStatusPending => "Chờ xét",
        _ => "Chưa xét"
    };

    private static string GetPayrollStatusBadge(string? status) => status switch
    {
        AttendanceIntegrityRules.PayrollStatusValid => "teacher-tag teacher-tag--success",
        AttendanceIntegrityRules.PayrollStatusInvalid => "teacher-tag teacher-tag--error",
        AttendanceIntegrityRules.PayrollStatusPending => "teacher-tag teacher-tag--warning",
        _ => "teacher-tag teacher-tag--neutral"
    };

}
