using System;
using System.Collections.Generic;

namespace STEM.Web.Models;

public partial class Banner
{
    public int Id { get; set; }

    public string? Title { get; set; }

    public string ImageUrl { get; set; } = null!;

    public string? LinkUrl { get; set; }

    public bool IsActive { get; set; }
}
