using System;
using System.Collections.Generic;

namespace STEM.Web.Models;

public partial class Attendance
{
    public int Id { get; set; }

    public int SessionId { get; set; }

    public int StudentId { get; set; }

    public bool IsPresent { get; set; }

    public bool IsExcused { get; set; }

    public string? ProductMediaUrls { get; set; }

    public string? TeacherRawNote { get; set; }

    public string? AiEvaluation { get; set; }

    public string? VideoTranscript { get; set; }

    public string? AiProcessStatus { get; set; }

    public string? SoftSkillJson { get; set; }

    public virtual Session Session { get; set; } = null!;

    public virtual User Student { get; set; } = null!;
}
