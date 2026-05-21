using Microsoft.EntityFrameworkCore;
using STEM.Web.Data;
using STEM.Web.Models;

namespace STEM.Web.Services;

public sealed class PayrollCalculationService
{
    private const decimal ConsecutiveMissingEvidencePenalty = 100_000m;
    private const decimal LowEvidenceRatioPenaltyRate = 0.20m;
    private const decimal HighAttendanceBonus = 200_000m;
    private const decimal ExcellentAiBonus = 300_000m;
    private const decimal FullComplianceBonusRate = 0.10m;
    public const string PayrollDraftStatus = "Draft";

    private readonly ApplicationDbContext _context;

    public PayrollCalculationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PayrollRecord>> GenerateMonthlyPayrollAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var periodStart = new DateOnly(year, month, 1);
        var periodEnd = periodStart.AddMonths(1);

        var payRates = await _context.Set<PayRateConfig>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var teacherProfiles = await _context.Set<TeacherProfile>()
            .AsNoTracking()
            .ToDictionaryAsync(x => x.UserId, cancellationToken);

        var sessionRows = await _context.Sessions
            .AsNoTracking()
            .Where(x => x.Date >= periodStart && x.Date < periodEnd)
            .Select(x => new PayrollSessionRow
            {
                SessionId = x.Id,
                TeacherId = x.SubstituteTeacherId ?? x.Class.TeacherId,
                ClassId = x.ClassId,
                CourseId = x.Class.CourseId,
                CourseDifficulty = x.Class.Course.DifficultyLevel,
                SessionDate = x.Date,
                PayrollStatus = x.PayrollStatus,
                SessionRateApplied = x.SessionRateApplied,
                AttendanceCount = x.Attendances.Count,
                PresentCount = x.Attendances.Count(a => a.IsPresent),
                StudentCount = x.Class.Enrollments.Count,
                MediaReadyCount = x.Attendances.Count(a => !string.IsNullOrWhiteSpace(a.ProductMediaUrls) && !string.IsNullOrWhiteSpace(a.AiEvaluation)),
                ExcellentAiCount = x.Attendances.Count(a => !string.IsNullOrWhiteSpace(a.AiEvaluation))
            })
            .ToListAsync(cancellationToken);

        var aiScores = await _context.Attendances
            .AsNoTracking()
            .Where(x => x.Session.Date >= periodStart && x.Session.Date < periodEnd && !string.IsNullOrWhiteSpace(x.AiEvaluation))
            .Select(x => new
            {
                x.SessionId,
                x.StudentId,
                x.AiEvaluation
            })
            .ToListAsync(cancellationToken);

        var excellentAiCounts = aiScores
            .Where(x => TryExtractAiScore(x.AiEvaluation, out var score) && score > 85)
            .GroupBy(x => x.SessionId)
            .ToDictionary(x => x.Key, x => x.Count());

        foreach (var row in sessionRows)
        {
            row.ExcellentAiCount = excellentAiCounts.GetValueOrDefault(row.SessionId);
        }

        var generatedRecords = new List<PayrollRecord>();

        foreach (var teacherGroup in sessionRows.GroupBy(x => x.TeacherId))
        {
            if (!teacherProfiles.TryGetValue(teacherGroup.Key, out var teacherProfile))
            {
                continue;
            }

            var validSessions = teacherGroup
                .Where(x => string.Equals(x.PayrollStatus, AttendanceIntegrityRules.PayrollStatusValid, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var sessionEarnings = validSessions.Sum(session => ResolveSessionRate(session, teacherProfile, payRates));

            var bonuses = CalculateBonuses(teacherGroup.ToList(), validSessions, sessionEarnings);
            var deductions = CalculateDeductions(teacherGroup.ToList(), validSessions, sessionEarnings);
            var totalPay = Math.Max(0m, sessionEarnings + bonuses - deductions);

            var existingRecord = await _context.Set<PayrollRecord>()
                .FirstOrDefaultAsync(
                    x => x.TeacherId == teacherGroup.Key && x.Year == year && x.Month == month,
                    cancellationToken);

            if (existingRecord != null && string.Equals(existingRecord.Status, AttendanceWorkflowService.PayrollRecordStatusApproved, StringComparison.OrdinalIgnoreCase))
            {
                generatedRecords.Add(existingRecord);
                continue;
            }

            var targetRecord = existingRecord ?? new PayrollRecord
            {
                TeacherId = teacherGroup.Key,
                Year = year,
                Month = month,
                CreatedAt = DateTime.UtcNow
            };

            targetRecord.TotalValidSessions = validSessions.Count;
            targetRecord.SessionEarnings = sessionEarnings;
            targetRecord.Bonuses = bonuses;
            targetRecord.Deductions = deductions;
            targetRecord.TotalPay = totalPay;
            targetRecord.Status = PayrollDraftStatus;
            targetRecord.ApprovedAt = null;

            if (existingRecord == null)
            {
                _context.Add(targetRecord);
            }

            generatedRecords.Add(targetRecord);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return generatedRecords;
    }

    /// <summary>
    /// Tính ước tính lương real-time cho 1 giáo viên trong kỳ cho trước.
    /// Không cần Admin Generate trước — chỉ cần Session và PayRateConfig trong DB.
    /// </summary>
    public async Task<TeacherPayrollEstimate> GetTeacherEstimateAsync(
        int teacherId,
        int year,
        int month,
        DateOnly? asOfDate = null,
        CancellationToken cancellationToken = default)
    {
        var periodStart = new DateOnly(year, month, 1);
        var periodEnd = periodStart.AddMonths(1);
        var effectivePeriodEnd = asOfDate.HasValue && asOfDate.Value.AddDays(1) < periodEnd
            ? asOfDate.Value.AddDays(1)
            : periodEnd;

        var payRates = await _context.Set<PayRateConfig>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var teacherProfile = await _context.Set<TeacherProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == teacherId, cancellationToken);

        var sessionRows = await _context.Sessions
            .AsNoTracking()
            .Where(x => (x.Class.TeacherId == teacherId && x.SubstituteTeacherId == null || x.SubstituteTeacherId == teacherId) &&
                        x.Date >= periodStart && x.Date < effectivePeriodEnd)
            .Select(x => new PayrollSessionRow
            {
                SessionId = x.Id,
                TeacherId = teacherId,
                ClassId = x.ClassId,
                CourseId = x.Class.CourseId,
                CourseDifficulty = x.Class.Course.DifficultyLevel,
                SessionDate = x.Date,
                PayrollStatus = x.PayrollStatus,
                SessionRateApplied = x.SessionRateApplied,
                AttendanceCount = x.Attendances.Count,
                PresentCount = x.Attendances.Count(a => a.IsPresent),
                StudentCount = x.Class.Enrollments.Count,
                MediaReadyCount = x.Attendances.Count(a =>
                    !string.IsNullOrWhiteSpace(a.ProductMediaUrls) &&
                    !string.IsNullOrWhiteSpace(a.AiEvaluation)),
                ExcellentAiCount = 0
            })
            .ToListAsync(cancellationToken);

        var aiScores = await _context.Attendances
            .AsNoTracking()
            .Where(x => (x.Session.Class.TeacherId == teacherId && x.Session.SubstituteTeacherId == null || x.Session.SubstituteTeacherId == teacherId) &&
                        x.Session.Date >= periodStart && x.Session.Date < effectivePeriodEnd &&
                        !string.IsNullOrWhiteSpace(x.AiEvaluation))
            .Select(x => new { x.SessionId, x.AiEvaluation })
            .ToListAsync(cancellationToken);

        var excellentAiCounts = aiScores
            .Where(x => TryExtractAiScore(x.AiEvaluation, out var score) && score > 85)
            .GroupBy(x => x.SessionId)
            .ToDictionary(x => x.Key, x => x.Count());

        foreach (var row in sessionRows)
        {
            row.ExcellentAiCount = excellentAiCounts.GetValueOrDefault(row.SessionId);
        }

        var validSessions = sessionRows
            .Where(x => string.Equals(x.PayrollStatus, AttendanceIntegrityRules.PayrollStatusValid, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var sessionEarnings = teacherProfile == null
            ? 0m
            : validSessions.Sum(session => ResolveSessionRate(session, teacherProfile, payRates));

        var bonuses = CalculateBonuses(sessionRows, validSessions, sessionEarnings);
        var deductions = CalculateDeductions(sessionRows, validSessions, sessionEarnings);
        var totalPay = Math.Max(0m, sessionEarnings + bonuses - deductions);

        return new TeacherPayrollEstimate
        {
            TotalSessions = sessionRows.Count,
            ValidSessions = validSessions.Count,
            PendingSessions = sessionRows.Count(x =>
                string.Equals(x.PayrollStatus, AttendanceIntegrityRules.PayrollStatusPending, StringComparison.OrdinalIgnoreCase)),
            InvalidSessions = sessionRows.Count(x =>
                string.Equals(x.PayrollStatus, AttendanceIntegrityRules.PayrollStatusInvalid, StringComparison.OrdinalIgnoreCase)),
            EstimatedSessionEarnings = sessionEarnings,
            EstimatedBonuses = bonuses,
            EstimatedDeductions = deductions,
            EstimatedTotalPay = totalPay
        };
    }

    public async Task<int> ApproveMonthlyPayrollAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var records = await _context.PayrollRecords
            .Where(x => x.Year == year && x.Month == month)
            .ToListAsync(cancellationToken);

        foreach (var record in records.Where(x => !string.Equals(x.Status, AttendanceWorkflowService.PayrollRecordStatusApproved, StringComparison.OrdinalIgnoreCase)))
        {
            record.Status = AttendanceWorkflowService.PayrollRecordStatusApproved;
            record.ApprovedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return records.Count;
    }

    private static decimal CalculateBonuses(IReadOnlyCollection<PayrollSessionRow> teacherSessions, IReadOnlyCollection<PayrollSessionRow> validSessions, decimal sessionEarnings)
    {
        decimal bonuses = 0m;

        bonuses += teacherSessions
            .GroupBy(x => x.ClassId)
            .Count(group => group.Sum(x => x.StudentCount) > 0 &&
                            group.Sum(x => x.PresentCount) / (decimal)group.Sum(x => x.StudentCount) > 0.95m)
            * HighAttendanceBonus;

        if (validSessions.Sum(x => x.ExcellentAiCount) >= 5)
        {
            bonuses += ExcellentAiBonus;
        }

        if (teacherSessions.Count > 0 &&
            teacherSessions.All(x => string.Equals(x.PayrollStatus, AttendanceIntegrityRules.PayrollStatusValid, StringComparison.OrdinalIgnoreCase)))
        {
            bonuses += sessionEarnings * FullComplianceBonusRate;
        }

        return bonuses;
    }

    private static decimal CalculateDeductions(IReadOnlyCollection<PayrollSessionRow> teacherSessions, IReadOnlyCollection<PayrollSessionRow> validSessions, decimal sessionEarnings)
    {
        decimal deductions = 0m;

        foreach (var classGroup in teacherSessions.GroupBy(x => x.ClassId))
        {
            var ordered = classGroup.OrderBy(x => x.SessionDate).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i - 1].MediaReadyCount == 0 && ordered[i].MediaReadyCount == 0)
                {
                    deductions += ConsecutiveMissingEvidencePenalty;
                }
            }
        }

        foreach (var courseGroup in teacherSessions.GroupBy(x => x.CourseId))
        {
            if (courseGroup.Count() == 0)
            {
                continue;
            }

            var evidenceRatio = courseGroup.Count(x => x.MediaReadyCount > 0) / (decimal)courseGroup.Count();
            if (evidenceRatio < 0.8m)
            {
                var validCourseSessions = validSessions.Count(x => x.CourseId == courseGroup.Key);
                if (validSessions.Count > 0)
                {
                    deductions += sessionEarnings * (validCourseSessions / (decimal)validSessions.Count) * LowEvidenceRatioPenaltyRate;
                }
            }
        }

        return deductions;
    }

    private static bool TryExtractAiScore(string? rawJson, out int score)
    {
        score = 0;
        var parsed = AiEvaluationFormatter.Parse(rawJson);
        if (parsed == null || string.IsNullOrWhiteSpace(parsed.Score))
        {
            return false;
        }

        var rawValue = parsed.Score.Replace("/100", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        return int.TryParse(rawValue, out score);
    }

    private static decimal ResolveSessionRate(
        PayrollSessionRow session,
        TeacherProfile teacherProfile,
        IReadOnlyCollection<PayRateConfig> payRates)
    {
        if (session.SessionRateApplied > 0)
        {
            return session.SessionRateApplied;
        }

        if (teacherProfile.CustomSessionRate.HasValue && teacherProfile.CustomSessionRate.Value > 0)
        {
            return teacherProfile.CustomSessionRate.Value;
        }

        return payRates.FirstOrDefault(x =>
            x.TeacherTier == teacherProfile.SalaryTier &&
            x.CourseDifficulty == session.CourseDifficulty)?.RatePerSession ?? 0m;
    }

    private sealed class PayrollSessionRow
    {
        public int SessionId { get; set; }
        public int TeacherId { get; set; }
        public int ClassId { get; set; }
        public int CourseId { get; set; }
        public int CourseDifficulty { get; set; }
        public DateOnly SessionDate { get; set; }
        public string PayrollStatus { get; set; } = string.Empty;
        public int AttendanceCount { get; set; }
        public int PresentCount { get; set; }
        public int StudentCount { get; set; }
        public int MediaReadyCount { get; set; }
        public int ExcellentAiCount { get; set; }
        public decimal SessionRateApplied { get; set; }
    }
}

/// <summary>Kết quả ước tính lương real-time cho 1 giáo viên trong 1 kỳ.</summary>
public sealed class TeacherPayrollEstimate
{
    public int TotalSessions { get; set; }
    public int ValidSessions { get; set; }
    public int PendingSessions { get; set; }
    public int InvalidSessions { get; set; }
    public decimal EstimatedSessionEarnings { get; set; }
    public decimal EstimatedBonuses { get; set; }
    public decimal EstimatedDeductions { get; set; }
    public decimal EstimatedTotalPay { get; set; }
}
