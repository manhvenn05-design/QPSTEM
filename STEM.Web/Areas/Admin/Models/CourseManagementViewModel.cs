using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace STEM.Web.Areas.Admin.Models;

public class CourseManagementViewModel
{
    public string SelectedFilter { get; set; } = "all";
    public string SearchTerm { get; set; } = string.Empty;
    public int TotalCourses { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public IReadOnlyList<CourseFilterViewModel> Filters { get; set; } = [];
    public IReadOnlyList<CourseManagementItemViewModel> Courses { get; set; } = [];
}

public class CourseFilterViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class CourseManagementItemViewModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AgeRange { get; set; } = string.Empty;
    public string PriceText { get; set; } = string.Empty;
    public string TotalSessionsText { get; set; } = string.Empty;
    public int ClassCount { get; set; }
    public int EnrollmentCount { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}

public class CreateCourseViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Vui lòng nhập mã khóa học.")]
    [StringLength(20, ErrorMessage = "Mã khóa học tối đa 20 ký tự.")]
    [Display(Name = "Mã khóa học")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên khóa học.")]
    [StringLength(200, ErrorMessage = "Tên khóa học tối đa 200 ký tự.")]
    [Display(Name = "Tên khóa học")]
    public string Name { get; set; } = string.Empty;

    [Range(1, 100, ErrorMessage = "Độ tuổi tối thiểu phải từ 1 đến 100.")]
    [Display(Name = "Độ tuổi tối thiểu")]
    public byte TargetAgeMin { get; set; }

    [Range(1, 100, ErrorMessage = "Độ tuổi tối đa phải từ 1 đến 100.")]
    [Display(Name = "Độ tuổi tối đa")]
    public byte TargetAgeMax { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999", ErrorMessage = "Học phí phải lớn hơn hoặc bằng 0.")]
    [Display(Name = "Học phí")]
    public decimal Price { get; set; }

    [Range(1, 1000, ErrorMessage = "Tổng số buổi phải lớn hơn 0.")]
    [Display(Name = "Tổng số buổi")]
    public int TotalSessions { get; set; }

    public string? ImageUrl { get; set; }

    [Display(Name = "Ảnh đại diện")]
    public IFormFile? ImageFile { get; set; }

    [StringLength(1000, ErrorMessage = "Mô tả ngắn tối đa 1000 ký tự.")]
    [Display(Name = "Mô tả ngắn")]
    public string? Summary { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TargetAgeMin > TargetAgeMax)
        {
            yield return new ValidationResult("Độ tuổi tối thiểu không được lớn hơn độ tuổi tối đa.", [nameof(TargetAgeMin), nameof(TargetAgeMax)]);
        }
    }
}

public class EditCourseViewModel : CreateCourseViewModel
{
    public int Id { get; set; }
    public string? CurrentImageUrl { get; set; }
}

public class CourseDetailsViewModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public byte TargetAgeMin { get; set; }
    public byte TargetAgeMax { get; set; }
    public decimal Price { get; set; }
    public int TotalSessions { get; set; }
    public string? ImageUrl { get; set; }
    public string? Summary { get; set; }
    public int TotalClasses { get; set; }
    public int TotalEnrollments { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
    public IReadOnlyList<CourseClassSummaryViewModel> Classes { get; set; } = [];
}

public class CourseClassSummaryViewModel
{
    public string ClassCode { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int EnrollmentCount { get; set; }
}
