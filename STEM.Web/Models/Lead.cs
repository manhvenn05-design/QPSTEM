using System;
using System.Collections.Generic;

namespace STEM.Web.Models;

public partial class Lead
{
    public int Id { get; set; }

    public string ParentName { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string? Email { get; set; }

    public int? InterestedId { get; set; }

    public byte Status { get; set; }

    public virtual Course? Interested { get; set; }
}
