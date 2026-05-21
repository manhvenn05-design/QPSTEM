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

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var estimate = await _payrollService.GetTeacherEstimateAsync(teacherId.Value, now.Year, now.Month, today, ct);
        var currentOfficialRecord = history.FirstOrDefault(x => x.Year == now.Year && x.Month == now.Month);

        if (currentOfficialRecord != null && !currentOfficialRecord.IsApproved)
        {
            currentOfficialRecord.TotalValidSessions = estimate.ValidSessions;
            currentOfficialRecord.SessionEarnings = estimate.EstimatedSessionEarnings;
            currentOfficialRecord.Bonuses = estimate.EstimatedBonuses;
            currentOfficialRecord.Deductions = estimate.EstimatedDeductions;
            currentOfficialRecord.TotalPay = estimate.EstimatedTotalPay;
        }

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

    [HttpGet]
    public async Task<IActionResult> Period(int? id, int? year, int? month, CancellationToken ct)
    {
        var teacherId = GetCurrentTeacherId();
        if (!teacherId.HasValue)
        {
            return Challenge();
        }

        var recordQuery = _context.PayrollRecords
            .AsNoTracking()
            .Where(x => x.TeacherId == teacherId.Value);

        if (id.HasValue)
        {
            recordQuery = recordQuery.Where(x => x.Id == id.Value);
        }
        else
        {
            if (!year.HasValue || !month.HasValue || year < 2020 || year > 2100 || month < 1 || month > 12)
            {
                return BadRequest("Kỳ lương không hợp lệ.");
            }

            recordQuery = recordQuery.Where(x => x.Year == year.Value && x.Month == month.Value);
        }

        var record = await recordQuery
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
            if (id.HasValue || !year.HasValue || !month.HasValue)
            {
                return NotFound("Không tìm thấy bảng lương cho kỳ này.");
            }

            // Mock draft record for real-time viewing before admin generates
            var teacherName = await _context.Users.Where(x => x.Id == teacherId.Value).Select(x => x.FullName).FirstOrDefaultAsync(ct);
            record = new
            {
                Id = 0,
                Year = year.Value,
                Month = month.Value,
                TotalValidSessions = 0,
                SessionEarnings = 0m,
                Bonuses = 0m,
                Deductions = 0m,
                TotalPay = 0m,
                Status = "Nháp",
                CreatedAt = DateTime.UtcNow,
                ApprovedAt = (DateTime?)null,
                TeacherName = teacherName ?? string.Empty
            };
        }

        var teacherProfile = await _context.Set<TeacherProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == teacherId.Value, ct);
        var payRates = teacherProfile == null
            ? new List<PayRateConfig>()
            : await _context.Set<PayRateConfig>().AsNoTracking().ToListAsync(ct);
        var (tier, tierLabel) = GetTierInfo(teacherProfile);

        var periodStart = new DateOnly(record.Year, record.Month, 1);
        var periodEnd = periodStart.AddMonths(1);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var visiblePeriodEnd = periodEnd <= today.AddDays(1)
            ? periodEnd
            : today.AddDays(1);

        var sessions = await _context.Sessions
            .AsNoTracking()
            .Where(x => ((x.Class.TeacherId == teacherId.Value && x.SubstituteTeacherId == null) ||
                         x.SubstituteTeacherId == teacherId.Value) &&
                        x.Date >= periodStart && x.Date < visiblePeriodEnd)
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
                x.SessionRateApplied,
                ClassCode = x.Class.ClassCode,
                CourseName = x.Class.Course.Name,
                CourseDifficulty = x.Class.Course.DifficultyLevel
            })
            .ToListAsync(ct);

        var totalSessions = sessions.Count;
        var validCount = sessions.Count(s => string.Equals(s.PayrollStatus, AttendanceIntegrityRules.PayrollStatusValid, StringComparison.OrdinalIgnoreCase));
        var invalidCount = sessions.Count(s => string.Equals(s.PayrollStatus, AttendanceIntegrityRules.PayrollStatusInvalid, StringComparison.OrdinalIgnoreCase));
        var pendingCount = sessions.Count(s => string.Equals(s.PayrollStatus, AttendanceIntegrityRules.PayrollStatusPending, StringComparison.OrdinalIgnoreCase));
        var isApproved = string.Equals(record.Status, AttendanceWorkflowService.PayrollRecordStatusApproved, StringComparison.OrdinalIgnoreCase);
        // Nếu là bản ghi đã duyệt, không lấy estimate thực tế. Nếu chưa duyệt hoặc mock (Id = 0), luôn tính lại realtime.
        var liveEstimate = isApproved
            ? null
            : await _payrollService.GetTeacherEstimateAsync(teacherId.Value, record.Year, record.Month, today, ct);

        var sessionEarnings = liveEstimate?.EstimatedSessionEarnings ?? record.SessionEarnings;
        var bonuses = liveEstimate?.EstimatedBonuses ?? record.Bonuses;
        var deductions = liveEstimate?.EstimatedDeductions ?? record.Deductions;
        var totalPay = liveEstimate?.EstimatedTotalPay ?? record.TotalPay;

        var bonusItems = bonuses > 0
            ? new List<TeacherPayrollBreakdownItem> { new() { Description = "Thưởng chuyên cần và chất lượng", Amount = bonuses, Icon = "star", IsBonus = true } }
            : new List<TeacherPayrollBreakdownItem>();

        var deductionItems = deductions > 0
            ? new List<TeacherPayrollBreakdownItem> { new() { Description = "Khấu trừ vi phạm quy định", Amount = deductions, Icon = "remove_circle", IsBonus = false } }
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
            SessionEarnings = sessionEarnings,
            Bonuses = bonuses,
            Deductions = deductions,
            TotalPay = totalPay,
            Status = record.Status,
            IsApproved = isApproved,
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
                TimeText = $"{s.StartTime:HH\\:mm} - {s.EndTime:HH\\:mm}",
                PayrollStatus = s.PayrollStatus ?? string.Empty,
                PayrollStatusLabel = GetPayrollStatusLabel(s.PayrollStatus),
                PayrollStatusBadgeClass = GetPayrollStatusBadge(s.PayrollStatus),
                RateForSession = string.Equals(s.PayrollStatus, AttendanceIntegrityRules.PayrollStatusValid, StringComparison.OrdinalIgnoreCase)
                    ? ResolveSessionRateDisplay(s.SessionRateApplied, s.CourseDifficulty, teacherProfile, payRates)
                    : 0m
            }).ToList()
        };

        ViewData["Title"] = $"Chi tiết lương {model.PeriodLabel}";
        return View(model);
    }

    private int? GetCurrentTeacherId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userId, out var id) ? id : null;
    }

    private static (int tier, string label) GetTierInfo(TeacherProfile? profile)
    {
        if (profile == null)
        {
            return (0, "Chưa cấu hình");
        }

        if (profile.CustomSessionRate.HasValue)
        {
            return (0, $"Theo thỏa thuận ({profile.CustomSessionRate.Value:N0} đ/buổi)");
        }

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

    private static decimal ResolveSessionRateDisplay(
        decimal sessionRateApplied,
        int courseDifficulty,
        TeacherProfile? teacherProfile,
        IReadOnlyCollection<PayRateConfig> payRates)
    {
        if (sessionRateApplied > 0)
        {
            return sessionRateApplied;
        }

        if (teacherProfile?.CustomSessionRate.HasValue == true)
        {
            return teacherProfile.CustomSessionRate.Value;
        }

        if (teacherProfile == null)
        {
            return 0m;
        }

        return payRates.FirstOrDefault(x =>
            x.TeacherTier == teacherProfile.SalaryTier &&
            x.CourseDifficulty == courseDifficulty)?.RatePerSession ?? 0m;
    }
}
