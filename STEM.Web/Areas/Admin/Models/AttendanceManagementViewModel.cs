using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace STEM.Web.Areas.Admin.Models;

public class AttendanceManagementViewModel
{
    public string SelectedFilter { get; set; } = "all";
    public string SearchTerm { get; set; } = string.Empty;
    public int TotalSessions { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public IReadOnlyList<AttendanceFilterViewModel> Filters { get; set; } = [];
    public IReadOnlyList<AttendanceSessionItemViewModel> Sessions { get; set; } = [];
}

public class AttendanceFilterViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class AttendanceSessionItemViewModel
{
    public int SessionId { get; set; }
    public string SessionLabel { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string ScheduleText { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public int AttendanceCount { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public string CompletionText { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
}

public class AttendanceBoardViewModel
{
    public int SessionId { get; set; }
    public string SessionLabel { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string ScheduleText { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public int AttendanceCount { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public int MissingCount { get; set; }
    public int CompletionPercent { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
    public bool HasStudents => Rows.Count > 0;
    public bool HasMissingRows => Rows.Any(x => x.AttendanceStatus == "pending");
    public IReadOnlyList<AttendanceBoardStudentRowViewModel> Rows { get; set; } = [];
}

public class AttendanceBoardStudentRowViewModel
{
    public int SessionId { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentUsername { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool HasAttendance { get; set; }
    public int? AttendanceId { get; set; }
    public bool IsPresent { get; set; }
    public string AttendanceStatus { get; set; } = "pending";
    public string PresenceLabel { get; set; } = string.Empty;
    public string PresenceBadgeClass { get; set; } = string.Empty;
    public string NotePreview { get; set; } = string.Empty;
    public string MediaSummary { get; set; } = string.Empty;
    public string? TeacherRawNote { get; set; }
    public string? ProductMediaUrls { get; set; }
}

public class AttendanceQuickUpdateViewModel
{
    public int SessionId { get; set; }
    public int StudentId { get; set; }
    public int? AttendanceId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn trạng thái.")]
    public string AttendanceStatus { get; set; } = "present";

    public string? TeacherRawNote { get; set; }
    public string? ProductMediaUrls { get; set; }
}

public class CreateAttendanceViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn buổi học.")]
    [Display(Name = "Buổi học")]
    public int? SessionId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn học viên.")]
    [Display(Name = "Học viên")]
    public int? StudentId { get; set; }

    [Display(Name = "Có mặt")]
    public bool IsPresent { get; set; } = true;

    [Display(Name = "Ghi chú giáo viên")]
    public string? TeacherRawNote { get; set; }

    [Display(Name = "Media sản phẩm")]
    public string? ProductMediaUrls { get; set; }

    [Display(Name = "AI đánh giá")]
    public string? AiEvaluation { get; set; }

    [Display(Name = "Video transcript")]
    public string? VideoTranscript { get; set; }

    public IReadOnlyList<AttendanceSkillScoreItemViewModel> SkillScores { get; set; } = [];

    public string SessionSummary { get; set; } = string.Empty;
    public string StudentSummary { get; set; } = string.Empty;
    public IReadOnlyList<SelectListItem> SessionOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> StudentOptions { get; set; } = [];
}

public class EditAttendanceViewModel : CreateAttendanceViewModel
{
    public int Id { get; set; }
}

public class AttendanceDetailsViewModel
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentUsername { get; set; } = string.Empty;
    public string? StudentAvatarUrl { get; set; }
    public string SessionLabel { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public DateOnly SessionDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsPresent { get; set; }
    public string PresenceLabel { get; set; } = string.Empty;
    public string PresenceBadgeClass { get; set; } = string.Empty;
    public string? ProductMediaUrls { get; set; }
    public string? TeacherRawNote { get; set; }
    public string? AiEvaluation { get; set; }
    public string? VideoTranscript { get; set; }
    public IReadOnlyList<AttendanceSkillScoreItemViewModel> SkillScores { get; set; } = [];
}

public class AttendanceSkillScoreItemViewModel
{
    public int Id { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public int Score { get; set; }
    public string? Feedback { get; set; }
}
