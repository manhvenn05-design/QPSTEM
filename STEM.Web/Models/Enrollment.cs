using System;
using System.Collections.Generic;

namespace STEM.Web.Models;

public partial class Enrollment
{
    public int Id { get; set; }

    public int ClassId { get; set; }

    public int StudentId { get; set; }

    public DateTime EnrollDate { get; set; }

    public virtual Class Class { get; set; } = null!;

    public virtual User Student { get; set; } = null!;
}
