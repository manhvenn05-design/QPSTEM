using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace STEM.Web.Areas.Admin.Models;

public class UserManagementViewModel
{
    public string SelectedRole { get; set; } = "all";
    public string SearchTerm { get; set; } = string.Empty;
    public int TotalUsers { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public IReadOnlyList<UserRoleFilterViewModel> Filters { get; set; } = [];
    public IReadOnlyList<UserManagementItemViewModel> Users { get; set; } = [];
}

public class UserRoleFilterViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class UserManagementItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string RoleBadgeClass { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
    public string JoinedAt { get; set; } = string.Empty;
    public string AvatarText { get; set; } = string.Empty;
    public string AvatarClass { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}

public class CreateUserViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [StringLength(100)]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    [StringLength(50)]
    [Display(Name = "Tên đăng nhập")]
    public string Username { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(100)]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    [StringLength(20)]
    [Display(Name = "Số điện thoại")]
    public string? Phone { get; set; }

    [Display(Name = "Ảnh đại diện")]
    public IFormFile? AvatarFile { get; set; }

    public string? AvatarUrl { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
    [Display(Name = "Vai trò")]
    public int? RoleId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    [Display(Name = "Xác nhận mật khẩu")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;

    [StringLength(150)]
    [Display(Name = "Trường đang theo học")]
    public string? CurrentSchool { get; set; }

    [StringLength(100)]
    [Display(Name = "Người giám hộ")]
    public string? GuardianName { get; set; }

    [StringLength(20)]
    [Display(Name = "Số điện thoại giám hộ")]
    public string? GuardianPhone { get; set; }

    [Display(Name = "Ghi chú y tế")]
    public string? MedicalNotes { get; set; }

    public IReadOnlyList<SelectListItem> RoleOptions { get; set; } = [];
    public IReadOnlyList<string> SuggestedUsernames { get; set; } = [];
    public bool IsStudentRoleSelected { get; set; }
    public bool IsTeacherRoleSelected { get; set; }

    [Display(Name = "Bậc lương (1-5)")]
    [Range(1, 5, ErrorMessage = "Bậc lương phải từ 1 đến 5.")]
    public int? SalaryTier { get; set; }

    [Display(Name = "Lương khoán ca (VNĐ)")]
    public decimal? CustomSessionRate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!IsStudentRoleSelected)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(GuardianName))
        {
            yield return new ValidationResult("Vui lòng nhập người giám hộ.", [nameof(GuardianName)]);
        }

        if (string.IsNullOrWhiteSpace(GuardianPhone))
        {
            yield return new ValidationResult("Vui lòng nhập số điện thoại giám hộ.", [nameof(GuardianPhone)]);
        }
    }
}

public class EditUserViewModel : IValidatableObject
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [StringLength(100)]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    [StringLength(50)]
    [Display(Name = "Tên đăng nhập")]
    public string Username { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(100)]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    [StringLength(20)]
    [Display(Name = "Số điện thoại")]
    public string? Phone { get; set; }

    [Display(Name = "Ảnh đại diện")]
    public IFormFile? AvatarFile { get; set; }

    public string? AvatarUrl { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
    [Display(Name = "Vai trò")]
    public int? RoleId { get; set; }

    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu mới phải có ít nhất 8 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    [Display(Name = "Xác nhận mật khẩu mới")]
    public string? ConfirmNewPassword { get; set; }

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;

    [StringLength(150)]
    [Display(Name = "Trường đang theo học")]
    public string? CurrentSchool { get; set; }

    [StringLength(100)]
    [Display(Name = "Người giám hộ")]
    public string? GuardianName { get; set; }

    [StringLength(20)]
    [Display(Name = "Số điện thoại giám hộ")]
    public string? GuardianPhone { get; set; }

    [Display(Name = "Ghi chú y tế")]
    public string? MedicalNotes { get; set; }

    public IReadOnlyList<SelectListItem> RoleOptions { get; set; } = [];
    public IReadOnlyList<string> SuggestedUsernames { get; set; } = [];
    public bool IsStudentRoleSelected { get; set; }
    public bool IsTeacherRoleSelected { get; set; }

    [Display(Name = "Bậc lương (1-5)")]
    [Range(1, 5, ErrorMessage = "Bậc lương phải từ 1 đến 5.")]
    public int? SalaryTier { get; set; }

    [Display(Name = "Lương khoán ca (VNĐ)")]
    public decimal? CustomSessionRate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!IsStudentRoleSelected)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(GuardianName))
        {
            yield return new ValidationResult("Vui lòng nhập người giám hộ.", [nameof(GuardianName)]);
        }

        if (string.IsNullOrWhiteSpace(GuardianPhone))
        {
            yield return new ValidationResult("Vui lòng nhập số điện thoại giám hộ.", [nameof(GuardianPhone)]);
        }
    }
}

public class UserDetailsViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
    public string? CurrentSchool { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianPhone { get; set; }
    public string? MedicalNotes { get; set; }
    
    // Teacher Info
    public int? SalaryTier { get; set; }
    public decimal? CustomSessionRate { get; set; }
}
