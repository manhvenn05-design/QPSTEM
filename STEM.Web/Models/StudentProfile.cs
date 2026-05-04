using System;
using System.Collections.Generic;

namespace STEM.Web.Models;

public partial class StudentProfile
{
    public int UserId { get; set; }

    public string? CurrentSchool { get; set; }

    public string GuardianName { get; set; } = null!;

    public string GuardianPhone { get; set; } = null!;

    public string? MedicalNotes { get; set; }

    public virtual User User { get; set; } = null!;
}
