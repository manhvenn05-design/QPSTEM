using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Areas.Admin.Models;
using STEM.Web.Data;
using STEM.Web.Models;

namespace STEM.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class FinanceController : Controller
{
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
            new FinanceFilterViewModel { Key = "all", Label = "Tất cả" },
            new FinanceFilterViewModel { Key = "unpaid", Label = "Chưa thu" },
            new FinanceFilterViewModel { Key = "partial", Label = "Thu một phần" },
            new FinanceFilterViewModel { Key = "paid", Label = "Đã thu đủ" }
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
            "unpaid" => invoicesQuery.Where(x => x.PaidAmount == 0),
            "partial" => invoicesQuery.Where(x => x.PaidAmount > 0 && x.PaidAmount < x.FinalAmount),
            "paid" => invoicesQuery.Where(x => x.PaidAmount >= x.FinalAmount),
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
            .Select(x => x.FinalAmount - (x.Payments.Sum(p => (decimal?)p.Amount) ?? 0m))
            .SumAsync();

        var model = new FinanceManagementViewModel
        {
            SelectedFilter = normalizedFilter,
            SearchTerm = searchTerm,
            TotalRevenue = totalRevenue,
            TotalOutstanding = totalOutstanding,
            UnpaidInvoiceCount = await _context.Invoices.CountAsync(x => !x.Payments.Any()),
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
                ClassCode = x.Class != null ? x.Class.ClassCode : "Chưa gắn lớp",
                FinalAmount = x.FinalAmount,
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
            ModelState.AddModelError(nameof(model.InvoiceNo), "Số hóa đơn đã tồn tại.");
        }

        if (!await IsValidStudentAsync(model.StudentId))
        {
            ModelState.AddModelError(nameof(model.StudentId), "Học viên không hợp lệ.");
        }

        if (model.ClassId.HasValue && !await IsValidClassAsync(model.ClassId))
        {
            ModelState.AddModelError(nameof(model.ClassId), "Lớp học không hợp lệ.");
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
            Status = 1
        };

        try
        {
            _context.Invoices.Add(entity);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateInvoiceNo(ex))
        {
            ModelState.AddModelError(nameof(model.InvoiceNo), "Số hóa đơn đã tồn tại.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã tạo hóa đơn mới.";
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
            ModelState.AddModelError(nameof(model.InvoiceNo), "Số hóa đơn đã tồn tại.");
        }

        if (!await IsValidStudentAsync(model.StudentId))
        {
            ModelState.AddModelError(nameof(model.StudentId), "Học viên không hợp lệ.");
        }

        if (model.ClassId.HasValue && !await IsValidClassAsync(model.ClassId))
        {
            ModelState.AddModelError(nameof(model.ClassId), "Lớp học không hợp lệ.");
        }

        var paidAmount = entity.Payments.Sum(x => x.Amount);
        if (model.FinalAmount < paidAmount)
        {
            ModelState.AddModelError(nameof(model.FinalAmount), "Tổng tiền không được nhỏ hơn số tiền đã thu.");
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
            ModelState.AddModelError(nameof(model.InvoiceNo), "Số hóa đơn đã tồn tại.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã cập nhật hóa đơn.";
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
            TempData["ErrorMessage"] = "Không thể xóa hóa đơn đã có thanh toán.";
            return RedirectToAction(nameof(Index));
        }

        _context.Invoices.Remove(entity);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã xóa hóa đơn.";
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
            ModelState.AddModelError(nameof(model.InvoiceId), "Hóa đơn không hợp lệ.");
        }

        if (model.Amount > model.InvoiceDueAmount)
        {
            ModelState.AddModelError(nameof(model.Amount), "Số tiền thanh toán không được vượt quá công nợ còn lại.");
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

        TempData["SuccessMessage"] = "Đã ghi nhận thanh toán.";
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
            ModelState.AddModelError(nameof(model.InvoiceId), "Hóa đơn không hợp lệ.");
        }

        if (model.Amount > model.InvoiceDueAmount)
        {
            ModelState.AddModelError(nameof(model.Amount), "Số tiền thanh toán không được vượt quá công nợ còn lại.");
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

        TempData["SuccessMessage"] = "Đã cập nhật thanh toán.";
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
                ClassCode = x.Invoice.Class != null ? x.Invoice.Class.ClassCode : "Chưa gắn lớp",
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

        TempData["SuccessMessage"] = "Đã xóa thanh toán.";
        return RedirectToAction(nameof(InvoiceDetails), new { id = invoiceId });
    }

    private static string NormalizeFilter(string value, IEnumerable<string> allowed)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "all" : value.Trim().ToLowerInvariant();
        return allowed.Contains(normalized) ? normalized : "all";
    }

    private static InvoiceManagementItemViewModel MapInvoiceItem(InvoiceListRow item)
    {
        var model = new InvoiceManagementItemViewModel
        {
            Id = item.Id,
            InvoiceNo = item.InvoiceNo,
            StudentName = item.StudentName,
            StudentUsername = item.StudentUsername,
            ClassCode = item.ClassCode ?? "Chưa gắn lớp",
            FinalAmount = item.FinalAmount,
            PaidAmount = item.PaidAmount,
            DueAmount = item.FinalAmount - item.PaidAmount,
            FinalAmountText = FormatMoney(item.FinalAmount),
            PaidAmountText = FormatMoney(item.PaidAmount),
            DueAmountText = FormatMoney(item.FinalAmount - item.PaidAmount)
        };

        ApplyInvoiceStatus(model, model.PaidAmount);
        return model;
    }

    private static void ApplyInvoiceStatus(InvoiceManagementItemViewModel model, decimal paidAmount)
    {
        model.Status = paidAmount <= 0 ? (byte)1 : paidAmount >= model.FinalAmount ? (byte)3 : (byte)2;
        (model.StatusLabel, model.StatusBadgeClass) = GetInvoiceStatusDisplay(model.Status);
    }

    private static void ApplyInvoiceStatus(InvoiceDetailsViewModel model, decimal paidAmount)
    {
        model.DueAmount = model.FinalAmount - paidAmount;
        model.FinalAmountText = FormatMoney(model.FinalAmount);
        model.PaidAmountText = FormatMoney(paidAmount);
        model.DueAmountText = FormatMoney(model.DueAmount);
        model.Status = paidAmount <= 0 ? (byte)1 : paidAmount >= model.FinalAmount ? (byte)3 : (byte)2;
        (model.StatusLabel, model.StatusBadgeClass) = GetInvoiceStatusDisplay(model.Status);
    }

    private static (string Label, string BadgeClass) GetInvoiceStatusDisplay(byte status)
    {
        return status switch
        {
            1 => ("Chưa thu", "bg-[#ffdad6] text-[#ba1a1a]"),
            2 => ("Thu một phần", "bg-[#fff4e8] text-[#9b682f]"),
            3 => ("Đã thu đủ", "bg-[#edf7e8] text-[#456c3f]"),
            _ => ($"Trạng thái {status}", "bg-[#eeeee9] text-[#42493d]")
        };
    }

    private async Task PopulateInvoiceOptionsAsync(CreateInvoiceViewModel model)
    {
        model.StudentOptions = await _context.Users
            .AsNoTracking()
            .Where(x => x.Role.Name == "Student")
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
            .OrderByDescending(x => x.Id)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = $"{x.InvoiceNo} · {x.Student.FullName}"
            })
            .ToListAsync();

        model.PaymentMethodOptions =
        [
            new SelectListItem { Value = "Cash", Text = "Tiền mặt" },
            new SelectListItem { Value = "Banking", Text = "Chuyển khoản" },
            new SelectListItem { Value = "Card", Text = "Thẻ" },
            new SelectListItem { Value = "Other", Text = "Khác" }
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
        model.InvoiceDueAmount = invoice.FinalAmount - invoice.PaidAmount;
    }

    private static void SyncInvoiceStatus(Invoice invoice)
    {
        var paidAmount = invoice.Payments.Sum(x => x.Amount);
        invoice.Status = paidAmount <= 0 ? (byte)1 : paidAmount >= invoice.FinalAmount ? (byte)3 : (byte)2;
    }

    private async Task<bool> IsValidStudentAsync(int? studentId)
    {
        return studentId.HasValue && await _context.Users.AnyAsync(x => x.Id == studentId.Value && x.Role.Name == "Student");
    }

    private async Task<bool> IsValidClassAsync(int? classId)
    {
        return classId.HasValue && await _context.Classes.AnyAsync(x => x.Id == classId.Value);
    }

    private async Task<bool> IsValidInvoiceAsync(int? invoiceId)
    {
        return invoiceId.HasValue && await _context.Invoices.AnyAsync(x => x.Id == invoiceId.Value);
    }

    private static string FormatMoney(decimal amount)
    {
        return $"{amount:N0}đ";
    }

    private static string GetPaymentMethodLabel(string method)
    {
        return method switch
        {
            "Cash" => "Tiền mặt",
            "Banking" => "Chuyển khoản",
            "Card" => "Thẻ",
            "Other" => "Khác",
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
        public decimal FinalAmount { get; init; }
        public decimal PaidAmount { get; init; }
    }
}
