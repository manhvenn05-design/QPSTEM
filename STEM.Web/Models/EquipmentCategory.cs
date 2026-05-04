using System;
using System.Collections.Generic;

namespace STEM.Web.Models;

public partial class EquipmentCategory
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public int TotalQuantity { get; set; }

    public virtual ICollection<Equipment> Equipment { get; set; } = new List<Equipment>();
}
