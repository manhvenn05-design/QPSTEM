namespace STEM.Web.Models.StudentViewModels;

public class StudentSchedulePageViewModel
{
    public List<StudentScheduleSessionViewModel> Sessions { get; set; } = new();

    // List view
    public string Filter { get; set; } = "upcoming";
    public int TotalCount { get; set; }

    // Calendar view
    public string ViewMode { get; set; } = "list"; // "list" | "calendar"
    public int CalendarYear { get; set; } = DateTime.Today.Year;
    public int CalendarMonth { get; set; } = DateTime.Today.Month;
}

public class StudentScheduleSessionViewModel
{
    public int SessionId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public int SessionNo { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? Topic { get; set; }
    public bool IsToday { get; set; }
    public bool IsPast { get; set; }
    public bool HasAttendance { get; set; }
    public bool? WasPresent { get; set; }

    public string StatusLabel => IsToday ? "Hôm nay" : IsPast ? "Đã qua" : "Sắp tới";
    public string StatusBadgeClass => IsToday
        ? "bg-[#edf7e8] text-[#456c3f]"
        : IsPast
            ? "bg-[#eeeee9] text-[#42493d]"
            : "bg-[#fff4e8] text-[#9b682f]";

    public string AttendanceLabel => HasAttendance
        ? (WasPresent == true ? "Có mặt" : "Vắng")
        : string.Empty;

    public string AttendanceBadgeClass => HasAttendance
        ? (WasPresent == true ? "bg-[#edf7e8] text-[#456c3f]" : "bg-[#ffdad6] text-[#ba1a1a]")
        : string.Empty;
}
