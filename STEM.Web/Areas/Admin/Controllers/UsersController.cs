using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Areas.Admin.Infrastructure;
using STEM.Web.Areas.Admin.Models;
using STEM.Web.Data;
using STEM.Web.Models;

namespace STEM.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public UsersController(ApplicationDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string role = "all", string? q = null)
    {
        var filters = new[]
        {
            new UserRoleFilterViewModel { Key = "all", Label = "Tất cả" },
            new UserRoleFilterViewModel { Key = "admin", Label = "Quản trị" },
            new UserRoleFilterViewModel { Key = "teacher", Label = "Giáo viên" },
            new UserRoleFilterViewModel { Key = "student", Label = "Học sinh" }
        };

        var normalizedRole = string.IsNullOrWhiteSpace(role) ? "all" : role.Trim().ToLowerInvariant();
        if (filters.All(x => x.Key != normalizedRole))
        {
            normalizedRole = "all";
        }

        var searchTerm = q?.Trim() ?? string.Empty;

        var query = _context.Users
            .AsNoTracking()
            .Include(x => x.Role)
            .Where(x => x.Role.Name != "Parent" && x.Role.Name != "Phụ huynh");

        query = ApplyRoleFilter(query, normalizedRole);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(x =>
                x.FullName.Contains(searchTerm) ||
                x.Username.Contains(searchTerm) ||
                (x.Email != null && x.Email.Contains(searchTerm)));
        }

        var users = await query
            .OrderByDescending(x => x.Id)
            .Select(x => new UserManagementItemViewModel
            {
                Id = x.Id,
                Name = x.FullName,
                Email = x.Email ?? x.Username,
                Username = x.Username,
                Role = NormalizeRoleLabel(x.Role.Name),
                RoleBadgeClass = GetRoleBadgeClass(x.Role.Name),
                Status = x.IsActive ? "Hoạt động" : "Bị khóa",
                StatusBadgeClass = x.IsActive ? "bg-[#edf7e8] text-[#5b8d3f]" : "bg-[#fdeaea] text-[#c94d48]",
                JoinedAt = x.CreatedAt.HasValue ? x.CreatedAt.Value.ToString("dd/MM/yyyy") : "Chưa có dữ liệu",
                AvatarText = GetAvatarText(x.FullName),
                AvatarClass = GetAvatarClass(x.Role.Name),
                AvatarUrl = x.AvatarUrl
            })
            .ToListAsync();

        var model = new UserManagementViewModel
        {
            SelectedRole = normalizedRole,
            SearchTerm = searchTerm,
            TotalUsers = users.Count,
            CurrentPage = 1,
            TotalPages = 1,
            Filters = filters,
            Users = users
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Include(x => x.Role)
            .Include(x => x.StudentProfile)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
        {
            return NotFound();
        }

        var model = new UserDetailsViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Username = user.Username,
            Email = user.Email,
            Phone = user.Phone,
            AvatarUrl = user.AvatarUrl,
            RoleName = NormalizeRoleLabel(user.Role.Name),
            IsActive = user.IsActive,
            StatusLabel = user.IsActive ? "Hoạt động" : "Bị khóa",
            StatusBadgeClass = user.IsActive ? "bg-[#edf7e8] text-[#5b8d3f]" : "bg-[#fdeaea] text-[#c94d48]",
            CurrentSchool = user.StudentProfile?.CurrentSchool,
            GuardianName = user.StudentProfile?.GuardianName,
            GuardianPhone = user.StudentProfile?.GuardianPhone,
            MedicalNotes = user.StudentProfile?.MedicalNotes
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new CreateUserViewModel
        {
            RoleOptions = await BuildRoleOptionsAsync()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        var roles = await GetAvailableRolesAsync();
        model.RoleOptions = roles
            .Select(x => new SelectListItem(NormalizeRoleLabel(x.Name), x.Id.ToString()))
            .ToList();

        var normalizedUsername = model.Username.Trim();
        var normalizedEmail = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
        var selectedRole = roles.FirstOrDefault(x => x.Id == model.RoleId);
        model.IsStudentRoleSelected = selectedRole != null && IsStudentRole(selectedRole.Name);

        if (!string.IsNullOrWhiteSpace(normalizedUsername) &&
            await _context.Users.AnyAsync(x => x.Username.ToLower() == normalizedUsername.ToLower()))
        {
            model.SuggestedUsernames = await SuggestAvailableUsernamesAsync(normalizedUsername, model.FullName);
            ModelState.AddModelError(nameof(model.Username), "Tên đăng nhập đã tồn tại. Hãy chọn tên khác hoặc dùng một trong các gợi ý bên dưới.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedEmail) &&
            await _context.Users.AnyAsync(x => x.Email != null && x.Email.ToLower() == normalizedEmail.ToLower()))
        {
            ModelState.AddModelError(nameof(model.Email), "Email đã được sử dụng.");
        }

        if (selectedRole == null)
        {
            ModelState.AddModelError(nameof(model.RoleId), "Vai trò không hợp lệ.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string? uploadedAvatarUrl = null;
        try
        {
            if (model.AvatarFile != null)
            {
                uploadedAvatarUrl = await AdminImageStorage.SaveImageAsync(model.AvatarFile, _environment.WebRootPath, "users");
            }
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(model.AvatarFile), ex.Message);
            return View(model);
        }

        var user = new User
        {
            FullName = model.FullName.Trim(),
            Username = normalizedUsername,
            Email = normalizedEmail,
            Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim(),
            AvatarUrl = uploadedAvatarUrl,
            CreatedAt = DateTime.Now,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
            RoleId = model.RoleId!.Value,
            IsActive = model.IsActive
        };

        try
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await UpsertStudentProfileAsync(user.Id, model.IsStudentRoleSelected, model.CurrentSchool, model.GuardianName, model.GuardianPhone, model.MedicalNotes);
        }
        catch (DbUpdateException ex) when (IsDuplicateUsername(ex))
        {
            if (uploadedAvatarUrl != null)
            {
                AdminImageStorage.DeleteIfManaged(uploadedAvatarUrl, _environment.WebRootPath);
            }

            model.SuggestedUsernames = await SuggestAvailableUsernamesAsync(normalizedUsername, model.FullName);
            ModelState.AddModelError(nameof(model.Username), "Tên đăng nhập đã tồn tại. Hãy chọn tên khác hoặc dùng một trong các gợi ý bên dưới.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã tạo người dùng mới.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Include(x => x.Role)
            .Include(x => x.StudentProfile)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
        {
            return NotFound();
        }

        var model = new EditUserViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Username = user.Username,
            Email = user.Email,
            Phone = user.Phone,
            AvatarUrl = user.AvatarUrl,
            RoleId = user.RoleId,
            IsActive = user.IsActive,
            CurrentSchool = user.StudentProfile?.CurrentSchool,
            GuardianName = user.StudentProfile?.GuardianName,
            GuardianPhone = user.StudentProfile?.GuardianPhone,
            MedicalNotes = user.StudentProfile?.MedicalNotes,
            IsStudentRoleSelected = IsStudentRole(user.Role.Name),
            RoleOptions = await BuildRoleOptionsAsync()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditUserViewModel model)
    {
        var roles = await GetAvailableRolesAsync();
        model.RoleOptions = roles
            .Select(x => new SelectListItem(NormalizeRoleLabel(x.Name), x.Id.ToString()))
            .ToList();

        var user = await _context.Users
            .Include(x => x.Role)
            .Include(x => x.StudentProfile)
            .FirstOrDefaultAsync(x => x.Id == model.Id);

        if (user == null)
        {
            return NotFound();
        }

        model.AvatarUrl = user.AvatarUrl;

        var normalizedUsername = model.Username.Trim();
        var normalizedEmail = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
        var selectedRole = roles.FirstOrDefault(x => x.Id == model.RoleId);
        model.IsStudentRoleSelected = selectedRole != null && IsStudentRole(selectedRole.Name);

        if (!string.IsNullOrWhiteSpace(normalizedUsername) &&
            await _context.Users.AnyAsync(x => x.Id != model.Id && x.Username.ToLower() == normalizedUsername.ToLower()))
        {
            model.SuggestedUsernames = await SuggestAvailableUsernamesAsync(normalizedUsername, model.FullName);
            ModelState.AddModelError(nameof(model.Username), "Tên đăng nhập đã tồn tại. Hãy chọn tên khác hoặc dùng một trong các gợi ý bên dưới.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedEmail) &&
            await _context.Users.AnyAsync(x => x.Id != model.Id && x.Email != null && x.Email.ToLower() == normalizedEmail.ToLower()))
        {
            ModelState.AddModelError(nameof(model.Email), "Email đã được sử dụng.");
        }

        if (selectedRole == null)
        {
            ModelState.AddModelError(nameof(model.RoleId), "Vai trò không hợp lệ.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var previousAvatarUrl = user.AvatarUrl;
        string? uploadedAvatarUrl = null;

        try
        {
            if (model.AvatarFile != null)
            {
                uploadedAvatarUrl = await AdminImageStorage.SaveImageAsync(model.AvatarFile, _environment.WebRootPath, "users");
                user.AvatarUrl = uploadedAvatarUrl;
            }
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(model.AvatarFile), ex.Message);
            return View(model);
        }

        user.FullName = model.FullName.Trim();
        user.Username = normalizedUsername;
        user.Email = normalizedEmail;
        user.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
        user.RoleId = model.RoleId!.Value;
        user.IsActive = model.IsActive;

        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
        }

        try
        {
            await _context.SaveChangesAsync();
            await UpsertStudentProfileAsync(user.Id, model.IsStudentRoleSelected, model.CurrentSchool, model.GuardianName, model.GuardianPhone, model.MedicalNotes);
        }
        catch (DbUpdateException ex) when (IsDuplicateUsername(ex))
        {
            if (uploadedAvatarUrl != null)
            {
                AdminImageStorage.DeleteIfManaged(uploadedAvatarUrl, _environment.WebRootPath);
                user.AvatarUrl = previousAvatarUrl;
            }

            model.SuggestedUsernames = await SuggestAvailableUsernamesAsync(normalizedUsername, model.FullName);
            ModelState.AddModelError(nameof(model.Username), "Tên đăng nhập đã tồn tại. Hãy chọn tên khác hoặc dùng một trong các gợi ý bên dưới.");
            return View(model);
        }

        if (uploadedAvatarUrl != null && previousAvatarUrl != uploadedAvatarUrl)
        {
            AdminImageStorage.DeleteIfManaged(previousAvatarUrl, _environment.WebRootPath);
        }

        TempData["SuccessMessage"] = "Đã cập nhật người dùng.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _context.Users
            .Include(x => x.StudentProfile)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy người dùng cần xóa.";
            return RedirectToAction(nameof(Index));
        }

        // Kiểm tra có dữ liệu liên quan không
        var hasRelatedData =
            await _context.Attendances.AnyAsync(x => x.StudentId == id) ||
            await _context.Enrollments.AnyAsync(x => x.StudentId == id) ||
            await _context.Invoices.AnyAsync(x => x.StudentId == id) ||
            await _context.Classes.AnyAsync(x => x.TeacherId == id) ||
            await _context.MaintenanceLogs.AnyAsync(x => x.ReportedBy == id) ||
            await _context.EquipmentBorrows.AnyAsync(x => x.BorrowerId == id) ||
            await _context.Posts.AnyAsync(x => x.AuthorId == id);

        if (hasRelatedData)
        {
            TempData["ErrorMessage"] = $"Tài khoản \"{ user.FullName}\" đang có dữ liệu liên quan. Dùng \"Khóa tài khoản\" để vô hiệu hóa, hoặc \"Xóa toàn bộ dữ liệu\" nếu là tài khoản test.";
            return RedirectToAction(nameof(Index));
        }

        var avatarUrl = user.AvatarUrl;

        if (user.StudentProfile != null)
            _context.StudentProfiles.Remove(user.StudentProfile);

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        AdminImageStorage.DeleteIfManaged(avatarUrl, _environment.WebRootPath);

        TempData["SuccessMessage"] = "Đã xóa người dùng.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Xóa toàn bộ người dùng và tất cả dữ liệu liên quan theo đúng thứ tự FK.
    /// Yêu cầu admin nhập lại username để xác nhận.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Purge(int id, string confirmUsername)
    {
        var user = await _context.Users
            .Include(x => x.StudentProfile)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy người dùng.";
            return RedirectToAction(nameof(Index));
        }

        // Xác nhận bằng tên đăng nhập để tránh xóa nhầm
        if (!string.Equals(confirmUsername?.Trim(), user.Username, StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = $"Tên đăng nhập xác nhận không khớp. Vui lòng nhập đúng \"{user.Username}\".";
            return RedirectToAction(nameof(Index));
        }

        var avatarUrl = user.AvatarUrl;

        try
        {
            // ── Xóa theo đúng thứ tự FK: con trước, cha sau ──────────────────

            // 1. MaintenanceLogs (ReportedBy NOT NULL)
            var maintenanceLogs = await _context.MaintenanceLogs
                .Where(x => x.ReportedBy == id)
                .ToListAsync();
            _context.MaintenanceLogs.RemoveRange(maintenanceLogs);

            // 2. EquipmentBorrows (BorrowerId NOT NULL)
            var equipmentBorrows = await _context.EquipmentBorrows
                .Where(x => x.BorrowerId == id)
                .ToListAsync();
            _context.EquipmentBorrows.RemoveRange(equipmentBorrows);

            // 3. Posts (AuthorId NOT NULL — giáo viên/admin viết bài CMS)
            var posts = await _context.Posts
                .Where(x => x.AuthorId == id)
                .ToListAsync();
            _context.Posts.RemoveRange(posts);

            // 4. Xử lý Classes mà user là giáo viên phụ trách
            var classIds = await _context.Classes
                .Where(x => x.TeacherId == id)
                .Select(x => x.Id)
                .ToListAsync();

            if (classIds.Count > 0)
            {
                // 4a. Attendances thuộc Sessions của các lớp này
                var classSessionIds = await _context.Sessions
                    .Where(x => classIds.Contains(x.ClassId))
                    .Select(x => x.Id)
                    .ToListAsync();

                if (classSessionIds.Count > 0)
                {
                    var sessionAttendances = await _context.Attendances
                        .Where(x => classSessionIds.Contains(x.SessionId))
                        .ToListAsync();
                    _context.Attendances.RemoveRange(sessionAttendances);

                    var sessionBorrows = await _context.EquipmentBorrows
                        .Where(x => classSessionIds.Contains(x.SessionId))
                        .ToListAsync();
                    _context.EquipmentBorrows.RemoveRange(sessionBorrows);
                }

                // 4b. Sessions của lớp
                var classSessions = await _context.Sessions
                    .Where(x => classIds.Contains(x.ClassId))
                    .ToListAsync();
                _context.Sessions.RemoveRange(classSessions);

                // 4c. Payments → Invoices của lớp
                var classInvoiceIds = await _context.Invoices
                    .Where(x => x.ClassId != null && classIds.Contains(x.ClassId.Value))
                    .Select(x => x.Id)
                    .ToListAsync();

                if (classInvoiceIds.Count > 0)
                {
                    var classPayments = await _context.Payments
                        .Where(x => classInvoiceIds.Contains(x.InvoiceId))
                        .ToListAsync();
                    _context.Payments.RemoveRange(classPayments);

                    var classInvoices = await _context.Invoices
                        .Where(x => classInvoiceIds.Contains(x.Id))
                        .ToListAsync();
                    _context.Invoices.RemoveRange(classInvoices);
                }

                // 4d. Enrollments của lớp
                var classEnrollments = await _context.Enrollments
                    .Where(x => classIds.Contains(x.ClassId))
                    .ToListAsync();
                _context.Enrollments.RemoveRange(classEnrollments);

                // 4e. Bản thân các lớp
                var classes = await _context.Classes
                    .Where(x => classIds.Contains(x.Id))
                    .ToListAsync();
                _context.Classes.RemoveRange(classes);
            }

            // 5. Attendances (StudentId — nếu user là học sinh)
            var studentAttendances = await _context.Attendances
                .Where(x => x.StudentId == id)
                .ToListAsync();
            _context.Attendances.RemoveRange(studentAttendances);

            // 6. Enrollments (StudentId)
            var enrollments = await _context.Enrollments
                .Where(x => x.StudentId == id)
                .ToListAsync();
            _context.Enrollments.RemoveRange(enrollments);

            // 7. Payments → Invoices (StudentId)
            var invoiceIds = await _context.Invoices
                .Where(x => x.StudentId == id)
                .Select(x => x.Id)
                .ToListAsync();

            if (invoiceIds.Count > 0)
            {
                var payments = await _context.Payments
                    .Where(x => invoiceIds.Contains(x.InvoiceId))
                    .ToListAsync();
                _context.Payments.RemoveRange(payments);
            }

            var invoices = await _context.Invoices
                .Where(x => x.StudentId == id)
                .ToListAsync();
            _context.Invoices.RemoveRange(invoices);

            // 8. StudentProfile
            if (user.StudentProfile != null)
            {
                _context.StudentProfiles.Remove(user.StudentProfile);
            }

            // 9. Cuối cùng: xóa User
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            AdminImageStorage.DeleteIfManaged(avatarUrl, _environment.WebRootPath);

            TempData["SuccessMessage"] = $"Đã xóa toàn bộ dữ liệu của \"{user.FullName}\".";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Xóa thất bại: {ex.InnerException?.Message ?? ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Khóa hoặc mở khóa tài khoản người dùng (toggle IsActive).
    /// Đây là hành động an toàn, có thể hoàn tác.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLock(int id)
    {
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy người dùng.";
            return RedirectToAction(nameof(Index));
        }

        user.IsActive = !user.IsActive;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = user.IsActive
            ? $"Đã mở khóa tài khoản \"{user.FullName}\"."
            : $"Đã khóa tài khoản \"{user.FullName}\". Người dùng sẽ không thể đăng nhập.";

        return RedirectToAction(nameof(Index));
    }

    private IQueryable<User> ApplyRoleFilter(IQueryable<User> query, string roleKey)
    {
        return roleKey switch
        {
            "admin" => query.Where(x => x.Role.Name == "Admin"),
            "teacher" => query.Where(x => x.Role.Name == "Teacher" || x.Role.Name == "Giáo viên"),
            "student" => query.Where(x => x.Role.Name == "Student" || x.Role.Name == "Học sinh"),
            _ => query
        };
    }

    private async Task<IReadOnlyList<SelectListItem>> BuildRoleOptionsAsync()
    {
        return (await GetAvailableRolesAsync())
            .Select(x => new SelectListItem(NormalizeRoleLabel(x.Name), x.Id.ToString()))
            .ToList();
    }

    private async Task<List<Role>> GetAvailableRolesAsync()
    {
        return await _context.Roles
            .AsNoTracking()
            .Where(x => x.Name != "Parent" && x.Name != "Phụ huynh")
            .OrderBy(x => x.Name == "Admin" ? 0 : x.Name == "Teacher" || x.Name == "Giáo viên" ? 1 : 2)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    private async Task UpsertStudentProfileAsync(int userId, bool isStudentRoleSelected, string? currentSchool, string? guardianName, string? guardianPhone, string? medicalNotes)
    {
        var profile = await _context.StudentProfiles.FirstOrDefaultAsync(x => x.UserId == userId);

        if (!isStudentRoleSelected)
        {
            if (profile != null)
            {
                _context.StudentProfiles.Remove(profile);
                await _context.SaveChangesAsync();
            }

            return;
        }

        if (profile == null)
        {
            profile = new StudentProfile
            {
                UserId = userId
            };
            _context.StudentProfiles.Add(profile);
        }

        profile.CurrentSchool = string.IsNullOrWhiteSpace(currentSchool) ? null : currentSchool.Trim();
        profile.GuardianName = guardianName!.Trim();
        profile.GuardianPhone = guardianPhone!.Trim();
        profile.MedicalNotes = string.IsNullOrWhiteSpace(medicalNotes) ? null : medicalNotes.Trim();

        await _context.SaveChangesAsync();
    }

    private static string NormalizeRoleLabel(string roleName)
    {
        return roleName switch
        {
            "Admin" => "Quản trị",
            "Teacher" => "Giáo viên",
            "Student" => "Học sinh",
            _ => roleName
        };
    }

    private static bool IsStudentRole(string roleName)
    {
        return roleName == "Student" || roleName == "Học sinh";
    }

    private static string GetRoleBadgeClass(string roleName)
    {
        return roleName switch
        {
            "Admin" => "bg-[#eaf3e4] text-[#5a7f48]",
            "Teacher" or "Giáo viên" => "bg-[#f0f2eb] text-[#677063]",
            "Student" or "Học sinh" => "bg-[#eef1f4] text-[#5f7383]",
            _ => "bg-[#f0f2eb] text-[#677063]"
        };
    }

    private static string GetAvatarText(string fullName)
    {
        return string.IsNullOrWhiteSpace(fullName) ? "U" : fullName.Trim()[0].ToString().ToUpperInvariant();
    }

    private static string GetAvatarClass(string roleName)
    {
        return roleName switch
        {
            "Admin" => "bg-[#dce9d4] text-[#547144]",
            "Teacher" or "Giáo viên" => "bg-[#bde5a8] text-[#5e8b4a]",
            "Student" or "Học sinh" => "bg-[#dbe6ef] text-[#567187]",
            _ => "bg-[#ecece7] text-[#7a7f75]"
        };
    }

    private static bool IsDuplicateUsername(DbUpdateException ex)
    {
        if (ex.InnerException is not SqlException sqlEx)
        {
            return false;
        }

        return sqlEx.Number is 2601 or 2627
            && sqlEx.Message.Contains("UQ__Users__536C85E4F7CBAA04", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<string>> SuggestAvailableUsernamesAsync(string requestedUsername, string? fullName)
    {
        var seed = string.IsNullOrWhiteSpace(requestedUsername)
            ? BuildUsernameSeed(fullName)
            : requestedUsername.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(seed))
        {
            seed = "user";
        }

        seed = new string(seed.Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrWhiteSpace(seed))
        {
            seed = "user";
        }

        var suggestions = new List<string>();
        var candidates = new List<string>
        {
            $"{seed}1",
            $"{seed}2026",
            $"{seed}01",
            $"{seed}.qp",
            $"{seed}_st"
        };

        for (var i = 2; i <= 20 && suggestions.Count < 5; i++)
        {
            candidates.Add($"{seed}{i}");
        }

        foreach (var candidate in candidates
                     .Select(x => x.ToLowerInvariant())
                     .Where(x => x.Length <= 50)
                     .Distinct())
        {
            var exists = await _context.Users.AsNoTracking()
                .AnyAsync(x => x.Username.ToLower() == candidate);

            if (!exists)
            {
                suggestions.Add(candidate);
            }

            if (suggestions.Count == 5)
            {
                break;
            }
        }

        return suggestions;
    }

    private static string BuildUsernameSeed(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return "user";
        }

        var normalized = fullName.Trim().ToLowerInvariant();
        normalized = normalized
            .Replace("à", "a").Replace("á", "a").Replace("ạ", "a").Replace("ả", "a").Replace("ã", "a")
            .Replace("â", "a").Replace("ầ", "a").Replace("ấ", "a").Replace("ậ", "a").Replace("ẩ", "a").Replace("ẫ", "a")
            .Replace("ă", "a").Replace("ằ", "a").Replace("ắ", "a").Replace("ặ", "a").Replace("ẳ", "a").Replace("ẵ", "a")
            .Replace("è", "e").Replace("é", "e").Replace("ẹ", "e").Replace("ẻ", "e").Replace("ẽ", "e")
            .Replace("ê", "e").Replace("ề", "e").Replace("ế", "e").Replace("ệ", "e").Replace("ể", "e").Replace("ễ", "e")
            .Replace("ì", "i").Replace("í", "i").Replace("ị", "i").Replace("ỉ", "i").Replace("ĩ", "i")
            .Replace("ò", "o").Replace("ó", "o").Replace("ọ", "o").Replace("ỏ", "o").Replace("õ", "o")
            .Replace("ô", "o").Replace("ồ", "o").Replace("ố", "o").Replace("ộ", "o").Replace("ổ", "o").Replace("ỗ", "o")
            .Replace("ơ", "o").Replace("ờ", "o").Replace("ớ", "o").Replace("ợ", "o").Replace("ở", "o").Replace("ỡ", "o")
            .Replace("ù", "u").Replace("ú", "u").Replace("ụ", "u").Replace("ủ", "u").Replace("ũ", "u")
            .Replace("ư", "u").Replace("ừ", "u").Replace("ứ", "u").Replace("ự", "u").Replace("ử", "u").Replace("ữ", "u")
            .Replace("ỳ", "y").Replace("ý", "y").Replace("ỵ", "y").Replace("ỷ", "y").Replace("ỹ", "y")
            .Replace("đ", "d");

        var parts = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => new string(x.Where(char.IsLetterOrDigit).ToArray()))
            .Where(x => x.Length > 0)
            .ToArray();

        if (parts.Length == 0)
        {
            return "user";
        }

        if (parts.Length == 1)
        {
            return parts[0];
        }

        return $"{parts[^1]}{string.Concat(parts.Take(parts.Length - 1).Select(x => x[0]))}";
    }
}
