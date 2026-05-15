using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace STEM.Web.Models;

public class AttendanceSkillScore
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int AttendanceId { get; set; }

    [Required]
    [StringLength(100)]
    public string SkillName { get; set; } = null!;

    [Range(1, 10)]
    public int Score { get; set; }

    [StringLength(1000)]
    public string? Feedback { get; set; }

    [ForeignKey(nameof(AttendanceId))]
    public virtual Attendance Attendance { get; set; } = null!;
}
