namespace STEM.Web.Models;

public partial class TeacherProfile
{
    public int UserId { get; set; }

    public int SalaryTier { get; set; }

    public decimal? BaseSalary { get; set; }

    public decimal? CustomSessionRate { get; set; }

    public virtual User User { get; set; } = null!;
}
