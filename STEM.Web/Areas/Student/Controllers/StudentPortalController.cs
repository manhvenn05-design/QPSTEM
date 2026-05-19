using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Data;
using STEM.Web.Models.StudentViewModels;
using STEM.Web.Services;

namespace STEM.Web.Areas.Student.Controllers;

[Area("Student")]
[Authorize(Roles = "Student")]
public class StudentPortalController : Controller
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".webm", ".ogg", ".mov"
    };

    private readonly ApplicationDbContext _context;

    public StudentPortalController(ApplicationDbContext context)
    {
        _context = context;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [1] DASHBOARD – Tổng quan
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Tổng quan";

        var studentId = GetCurrentStudentId();
        if (!studentId.HasValue)
            return RedirectToAction("Login", "Account", new { area = "" });

        // Lấy thông tin cá nhân
        var userInfo = await _context.Users
            .AsNoTracking()
            .Where(x => x.Id == studentId.Value)
            .Select(x => new
            {
                x.FullName,
                x.AvatarUrl,     // FIX 1: lấy AvatarUrl thật từ User
                Profile = x.StudentProfile == null ? null : new
                {
                    x.StudentProfile.GuardianName,
                    x.StudentProfile.GuardianPhone,
                    x.StudentProfile.CurrentSchool,
                    x.StudentProfile.MedicalNotes
                }
            })
            .FirstOrDefaultAsync();

        var viewModel = new StudentDashboardViewModel
        {
            Student = new StudentInfo
            {
                FullName        = userInfo?.FullName ?? User.Identity?.Name ?? "Học sinh",
                Avatar          = userInfo?.AvatarUrl ?? string.Empty,  // FIX 1
                GuardianName    = userInfo?.Profile?.GuardianName ?? string.Empty,
                GuardianPhone   = userInfo?.Profile?.GuardianPhone ?? string.Empty,
                CurrentSchool   = userInfo?.Profile?.CurrentSchool ?? string.Empty,
                MedicalNotes    = userInfo?.Profile?.MedicalNotes ?? string.Empty
            }
        };

        // Lấy các lớp học sinh đang enrolled
        var classIds = await _context.Enrollments
            .AsNoTracking()
            .Where(x => x.StudentId == studentId.Value)
            .Select(x => x.ClassId)
            .ToListAsync();

        var today     = DateOnly.FromDateTime(DateTime.Today);
        var weekStart = today.AddDays(-(int)today.DayOfWeek == 0 ? 6 : (int)today.DayOfWeek - 1);
        var weekEnd   = weekStart.AddDays(6);

        // 1. Lịch học sắp tới (5 buổi gần nhất từ hôm nay)
        var upcomingSessions = await _context.Sessions
            .AsNoTracking()
            .Where(x => classIds.Contains(x.ClassId) && x.Date >= today)
            .OrderBy(x => x.Date)
            .ThenBy(x => x.StartTime)
            .Take(5)
            .Select(x => new StudentScheduleItemViewModel
            {
                SessionId    = x.Id,
                CourseName   = x.Class.Course.Name,
                ClassCode    = x.Class.ClassCode,
                SessionLabel = $"Buổi {x.SessionNo:00}",
                Date         = x.Date.ToDateTime(TimeOnly.MinValue),
                StartTime    = x.StartTime,
                EndTime      = x.EndTime,
                Topic        = string.IsNullOrWhiteSpace(x.Topic) ? "Chưa cập nhật chủ đề" : x.Topic,
                Status       = x.Date == today ? "Hôm nay" : "Sắp tới"
            })
            .ToListAsync();

        viewModel.UpcomingSessions = upcomingSessions;

        // 2. Nhận xét & Media mới nhất (5 buổi gần nhất có mặt)
        var recentAttendances = await _context.Attendances
            .AsNoTracking()
            .Where(x => x.StudentId == studentId.Value && x.IsPresent && x.Session.Date <= today)
            .OrderByDescending(x => x.Session.Date)
            .Take(5)
            .Select(x => new
            {
                x.SessionId,
                CourseName   = x.Session.Class.Course.Name,
                SessionLabel = $"Buổi {x.Session.SessionNo:00}",
                Date         = x.Session.Date,
                x.AiEvaluation,
                x.TeacherRawNote,
                x.ProductMediaUrls
            })
            .ToListAsync();

        foreach (var att in recentAttendances)
        {
            var mediaUrls = SplitMediaUrls(att.ProductMediaUrls);
            var mediaItems = mediaUrls.Select(url => new MediaItem
            {
                Url  = url,
                Type = IsVideoUrl(url) ? "Video" : "Image"
            }).ToList();

            viewModel.RecentFeedbacks.Add(new StudentFeedbackViewModel
            {
                SessionId    = att.SessionId,
                CourseName   = att.CourseName,
                SessionLabel = att.SessionLabel,
                Date         = att.Date.ToDateTime(TimeOnly.MinValue),
                AiEvaluation = FormatAiEvaluation(att.AiEvaluation),
                TeacherNote  = att.TeacherRawNote ?? string.Empty,
                MediaItems   = mediaItems
            });
        }

        // 3. Hóa đơn học phí
        var invoices = await _context.Invoices
            .Include(x => x.Class)
            .ThenInclude(c => c!.Course)
            .Include(x => x.Payments)
            .Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.Id)
            .Select(x => new
            {
                x.InvoiceNo,
                CourseName  = x.Class != null && x.Class.Course != null ? x.Class.Course.Name : "Khóa học tổng hợp",
                x.FinalAmount,
                x.DueDate,
                PaidAmount  = x.Payments.Sum(p => p.Amount)
            })
            .ToListAsync();

        // FIX 2: tính status từ PaidAmount, đồng bộ FinanceController
        viewModel.Invoices = invoices.Select(x =>
        {
            var (label, badgeClass) = GetInvoiceStatus(x.FinalAmount, x.PaidAmount);
            return new StudentInvoiceViewModel
            {
                InvoiceNo   = x.InvoiceNo,
                CourseName  = x.CourseName,
                TotalAmount = x.FinalAmount,
                PaidAmount  = x.PaidAmount,
                DueDate     = x.DueDate,
                Status      = (byte)(x.PaidAmount <= 0 ? 1 : x.PaidAmount >= x.FinalAmount ? 3 : 2)
            };
        }).ToList();

        // 4. Stats – FIX 4+7: đếm trực tiếp từ DB
        viewModel.SessionsThisWeekCount = await _context.Sessions
            .AsNoTracking()
            .CountAsync(x => classIds.Contains(x.ClassId) && x.Date >= weekStart && x.Date <= weekEnd);

        viewModel.NewFeedbackCount = await _context.Attendances
            .AsNoTracking()
            .CountAsync(x =>
                x.StudentId == studentId.Value &&
                x.Session.Date >= today.AddDays(-7) &&
                !string.IsNullOrWhiteSpace(x.TeacherRawNote));

        viewModel.NewMediaCount = await _context.Attendances
            .AsNoTracking()
            .CountAsync(x =>
                x.StudentId == studentId.Value &&
                x.Session.Date >= today.AddDays(-7) &&
                !string.IsNullOrWhiteSpace(x.ProductMediaUrls));

        viewModel.PendingInvoiceCount = await _context.Invoices
            .AsNoTracking()
            .CountAsync(x =>
                x.StudentId == studentId.Value &&
                x.Payments.Sum(p => p.Amount) < x.FinalAmount);

        return View(viewModel);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [2] SCHEDULE – Lịch học
    // ─────────────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Schedule(
        string filter = "upcoming",
        string view   = "calendar",
        int?   month  = null,
        int?   year   = null)
    {
        ViewData["Title"] = "Lịch học";

        var studentId = GetCurrentStudentId();
        if (!studentId.HasValue)
            return RedirectToAction("Login", "Account", new { area = "" });

        // Validate params
        view   = view is "list" or "calendar" ? view : "calendar";
        filter = new[] { "upcoming", "past", "all" }.Contains(filter) ? filter : "upcoming";

        var today = DateOnly.FromDateTime(DateTime.Today);

        // ── CALENDAR VIEW ──────────────────────────────────────────────────
        if (view == "calendar")
        {
            var calYear  = Math.Max(2020, year ?? today.Year);
            var calMonth = Math.Clamp(month ?? today.Month, 1, 12);
            var firstDay = new DateOnly(calYear, calMonth, 1);
            var lastDay = firstDay.AddMonths(1).AddDays(-1);
            var calItems = await GetStudentScheduleSessionsAsync(studentId.Value, today, firstDay, lastDay);

            return View(new StudentSchedulePageViewModel
            {
                Sessions      = calItems,
                ViewMode      = "calendar",
                CalendarYear  = calYear,
                CalendarMonth = calMonth,
                Filter        = filter,
                TotalCount    = calItems.Count
            });
        }

        // ── LIST VIEW ──────────────────────────────────────────────────────
        var items = await GetStudentScheduleSessionsAsync(studentId.Value, today, filter: filter);

        if (filter == "past")
            items = items.OrderByDescending(x => x.Date).ThenBy(x => x.StartTime).ToList();

        return View(new StudentSchedulePageViewModel
        {
            Sessions   = items,
            Filter     = filter,
            ViewMode   = "list",
            TotalCount = items.Count,
            CalendarYear  = today.Year,
            CalendarMonth = today.Month
        });
    }

    [HttpGet]
    public async Task<IActionResult> ScheduleCalendarData(int month, int year)
    {
        var studentId = GetCurrentStudentId();
        if (!studentId.HasValue)
            return Unauthorized();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var calYear = Math.Max(2020, year);
        var calMonth = Math.Clamp(month, 1, 12);
        var firstDay = new DateOnly(calYear, calMonth, 1);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);
        var sessions = await GetStudentScheduleSessionsAsync(studentId.Value, today, firstDay, lastDay);
        var initialDate = calYear == today.Year && calMonth == today.Month
            ? today
            : sessions.FirstOrDefault()?.Date ?? firstDay;

        return Json(new
        {
            year = calYear,
            month = calMonth,
            totalCount = sessions.Count,
            selectedDateKey = initialDate.ToString("yyyy-MM-dd"),
            sessions = sessions.Select(x => new
            {
                sessionId = x.SessionId,
                dateKey = x.Date.ToString("yyyy-MM-dd"),
                dateDisplay = x.Date.ToString("dd/MM/yyyy"),
                dayLabel = GetDayLabel(x.Date.DayOfWeek),
                courseName = x.CourseName,
                classCode = x.ClassCode,
                sessionNo = x.SessionNo,
                startTime = x.StartTime.ToString("HH:mm"),
                endTime = x.EndTime.ToString("HH:mm"),
                topic = x.Topic,
                teacherName = x.TeacherName,
                teachingMaterialUrl = x.TeachingMaterialUrl,
                statusLabel = x.StatusLabel,
                attendanceLabel = x.HasAttendance ? x.AttendanceLabel : "Chưa điểm danh",
                hasAttendance = x.HasAttendance,
                wasPresent = x.WasPresent,
                isPast = x.IsPast,
                isToday = x.IsToday,
                evidenceUrl = x.IsPast && x.HasAttendance && x.WasPresent == true
                    ? $"/Student/StudentPortal/Evidence?sessionId={x.SessionId}#evidence-session-{x.SessionId}"
                    : null
            })
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [3] EVIDENCE – Nhận xét & Minh chứng
    // ─────────────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Evidence(string? classCode = null, int? sessionId = null, int page = 1)
    {
        ViewData["Title"] = "Nhận xét & Minh chứng";

        var studentId = GetCurrentStudentId();
        if (!studentId.HasValue)
            return RedirectToAction("Login", "Account", new { area = "" });

        const int pageSize = 10;
        page = Math.Max(1, page);

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Lấy danh sách lớp học của học sinh để populate filter dropdown
        var availableClasses = await _context.Enrollments
            .AsNoTracking()
            .Where(x => x.StudentId == studentId.Value)
            .Select(x => x.Class.ClassCode)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        // Query gốc
        var query = _context.Attendances
            .AsNoTracking()
            .Where(x => x.StudentId == studentId.Value && x.IsPresent && x.Session.Date <= today);

        // Filter theo lớp nếu có
        if (!string.IsNullOrWhiteSpace(classCode))
            query = query.Where(x => x.Session.Class.ClassCode == classCode);

        // Nếu có sessionId cụ thể, lấy riêng session đó trước
        if (sessionId.HasValue)
            query = query.Where(x => x.SessionId == sessionId.Value);

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        if (page > totalPages && totalPages > 0) page = totalPages;

        var attendances = await query
            .OrderByDescending(x => x.Session.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.SessionId,
                CourseName   = x.Session.Class.Course.Name,
                ClassCode    = x.Session.Class.ClassCode,
                x.Session.SessionNo,
                x.Session.Date,
                x.TeacherRawNote,
                x.AiEvaluation,
                x.ProductMediaUrls
            })
            .ToListAsync();

        var items = attendances.Select(x =>
        {
            var allUrls   = SplitMediaUrls(x.ProductMediaUrls);
            var videoUrls = allUrls.Where(IsYouTubeUrl).ToList();
            var imageUrls = allUrls.Where(u => !IsYouTubeUrl(u) && IsImageUrl(u)).ToList();
            var extUrls   = allUrls.Except(videoUrls).Except(imageUrls).ToList();

            return new StudentEvidenceItemViewModel
            {
                AttendanceId    = x.Id,
                SessionId       = x.SessionId,
                CourseName      = x.CourseName,
                ClassCode       = x.ClassCode,
                SessionNo       = x.SessionNo,
                SessionDate     = x.Date,
                TeacherRawNote  = x.TeacherRawNote,
                AiEvaluation    = FormatAiEvaluation(x.AiEvaluation),
                VideoUrls       = videoUrls,
                ImageUrls       = imageUrls,
                ExternalUrls    = extUrls,
                IsHighlighted   = sessionId.HasValue && x.SessionId == sessionId.Value
            };
        }).ToList();

        var model = new StudentEvidencePageViewModel
        {
            Items            = items,
            TotalCount       = totalCount,
            Page             = page,
            PageSize         = pageSize,
            TotalPages       = totalPages,
            SelectedClass    = classCode,
            HighlightSession = sessionId,
            AvailableClasses = availableClasses
        };

        return View(model);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [4] FINANCE – Học phí
    // ─────────────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Finance()
    {
        ViewData["Title"] = "Học phí";

        var studentId = GetCurrentStudentId();
        if (!studentId.HasValue)
            return RedirectToAction("Login", "Account", new { area = "" });

        var invoices = await _context.Invoices
            .AsNoTracking()
            .Where(x => x.StudentId == studentId.Value)
            .OrderByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.InvoiceNo,
                ClassCode  = x.Class != null ? x.Class.ClassCode : (string?)null,
                CourseName = x.Class != null && x.Class.Course != null ? x.Class.Course.Name : (string?)null,
                x.FinalAmount,
                x.DueDate,
                Payments = x.Payments.Select(p => new
                {
                    p.Amount,
                    p.PaymentMethod,
                    p.TransDate
                }).ToList()
            })
            .ToListAsync();

        var invoiceItems = invoices.Select(x =>
        {
            var paidAmount = x.Payments.Sum(p => p.Amount);
            var (label, badgeClass) = GetInvoiceStatus(x.FinalAmount, paidAmount);
            return new StudentInvoiceDetailViewModel
            {
                Id            = x.Id,
                InvoiceNo     = x.InvoiceNo,
                ClassCode     = x.ClassCode,
                CourseName    = x.CourseName ?? "Khóa học tổng hợp",
                FinalAmount   = x.FinalAmount,
                PaidAmount    = paidAmount,
                DueDate       = x.DueDate,
                StatusLabel   = label,
                StatusBadgeClass = badgeClass,
                Payments      = x.Payments.Select(p => new StudentPaymentItemViewModel
                {
                    Amount             = p.Amount,
                    PaymentMethodLabel = GetPaymentMethodLabel(p.PaymentMethod),
                    TransDate          = p.TransDate
                }).OrderByDescending(p => p.TransDate).ToList()
            };
        }).ToList();

        var totalPaid = invoiceItems.Sum(x => x.PaidAmount);

        var model = new StudentFinancePageViewModel
        {
            TotalBilled   = invoiceItems.Sum(x => x.FinalAmount),
            TotalPaid     = totalPaid,
            PendingCount  = invoiceItems.Count(x => x.RemainingAmount > 0),
            Invoices      = invoiceItems
        };

        return View(model);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [5] PROFILE – Hồ sơ cá nhân (read-only + đổi mật khẩu)
    // ─────────────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        ViewData["Title"] = "Hồ sơ của tôi";

        var studentId = GetCurrentStudentId();
        if (!studentId.HasValue)
            return RedirectToAction("Login", "Account", new { area = "" });

        var today = DateOnly.FromDateTime(DateTime.Today);

        var user = await _context.Users
            .AsNoTracking()
            .Where(x => x.Id == studentId.Value)
            .Select(x => new
            {
                x.FullName,
                x.Username,
                x.Email,
                x.Phone,
                x.AvatarUrl,
                Profile = x.StudentProfile == null ? null : new
                {
                    x.StudentProfile.GuardianName,
                    x.StudentProfile.GuardianPhone,
                    x.StudentProfile.CurrentSchool,
                    x.StudentProfile.MedicalNotes
                },
                Enrollments = x.Enrollments.Select(e => new
                {
                    e.Class.ClassCode,
                    CourseName      = e.Class.Course.Name,
                    e.Class.StartDate,
                    e.Class.EndDate,
                    TeacherName     = e.Class.Teacher.FullName,
                    TotalSessions   = e.Class.Course.TotalSessions,
                    AttendedCount   = e.Class.Sessions
                        .SelectMany(s => s.Attendances)
                        .Count(a => a.StudentId == studentId.Value && a.IsPresent)
                }).ToList()
            })
            .FirstOrDefaultAsync();

        if (user == null)
            return RedirectToAction("Login", "Account", new { area = "" });

        var model = new StudentProfileViewModel
        {
            FullName      = user.FullName,
            Username      = user.Username,
            Email         = user.Email,
            Phone         = user.Phone,
            AvatarUrl     = user.AvatarUrl,
            GuardianName  = user.Profile?.GuardianName,
            GuardianPhone = user.Profile?.GuardianPhone,
            CurrentSchool = user.Profile?.CurrentSchool,
            MedicalNotes  = user.Profile?.MedicalNotes,
            EnrolledClasses = user.Enrollments.Select(e =>
            {
                string statusLabel;
                string statusBadgeClass;
                if (e.StartDate > today)
                {
                    statusLabel      = "Sắp khai giảng";
                    statusBadgeClass = "student-tag--warning";  // dùng CSS class chuẩn
                }
                else if (!e.EndDate.HasValue || e.EndDate.Value >= today)
                {
                    statusLabel      = "Đang học";
                    statusBadgeClass = "student-tag--success";
                }
                else
                {
                    statusLabel      = "Đã kết thúc";
                    statusBadgeClass = "student-tag--neutral";
                }
                return new StudentEnrolledClassViewModel
                {
                    ClassCode           = e.ClassCode,
                    CourseName          = e.CourseName,
                    StartDate           = e.StartDate,
                    EndDate             = e.EndDate,
                    TeacherName         = e.TeacherName,
                    TotalCourseSessions = e.TotalSessions,
                    AttendedSessions    = e.AttendedCount,
                    StatusLabel         = statusLabel,
                    StatusBadgeClass    = statusBadgeClass
                };
            }).OrderByDescending(x => x.StartDate).ToList()
        };

        return View(model);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [6] CHANGE PASSWORD – Đổi mật khẩu (POST)
    // ─────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        var studentId = GetCurrentStudentId();
        if (!studentId.HasValue)
            return RedirectToAction("Login", "Account", new { area = "" });

        if (string.IsNullOrWhiteSpace(model.CurrentPassword) ||
            string.IsNullOrWhiteSpace(model.NewPassword) ||
            string.IsNullOrWhiteSpace(model.ConfirmPassword))
        {
            TempData["ErrorMessage"] = "Vui lòng điền đầy đủ thông tin.";
            return RedirectToAction(nameof(Profile));
        }

        if (model.NewPassword != model.ConfirmPassword)
        {
            TempData["ErrorMessage"] = "Mật khẩu mới và xác nhận mật khẩu không khớp.";
            return RedirectToAction(nameof(Profile));
        }

        if (model.NewPassword.Length < 6)
        {
            TempData["ErrorMessage"] = "Mật khẩu mới phải có ít nhất 6 ký tự.";
            return RedirectToAction(nameof(Profile));
        }

        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == studentId.Value);
        if (user == null)
            return RedirectToAction("Login", "Account", new { area = "" });

        if (!BCrypt.Net.BCrypt.Verify(model.CurrentPassword, user.PasswordHash))
        {
            TempData["ErrorMessage"] = "Mật khẩu hiện tại không đúng.";
            return RedirectToAction(nameof(Profile));
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã đổi mật khẩu thành công.";
        return RedirectToAction(nameof(Profile));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PRIVATE HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<List<StudentScheduleSessionViewModel>> GetStudentScheduleSessionsAsync(
        int studentId,
        DateOnly today,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        string? filter = null)
    {
        var query = _context.Sessions
            .AsNoTracking()
            .Where(x => x.Class.Enrollments.Any(e => e.StudentId == studentId));

        if (startDate.HasValue)
            query = query.Where(x => x.Date >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(x => x.Date <= endDate.Value);

        query = filter switch
        {
            "upcoming" => query.Where(x => x.Date >= today),
            "past" => query.Where(x => x.Date < today),
            _ => query
        };

        var sessionsData = await query
            .OrderBy(x => x.Date)
            .ThenBy(x => x.StartTime)
            .ThenBy(x => x.Class.ClassCode)
            .ThenBy(x => x.SessionNo)
            .Select(x => new
            {
                x.Id,
                CourseName = x.Class.Course.Name,
                x.Class.ClassCode,
                x.SessionNo,
                x.Date,
                x.StartTime,
                x.EndTime,
                x.Topic,
                TeacherName = x.Class.Teacher.FullName,
                x.TeachingMaterialUrl,
                Attendance = x.Attendances.FirstOrDefault(a => a.StudentId == studentId)
            })
            .ToListAsync();

        return sessionsData.Select(x => new StudentScheduleSessionViewModel
        {
            SessionId = x.Id,
            CourseName = x.CourseName,
            ClassCode = x.ClassCode,
            SessionNo = x.SessionNo,
            Date = x.Date,
            StartTime = x.StartTime,
            EndTime = x.EndTime,
            Topic = x.Topic,
            TeacherName = x.TeacherName,
            TeachingMaterialUrl = x.TeachingMaterialUrl,
            IsToday = x.Date == today,
            IsPast = x.Date < today,
            HasAttendance = x.Attendance != null,
            WasPresent = x.Attendance?.IsPresent,
            WasExcused = x.Attendance?.IsExcused
        }).ToList();
    }

    private int? GetCurrentStudentId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userId, out var id) ? id : null;
    }

    private static string GetDayLabel(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => "Thứ hai",
        DayOfWeek.Tuesday => "Thứ ba",
        DayOfWeek.Wednesday => "Thứ tư",
        DayOfWeek.Thursday => "Thứ năm",
        DayOfWeek.Friday => "Thứ sáu",
        DayOfWeek.Saturday => "Thứ bảy",
        _ => "Chủ nhật"
    };

    /// <summary>
    /// Parse ProductMediaUrls: thử JSON array trước, fallback split theo ký tự phân cách.
    /// Đồng bộ với EvidenceController.SplitUrls().
    /// </summary>
    private static List<string> SplitMediaUrls(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        // Thử parse JSON array
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(raw);
            if (list != null)
                return list.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
        }
        catch { }

        // Fallback: split theo newline, comma, semicolon (giống EvidenceController)
        return raw.Split(new[] { '\n', '\r', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                  .Select(x => x.Trim())
                  .Where(x => !string.IsNullOrWhiteSpace(x))
                  .ToList();
    }

    private static bool IsYouTubeUrl(string url) =>
        url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase);

    private static bool IsVideoUrl(string url)
    {
        if (IsYouTubeUrl(url)) return true;
        try
        {
            var ext = Path.GetExtension(new Uri(url).LocalPath);
            return VideoExtensions.Contains(ext);
        }
        catch
        {
            return VideoExtensions.Contains(Path.GetExtension(url));
        }
    }

    private static bool IsImageUrl(string url)
    {
        try
        {
            var ext = Path.GetExtension(new Uri(url).LocalPath);
            return ImageExtensions.Contains(ext);
        }
        catch
        {
            return ImageExtensions.Contains(Path.GetExtension(url));
        }
    }

    /// <summary>Đồng bộ logic với FinanceController: 1=Chưa thu, 2=Một phần, 3=Đã thu đủ</summary>
    private static (string Label, string BadgeClass) GetInvoiceStatus(decimal finalAmount, decimal paidAmount)
    {
        if (paidAmount <= 0)
            return ("Chưa thu", "student-tag--warning");
        if (paidAmount >= finalAmount)
            return ("Đã thu đủ", "student-tag--success");
        return ("Thu một phần", "student-tag--warning");
    }

    private static string GetPaymentMethodLabel(string method) => method switch
    {
        "Cash"    => "Tiền mặt",
        "Banking" => "Chuyển khoản",
        "Card"    => "Thẻ",
        "Other"   => "Khác",
        _         => method
    };

    private static string FormatAiEvaluation(string? rawJson)
    {
        return AiEvaluationFormatter.FormatForDisplay(rawJson);
    }
}

