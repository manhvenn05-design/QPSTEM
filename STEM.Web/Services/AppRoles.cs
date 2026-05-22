namespace STEM.Web.Services;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Teacher = "Teacher";
    public const string Student = "Student";

    public static string Canonicalize(string? rawRoleName)
    {
        return rawRoleName switch
        {
            Admin => Admin,
            Teacher or "Giáo viên" => Teacher,
            Student or "Học sinh" => Student,
            _ => rawRoleName?.Trim() ?? string.Empty
        };
    }

    public static bool IsAdmin(string? rawRoleName) =>
        string.Equals(Canonicalize(rawRoleName), Admin, StringComparison.Ordinal);

    public static bool IsTeacher(string? rawRoleName) =>
        string.Equals(Canonicalize(rawRoleName), Teacher, StringComparison.Ordinal);

    public static bool IsStudent(string? rawRoleName) =>
        string.Equals(Canonicalize(rawRoleName), Student, StringComparison.Ordinal);
}
