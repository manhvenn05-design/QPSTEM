using System;
using System.Collections.Generic;

namespace STEM.Web.Models;

public partial class Equipment
{
    public int Id { get; set; }

    public int CategoryId { get; set; }

    public string SerialNumber { get; set; } = null!;

    public byte Status { get; set; }

    public string? ImageUrl { get; set; }

    public virtual EquipmentCategory Category { get; set; } = null!;

    public virtual ICollection<EquipmentBorrow> EquipmentBorrows { get; set; } = new List<EquipmentBorrow>();

    public virtual ICollection<MaintenanceLog> MaintenanceLogs { get; set; } = new List<MaintenanceLog>();
}
