using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Areas.Admin.Models;
using STEM.Web.Data;
using STEM.Web.Models;

namespace STEM.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class SessionsController : Controller
{
    private const int PageSize = 10;
    private readonly ApplicationDbContext _context;

    public SessionsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string filter = "all", string? q = null, int page = 1)
    {
        var filters = new[]
        {
            new SessionFilterViewModel { Key = "all", Label = "Tất cả" },
            new SessionFilterViewModel { Key = "today", Label = "Hôm nay" },
            new SessionFilterViewModel { Key = "upcoming", Label = "Sắp tới" },
            new SessionFilterViewModel { Key = "past", Label = "Đã diễn ra" }
        };

        var normalizedFilter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.Trim().ToLowerInvariant();
        if (filters.All(x => x.Key != normalizedFilter))
        {
            normalizedFilter = "all";
        }

        var searchTerm = q?.Trim() ?? string.Empty;
        var today = DateOnly.FromDateTime(DateTime.Today);

        var query = _context.Sessions
            .AsNoTracking()
            .Select(x => new SessionListProjection
            {
                Id = x.Id,
                ClassCode = x.Class.ClassCode,
                CourseName = x.Class.Course.Name,
                TeacherName = x.Class.Teacher.FullName,
                SessionNo = x.SessionNo,
                Date = x.Date,
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                Topic = x.Topic,
                TeachingMaterialUrl = x.TeachingMaterialUrl,
                ClassMediaUrls = x.ClassMediaUrls,
                AttendanceCount = x.Attendances.Count,
                EquipmentBorrowCount = x.EquipmentBorrows.Count
            });

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(x =>
                x.ClassCode.Contains(searchTerm) ||
                x.CourseName.Contains(searchTerm) ||
                x.TeacherName.Contains(searchTerm) ||
                (x.Topic != null && x.Topic.Contains(searchTerm)));
        }

        query = ApplyFilter(query, normalizedFilter, today);

        var totalItems = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var sessions = await query
            .OrderByDescending(x => x.Date)
            .ThenBy(x => x.StartTime)
            .ThenBy(x => x.SessionNo)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        var model = new SessionManagementViewModel
        {
            SelectedFilter = normalizedFilter,
            SearchTerm = searchTerm,
            TotalSessions = totalItems,
            CurrentPage = page,
            TotalPages = totalPages,
            Filters = filters,
            Sessions = sessions.Select(x => MapSessionListItem(x, today)).ToList()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var model = await _context.Sessions
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new SessionDetailsViewModel
            {
                Id = x.Id,
                SessionLabel = $"Buổi {x.SessionNo:00}",
                ClassCode = x.Class.ClassCode,
                CourseName = x.Class.Course.Name,
                TeacherName = x.Class.Teacher.FullName,
                TeacherEmail = x.Class.Teacher.Email,
                ClassStartDate = x.Class.StartDate,
                ClassEndDate = x.Class.EndDate,
                Date = x.Date,
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                Topic = x.Topic,
                TeachingMaterialUrl = x.TeachingMaterialUrl,
                ClassMediaUrls = x.ClassMediaUrls,
                AssistantNote = x.AssistantNote,
                AttendanceCount = x.Attendances.Count,
                EquipmentBorrowCount = x.EquipmentBorrows.Count,
                StatusLabel = x.Date > today ? "Sắp tới" : x.Date < today ? "Đã diễn ra" : "Hôm nay",
                StatusBadgeClass = x.Date > today
                    ? "bg-[#fff4e8] text-[#9b682f]"
                    : x.Date < today
                        ? "bg-[#eeeee9] text-[#42493d]"
                        : "bg-[#edf7e8] text-[#456c3f]",
                Attendances = x.Attendances
                    .OrderBy(a => a.Student.FullName)
                    .Select(a => new SessionAttendanceSummaryViewModel
                    {
                        StudentName = a.Student.FullName,
                        IsPresent = a.IsPresent,
                        AiProcessStatus = a.AiProcessStatus ?? "Chưa xử lý"
                    })
                    .ToList(),
                EquipmentBorrows = x.EquipmentBorrows
                    .OrderByDescending(b => b.BorrowTime)
                    .Select(b => new SessionEquipmentBorrowSummaryViewModel
                    {
                        SerialNumber = b.Equipment.SerialNumber,
                        BorrowerName = b.Borrower.FullName,
                        BorrowTimeText = b.BorrowTime.ToString("dd/MM/yyyy HH:mm"),
                        ReturnTimeText = b.ReturnTime.HasValue ? b.ReturnTime.Value.ToString("dd/MM/yyyy HH:mm") : "Chưa trả"
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (model == null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? classId = null)
    {
        var model = new CreateSessionViewModel
        {
            ClassId = classId
        };
        await PopulateOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateSessionViewModel model)
    {
        await PopulateOptionsAsync(model);
        await ValidateSessionAsync(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var entity = new Session
        {
            ClassId = model.ClassId!.Value,
            SessionNo = model.SessionNo,
            Date = model.Date!.Value,
            StartTime = model.StartTime!.Value,
            EndTime = model.EndTime!.Value,
            Topic = NormalizeText(model.Topic),
            TeachingMaterialUrl = NormalizeText(model.TeachingMaterialUrl),
            ClassMediaUrls = NormalizeText(model.ClassMediaUrls),
            AssistantNote = NormalizeText(model.AssistantNote)
        };

        _context.Sessions.Add(entity);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã tạo buổi học mới.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await _context.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null)
        {
            return NotFound();
        }

        var model = new EditSessionViewModel
        {
            Id = entity.Id,
            ClassId = entity.ClassId,
            SessionNo = entity.SessionNo,
            Date = entity.Date,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            Topic = entity.Topic,
            TeachingMaterialUrl = entity.TeachingMaterialUrl,
            ClassMediaUrls = entity.ClassMediaUrls,
            AssistantNote = entity.AssistantNote
        };

        await PopulateOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditSessionViewModel model)
    {
        await PopulateOptionsAsync(model);

        var entity = await _context.Sessions.FirstOrDefaultAsync(x => x.Id == model.Id);
        if (entity == null)
        {
            return NotFound();
        }

        await ValidateSessionAsync(model, model.Id);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        entity.ClassId = model.ClassId!.Value;
        entity.SessionNo = model.SessionNo;
        entity.Date = model.Date!.Value;
        entity.StartTime = model.StartTime!.Value;
        entity.EndTime = model.EndTime!.Value;
        entity.Topic = NormalizeText(model.Topic);
        entity.TeachingMaterialUrl = NormalizeText(model.TeachingMaterialUrl);
        entity.ClassMediaUrls = NormalizeText(model.ClassMediaUrls);
        entity.AssistantNote = NormalizeText(model.AssistantNote);

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã cập nhật buổi học.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _context.Sessions
            .Include(x => x.Attendances)
            .Include(x => x.EquipmentBorrows)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy buổi học cần xóa.";
            return RedirectToAction(nameof(Index));
        }

        if (entity.Attendances.Count > 0 || entity.EquipmentBorrows.Count > 0)
        {
            TempData["ErrorMessage"] = "Không thể xóa buổi học này vì đã có điểm danh hoặc dữ liệu mượn trả thiết bị liên quan.";
            return RedirectToAction(nameof(Index));
        }

        _context.Sessions.Remove(entity);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã xóa buổi học.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateOptionsAsync(CreateSessionViewModel model)
    {
        model.ClassOptions = await _context.Classes
            .AsNoTracking()
            .OrderBy(x => x.ClassCode)
            .Select(x => new SelectListItem($"{x.ClassCode} - {x.Course.Name}", x.Id.ToString()))
            .ToListAsync();
    }

    private async Task ValidateSessionAsync(CreateSessionViewModel model, int? currentId = null)
    {
        if (!model.ClassId.HasValue || !await _context.Classes.AnyAsync(x => x.Id == model.ClassId.Value))
        {
            ModelState.AddModelError(nameof(model.ClassId), "Lớp học không hợp lệ.");
            return;
        }

        var classInfo = await _context.Classes
            .AsNoTracking()
            .Where(x => x.Id == model.ClassId.Value)
            .Select(x => new
            {
                x.StartDate,
                x.EndDate,
                TotalSessions = x.Course.TotalSessions
            })
            .FirstAsync();

        if (model.SessionNo > classInfo.TotalSessions)
        {
            ModelState.AddModelError(nameof(model.SessionNo), $"Số buổi không được vượt quá tổng số buổi của khóa học ({classInfo.TotalSessions}).");
        }

        if (model.Date.HasValue)
        {
            if (model.Date.Value < classInfo.StartDate)
            {
                ModelState.AddModelError(nameof(model.Date), "Ngày học không được nhỏ hơn ngày bắt đầu của lớp.");
            }

            if (classInfo.EndDate.HasValue && model.Date.Value > classInfo.EndDate.Value)
            {
                ModelState.AddModelError(nameof(model.Date), "Ngày học không được lớn hơn ngày kết thúc của lớp.");
            }
        }

        var duplicateSessionNo = await _context.Sessions.AnyAsync(x =>
            x.ClassId == model.ClassId.Value &&
            x.SessionNo == model.SessionNo &&
            (!currentId.HasValue || x.Id != currentId.Value));

        if (duplicateSessionNo)
        {
            ModelState.AddModelError(nameof(model.SessionNo), "Số buổi này đã tồn tại trong lớp học đã chọn.");
        }
    }

    private static IQueryable<SessionListProjection> ApplyFilter(IQueryable<SessionListProjection> query, string filter, DateOnly today)
    {
        return filter switch
        {
            "today" => query.Where(x => x.Date == today),
            "upcoming" => query.Where(x => x.Date > today),
            "past" => query.Where(x => x.Date < today),
            _ => query
        };
    }

    private static SessionManagementItemViewModel MapSessionListItem(SessionListProjection item, DateOnly today)
    {
        var statusLabel = item.Date > today ? "Sắp tới" : item.Date < today ? "Đã diễn ra" : "Hôm nay";
        var statusBadgeClass = item.Date > today
            ? "bg-[#fff4e8] text-[#9b682f]"
            : item.Date < today
                ? "bg-[#eeeee9] text-[#42493d]"
                : "bg-[#edf7e8] text-[#456c3f]";

        return new SessionManagementItemViewModel
        {
            Id = item.Id,
            SessionLabel = $"Buổi {item.SessionNo:00}",
            ClassCode = item.ClassCode,
            CourseName = item.CourseName,
            TeacherName = item.TeacherName,
            DateText = item.Date.ToString("dd/MM/yyyy"),
            TimeRangeText = $"{item.StartTime:HH\\:mm} - {item.EndTime:HH\\:mm}",
            TopicText = string.IsNullOrWhiteSpace(item.Topic) ? "Chưa cập nhật chủ đề" : item.Topic,
            MaterialText = string.IsNullOrWhiteSpace(item.TeachingMaterialUrl) ? "Chưa gắn giáo án" : "Đã gắn giáo án",
            MediaText = string.IsNullOrWhiteSpace(item.ClassMediaUrls) ? "Chưa có media lớp" : "Đã có media lớp",
            AttendanceCount = item.AttendanceCount,
            EquipmentBorrowCount = item.EquipmentBorrowCount,
            StatusLabel = statusLabel,
            StatusBadgeClass = statusBadgeClass
        };
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed class SessionListProjection
    {
        public int Id { get; set; }
        public string ClassCode { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public int SessionNo { get; set; }
        public DateOnly Date { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public string? Topic { get; set; }
        public string? TeachingMaterialUrl { get; set; }
        public string? ClassMediaUrls { get; set; }
        public int AttendanceCount { get; set; }
        public int EquipmentBorrowCount { get; set; }
    }
}
