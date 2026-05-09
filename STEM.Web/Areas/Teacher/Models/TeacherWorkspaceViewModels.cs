using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace STEM.Web.Areas.Teacher.Models;

public class TeacherDashboardViewModel
{
    public string TodayLabel { get; set; } = string.Empty;
    public int ActiveClassCount { get; set; }
    public int TotalStudentCount { get; set; }
    public int TodaySessionCount { get; set; }
    public int PendingAttendanceCount { get; set; }
    public int UpcomingSessionCount { get; set; }
    public int ActiveBorrowCount { get; set; }
    public int EvidenceReadyCount { get; set; }
    public IReadOnlyList<TeacherDashboardSessionItemViewModel> TodaySessions { get; set; } = [];
    public IReadOnlyList<TeacherDashboardSessionItemViewModel> UpcomingSessions { get; set; } = [];
    public IReadOnlyList<TeacherDashboardEvidenceItemViewModel> EvidenceQueue { get; set; } = [];
    public IReadOnlyList<TeacherDashboardBorrowItemViewModel> ActiveBorrows { get; set; } = [];
}

public class TeacherDashboardSessionItemViewModel
{
    public int SessionId { get; set; }
    public string SessionLabel { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string ScheduleText { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public int AttendanceCount { get; set; }
    public int AttendancePercent { get; set; }
}

public class TeacherDashboardEvidenceItemViewModel
{
    public int AttendanceId { get; set; }
    public int SessionId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public string SessionLabel { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
}

public class TeacherDashboardBorrowItemViewModel
{
    public int BorrowId { get; set; }
    public int EquipmentId { get; set; }
    public string EquipmentSerialNumber { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string SessionLabel { get; set; } = string.Empty;
    public string BorrowTimeText { get; set; } = string.Empty;
}

public class TeacherScheduleIndexViewModel
{
    public string SelectedFilter { get; set; } = "today";
    public string SearchTerm { get; set; } = string.Empty;
    public int TotalSessions { get; set; }
    public DateTime TargetDate { get; set; }
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
    public IReadOnlyList<TeacherScheduleFilterViewModel> Filters { get; set; } = [];
    public IReadOnlyList<TeacherScheduleItemViewModel> Sessions { get; set; } = [];
    public IReadOnlyList<TeacherScheduleItemViewModel> CalendarSessions { get; set; } = [];
}

public class TeacherScheduleFilterViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class TeacherScheduleItemViewModel
{
    public int SessionId { get; set; }
    public string SessionLabel { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string ScheduleText { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public int AttendanceCount { get; set; }
    public int AttendancePercent { get; set; }
    public bool HasTeachingMaterial { get; set; }
    public string TeachingMaterialUrl { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
}

public class TeacherScheduleDetailsViewModel
{
    public int SessionId { get; set; }
    public string SessionLabel { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
    public string TimeRangeText { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string TeachingMaterialUrl { get; set; } = string.Empty;
    public string? AssistantNote { get; set; }
    public int StudentCount { get; set; }
    public int AttendanceCount { get; set; }
    public int EquipmentBorrowCount { get; set; }
    public IReadOnlyList<TeacherScheduleStudentItemViewModel> Students { get; set; } = [];
    public IReadOnlyList<TeacherScheduleEquipmentItemViewModel> Equipments { get; set; } = [];
}

public class TeacherScheduleStudentItemViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
}

public class TeacherScheduleEquipmentItemViewModel
{
    public string SerialNumber { get; set; } = string.Empty;
    public string BorrowerName { get; set; } = string.Empty;
    public string BorrowTimeText { get; set; } = string.Empty;
    public string ReturnTimeText { get; set; } = string.Empty;
}

public class TeacherEvidenceIndexViewModel
{
    public string SelectedFilter { get; set; } = "needs-media";
    public string SearchTerm { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int MissingMediaCount { get; set; }
    public int MissingNoteCount { get; set; }
    public int ReadyCount { get; set; }
    public IReadOnlyList<TeacherEvidenceFilterViewModel> Filters { get; set; } = [];
    public IReadOnlyList<TeacherEvidenceItemViewModel> Items { get; set; } = [];
}

public class TeacherEvidenceFilterViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class TeacherEvidenceItemViewModel
{
    public int AttendanceId { get; set; }
    public int SessionId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentUsername { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string SessionLabel { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string NotePreview { get; set; } = string.Empty;
    public int MediaCount { get; set; }
    public string MediaStatusLabel { get; set; } = string.Empty;
    public string MediaStatusBadgeClass { get; set; } = string.Empty;
}

public class TeacherEvidenceDetailsViewModel
{
    public int AttendanceId { get; set; }
    public int SessionId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentUsername { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string SessionLabel { get; set; } = string.Empty;
    public string ScheduleText { get; set; } = string.Empty;
    public string? TeacherRawNote { get; set; }
    public IReadOnlyList<string> MediaUrls { get; set; } = [];
}

public class TeacherAICopilotViewModel
{
    public string SelectedFilter { get; set; } = "ready";
    public int? SelectedAttendanceId { get; set; }
    public int ReadyCount { get; set; }
    public int CompletedCount { get; set; }
    public int MissingNoteCount { get; set; }
    public IReadOnlyList<TeacherAIFilterViewModel> Filters { get; set; } = [];
    public IReadOnlyList<TeacherAICandidateItemViewModel> Candidates { get; set; } = [];
    public TeacherAIPreviewViewModel? Preview { get; set; }
}

public class TeacherAIFilterViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class TeacherAICandidateItemViewModel
{
    public int AttendanceId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public string SessionLabel { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
}

public class TeacherAIPreviewViewModel
{
    public int AttendanceId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public string SessionLabel { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string RawNote { get; set; } = string.Empty;
    public string ExistingEvaluation { get; set; } = string.Empty;
}

public class TeacherEquipmentIndexViewModel
{
    public int AvailableCount { get; set; }
    public int ActiveBorrowCount { get; set; }
    public int ReturnedCount { get; set; }
    public IReadOnlyList<TeacherEquipmentAvailableItemViewModel> AvailableEquipments { get; set; } = [];
    public IReadOnlyList<TeacherEquipmentBorrowItemViewModel> ActiveBorrows { get; set; } = [];
    public IReadOnlyList<TeacherEquipmentBorrowItemViewModel> History { get; set; } = [];
}

public class TeacherEquipmentAvailableItemViewModel
{
    public int Id { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
}

public class TeacherEquipmentBorrowItemViewModel
{
    public int BorrowId { get; set; }
    public int EquipmentId { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string SessionLabel { get; set; } = string.Empty;
    public string BorrowTimeText { get; set; } = string.Empty;
    public string ReturnTimeText { get; set; } = string.Empty;
    public bool IsReturned { get; set; }
}

public class TeacherBorrowEquipmentViewModel
{
    public int? EquipmentId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn thiết bị.")]
    [Display(Name = "Thiết bị")]
    public int? SelectedEquipmentId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn buổi học.")]
    [Display(Name = "Buổi học")]
    public int? SessionId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn thời gian mượn.")]
    [Display(Name = "Thời gian mượn")]
    public DateTime? BorrowTime { get; set; }

    public IReadOnlyList<SelectListItem> EquipmentOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> SessionOptions { get; set; } = [];
}

