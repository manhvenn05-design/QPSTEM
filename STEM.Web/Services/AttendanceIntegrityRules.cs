using System.Text.Json;

namespace STEM.Web.Services;

public static class AttendanceIntegrityRules
{
    private static readonly JsonSerializerOptions AiResultJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public const int TeacherEditWindowHours = 48;
    public const string PayrollStatusPending = "Pending";
    public const string PayrollStatusValid = "Valid";
    public const string PayrollStatusInvalid = "Invalid";
    public const string AiProcessStatusCompleted = "Completed";
    public const string AiProcessStatusFailed = "Failed";

    public static string? GetTeacherEditLockMessage(DateOnly sessionDate, TimeOnly sessionStartTime, TimeOnly sessionEndTime, DateTime now)
    {
        var sessionStart = sessionDate.ToDateTime(sessionStartTime);
        if (now < sessionStart)
        {
            return "Không thể điểm danh cho buổi học chưa diễn ra.";
        }

        var sessionEnd = sessionDate.ToDateTime(sessionEndTime);
        if (now > sessionEnd.AddHours(TeacherEditWindowHours))
        {
            return "Sau 48h không thể thao tác điểm danh nữa.";
        }

        return null;
    }

    public static bool IsValidProductMediaCollection(string? rawValue, string? cloudName)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return true;
        }

        return SplitMediaUrls(rawValue).All(url => IsValidProductMediaUrl(url, cloudName));
    }

    public static bool IsValidLocalMediaUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return false;

        var url = rawUrl.Trim();
        // Local upload path: /uploads/videos/...
        return url.StartsWith("/uploads/videos/", StringComparison.OrdinalIgnoreCase)
            && (url.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
             || url.EndsWith(".webm", StringComparison.OrdinalIgnoreCase)
             || url.EndsWith(".mov", StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<string> SplitMediaUrls(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return [];
        }

        return rawValue
            .Split([',', '\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool IsValidProductMediaUrl(string? rawUrl, string? cloudName)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return false;

        var url = rawUrl.Trim();

        // ── Ưu tiên: local storage (/uploads/videos/...) ────────────────────
        if (IsValidLocalMediaUrl(url))
            return true;

        // ── Legacy: Cloudinary URL ───────────────────────────────────────────
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(uri.Host, "res.cloudinary.com", StringComparison.OrdinalIgnoreCase))
            return false;

        var path = uri.AbsolutePath;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        if (!string.IsNullOrWhiteSpace(cloudName) &&
            !path.Contains($"/{cloudName.Trim('/')}/", StringComparison.OrdinalIgnoreCase))
            return false;

        return path.Contains("/qpstem/videos/", StringComparison.OrdinalIgnoreCase);
    }

    public static string ComputePayrollStatus(
        DateOnly sessionDate,
        int studentCount,
        int attendanceCount,
        int presentCount,
        int notedPresentAttendanceCount,
        int mediaReadyAttendanceCount,
        DateOnly today)
    {
        if (sessionDate > today)
        {
            return PayrollStatusPending;
        }

        if (studentCount <= 0)
        {
            return PayrollStatusInvalid;
        }

        if (attendanceCount < studentCount)
        {
            return PayrollStatusPending;
        }

        if (presentCount <= 0)
        {
            return PayrollStatusInvalid;
        }

        if (notedPresentAttendanceCount < presentCount)
        {
            return PayrollStatusInvalid;
        }

        if (mediaReadyAttendanceCount < presentCount)
        {
            return PayrollStatusInvalid;
        }

        return PayrollStatusValid;
    }

    public static string SerializeAiResult(object? result)
    {
        return JsonSerializer.Serialize(result, AiResultJsonOptions);
    }
}
