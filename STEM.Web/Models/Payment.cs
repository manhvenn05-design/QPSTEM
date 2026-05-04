using System;
using System.Collections.Generic;

namespace STEM.Web.Models;

public partial class Payment
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public DateTime TransDate { get; set; }

    public virtual Invoice Invoice { get; set; } = null!;
}
