using System;
using System.Collections.Generic;

namespace STEM.Web.Models;

public partial class Invoice
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public int? ClassId { get; set; }

    public string InvoiceNo { get; set; } = null!;

    public decimal FinalAmount { get; set; }

    public byte Status { get; set; }

    public virtual Class? Class { get; set; }

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual User Student { get; set; } = null!;
}
