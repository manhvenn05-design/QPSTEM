namespace STEM.Web.Models.Layout;

public sealed class TeacherShellState
{
    public int? UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
}
