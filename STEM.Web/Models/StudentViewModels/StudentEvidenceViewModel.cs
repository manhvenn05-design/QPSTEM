namespace STEM.Web.Models.StudentViewModels;

public class StudentEvidencePageViewModel
{
    public List<StudentEvidenceItemViewModel> Items { get; set; } = new();

    // Pagination
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages { get; set; }
    public bool HasPrev => Page > 1;
    public bool HasNext => Page < TotalPages;

    // Filter
    public string? SelectedClass { get; set; }
    public List<string> AvailableClasses { get; set; } = new();

    // Deep-link highlight từ Schedule
    public int? HighlightSession { get; set; }
}

public class StudentEvidenceItemViewModel
{
    public int AttendanceId { get; set; }
    public int SessionId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public int SessionNo { get; set; }
    public DateOnly SessionDate { get; set; }
    public string? TeacherRawNote { get; set; }
    public string? AiEvaluation { get; set; }

    /// <summary>YouTube/video URLs tách riêng để embed iframe</summary>
    public List<string> VideoUrls { get; set; } = new();

    /// <summary>Image URLs (internal /uploads/ hoặc đuôi ảnh)</summary>
    public List<string> ImageUrls { get; set; } = new();

    /// <summary>External links khác (Google Drive, ...)</summary>
    public List<string> ExternalUrls { get; set; } = new();

    /// <summary>Được highlight khi deep-link từ Schedule</summary>
    public bool IsHighlighted { get; set; }

    public bool HasContent =>
        !string.IsNullOrWhiteSpace(TeacherRawNote) ||
        !string.IsNullOrWhiteSpace(AiEvaluation) ||
        VideoUrls.Any() || ImageUrls.Any() || ExternalUrls.Any();

    public int MediaCount => VideoUrls.Count + ImageUrls.Count + ExternalUrls.Count;
}
