using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace STEM.Web.Areas.Admin.Models;

public class SessionManagementViewModel
{
    public string SelectedFilter { get; set; } = "all";
    public string SearchTerm { get; set; } = string.Empty;
    public int TotalSessions { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public IReadOnlyList<SessionFilterViewModel> Filters { get; set; } = [];
    public IReadOnlyList<SessionManagementItemViewModel> Sessions { get; set; } = [];
}

public class SessionFilterViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class SessionManagementItemViewModel
{
    public int Id { get; set; }
    public string SessionLabel { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
    public string TimeRangeText { get; set; } = string.Empty;
    public string TopicText { get; set; } = string.Empty;
    public string MaterialText { get; set; } = string.Empty;
    public string MediaText { get; set; } = string.Empty;
    public int AttendanceCount { get; set; }
    public int EquipmentBorrowCount { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
}

public class CreateSessionViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Vui lòng chọn lớp học.")]
    [Display(Name = "Lớp học")]
    public int? ClassId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số buổi.")]
    [Range(1, 1000, ErrorMessage = "Số buổi phải lớn hơn 0.")]
    [Display(Name = "Số buổi")]
    public int SessionNo { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày học.")]
    [DataType(DataType.Date)]
    [Display(Name = "Ngày học")]
    public DateOnly? Date { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn giờ bắt đầu.")]
    [Display(Name = "Giờ bắt đầu")]
    public TimeOnly? StartTime { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn giờ kết thúc.")]
    [Display(Name = "Giờ kết thúc")]
    public TimeOnly? EndTime { get; set; }

    [StringLength(200, ErrorMessage = "Chủ đề tối đa 200 ký tự.")]
    [Display(Name = "Chủ đề buổi học")]
    public string? Topic { get; set; }

    [Display(Name = "Link giáo án")]
    public string? TeachingMaterialUrl { get; set; }

    [Display(Name = "Media cả lớp")]
    public string? ClassMediaUrls { get; set; }

    [Display(Name = "Ghi chú trợ giảng")]
    public string? AssistantNote { get; set; }

    public IReadOnlyList<SelectListItem> ClassOptions { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartTime.HasValue && EndTime.HasValue && EndTime.Value <= StartTime.Value)
        {
            yield return new ValidationResult(
                "Giờ kết thúc phải lớn hơn giờ bắt đầu.",
                [nameof(StartTime), nameof(EndTime)]);
        }
    }
}

public class EditSessionViewModel : CreateSessionViewModel
{
    public int Id { get; set; }
}

public class SessionDetailsViewModel
{
    public int Id { get; set; }
    public string SessionLabel { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string? TeacherEmail { get; set; }
    public DateOnly ClassStartDate { get; set; }
    public DateOnly? ClassEndDate { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? Topic { get; set; }
    public string? TeachingMaterialUrl { get; set; }
    public string? ClassMediaUrls { get; set; }
    public string? AssistantNote { get; set; }
    public int AttendanceCount { get; set; }
    public int EquipmentBorrowCount { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
    public IReadOnlyList<SessionAttendanceSummaryViewModel> Attendances { get; set; } = [];
    public IReadOnlyList<SessionEquipmentBorrowSummaryViewModel> EquipmentBorrows { get; set; } = [];
}

public class SessionAttendanceSummaryViewModel
{
    public string StudentName { get; set; } = string.Empty;
    public bool IsPresent { get; set; }
    public string AiProcessStatus { get; set; } = string.Empty;
}

public class SessionEquipmentBorrowSummaryViewModel
{
    public string SerialNumber { get; set; } = string.Empty;
    public string BorrowerName { get; set; } = string.Empty;
    public string BorrowTimeText { get; set; } = string.Empty;
    public string ReturnTimeText { get; set; } = string.Empty;
}
