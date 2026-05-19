namespace STEM.Web.Models;

public partial class PayRateConfig
{
    public int Id { get; set; }

    public int TeacherTier { get; set; }

    public int CourseDifficulty { get; set; }

    public decimal RatePerSession { get; set; }
}
