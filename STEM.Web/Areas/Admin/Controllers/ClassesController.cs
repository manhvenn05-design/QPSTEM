using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Areas.Admin.Models;
using STEM.Web.Data;
using STEM.Web.Models;

namespace STEM.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class ClassesController : Controller
{
    private const int PageSize = 10;
    private readonly ApplicationDbContext _context;

    public ClassesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // ─── Index ────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index(string filter = "all", string? q = null, int page = 1)
    {
        var filters = new[]
        {
            new ClassFilterViewModel { Key = "all",       Label = "Tất cả" },
            new ClassFilterViewModel { Key = "active",    Label = "Đang mở" },
            new ClassFilterViewModel { Key = "completed", Label = "Đã kết thúc" },
            new ClassFilterViewModel { Key = "cancelled", Label = "Đã hủy" },
            new ClassFilterViewModel { Key = "suspended", Label = "Tạm dừng" }
        };

        var normalizedFilter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.Trim().ToLowerInvariant();
        if (filters.All(x => x.Key != normalizedFilter))
            normalizedFilter = "all";

        var searchTerm = q?.Trim() ?? string.Empty;
        var today = DateOnly.FromDateTime(DateTime.Today);

        var query = _context.Classes
            .AsNoTracking()
            .Select(x => new ClassListProjection
            {
                Id              = x.Id,
                ClassCode       = x.ClassCode,
                CourseName      = x.Course.Name,
                CourseCode      = x.Course.Code,
                TeacherName     = x.Teacher.FullName,
                StartDate       = x.StartDate,
                EndDate         = x.EndDate,
                Status          = x.Status,
                EnrollmentCount = x.Enrollments.Count,
                SessionCount    = x.Sessions.Count
            });

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(x =>
                x.ClassCode.Contains(searchTerm) ||
                x.CourseName.Contains(searchTerm) ||
                x.CourseCode.Contains(searchTerm) ||
                x.TeacherName.Contains(searchTerm));
        }

        query = ApplyFilter(query, normalizedFilter, today);

        var totalItems = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var classes = await query
            .OrderByDescending(x => x.StartDate)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        var model = new ClassManagementViewModel
        {
            SelectedFilter = normalizedFilter,
            SearchTerm     = searchTerm,
            TotalClasses   = totalItems,
            CurrentPage    = page,
            TotalPages     = totalPages,
            Filters        = filters,
            Classes        = classes.Select(x => MapClassListItem(x, today)).ToList()
        };

        return View(model);
    }

    // ─── Details ──────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Details(int id, string? studentSearch = null)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var model = await _context.Classes
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new ClassDetailsViewModel
            {
                Id              = x.Id,
                ClassCode       = x.ClassCode,
                CourseName      = x.Course.Name,
                CourseCode      = x.Course.Code,
                TeacherName     = x.Teacher.FullName,
                TeacherEmail    = x.Teacher.Email,
                TeacherPhone    = x.Teacher.Phone,
                StartDate       = x.StartDate,
                EndDate         = x.EndDate,
                EnrollmentCount = x.Enrollments.Count,
                SessionCount    = x.Sessions.Count,
                StatusLabel     = GetStatusLabel(x.Status),
                StatusBadgeClass = GetStatusBadgeClass(x.Status),
                AgeRangeText    = $"{x.Course.TargetAgeMin}-{x.Course.TargetAgeMax} tuổi",
                PriceText       = $"{x.Course.Price:N0}đ",
                TotalSessionsText = $"{x.Course.TotalSessions} buổi",
                Students = x.Enrollments
                    .OrderBy(e => e.Student.FullName)
                    .Select(e => new ClassStudentSummaryViewModel
                    {
                        StudentId      = e.StudentId,
                        FullName       = e.Student.FullName,
                        Username       = e.Student.Username,
                        EnrollDateText = e.EnrollDate.ToString("dd/MM/yyyy")
                    })
                    .ToList(),
                Sessions = x.Sessions
                    .OrderBy(s => s.SessionNo)
                    .Select(s => new ClassSessionSummaryViewModel
                    {
                        Id        = s.Id,
                        SessionNo = s.SessionNo,
                        Date      = s.Date,
                        StartTime = s.StartTime,
                        EndTime   = s.EndTime,
                        Topic     = s.Topic
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (model == null)
            return NotFound();

        var normalizedStudentSearch = studentSearch?.Trim() ?? string.Empty;
        var availableStudents = await GetAvailableStudentsAsync(id, normalizedStudentSearch);
        model.StudentSearchTerm        = normalizedStudentSearch;
        model.AvailableStudents        = availableStudents;
        model.AvailableStudentCount    = availableStudents.Count;

        return View(model);
    }

    // ─── Create ───────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new CreateClassViewModel();
        await PopulateOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateClassViewModel model)
    {
        await PopulateOptionsAsync(model);
        var normalizedCode = model.ClassCode.Trim().ToUpperInvariant();

        if (await _context.Classes.AnyAsync(x => x.ClassCode.ToLower() == normalizedCode.ToLower()))
            ModelState.AddModelError(nameof(model.ClassCode), "Mã lớp đã tồn tại.");

        if (!await IsValidCourseAsync(model.CourseId))
            ModelState.AddModelError(nameof(model.CourseId), "Khóa học không hợp lệ.");

        if (!await IsValidTeacherAsync(model.TeacherId))
            ModelState.AddModelError(nameof(model.TeacherId), "Giáo viên không hợp lệ.");

        if (!ModelState.IsValid)
            return View(model);

        var entity = new Class
        {
            CourseId  = model.CourseId!.Value,
            TeacherId = model.TeacherId!.Value,
            ClassCode = normalizedCode,
            StartDate = model.StartDate!.Value,
            EndDate   = model.EndDate,
            Status    = model.Status
        };

        try
        {
            _context.Classes.Add(entity);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateClassCode(ex))
        {
            ModelState.AddModelError(nameof(model.ClassCode), "Mã lớp đã tồn tại.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã tạo lớp học mới.";
        return RedirectToAction(nameof(Index));
    }

    // ─── Edit ─────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await _context.Classes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null)
            return NotFound();

        var model = new EditClassViewModel
        {
            Id        = entity.Id,
            CourseId  = entity.CourseId,
            TeacherId = entity.TeacherId,
            ClassCode = entity.ClassCode,
            StartDate = entity.StartDate,
            EndDate   = entity.EndDate,
            Status    = entity.Status
        };

        await PopulateOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditClassViewModel model)
    {
        await PopulateOptionsAsync(model);
        var entity = await _context.Classes.FirstOrDefaultAsync(x => x.Id == model.Id);

        if (entity == null)
            return NotFound();

        var normalizedCode = model.ClassCode.Trim().ToUpperInvariant();

        if (await _context.Classes.AnyAsync(x => x.Id != model.Id && x.ClassCode.ToLower() == normalizedCode.ToLower()))
            ModelState.AddModelError(nameof(model.ClassCode), "Mã lớp đã tồn tại.");

        if (!await IsValidCourseAsync(model.CourseId))
            ModelState.AddModelError(nameof(model.CourseId), "Khóa học không hợp lệ.");

        if (!await IsValidTeacherAsync(model.TeacherId))
            ModelState.AddModelError(nameof(model.TeacherId), "Giáo viên không hợp lệ.");

        if (!ModelState.IsValid)
            return View(model);

        entity.CourseId   = model.CourseId!.Value;
        entity.TeacherId  = model.TeacherId!.Value;
        entity.ClassCode  = normalizedCode;
        entity.StartDate  = model.StartDate!.Value;
        entity.EndDate    = model.EndDate;
        entity.Status     = model.Status;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateClassCode(ex))
        {
            ModelState.AddModelError(nameof(model.ClassCode), "Mã lớp đã tồn tại.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã cập nhật lớp học.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _context.Classes
            .Include(x => x.Enrollments)
            .Include(x => x.Sessions)
            .Include(x => x.Invoices)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy lớp học cần xóa.";
            return RedirectToAction(nameof(Index));
        }

        if (entity.Enrollments.Count > 0 || entity.Sessions.Count > 0 || entity.Invoices.Count > 0)
        {
            TempData["ErrorMessage"] = "Không thể xóa lớp học này vì đang có học viên, buổi học hoặc hóa đơn liên quan. Dùng \"Xóa toàn bộ dữ liệu\" nếu muốn xóa sạch.";
            return RedirectToAction(nameof(Index));
        }

        _context.Classes.Remove(entity);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã xóa lớp học.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Xóa toàn bộ lớp học và tất cả dữ liệu liên quan theo đúng thứ tự FK.
    /// Yêu cầu admin nhập lại ClassCode để xác nhận.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Purge(int id, string confirmCode)
    {
        var entity = await _context.Classes
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy lớp học.";
            return RedirectToAction(nameof(Index));
        }

        if (!string.Equals(confirmCode?.Trim(), entity.ClassCode, StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = $"Mã lớp xác nhận không khớp. Vui lòng nhập đúng \"{entity.ClassCode}\".";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            // 1. Xóa Attendance (thuộc Sessions của lớp)
            var sessionIds = await _context.Sessions
                .Where(x => x.ClassId == id)
                .Select(x => x.Id)
                .ToListAsync();

            if (sessionIds.Count > 0)
            {
                var attendances = await _context.Attendances
                    .Where(x => sessionIds.Contains(x.SessionId))
                    .ToListAsync();
                _context.Attendances.RemoveRange(attendances);
            }

            // 2. Xóa Sessions
            var sessions = await _context.Sessions
                .Where(x => x.ClassId == id)
                .ToListAsync();
            _context.Sessions.RemoveRange(sessions);

            // 3. Xóa Payments (thuộc Invoices của lớp)
            var invoiceIds = await _context.Invoices
                .Where(x => x.ClassId == id)
                .Select(x => x.Id)
                .ToListAsync();

            if (invoiceIds.Count > 0)
            {
                var payments = await _context.Payments
                    .Where(x => invoiceIds.Contains(x.InvoiceId))
                    .ToListAsync();
                _context.Payments.RemoveRange(payments);
            }

            // 4. Xóa Invoices
            var invoices = await _context.Invoices
                .Where(x => x.ClassId == id)
                .ToListAsync();
            _context.Invoices.RemoveRange(invoices);

            // 5. Xóa Enrollments
            var enrollments = await _context.Enrollments
                .Where(x => x.ClassId == id)
                .ToListAsync();
            _context.Enrollments.RemoveRange(enrollments);

            // 6. Cuối cùng xóa Class
            _context.Classes.Remove(entity);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã xóa toàn bộ dữ liệu của lớp \"{entity.ClassCode}\".";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Xóa thất bại: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    // ─── Enroll / Remove Students ─────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnrollStudent(int id, int? studentId)
    {
        if (!studentId.HasValue)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn học viên.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (!await _context.Classes.AnyAsync(x => x.Id == id))
            return NotFound();

        var isValidStudent = await _context.Users.AnyAsync(x =>
            x.Id == studentId.Value &&
            x.IsActive &&
            x.Role.Name == "Student");

        if (!isValidStudent)
        {
            TempData["ErrorMessage"] = "Học viên không hợp lệ.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (await _context.Enrollments.AnyAsync(x => x.ClassId == id && x.StudentId == studentId.Value))
        {
            TempData["ErrorMessage"] = "Học viên này đã có trong lớp.";
            return RedirectToAction(nameof(Details), new { id });
        }

        _context.Enrollments.Add(new Enrollment
        {
            ClassId   = id,
            StudentId = studentId.Value
        });

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã thêm học viên vào lớp.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnrollStudents(int id, List<int> studentIds)
    {
        if (studentIds.Count == 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn ít nhất một học viên.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (!await _context.Classes.AnyAsync(x => x.Id == id))
            return NotFound();

        var validStudentIds = await _context.Users
            .AsNoTracking()
            .Where(x =>
                studentIds.Contains(x.Id) &&
                x.IsActive &&
                x.Role.Name == "Student")
            .Select(x => x.Id)
            .ToListAsync();

        if (validStudentIds.Count == 0)
        {
            TempData["ErrorMessage"] = "Không có học viên hợp lệ để thêm vào lớp.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var enrolledIds = await _context.Enrollments
            .AsNoTracking()
            .Where(x => x.ClassId == id && validStudentIds.Contains(x.StudentId))
            .Select(x => x.StudentId)
            .ToListAsync();

        var newIds = validStudentIds.Except(enrolledIds).ToList();
        if (newIds.Count == 0)
        {
            TempData["ErrorMessage"] = "Các học viên đã chọn đều đã có trong lớp.";
            return RedirectToAction(nameof(Details), new { id });
        }

        _context.Enrollments.AddRange(newIds.Select(sid => new Enrollment
        {
            ClassId   = id,
            StudentId = sid
        }));

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Đã thêm {newIds.Count} học viên vào lớp.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveStudent(int id, int studentId)
    {
        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(x => x.ClassId == id && x.StudentId == studentId);

        if (enrollment == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy học viên trong lớp này.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (await _context.Attendances.AnyAsync(x => x.StudentId == studentId && x.Session.ClassId == id))
        {
            TempData["ErrorMessage"] = "Không thể gỡ học viên này vì đã có điểm danh trong lớp.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (await _context.Invoices.AnyAsync(x => x.ClassId == id && x.StudentId == studentId))
        {
            TempData["ErrorMessage"] = "Không thể gỡ học viên này vì đã có hóa đơn liên quan.";
            return RedirectToAction(nameof(Details), new { id });
        }

        _context.Enrollments.Remove(enrollment);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã gỡ học viên khỏi lớp.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private async Task PopulateOptionsAsync(CreateClassViewModel model)
    {
        model.CourseOptions = await _context.Courses
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem($"{x.Name} ({x.Code})", x.Id.ToString()))
            .ToListAsync();

        model.TeacherOptions = await _context.Users
            .AsNoTracking()
            .Where(x => x.IsActive && x.Role.Name == "Teacher")
            .OrderBy(x => x.FullName)
            .Select(x => new SelectListItem(x.FullName, x.Id.ToString()))
            .ToListAsync();

        var statusValues = Enum.GetValues<ClassStatus>();
        model.StatusOptions = statusValues.Select(s => new SelectListItem
        {
            Value = ((int)s).ToString(),
            Text = GetStatusLabel(s)
        }).ToList();
    }

    private async Task<IReadOnlyList<ClassAvailableStudentViewModel>> GetAvailableStudentsAsync(int classId, string searchTerm)
    {
        var query = _context.Users
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.Role.Name == "Student" &&
                !x.Enrollments.Any(e => e.ClassId == classId));

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(x =>
                x.FullName.Contains(searchTerm) ||
                x.Username.Contains(searchTerm));
        }

        return await query
            .OrderBy(x => x.FullName)
            .Select(x => new ClassAvailableStudentViewModel
            {
                StudentId  = x.Id,
                FullName   = x.FullName,
                Username   = x.Username,
                SchoolName = x.StudentProfile != null ? x.StudentProfile.CurrentSchool : null
            })
            .Take(100)
            .ToListAsync();
    }

    private async Task<bool> IsValidCourseAsync(int? courseId)
    {
        return courseId.HasValue && await _context.Courses.AnyAsync(x => x.Id == courseId.Value);
    }

    private async Task<bool> IsValidTeacherAsync(int? teacherId)
    {
        return teacherId.HasValue && await _context.Users.AnyAsync(x =>
            x.Id == teacherId.Value &&
            x.IsActive &&
            x.Role.Name == "Teacher");
    }

    private static IQueryable<ClassListProjection> ApplyFilter(IQueryable<ClassListProjection> query, string filter, DateOnly today)
    {
        return filter switch
        {
            "active"    => query.Where(x => x.Status == ClassStatus.Active),
            "completed" => query.Where(x => x.Status == ClassStatus.Completed),
            "cancelled" => query.Where(x => x.Status == ClassStatus.Cancelled),
            "suspended" => query.Where(x => x.Status == ClassStatus.Suspended),
            _           => query
        };
    }

    private static ClassManagementItemViewModel MapClassListItem(ClassListProjection item, DateOnly today)
    {
        var statusLabel = GetStatusLabel(item.Status);
        var statusBadgeClass = GetStatusBadgeClass(item.Status);

        var scheduleText = $"{item.StartDate:dd/MM/yyyy} – {(item.EndDate.HasValue ? item.EndDate.Value.ToString("dd/MM/yyyy") : "Chưa rõ")}";

        return new ClassManagementItemViewModel
        {
            Id              = item.Id,
            ClassCode       = item.ClassCode,
            CourseName      = item.CourseName,
            CourseCode      = item.CourseCode,
            TeacherName     = item.TeacherName,
            ScheduleText    = scheduleText,
            EnrollmentCount = item.EnrollmentCount,
            SessionCount    = item.SessionCount,
            EnrollmentText  = $"{item.EnrollmentCount} học viên · {item.SessionCount} buổi",
            StatusLabel     = statusLabel,
            StatusBadgeClass = statusBadgeClass
        };
    }

    private static bool IsDuplicateClassCode(DbUpdateException ex)
    {
        if (ex.InnerException is not SqlException sqlEx)
            return false;

        return sqlEx.Number is 2601 or 2627
            && sqlEx.Message.Contains("UQ__Classes__2ECD4A55", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetStatusLabel(ClassStatus status) => status switch
    {
        ClassStatus.Active => "Đang mở",
        ClassStatus.Completed => "Đã kết thúc",
        ClassStatus.Cancelled => "Đã hủy",
        ClassStatus.Suspended => "Tạm dừng",
        _ => "Không xác định"
    };

    private static string GetStatusBadgeClass(ClassStatus status) => status switch
    {
        ClassStatus.Active => "bg-[#edf7e8] text-[#456c3f]", // Green
        ClassStatus.Completed => "bg-[#eeeee9] text-[#42493d]", // Gray
        ClassStatus.Suspended => "bg-[#fff4e8] text-[#9b682f]", // Orange
        ClassStatus.Cancelled => "bg-[#fceeed] text-[#b33a3a]", // Red
        _ => "bg-[#eeeee9] text-[#42493d]"
    };

    // ─── Nested projection ────────────────────────────────────────────────────

    private sealed class ClassListProjection
    {
        public int Id { get; set; }
        public string ClassCode { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public ClassStatus Status { get; set; }
        public int EnrollmentCount { get; set; }
        public int SessionCount { get; set; }
    }
}
