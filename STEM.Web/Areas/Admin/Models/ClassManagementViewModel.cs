using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace STEM.Web.Areas.Admin.Models;

public class ClassManagementViewModel
{
    public string SelectedFilter { get; set; } = "all";
    public string SearchTerm { get; set; } = string.Empty;
    public int TotalClasses { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public IReadOnlyList<ClassFilterViewModel> Filters { get; set; } = [];
    public IReadOnlyList<ClassManagementItemViewModel> Classes { get; set; } = [];
}

public class ClassFilterViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class ClassManagementItemViewModel
{
    public int Id { get; set; }
    public string ClassCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string ScheduleText { get; set; } = string.Empty;
    public string EnrollmentText { get; set; } = string.Empty;
    public int EnrollmentCount { get; set; }
    public int SessionCount { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
}

public class CreateClassViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Vui lòng chọn khóa học.")]
    [Display(Name = "Khóa học")]
    public int? CourseId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn giáo viên.")]
    [Display(Name = "Giáo viên")]
    public int? TeacherId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã lớp.")]
    [StringLength(50, ErrorMessage = "Mã lớp tối đa 50 ký tự.")]
    [Display(Name = "Mã lớp")]
    public string ClassCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu.")]
    [DataType(DataType.Date)]
    [Display(Name = "Ngày bắt đầu")]
    public DateOnly? StartDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Ngày kết thúc")]
    public DateOnly? EndDate { get; set; }

    public IReadOnlyList<SelectListItem> CourseOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> TeacherOptions { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate.HasValue && EndDate.HasValue && EndDate.Value < StartDate.Value)
        {
            yield return new ValidationResult("Ngày kết thúc không được nhỏ hơn ngày bắt đầu.", [nameof(StartDate), nameof(EndDate)]);
        }
    }
}

public class EditClassViewModel : CreateClassViewModel
{
    public int Id { get; set; }
}

public class ClassDetailsViewModel
{
    public int Id { get; set; }
    public string ClassCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string? TeacherEmail { get; set; }
    public string? TeacherPhone { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int EnrollmentCount { get; set; }
    public int SessionCount { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
    public string AgeRangeText { get; set; } = string.Empty;
    public string PriceText { get; set; } = string.Empty;
    public string TotalSessionsText { get; set; } = string.Empty;
    public IReadOnlyList<ClassStudentSummaryViewModel> Students { get; set; } = [];
    public IReadOnlyList<ClassSessionSummaryViewModel> Sessions { get; set; } = [];
}

public class ClassStudentSummaryViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string EnrollDateText { get; set; } = string.Empty;
}

public class ClassSessionSummaryViewModel
{
    public int SessionNo { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? Topic { get; set; }
}
