using System;
using System.Collections.Generic;

namespace STEM.Web.Models;

public partial class Course
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public byte TargetAgeMin { get; set; }

    public byte TargetAgeMax { get; set; }

    public decimal Price { get; set; }

    public int TotalSessions { get; set; }

    public int MinStudents { get; set; }

    public string? ImageUrl { get; set; }

    public string? Summary { get; set; }

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();

    public virtual ICollection<Lead> Leads { get; set; } = new List<Lead>();
}
