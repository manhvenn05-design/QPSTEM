namespace STEM.Web.Models.StudentViewModels;

public class StudentProfileViewModel
{
    // Thông tin tài khoản
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }

    // Thông tin hồ sơ học sinh
    public string? GuardianName { get; set; }
    public string? GuardianPhone { get; set; }
    public string? CurrentSchool { get; set; }
    public string? MedicalNotes { get; set; }

    // Lớp đang/đã học
    public List<StudentEnrolledClassViewModel> EnrolledClasses { get; set; } = new();
}

public class StudentEnrolledClassViewModel
{
    public string ClassCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int TotalCourseSessions { get; set; }  // Course.TotalSessions
    public int AttendedSessions { get; set; }     // Count Attendance.IsPresent=true
    public string TeacherName { get; set; } = string.Empty;

    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;

    public int AttendancePercent => TotalCourseSessions > 0
        ? (int)Math.Min(100, Math.Round((double)AttendedSessions / TotalCourseSessions * 100))
        : 0;
}

/// <summary>Model dùng cho form đổi mật khẩu (modal)</summary>
public class ChangePasswordViewModel
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
