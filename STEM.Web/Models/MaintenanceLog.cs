using System;
using System.Collections.Generic;

namespace STEM.Web.Models;

public partial class MaintenanceLog
{
    public int Id { get; set; }

    public int EquipmentId { get; set; }

    public int ReportedBy { get; set; }

    public string Issue { get; set; } = null!;

    public byte Status { get; set; }

    public virtual Equipment Equipment { get; set; } = null!;

    public virtual User ReportedByNavigation { get; set; } = null!;
}
