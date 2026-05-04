using System;
using System.Collections.Generic;

namespace STEM.Web.Models;

public partial class EquipmentBorrow
{
    public int Id { get; set; }

    public int EquipmentId { get; set; }

    public int SessionId { get; set; }

    public int BorrowerId { get; set; }

    public DateTime BorrowTime { get; set; }

    public DateTime? ReturnTime { get; set; }

    public virtual User Borrower { get; set; } = null!;

    public virtual Equipment Equipment { get; set; } = null!;

    public virtual Session Session { get; set; } = null!;
}
