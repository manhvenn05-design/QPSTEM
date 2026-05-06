using System.ComponentModel.DataAnnotations;

namespace STEM.Web.Areas.Teacher.Models;

public class TeacherAttendanceIndexViewModel
{
    public string SelectedFilter { get; set; } = "today";
    public string SearchTerm { get; set; } = string.Empty;
    public int TotalSessions { get; set; }
    public IReadOnlyList<TeacherAttendanceFilterViewModel> Filters { get; set; } = [];
    public IReadOnlyList<TeacherAttendanceSessionItemViewModel> Sessions { get; set; } = [];
}

public class TeacherAttendanceFilterViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class TeacherAttendanceSessionItemViewModel
{
    public int SessionId { get; set; }
    public string SessionLabel { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string ScheduleText { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public int AttendanceCount { get; set; }
    public string CompletionText { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
}

public class TeacherAttendanceBoardViewModel
{
    public int SessionId { get; set; }
    public string SessionLabel { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string ScheduleText { get; set; } = string.Empty;
    public string TeachingMaterialUrl { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public int AttendanceCount { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public bool HasStudents => Rows.Count > 0;
    public IReadOnlyList<TeacherAttendanceBoardRowViewModel> Rows { get; set; } = [];
}

public class TeacherAttendanceBoardRowViewModel
{
    public int? AttendanceId { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentUsername { get; set; } = string.Empty;
    public string? StudentAvatarUrl { get; set; }
    public bool IsPresent { get; set; } = true;

    [StringLength(1000)]
    public string? TeacherRawNote { get; set; }

    [StringLength(2000)]
    public string? ProductMediaUrls { get; set; }

    public string MediaHint { get; set; } = string.Empty;
}
