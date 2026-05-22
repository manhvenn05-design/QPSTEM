using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Areas.Admin.Models;
using STEM.Web.Data;
using STEM.Web.Models;
using STEM.Web.Services;

namespace STEM.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class FinanceController : Controller
{
    private const byte InvoiceStatusUnpaid = 1;
    private const byte InvoiceStatusPartial = 2;
    private const byte InvoiceStatusPaid = 3;
    private const byte InvoiceStatusVoided = 4;

    private readonly ApplicationDbContext _context;

    public FinanceController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string filter = "all", string? q = null)
    {
        var filters = new[]
        {
            new FinanceFilterViewModel { Key = "all", Label = "Táº¥t cáº£" },
            new FinanceFilterViewModel { Key = "unpaid", Label = "ChÆ°a thu" },
            new FinanceFilterViewModel { Key = "partial", Label = "Thu má»™t pháº§n" },
            new FinanceFilterViewModel { Key = "paid", Label = "ÄÃ£ thu Ä‘á»§" },
            new FinanceFilterViewModel { Key = "voided", Label = "ÄÃ£ há»§y" }
        };

        var normalizedFilter = NormalizeFilter(filter, filters.Select(x => x.Key));
        var searchTerm = q?.Trim() ?? string.Empty;

        var invoicesQuery = _context.Invoices
            .AsNoTracking()
            .Select(x => new InvoiceListRow
            {
                Id = x.Id,
                InvoiceNo = x.InvoiceNo,
                StudentName = x.Student.FullName,
                StudentUsername = x.Student.Username,
                ClassCode = x.Class != null ? x.Class.ClassCode : null,
                Status = x.Status,
                FinalAmount = x.FinalAmount,
                PaidAmount = x.Payments.Sum(p => (decimal?)p.Amount) ?? 0m
            });

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            invoicesQuery = invoicesQuery.Where(x =>
                x.InvoiceNo.Contains(searchTerm) ||
                x.StudentName.Contains(searchTerm) ||
                x.StudentUsername.Contains(searchTerm) ||
                (x.ClassCode != null && x.ClassCode.Contains(searchTerm)));
        }

        invoicesQuery = normalizedFilter switch
        {
            "unpaid" => invoicesQuery.Where(x => x.Status != InvoiceStatusVoided && x.PaidAmount == 0),
            "partial" => invoicesQuery.Where(x => x.Status != InvoiceStatusVoided && x.PaidAmount > 0 && x.PaidAmount < x.FinalAmount),
            "paid" => invoicesQuery.Where(x => x.Status != InvoiceStatusVoided && x.PaidAmount >= x.FinalAmount),
            "voided" => invoicesQuery.Where(x => x.Status == InvoiceStatusVoided),
            _ => invoicesQuery
        };

        var invoiceRows = await invoicesQuery
            .OrderByDescending(x => x.Id)
            .Take(30)
            .ToListAsync();

        var paymentRows = await _context.Payments
            .AsNoTracking()
            .Where(x =>
                string.IsNullOrWhiteSpace(searchTerm) ||
                x.Invoice.InvoiceNo.Contains(searchTerm) ||
                x.Invoice.Student.FullName.Contains(searchTerm) ||
                x.PaymentMethod.Contains(searchTerm))
            .OrderByDescending(x => x.TransDate)
            .Take(30)
            .Select(x => new PaymentManagementItemViewModel
            {
                Id = x.Id,
                InvoiceId = x.InvoiceId,
                InvoiceNo = x.Invoice.InvoiceNo,
                StudentName = x.Invoice.Student.FullName,
                Amount = x.Amount,
                AmountText = FormatMoney(x.Amount),
                PaymentMethod = GetPaymentMethodLabel(x.PaymentMethod),
                TransDate = x.TransDate,
                TransDateText = x.TransDate.ToString("dd/MM/yyyy HH:mm")
            })
            .ToListAsync();

        var totalRevenue = await _context.Payments.SumAsync(x => (decimal?)x.Amount) ?? 0m;
        var totalOutstanding = await _context.Invoices
            .Where(x => x.Status != InvoiceStatusVoided)
            .Select(x => x.FinalAmount - (x.Payments.Sum(p => (decimal?)p.Amount) ?? 0m))
            .SumAsync();

        var model = new FinanceManagementViewModel
        {
            SelectedFilter = normalizedFilter,
            SearchTerm = searchTerm,
            TotalRevenue = totalRevenue,
            TotalOutstanding = totalOutstanding,
            OutstandingInvoiceCount = await _context.Invoices.CountAsync(x =>
                x.Status != InvoiceStatusVoided &&
                x.FinalAmount > (x.Payments.Sum(p => (decimal?)p.Amount) ?? 0m)),
            PartialInvoiceCount = await _context.Invoices.CountAsync(x =>
                x.Status != InvoiceStatusVoided &&
                (x.Payments.Sum(p => (decimal?)p.Amount) ?? 0m) > 0m &&
                (x.Payments.Sum(p => (decimal?)p.Amount) ?? 0m) < x.FinalAmount),
            UnpaidInvoiceCount = await _context.Invoices.CountAsync(x =>
                x.Status != InvoiceStatusVoided &&
                !x.Payments.Any()),
            PaymentCount = await _context.Payments.CountAsync(),
            Filters = filters,
            Invoices = invoiceRows.Select(MapInvoiceItem).ToList(),
            Payments = paymentRows
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> InvoiceDetails(int id)
    {
        var model = await _context.Invoices
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new InvoiceDetailsViewModel
            {
                Id = x.Id,
                InvoiceNo = x.InvoiceNo,
                StudentName = x.Student.FullName,
                StudentUsername = x.Student.Username,
                StudentEmail = x.Student.Email,
                ClassCode = x.Class != null ? x.Class.ClassCode : "ChÆ°a gáº¯n lá»›p",
                FinalAmount = x.FinalAmount,
                Status = x.Status,
                PaidAmount = x.Payments.Sum(p => (decimal?)p.Amount) ?? 0m,
                Payments = x.Payments
                    .OrderByDescending(p => p.TransDate)
                    .Select(p => new PaymentManagementItemViewModel
                    {
                        Id = p.Id,
                        InvoiceId = p.InvoiceId,
                        InvoiceNo = x.InvoiceNo,
                        StudentName = x.Student.FullName,
                        Amount = p.Amount,
                        AmountText = FormatMoney(p.Amount),
                        PaymentMethod = GetPaymentMethodLabel(p.PaymentMethod),
                        TransDate = p.TransDate,
                        TransDateText = p.TransDate.ToString("dd/MM/yyyy HH:mm")
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (model == null)
        {
            return NotFound();
        }

        ApplyInvoiceStatus(model, model.PaidAmount);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> CreateInvoice()
    {
        var model = new CreateInvoiceViewModel();
        await PopulateInvoiceOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateInvoice(CreateInvoiceViewModel model)
    {
        await PopulateInvoiceOptionsAsync(model);
        var normalizedInvoiceNo = model.InvoiceNo.Trim().ToUpperInvariant();

        if (await _context.Invoices.AnyAsync(x => x.InvoiceNo.ToLower() == normalizedInvoiceNo.ToLower()))
        {
            ModelState.AddModelError(nameof(model.InvoiceNo), "Sá»‘ hÃ³a Ä‘Æ¡n Ä‘Ã£ tá»“n táº¡i.");
        }

        if (!await IsValidStudentAsync(model.StudentId))
        {
            ModelState.AddModelError(nameof(model.StudentId), "Há»c viÃªn khÃ´ng há»£p lá»‡.");
        }

        if (model.ClassId.HasValue && !await IsValidClassAsync(model.ClassId))
        {
            ModelState.AddModelError(nameof(model.ClassId), "Lá»›p há»c khÃ´ng há»£p lá»‡.");
        }

        if (model.StudentId.HasValue && model.ClassId.HasValue && !await IsStudentInClassAsync(model.StudentId.Value, model.ClassId.Value))
        {
            ModelState.AddModelError(nameof(model.ClassId), "Há»c viÃªn khÃ´ng thuá»™c lá»›p Ä‘Ã£ chá»n.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var entity = new Invoice
        {
            StudentId = model.StudentId!.Value,
            ClassId = model.ClassId,
            InvoiceNo = normalizedInvoiceNo,
            FinalAmount = model.FinalAmount,
            Status = InvoiceStatusUnpaid
        };

        try
        {
            _context.Invoices.Add(entity);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateInvoiceNo(ex))
        {
            ModelState.AddModelError(nameof(model.InvoiceNo), "Sá»‘ hÃ³a Ä‘Æ¡n Ä‘Ã£ tá»“n táº¡i.");
            return View(model);
        }

        TempData["SuccessMessage"] = "ÄÃ£ táº¡o hÃ³a Ä‘Æ¡n má»›i.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> EditInvoice(int id)
    {
        var entity = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
        {
            return NotFound();
        }

        var model = new EditInvoiceViewModel
        {
            Id = entity.Id,
            StudentId = entity.StudentId,
            ClassId = entity.ClassId,
            InvoiceNo = entity.InvoiceNo,
            FinalAmount = entity.FinalAmount
        };

        await PopulateInvoiceOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditInvoice(EditInvoiceViewModel model)
    {
        await PopulateInvoiceOptionsAsync(model);
        var entity = await _context.Invoices
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == model.Id);

        if (entity == null)
        {
            return NotFound();
        }

        var normalizedInvoiceNo = model.InvoiceNo.Trim().ToUpperInvariant();
        if (await _context.Invoices.AnyAsync(x => x.Id != model.Id && x.InvoiceNo.ToLower() == normalizedInvoiceNo.ToLower()))
        {
            ModelState.AddModelError(nameof(model.InvoiceNo), "Sá»‘ hÃ³a Ä‘Æ¡n Ä‘Ã£ tá»“n táº¡i.");
        }

        if (!await IsValidStudentAsync(model.StudentId))
        {
            ModelState.AddModelError(nameof(model.StudentId), "Há»c viÃªn khÃ´ng há»£p lá»‡.");
        }

        if (model.ClassId.HasValue && !await IsValidClassAsync(model.ClassId))
        {
            ModelState.AddModelError(nameof(model.ClassId), "Lá»›p há»c khÃ´ng há»£p lá»‡.");
        }

        if (model.StudentId.HasValue && model.ClassId.HasValue && !await IsStudentInClassAsync(model.StudentId.Value, model.ClassId.Value))
        {
            ModelState.AddModelError(nameof(model.ClassId), "Há»c viÃªn khÃ´ng thuá»™c lá»›p Ä‘Ã£ chá»n.");
        }

        var paidAmount = entity.Payments.Sum(x => x.Amount);
        if (model.FinalAmount < paidAmount)
        {
            ModelState.AddModelError(nameof(model.FinalAmount), "Tá»•ng tiá»n khÃ´ng Ä‘Æ°á»£c nhá» hÆ¡n sá»‘ tiá»n Ä‘Ã£ thu.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        entity.StudentId = model.StudentId!.Value;
        entity.ClassId = model.ClassId;
        entity.InvoiceNo = normalizedInvoiceNo;
        entity.FinalAmount = model.FinalAmount;
        SyncInvoiceStatus(entity);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateInvoiceNo(ex))
        {
            ModelState.AddModelError(nameof(model.InvoiceNo), "Sá»‘ hÃ³a Ä‘Æ¡n Ä‘Ã£ tá»“n táº¡i.");
            return View(model);
        }

        TempData["SuccessMessage"] = "ÄÃ£ cáº­p nháº­t hÃ³a Ä‘Æ¡n.";
        return RedirectToAction(nameof(InvoiceDetails), new { id = model.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteInvoice(int id)
    {
        var entity = await _context.Invoices
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null)
        {
            return NotFound();
        }

        if (entity.Payments.Count > 0)
        {
            TempData["ErrorMessage"] = $"HÃ³a Ä‘Æ¡n \"{entity.InvoiceNo}\" Ä‘Ã£ cÃ³ {entity.Payments.Count} láº§n thanh toÃ¡n. KhÃ´ng thá»ƒ xÃ³a há»“ sÆ¡ tÃ i chÃ­nh. DÃ¹ng \"Há»§y hÃ³a Ä‘Æ¡n\" náº¿u khÃ´ng cÃ²n hiá»‡u lá»±c.";
            return RedirectToAction(nameof(Index));
        }

        _context.Invoices.Remove(entity);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "ÄÃ£ xÃ³a hÃ³a Ä‘Æ¡n.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VoidInvoice(int id)
    {
        var entity = await _context.Invoices.FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null)
        {
            TempData["ErrorMessage"] = "KhÃ´ng tÃ¬m tháº¥y hÃ³a Ä‘Æ¡n.";
            return RedirectToAction(nameof(Index));
        }

        if (entity.Status == InvoiceStatusVoided)
        {
            TempData["ErrorMessage"] = "HÃ³a Ä‘Æ¡n nÃ y Ä‘Ã£ á»Ÿ tráº¡ng thÃ¡i Há»§y.";
            return RedirectToAction(nameof(Index));
        }

        var paymentCount = await _context.Payments.CountAsync(x => x.InvoiceId == id);
        if (paymentCount > 0)
        {
            TempData["ErrorMessage"] = $"HÃ³a Ä‘Æ¡n \"{entity.InvoiceNo}\" Ä‘Ã£ phÃ¡t sinh thanh toÃ¡n nÃªn khÃ´ng Ä‘Æ°á»£c há»§y trá»±c tiáº¿p.";
            return RedirectToAction(nameof(Index));
        }

        entity.Status = InvoiceStatusVoided;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"ÄÃ£ há»§y hÃ³a Ä‘Æ¡n \"{entity.InvoiceNo}\". Há»“ sÆ¡ váº«n Ä‘Æ°á»£c lÆ°u láº¡i.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> CreatePayment(int? invoiceId = null)
    {
        var model = new CreatePaymentViewModel
        {
            InvoiceId = invoiceId,
            TransDate = DateTime.Now
        };

        await PopulatePaymentOptionsAsync(model);
        await PopulateInvoiceBalanceAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePayment(CreatePaymentViewModel model)
    {
        await PopulatePaymentOptionsAsync(model);
        await PopulateInvoiceBalanceAsync(model);

        if (!await IsValidInvoiceAsync(model.InvoiceId))
        {
            ModelState.AddModelError(nameof(model.InvoiceId), "HÃ³a Ä‘Æ¡n khÃ´ng há»£p lá»‡.");
        }

        if (model.Amount > model.InvoiceDueAmount)
        {
            ModelState.AddModelError(nameof(model.Amount), "Sá»‘ tiá»n thanh toÃ¡n khÃ´ng Ä‘Æ°á»£c vÆ°á»£t quÃ¡ cÃ´ng ná»£ cÃ²n láº¡i.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var entity = new Payment
        {
            InvoiceId = model.InvoiceId!.Value,
            Amount = model.Amount,
            PaymentMethod = model.PaymentMethod.Trim(),
            TransDate = model.TransDate!.Value
        };

        _context.Payments.Add(entity);

        var invoice = await _context.Invoices.Include(x => x.Payments).FirstAsync(x => x.Id == model.InvoiceId.Value);
        invoice.Payments.Add(entity);
        SyncInvoiceStatus(invoice);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "ÄÃ£ ghi nháº­n thanh toÃ¡n.";
        return RedirectToAction(nameof(InvoiceDetails), new { id = entity.InvoiceId });
    }

    [HttpGet]
    public async Task<IActionResult> EditPayment(int id)
    {
        var entity = await _context.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
        {
            return NotFound();
        }

        var model = new EditPaymentViewModel
        {
            Id = entity.Id,
            InvoiceId = entity.InvoiceId,
            Amount = entity.Amount,
            PaymentMethod = entity.PaymentMethod,
            TransDate = entity.TransDate
        };

        await PopulatePaymentOptionsAsync(model);
        await PopulateInvoiceBalanceAsync(model, entity.Id);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPayment(EditPaymentViewModel model)
    {
        await PopulatePaymentOptionsAsync(model);
        await PopulateInvoiceBalanceAsync(model, model.Id);

        var entity = await _context.Payments.FirstOrDefaultAsync(x => x.Id == model.Id);
        if (entity == null)
        {
            return NotFound();
        }

        if (!await IsValidInvoiceAsync(model.InvoiceId))
        {
            ModelState.AddModelError(nameof(model.InvoiceId), "HÃ³a Ä‘Æ¡n khÃ´ng há»£p lá»‡.");
        }

        if (model.Amount > model.InvoiceDueAmount)
        {
            ModelState.AddModelError(nameof(model.Amount), "Sá»‘ tiá»n thanh toÃ¡n khÃ´ng Ä‘Æ°á»£c vÆ°á»£t quÃ¡ cÃ´ng ná»£ cÃ²n láº¡i.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var oldInvoiceId = entity.InvoiceId;
        entity.InvoiceId = model.InvoiceId!.Value;
        entity.Amount = model.Amount;
        entity.PaymentMethod = model.PaymentMethod.Trim();
        entity.TransDate = model.TransDate!.Value;

        await _context.SaveChangesAsync();

        foreach (var invoiceId in new[] { oldInvoiceId, entity.InvoiceId }.Distinct())
        {
            var invoice = await _context.Invoices.Include(x => x.Payments).FirstAsync(x => x.Id == invoiceId);
            SyncInvoiceStatus(invoice);
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "ÄÃ£ cáº­p nháº­t thanh toÃ¡n.";
        return RedirectToAction(nameof(PaymentDetails), new { id = model.Id });
    }

    [HttpGet]
    public async Task<IActionResult> PaymentDetails(int id)
    {
        var model = await _context.Payments
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new PaymentDetailsViewModel
            {
                Id = x.Id,
                InvoiceId = x.InvoiceId,
                InvoiceNo = x.Invoice.InvoiceNo,
                StudentName = x.Invoice.Student.FullName,
                ClassCode = x.Invoice.Class != null ? x.Invoice.Class.ClassCode : "ChÆ°a gáº¯n lá»›p",
                Amount = x.Amount,
                AmountText = FormatMoney(x.Amount),
                PaymentMethod = GetPaymentMethodLabel(x.PaymentMethod),
                TransDate = x.TransDate,
                TransDateText = x.TransDate.ToString("dd/MM/yyyy HH:mm")
            })
            .FirstOrDefaultAsync();

        if (model == null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePayment(int id)
    {
        var entity = await _context.Payments.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
        {
            return NotFound();
        }

        var invoiceId = entity.InvoiceId;
        _context.Payments.Remove(entity);
        await _context.SaveChangesAsync();

        var invoice = await _context.Invoices.Include(x => x.Payments).FirstAsync(x => x.Id == invoiceId);
        SyncInvoiceStatus(invoice);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "ÄÃ£ xÃ³a thanh toÃ¡n.";
        return RedirectToAction(nameof(InvoiceDetails), new { id = invoiceId });
    }

    private static string NormalizeFilter(string value, IEnumerable<string> allowed)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "all" : value.Trim().ToLowerInvariant();
        return allowed.Contains(normalized) ? normalized : "all";
    }

    private static InvoiceManagementItemViewModel MapInvoiceItem(InvoiceListRow item)
    {
        var dueAmount = item.Status == InvoiceStatusVoided
            ? 0m
            : item.FinalAmount - item.PaidAmount;

        var model = new InvoiceManagementItemViewModel
        {
            Id = item.Id,
            InvoiceNo = item.InvoiceNo,
            StudentName = item.StudentName,
            StudentUsername = item.StudentUsername,
            ClassCode = item.ClassCode ?? "ChÆ°a gáº¯n lá»›p",
            FinalAmount = item.FinalAmount,
            PaidAmount = item.PaidAmount,
            DueAmount = dueAmount,
            FinalAmountText = FormatMoney(item.FinalAmount),
            PaidAmountText = FormatMoney(item.PaidAmount),
            DueAmountText = FormatMoney(dueAmount),
            Status = item.Status
        };

        ApplyInvoiceStatus(model, model.PaidAmount);
        return model;
    }

    private static void ApplyInvoiceStatus(InvoiceManagementItemViewModel model, decimal paidAmount)
    {
        if (model.Status == InvoiceStatusVoided)
        {
            model.DueAmount = 0m;
            model.DueAmountText = FormatMoney(0m);
            (model.StatusLabel, model.StatusBadgeClass) = GetInvoiceStatusDisplay(InvoiceStatusVoided);
            return;
        }

        model.Status = paidAmount <= 0 ? InvoiceStatusUnpaid : paidAmount >= model.FinalAmount ? InvoiceStatusPaid : InvoiceStatusPartial;
        (model.StatusLabel, model.StatusBadgeClass) = GetInvoiceStatusDisplay(model.Status);
    }

    private static void ApplyInvoiceStatus(InvoiceDetailsViewModel model, decimal paidAmount)
    {
        if (model.Status == InvoiceStatusVoided)
        {
            model.DueAmount = 0m;
            model.FinalAmountText = FormatMoney(model.FinalAmount);
            model.PaidAmountText = FormatMoney(paidAmount);
            model.DueAmountText = FormatMoney(0m);
            (model.StatusLabel, model.StatusBadgeClass) = GetInvoiceStatusDisplay(InvoiceStatusVoided);
            return;
        }

        model.DueAmount = model.FinalAmount - paidAmount;
        model.FinalAmountText = FormatMoney(model.FinalAmount);
        model.PaidAmountText = FormatMoney(paidAmount);
        model.DueAmountText = FormatMoney(model.DueAmount);
        model.Status = paidAmount <= 0 ? InvoiceStatusUnpaid : paidAmount >= model.FinalAmount ? InvoiceStatusPaid : InvoiceStatusPartial;
        (model.StatusLabel, model.StatusBadgeClass) = GetInvoiceStatusDisplay(model.Status);
    }

    private static (string Label, string BadgeClass) GetInvoiceStatusDisplay(byte status)
    {
        return status switch
        {
            InvoiceStatusUnpaid => ("ChÆ°a thu", "bg-[#ffdad6] text-[#ba1a1a]"),
            InvoiceStatusPartial => ("Thu má»™t pháº§n", "bg-[#fff4e8] text-[#9b682f]"),
            InvoiceStatusPaid => ("ÄÃ£ thu Ä‘á»§", "bg-[#edf7e8] text-[#456c3f]"),
            InvoiceStatusVoided => ("ÄÃ£ há»§y", "bg-[#eeeee9] text-[#42493d]"),
            _ => ($"Tráº¡ng thÃ¡i {status}", "bg-[#eeeee9] text-[#42493d]")
        };
    }

    private async Task PopulateInvoiceOptionsAsync(CreateInvoiceViewModel model)
    {
        model.StudentOptions = await _context.Users
            .AsNoTracking()
            .Where(x => AppRoles.IsStudent(x.Role.Name))
            .OrderBy(x => x.FullName)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = $"{x.FullName} ({x.Username})"
            })
            .ToListAsync();

        model.ClassOptions = await _context.Classes
            .AsNoTracking()
            .OrderByDescending(x => x.StartDate)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.ClassCode
            })
            .ToListAsync();
    }

    private async Task PopulatePaymentOptionsAsync(CreatePaymentViewModel model)
    {
        model.InvoiceOptions = await _context.Invoices
            .AsNoTracking()
            .Where(x => x.Status != InvoiceStatusVoided)
            .OrderByDescending(x => x.Id)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = $"{x.InvoiceNo} Â· {x.Student.FullName}"
            })
            .ToListAsync();

        model.PaymentMethodOptions =
        [
            new SelectListItem { Value = "Cash", Text = "Tiá»n máº·t" },
            new SelectListItem { Value = "Banking", Text = "Chuyá»ƒn khoáº£n" },
            new SelectListItem { Value = "Card", Text = "Tháº»" },
            new SelectListItem { Value = "Other", Text = "KhÃ¡c" }
        ];
    }

    private async Task PopulateInvoiceBalanceAsync(CreatePaymentViewModel model, int? ignorePaymentId = null)
    {
        if (!model.InvoiceId.HasValue)
        {
            return;
        }

        var invoice = await _context.Invoices
            .AsNoTracking()
            .Where(x => x.Id == model.InvoiceId.Value)
            .Select(x => new
            {
                x.Status,
                x.FinalAmount,
                PaidAmount = x.Payments
                    .Where(p => !ignorePaymentId.HasValue || p.Id != ignorePaymentId.Value)
                    .Sum(p => (decimal?)p.Amount) ?? 0m
            })
            .FirstOrDefaultAsync();

        if (invoice == null)
        {
            return;
        }

        model.InvoiceFinalAmount = invoice.FinalAmount;
        model.InvoicePaidAmount = invoice.PaidAmount;
        model.InvoiceDueAmount = invoice.Status == InvoiceStatusVoided
            ? 0m
            : invoice.FinalAmount - invoice.PaidAmount;
    }

    private static void SyncInvoiceStatus(Invoice invoice)
    {
        if (invoice.Status == InvoiceStatusVoided)
        {
            return;
        }

        var paidAmount = invoice.Payments.Sum(x => x.Amount);
        invoice.Status = paidAmount <= 0 ? InvoiceStatusUnpaid : paidAmount >= invoice.FinalAmount ? InvoiceStatusPaid : InvoiceStatusPartial;
    }

    private async Task<bool> IsValidStudentAsync(int? studentId)
    {
        return studentId.HasValue &&
               await _context.Users.AnyAsync(x => x.Id == studentId.Value && AppRoles.IsStudent(x.Role.Name));
    }

    private async Task<bool> IsValidClassAsync(int? classId)
    {
        return classId.HasValue && await _context.Classes.AnyAsync(x => x.Id == classId.Value);
    }

    private async Task<bool> IsStudentInClassAsync(int studentId, int classId)
    {
        return await _context.Enrollments.AnyAsync(x => x.StudentId == studentId && x.ClassId == classId);
    }

    private async Task<bool> IsValidInvoiceAsync(int? invoiceId)
    {
        return invoiceId.HasValue &&
               await _context.Invoices.AnyAsync(x => x.Id == invoiceId.Value && x.Status != InvoiceStatusVoided);
    }

    private static string FormatMoney(decimal amount)
    {
        return $"{amount:N0}Ä‘";
    }

    private static string GetPaymentMethodLabel(string method)
    {
        return method switch
        {
            "Cash" => "Tiá»n máº·t",
            "Banking" => "Chuyá»ƒn khoáº£n",
            "Card" => "Tháº»",
            "Other" => "KhÃ¡c",
            _ => method
        };
    }

    private static bool IsDuplicateInvoiceNo(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException &&
               sqlException.Message.Contains("UQ__Invoices__D796B227", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class InvoiceListRow
    {
        public int Id { get; init; }
        public string InvoiceNo { get; init; } = string.Empty;
        public string StudentName { get; init; } = string.Empty;
        public string StudentUsername { get; init; } = string.Empty;
        public string? ClassCode { get; init; }
        public byte Status { get; init; }
        public decimal FinalAmount { get; init; }
        public decimal PaidAmount { get; init; }
    }
}
