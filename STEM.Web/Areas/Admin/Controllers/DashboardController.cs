using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Areas.Admin.Models;
using STEM.Web.Data;

namespace STEM.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var sixMonthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-5);
        var upcomingLimit = today.AddDays(14);

        var studentCount = await _context.Users.CountAsync(x => x.Role.Name == "Student");
        var activeClassCount = await _context.Classes.CountAsync(x => x.StartDate <= today && (!x.EndDate.HasValue || x.EndDate.Value >= today));
        var todaySessionCount = await _context.Sessions.CountAsync(x => x.Date == today);
        var revenueThisMonth = await _context.Payments
            .Where(x => x.TransDate >= monthStart)
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;
        var outstandingAmount = await _context.Invoices
            .Select(x => x.FinalAmount - (x.Payments.Sum(p => (decimal?)p.Amount) ?? 0m))
            .SumAsync();
        var openMaintenanceCount = await _context.MaintenanceLogs.CountAsync(x => x.Status == 1 || x.Status == 2);
        var publishedPostCount = await _context.Posts.CountAsync(x => x.IsPublished);
        var activeBannerCount = await _context.Banners.CountAsync(x => x.IsActive);
        var upcomingClassCount = await _context.Classes.CountAsync(x => x.StartDate > today && x.StartDate <= upcomingLimit);

        var todayAttendanceStats = await _context.Sessions
            .Where(x => x.Date == today)
            .Select(x => new
            {
                Expected = x.Class.Enrollments.Count,
                Actual = x.Attendances.Count
            })
            .ToListAsync();

        var expectedTodayAttendance = todayAttendanceStats.Sum(x => x.Expected);
        var actualTodayAttendance = todayAttendanceStats.Sum(x => x.Actual);
        var attendanceCompletionPercent = expectedTodayAttendance == 0
            ? 0
            : (int)Math.Round((double)actualTodayAttendance / expectedTodayAttendance * 100, MidpointRounding.AwayFromZero);

        var monthlyRevenueRaw = await _context.Payments
            .Where(x => x.TransDate >= sixMonthStart)
            .GroupBy(x => new { x.TransDate.Year, x.TransDate.Month })
            .Select(x => new
            {
                x.Key.Year,
                x.Key.Month,
                Amount = x.Sum(p => p.Amount)
            })
            .ToListAsync();

        var revenueSeries = Enumerable.Range(0, 6)
            .Select(offset => sixMonthStart.AddMonths(offset))
            .Select(month => new DashboardRevenuePointViewModel
            {
                Label = $"Thg {month.Month}",
                Amount = monthlyRevenueRaw
                    .Where(x => x.Year == month.Year && x.Month == month.Month)
                    .Select(x => x.Amount)
                    .FirstOrDefault(),
            })
            .ToList();

        var maxRevenue = revenueSeries.Max(x => x.Amount);
        foreach (var point in revenueSeries)
        {
            point.AmountText = $"{point.Amount:N0}đ";
            point.HeightPercent = maxRevenue <= 0 ? 8 : Math.Max(8, (int)Math.Round((double)(point.Amount / maxRevenue) * 100, MidpointRounding.AwayFromZero));
        }

        var todaySessions = await _context.Sessions
            .AsNoTracking()
            .Where(x => x.Date == today)
            .OrderBy(x => x.StartTime)
            .Select(x => new DashboardTodaySessionViewModel
            {
                Id = x.Id,
                ClassCode = x.Class.ClassCode,
                CourseName = x.Class.Course.Name,
                TeacherName = x.Class.Teacher.FullName,
                TimeRange = $"{x.StartTime:HH\\:mm} - {x.EndTime:HH\\:mm}",
                EnrollmentCount = x.Class.Enrollments.Count,
                AttendanceCount = x.Attendances.Count
            })
            .ToListAsync();

        foreach (var session in todaySessions)
        {
            session.AttendancePercent = session.EnrollmentCount == 0
                ? 0
                : (int)Math.Round((double)session.AttendanceCount / session.EnrollmentCount * 100, MidpointRounding.AwayFromZero);
        }

        var upcomingClasses = await _context.Classes
            .AsNoTracking()
            .Where(x => x.StartDate > today && x.StartDate <= upcomingLimit)
            .OrderBy(x => x.StartDate)
            .Take(5)
            .Select(x => new DashboardUpcomingClassViewModel
            {
                Id = x.Id,
                ClassCode = x.ClassCode,
                CourseName = x.Course.Name,
                TeacherName = x.Teacher.FullName,
                StartDateText = x.StartDate.ToString("dd/MM/yyyy"),
                EnrollmentCount = x.Enrollments.Count
            })
            .ToListAsync();

        var invoiceAlerts = await _context.Invoices
            .AsNoTracking()
            .Select(x => new
            {
                x.Id,
                x.InvoiceNo,
                StudentName = x.Student.FullName,
                DueAmount = x.FinalAmount - (x.Payments.Sum(p => (decimal?)p.Amount) ?? 0m)
            })
            .Where(x => x.DueAmount > 0)
            .OrderByDescending(x => x.DueAmount)
            .Take(5)
            .Select(x => new DashboardInvoiceAlertViewModel
            {
                Id = x.Id,
                InvoiceNo = x.InvoiceNo,
                StudentName = x.StudentName,
                DueAmountText = $"{x.DueAmount:N0}đ"
            })
            .ToListAsync();

        var maintenanceAlerts = await _context.MaintenanceLogs
            .AsNoTracking()
            .Where(x => x.Status == 1 || x.Status == 2)
            .OrderBy(x => x.Status)
            .ThenByDescending(x => x.Id)
            .Take(5)
            .Select(x => new DashboardMaintenanceAlertViewModel
            {
                Id = x.Id,
                EquipmentSerialNumber = x.Equipment.SerialNumber,
                Issue = x.Issue,
                StatusLabel = x.Status == 1 ? "Mới ghi nhận" : "Đang xử lý"
            })
            .ToListAsync();

        var model = new AdminDashboardViewModel
        {
            TodayLabel = DateTime.Today.ToString("dddd, dd/MM/yyyy"),
            StudentCount = studentCount,
            ActiveClassCount = activeClassCount,
            TodaySessionCount = todaySessionCount,
            RevenueThisMonth = revenueThisMonth,
            OutstandingAmount = outstandingAmount,
            OpenMaintenanceCount = openMaintenanceCount,
            PublishedPostCount = publishedPostCount,
            ActiveBannerCount = activeBannerCount,
            UpcomingClassCount = upcomingClassCount,
            AttendanceCompletionPercent = attendanceCompletionPercent,
            RevenueSeries = revenueSeries,
            TodaySessions = todaySessions,
            UpcomingClasses = upcomingClasses,
            InvoiceAlerts = invoiceAlerts,
            MaintenanceAlerts = maintenanceAlerts,
            Metrics =
            [
                new DashboardMetricViewModel
                {
                    Label = "Học viên đang theo học",
                    Value = studentCount.ToString("N0"),
                    Hint = $"{activeClassCount} lớp đang hoạt động",
                    Icon = "group"
                },
                new DashboardMetricViewModel
                {
                    Label = "Buổi học hôm nay",
                    Value = todaySessionCount.ToString("N0"),
                    Hint = $"{attendanceCompletionPercent}% sổ điểm danh đã ghi",
                    Icon = "event_note"
                },
                new DashboardMetricViewModel
                {
                    Label = "Đã thu trong tháng",
                    Value = $"{revenueThisMonth:N0}đ",
                    Hint = $"{outstandingAmount:N0}đ còn cần theo dõi",
                    Icon = "payments"
                },
                new DashboardMetricViewModel
                {
                    Label = "Kho & nội dung",
                    Value = $"{openMaintenanceCount + activeBannerCount}",
                    Hint = $"{openMaintenanceCount} thiết bị xử lý · {activeBannerCount} banner đang bật",
                    Icon = "inventory_2"
                }
            ]
        };

        return View(model);
    }
}
