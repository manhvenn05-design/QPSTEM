using Microsoft.EntityFrameworkCore;
using STEM.Web.Data;
using STEM.Web.Models;

namespace STEM.Web.Services;

public sealed class AttendanceWorkflowService
{
    public const string PayrollRecordStatusApproved = "Approved";

    private readonly ApplicationDbContext _context;
    private readonly string? _cloudinaryCloudName;

    public AttendanceWorkflowService(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _cloudinaryCloudName = configuration["Cloudinary:CloudName"];
    }

    public bool IsValidProductMediaCollection(string? rawValue)
    {
        return AttendanceIntegrityRules.IsValidProductMediaCollection(rawValue, _cloudinaryCloudName);
    }

    public bool IsValidProductMediaUrl(string? rawValue)
    {
        return AttendanceIntegrityRules.IsValidProductMediaUrl(rawValue, _cloudinaryCloudName);
    }

    public async Task<string?> GetTeacherEditLockMessageAsync(int teacherId, int sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _context.Sessions
            .AsNoTracking()
            .Where(x => x.Id == sessionId && x.Class.TeacherId == teacherId)
            .Select(x => new
            {
                x.Date,
                x.StartTime,
                x.EndTime
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (session == null)
        {
            return null;
        }

        return await GetTeacherEditLockMessageAsync(
            teacherId,
            session.Date,
            session.StartTime,
            session.EndTime,
            cancellationToken);
    }

    public async Task<string?> GetTeacherEditLockMessageAsync(
        int teacherId,
        DateOnly sessionDate,
        TimeOnly sessionStartTime,
        TimeOnly sessionEndTime,
        CancellationToken cancellationToken = default)
    {
        var lockMessage = AttendanceIntegrityRules.GetTeacherEditLockMessage(sessionDate, sessionStartTime, sessionEndTime, DateTime.Now);
        if (!string.IsNullOrWhiteSpace(lockMessage))
        {
            return lockMessage;
        }

        var hasApprovedPayroll = await _context.PayrollRecords
            .AsNoTracking()
            .AnyAsync(
                x => x.TeacherId == teacherId &&
                     x.Month == sessionDate.Month &&
                     x.Year == sessionDate.Year &&
                     x.Status == PayrollRecordStatusApproved,
                cancellationToken);

        return hasApprovedPayroll
            ? "Buoi hoc nay thuoc ky luong da duoc chot va khong con cho phep giao vien chinh sua."
            : null;
    }

    public async Task<string> RecomputeSessionPayrollStatusAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _context.Sessions
            .Include(x => x.Class)
            .ThenInclude(x => x.Enrollments)
            .Include(x => x.Attendances)
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);

        if (session == null)
        {
            throw new InvalidOperationException($"Session {sessionId} was not found.");
        }

        var studentCount = session.Class.Enrollments.Count;
        var attendanceCount = session.Attendances.Count;
        var presentAttendances = session.Attendances.Where(x => x.IsPresent).ToList();
        var notedPresentCount = presentAttendances.Count(x => !string.IsNullOrWhiteSpace(x.TeacherRawNote));
        var mediaReadyCount = session.Attendances.Count(x =>
            !string.IsNullOrWhiteSpace(x.ProductMediaUrls) &&
            !string.IsNullOrWhiteSpace(x.AiEvaluation));

        var payrollStatus = AttendanceIntegrityRules.ComputePayrollStatus(
            session.Date,
            studentCount,
            attendanceCount,
            presentAttendances.Count,
            notedPresentCount,
            mediaReadyCount,
            DateOnly.FromDateTime(DateTime.Today));

        session.PayrollStatus = payrollStatus;

        if (string.Equals(payrollStatus, AttendanceIntegrityRules.PayrollStatusValid, StringComparison.OrdinalIgnoreCase))
        {
            var effectiveTeacherId = session.SubstituteTeacherId ?? session.Class.TeacherId;
            var teacherProfile = await _context.Set<TeacherProfile>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == effectiveTeacherId, cancellationToken);
                
            if (teacherProfile != null)
            {
                if (teacherProfile.CustomSessionRate.HasValue)
                {
                    session.SessionRateApplied = teacherProfile.CustomSessionRate.Value;
                }
                else
                {
                    var payRate = await _context.Set<PayRateConfig>()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.TeacherTier == teacherProfile.SalaryTier && x.CourseDifficulty == session.Class.Course.DifficultyLevel, cancellationToken);
                    
                    if (payRate != null)
                    {
                        session.SessionRateApplied = payRate.RatePerSession;
                    }
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return payrollStatus;
    }
}
