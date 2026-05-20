using System;
using System.Collections.Generic;

namespace STEM.Web.Models;

public partial class Session
{
    public int Id { get; set; }

    public int ClassId { get; set; }

    public int SessionNo { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string? Topic { get; set; }

    public string? TeachingMaterialUrl { get; set; }

    public string? ClassMediaUrls { get; set; }

    public string? AssistantNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string PayrollStatus { get; set; } = string.Empty;

    public int? RoomId { get; set; }

    public int? SubstituteTeacherId { get; set; }

    public decimal SessionRateApplied { get; set; }

    public virtual ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();

    public virtual Class Class { get; set; } = null!;

    public virtual Room? Room { get; set; }

    public virtual User? SubstituteTeacher { get; set; }

    public virtual ICollection<EquipmentBorrow> EquipmentBorrows { get; set; } = new List<EquipmentBorrow>();
}
