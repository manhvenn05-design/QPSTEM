using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Data;
using STEM.Web.Models.StudentViewModels;

namespace STEM.Web.Controllers;

[Authorize(Roles = "Student")]
public class StudentPortalController : Controller
{
    private readonly ApplicationDbContext _context;

    public StudentPortalController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Cổng học sinh";

        // Lấy ID học sinh đăng nhập
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var studentId))
            return RedirectToAction("Login", "Account");

        // Lấy thông tin cá nhân
        var profile = await _context.StudentProfiles
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.UserId == studentId);

        var viewModel = new StudentDashboardViewModel
        {
            Student = new StudentInfo
            {
                FullName = profile?.User?.FullName ?? User.Identity?.Name ?? "Học sinh",
                Avatar = "", // Xoá User.Avatar vì không có trong DB
                GuardianName = profile?.GuardianName ?? "",
                GuardianPhone = profile?.GuardianPhone ?? "",
                CurrentSchool = profile?.CurrentSchool ?? "",
                MedicalNotes = profile?.MedicalNotes ?? ""
            }
        };

        // Tìm các lớp học sinh đang học
        var classIds = await _context.Enrollments
            .Where(x => x.StudentId == studentId)
            .Select(x => x.ClassId)
            .ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var weekStart = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
        var weekEnd = weekStart.AddDays(6);

        // 1. Lịch học sắp tới (Upcoming Sessions) - lấy từ hôm nay trở đi
        var upcomingSessions = await _context.Sessions
            .Include(x => x.Class)
            .ThenInclude(c => c.Course)
            .Where(x => classIds.Contains(x.ClassId) && x.Date >= today)
            .OrderBy(x => x.Date).ThenBy(x => x.StartTime)
            .Take(5)
            .ToListAsync();

        viewModel.UpcomingSessions = upcomingSessions.Select(x => new StudentScheduleItemViewModel
        {
            SessionId = x.Id,
            CourseName = x.Class.Course.Name,
            ClassCode = x.Class.ClassCode,
            SessionLabel = $"Buổi {x.SessionNo:00}",
            Date = x.Date.ToDateTime(TimeOnly.MinValue),
            StartTime = x.StartTime,
            EndTime = x.EndTime,
            Topic = string.IsNullOrWhiteSpace(x.Topic) ? "Chưa cập nhật chủ đề" : x.Topic,
            Status = x.Date == today ? "Hôm nay" : "Sắp tới"
        }).ToList();

        // 2. Nhận xét & Media mới nhất (Recent Feedbacks)
        var recentAttendances = await _context.Attendances
            .Include(x => x.Session)
            .ThenInclude(s => s.Class)
            .ThenInclude(c => c.Course)
            .Where(x => x.StudentId == studentId && x.IsPresent && x.Session.Date <= today)
            .OrderByDescending(x => x.Session.Date)
            .Take(3)
            .ToListAsync();

        foreach (var att in recentAttendances)
        {
            var mediaItems = new List<MediaItem>();
            if (!string.IsNullOrWhiteSpace(att.ProductMediaUrls))
            {
                try
                {
                    // Thử parse mảng JSON, nếu không được thì xem như 1 chuỗi URL
                    var urls = JsonSerializer.Deserialize<List<string>>(att.ProductMediaUrls);
                    if (urls != null)
                    {
                        foreach (var url in urls)
                        {
                            mediaItems.Add(new MediaItem
                            {
                                Url = url,
                                Type = url.Contains("youtube.com") || url.Contains("youtu.be") ? "Video" : "Image"
                            });
                        }
                    }
                }
                catch
                {
                    mediaItems.Add(new MediaItem
                    {
                        Url = att.ProductMediaUrls,
                        Type = att.ProductMediaUrls.Contains("youtube") ? "Video" : "Image"
                    });
                }
            }

            viewModel.RecentFeedbacks.Add(new StudentFeedbackViewModel
            {
                SessionId = att.SessionId,
                CourseName = att.Session.Class.Course.Name,
                SessionLabel = $"Buổi {att.Session.SessionNo:00}",
                Date = att.Session.Date.ToDateTime(TimeOnly.MinValue),
                AiEvaluation = att.AiEvaluation ?? "",
                TeacherNote = att.TeacherRawNote ?? "",
                MediaItems = mediaItems
            });
        }

        // 3. Học phí và Công nợ (Invoices)
        var invoices = await _context.Invoices
            .Include(x => x.Class)
            .ThenInclude(c => c!.Course)
            .Include(x => x.Payments)
            .Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.Id)
            .ToListAsync();

        viewModel.Invoices = invoices.Select(x => new StudentInvoiceViewModel
        {
            InvoiceNo = x.InvoiceNo,
            CourseName = x.Class?.Course?.Name ?? "Khóa học tổng hợp",
            TotalAmount = x.FinalAmount,
            PaidAmount = x.Payments?.Sum(p => p.Amount) ?? 0,
            DueDate = x.DueDate,
            Status = x.Status // 0: Mới, 1: Thanh toán một phần, 2: Hoàn tất
        }).ToList();

        // 4. Các con số thống kê (Stats)
        viewModel.SessionsThisWeekCount = await _context.Sessions
            .Where(x => classIds.Contains(x.ClassId) && x.Date >= weekStart && x.Date <= weekEnd)
            .CountAsync();
            
        viewModel.NewFeedbackCount = viewModel.RecentFeedbacks.Count(x => x.Date >= DateTime.Today.AddDays(-7));
        viewModel.NewMediaCount = viewModel.RecentFeedbacks.Sum(x => x.MediaItems.Count);
        viewModel.PendingInvoiceCount = viewModel.Invoices.Count(x => x.RemainingAmount > 0);

        return View(viewModel);
    }
}
