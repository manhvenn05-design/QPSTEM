using System;
using System.Collections.Generic;

namespace STEM.Web.Models.StudentViewModels;

public class StudentDashboardViewModel
{
    public StudentInfo Student { get; set; } = new();
    public List<StudentScheduleItemViewModel> UpcomingSessions { get; set; } = new();
    public List<StudentFeedbackViewModel> RecentFeedbacks { get; set; } = new();
    public List<StudentInvoiceViewModel> Invoices { get; set; } = new();

    // Stats
    public int SessionsThisWeekCount { get; set; }
    public int NewMediaCount { get; set; }
    public int NewFeedbackCount { get; set; }
    public int PendingInvoiceCount { get; set; }
}

public class StudentInfo
{
    public string FullName { get; set; } = string.Empty;
    public string Avatar { get; set; } = string.Empty;
    public string GuardianName { get; set; } = string.Empty;
    public string GuardianPhone { get; set; } = string.Empty;
    public string CurrentSchool { get; set; } = string.Empty;
    public string MedicalNotes { get; set; } = string.Empty;
}

public class StudentScheduleItemViewModel
{
    public int SessionId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public string SessionLabel { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string Status { get; set; } = "Upcoming"; // "Upcoming", "Today"
}

public class StudentFeedbackViewModel
{
    public int SessionId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string SessionLabel { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string AiEvaluation { get; set; } = string.Empty;
    public string TeacherNote { get; set; } = string.Empty;
    public List<MediaItem> MediaItems { get; set; } = new();
}

public class MediaItem
{
    public string Url { get; set; } = string.Empty;
    public string Type { get; set; } = "Image"; // "Image" or "Video"
}

public class StudentInvoiceViewModel
{
    public string InvoiceNo { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount => TotalAmount - PaidAmount;
    public DateTime? DueDate { get; set; }
    public byte Status { get; set; } // 0: Nợ, 1: Xong
    
    public int ProgressPercent => TotalAmount > 0 ? (int)((PaidAmount / TotalAmount) * 100) : 100;
}
