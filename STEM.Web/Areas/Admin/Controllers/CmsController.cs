using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
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
public class CmsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorage;

    public CmsController(ApplicationDbContext context, IFileStorageService fileStorage)
    {
        _context = context;
        _fileStorage = fileStorage;
    }

    // ─── Index ────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index(string filter = "all", string? q = null)
    {
        var filters = new[]
        {
            new CmsFilterViewModel { Key = "all",           Label = "Tất cả" },
            new CmsFilterViewModel { Key = "published",     Label = "Bài đang hiển thị" },
            new CmsFilterViewModel { Key = "draft",         Label = "Bài nháp" },
            new CmsFilterViewModel { Key = "active-banner", Label = "Banner đang bật" }
        };

        var normalizedFilter = NormalizeFilter(filter, filters.Select(x => x.Key));
        var searchTerm = q?.Trim() ?? string.Empty;

        var postsQuery = _context.Posts
            .AsNoTracking()
            .Select(x => new PostManagementItemViewModel
            {
                Id            = x.Id,
                Title         = x.Title,
                Slug          = x.Slug,
                Category      = x.Category,
                ImageUrl      = x.ImageUrl,
                AuthorName    = x.Author.FullName,
                IsPublished   = x.IsPublished,
                StatusLabel   = x.IsPublished ? "Đang hiển thị" : "Bản nháp",
                StatusBadgeClass = x.IsPublished
                    ? "bg-[#edf7e8] text-[#456c3f]"
                    : "bg-[#eeeee9] text-[#42493d]",
                PublishedAtText = x.PublishedAt.HasValue
                    ? x.PublishedAt.Value.ToString("dd/MM/yyyy")
                    : "Chưa đặt ngày"
            });

        var bannersQuery = _context.Banners
            .AsNoTracking()
            .Select(x => new BannerManagementItemViewModel
            {
                Id            = x.Id,
                Title         = string.IsNullOrWhiteSpace(x.Title) ? "Banner không tiêu đề" : x.Title!,
                ImageUrl      = x.ImageUrl,
                LinkUrl       = x.LinkUrl,
                IsActive      = x.IsActive,
                SortOrder     = x.SortOrder,
                StatusLabel   = x.IsActive ? "Đang hiển thị" : "Đã ẩn",
                StatusBadgeClass = x.IsActive
                    ? "bg-[#edf7e8] text-[#456c3f]"
                    : "bg-[#eeeee9] text-[#42493d]"
            });

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            postsQuery = postsQuery.Where(x =>
                x.Title.Contains(searchTerm) ||
                x.Slug.Contains(searchTerm) ||
                x.AuthorName.Contains(searchTerm) ||
                (x.Category != null && x.Category.Contains(searchTerm)));

            bannersQuery = bannersQuery.Where(x =>
                x.Title.Contains(searchTerm) ||
                (x.LinkUrl != null && x.LinkUrl.Contains(searchTerm)));
        }

        if (normalizedFilter == "published")
            postsQuery = postsQuery.Where(x => x.IsPublished);
        else if (normalizedFilter == "draft")
            postsQuery = postsQuery.Where(x => !x.IsPublished);
        else if (normalizedFilter == "active-banner")
            bannersQuery = bannersQuery.Where(x => x.IsActive);

        var model = new CmsManagementViewModel
        {
            SelectedFilter = normalizedFilter,
            SearchTerm     = searchTerm,
            TotalPosts     = await _context.Posts.CountAsync(),
            PublishedPosts = await _context.Posts.CountAsync(x => x.IsPublished),
            TotalBanners   = await _context.Banners.CountAsync(),
            ActiveBanners  = await _context.Banners.CountAsync(x => x.IsActive),
            Filters        = filters,
            Posts   = await postsQuery.OrderByDescending(x => x.Id).Take(20).ToListAsync(),
            Banners = await bannersQuery.OrderBy(x => x.SortOrder).ThenByDescending(x => x.Id).Take(12).ToListAsync()
        };

        return View(model);
    }

    // ─── Post Details ─────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> PostDetails(int id)
    {
        var model = await _context.Posts
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new PostDetailsViewModel
            {
                Id             = x.Id,
                Title          = x.Title,
                Slug           = x.Slug,
                Excerpt        = x.Excerpt,
                Category       = x.Category,
                ImageUrl       = x.ImageUrl,
                AuthorName     = x.Author.FullName,
                IsPublished    = x.IsPublished,
                StatusLabel    = x.IsPublished ? "Đang hiển thị" : "Bản nháp",
                StatusBadgeClass = x.IsPublished
                    ? "bg-[#edf7e8] text-[#456c3f]"
                    : "bg-[#eeeee9] text-[#42493d]",
                Content        = x.Content,
                PublishedAtText = x.PublishedAt.HasValue
                    ? x.PublishedAt.Value.ToString("dd/MM/yyyy HH:mm")
                    : "Chưa đặt ngày",
                PublicUrl = $"/News/Details/{x.Slug}"
            })
            .FirstOrDefaultAsync();

        return model == null ? NotFound() : View(model);
    }

    // ─── Create Post ──────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> CreatePost()
    {
        var model = new CreatePostViewModel();
        await PopulatePostDropdownsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePost(CreatePostViewModel model)
    {
        await PopulatePostDropdownsAsync(model);
        model.Slug = NormalizeSlugInput(model.Slug, model.Title);

        if (!await IsValidAuthorAsync(model.AuthorId))
            ModelState.AddModelError(nameof(model.AuthorId), "Tác giả không hợp lệ.");

        if (string.IsNullOrWhiteSpace(model.Slug))
            ModelState.AddModelError(nameof(model.Slug), "Vui lòng kiểm tra lại slug.");
        else if (await _context.Posts.AnyAsync(x => x.Slug == model.Slug))
            ModelState.AddModelError(nameof(model.Slug), "Slug đã tồn tại.");

        if (!ModelState.IsValid)
            return View(model);

        // Xử lý upload ảnh thumbnail
        string? uploadedImageUrl = null;
        if (model.ThumbnailFile != null)
        {
            try
            {
                uploadedImageUrl = await _fileStorage.SaveFileAsync(
                    model.ThumbnailFile, "posts");
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(model.ThumbnailFile), ex.Message);
                return View(model);
            }
        }

        var entity = new Post
        {
            AuthorId    = model.AuthorId!.Value,
            Title       = model.Title.Trim(),
            Slug        = model.Slug,
            Excerpt     = string.IsNullOrWhiteSpace(model.Excerpt) ? null : model.Excerpt.Trim(),
            Category    = string.IsNullOrWhiteSpace(model.Category) ? null : model.Category.Trim(),
            ImageUrl    = uploadedImageUrl,
            Content     = model.Content.Trim(),
            IsPublished = model.IsPublished,
            PublishedAt = model.IsPublished ? DateTime.Now : null
        };

        try
        {
            _context.Posts.Add(entity);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateSlug(ex))
        {
            ModelState.AddModelError(nameof(model.Slug), "Slug đã tồn tại.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã tạo bài viết mới.";
        return RedirectToAction(nameof(PostDetails), new { id = entity.Id });
    }

    // ─── Edit Post ────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> EditPost(int id)
    {
        var entity = await _context.Posts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return NotFound();

        var model = new EditPostViewModel
        {
            Id               = entity.Id,
            AuthorId         = entity.AuthorId,
            Title            = entity.Title,
            Slug             = entity.Slug,
            Excerpt          = entity.Excerpt,
            Category         = entity.Category,
            CurrentImageUrl  = entity.ImageUrl,
            Content          = entity.Content,
            IsPublished      = entity.IsPublished
        };

        await PopulatePostDropdownsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPost(EditPostViewModel model)
    {
        await PopulatePostDropdownsAsync(model);
        var entity = await _context.Posts.FirstOrDefaultAsync(x => x.Id == model.Id);
        if (entity == null) return NotFound();

        model.CurrentImageUrl = entity.ImageUrl;
        model.Slug = NormalizeSlugInput(model.Slug, model.Title);

        if (!await IsValidAuthorAsync(model.AuthorId))
            ModelState.AddModelError(nameof(model.AuthorId), "Tác giả không hợp lệ.");

        if (string.IsNullOrWhiteSpace(model.Slug))
            ModelState.AddModelError(nameof(model.Slug), "Vui lòng kiểm tra lại slug.");
        else if (await _context.Posts.AnyAsync(x => x.Id != model.Id && x.Slug == model.Slug))
            ModelState.AddModelError(nameof(model.Slug), "Slug đã tồn tại.");

        if (!ModelState.IsValid)
            return View(model);

        // Xử lý upload ảnh mới
        if (model.ThumbnailFile != null)
        {
            try
            {
                var newImageUrl = await _fileStorage.SaveFileAsync(
                    model.ThumbnailFile, "posts");
                if (!string.IsNullOrWhiteSpace(entity.ImageUrl))
                {
                    await _fileStorage.DeleteFileAsync(entity.ImageUrl);
                }
                entity.ImageUrl = newImageUrl;
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(model.ThumbnailFile), ex.Message);
                return View(model);
            }
        }

        // Cập nhật PublishedAt khi bài được publish lần đầu
        if (model.IsPublished && !entity.IsPublished)
            entity.PublishedAt ??= DateTime.Now;

        entity.AuthorId    = model.AuthorId!.Value;
        entity.Title       = model.Title.Trim();
        entity.Slug        = model.Slug;
        entity.Excerpt     = string.IsNullOrWhiteSpace(model.Excerpt) ? null : model.Excerpt.Trim();
        entity.Category    = string.IsNullOrWhiteSpace(model.Category) ? null : model.Category.Trim();
        entity.Content     = model.Content.Trim();
        entity.IsPublished = model.IsPublished;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateSlug(ex))
        {
            ModelState.AddModelError(nameof(model.Slug), "Slug đã tồn tại.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã cập nhật bài viết.";
        return RedirectToAction(nameof(PostDetails), new { id = model.Id });
    }

    // ─── Delete Post ──────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePost(int id)
    {
        var entity = await _context.Posts.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(entity.ImageUrl))
        {
            await _fileStorage.DeleteFileAsync(entity.ImageUrl);
        }
        _context.Posts.Remove(entity);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã xóa bài viết.";
        return RedirectToAction(nameof(Index));
    }

    // ─── Banner CRUD ──────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult CreateBanner()
    {
        return View(new CreateBannerViewModel { IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBanner(CreateBannerViewModel model, CancellationToken cancellationToken)
    {
        if (model.ImageFile == null)
            ModelState.AddModelError(nameof(model.ImageFile), "Vui lòng chọn ảnh banner.");

        if (!ModelState.IsValid)
            return View(model);

        try
        {
            model.ImageUrl = await _fileStorage.SaveFileAsync(
                model.ImageFile!, "banners", cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(model.ImageFile), ex.Message);
            return View(model);
        }

        var entity = new Banner
        {
            Title     = string.IsNullOrWhiteSpace(model.Title) ? null : model.Title.Trim(),
            ImageUrl  = model.ImageUrl!,
            LinkUrl   = string.IsNullOrWhiteSpace(model.LinkUrl) ? null : model.LinkUrl.Trim(),
            IsActive  = model.IsActive,
            SortOrder = model.SortOrder
        };

        _context.Banners.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = "Đã thêm banner.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> EditBanner(int id)
    {
        var entity = await _context.Banners.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return NotFound();

        var model = new EditBannerViewModel
        {
            Id              = entity.Id,
            Title           = entity.Title,
            LinkUrl         = entity.LinkUrl,
            IsActive        = entity.IsActive,
            SortOrder       = entity.SortOrder,
            CurrentImageUrl = entity.ImageUrl
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditBanner(EditBannerViewModel model, CancellationToken cancellationToken)
    {
        var entity = await _context.Banners.FirstOrDefaultAsync(x => x.Id == model.Id, cancellationToken);
        if (entity == null) return NotFound();

        if (!ModelState.IsValid)
        {
            model.CurrentImageUrl = entity.ImageUrl;
            return View(model);
        }

        if (model.ImageFile != null)
        {
            try
            {
                var newImageUrl = await _fileStorage.SaveFileAsync(
                    model.ImageFile, "banners", cancellationToken);
                await _fileStorage.DeleteFileAsync(entity.ImageUrl);
                entity.ImageUrl = newImageUrl;
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(model.ImageFile), ex.Message);
                model.CurrentImageUrl = entity.ImageUrl;
                return View(model);
            }
        }

        entity.Title     = string.IsNullOrWhiteSpace(model.Title) ? null : model.Title.Trim();
        entity.LinkUrl   = string.IsNullOrWhiteSpace(model.LinkUrl) ? null : model.LinkUrl.Trim();
        entity.IsActive  = model.IsActive;
        entity.SortOrder = model.SortOrder;

        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = "Đã cập nhật banner.";
        return RedirectToAction(nameof(BannerDetails), new { id = model.Id });
    }

    [HttpGet]
    public async Task<IActionResult> BannerDetails(int id)
    {
        var model = await _context.Banners
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new BannerDetailsViewModel
            {
                Id               = x.Id,
                Title            = x.Title,
                ImageUrl         = x.ImageUrl,
                LinkUrl          = x.LinkUrl,
                IsActive         = x.IsActive,
                SortOrder        = x.SortOrder,
                StatusLabel      = x.IsActive ? "Đang hiển thị" : "Đã ẩn",
                StatusBadgeClass = x.IsActive
                    ? "bg-[#edf7e8] text-[#456c3f]"
                    : "bg-[#eeeee9] text-[#42493d]"
            })
            .FirstOrDefaultAsync();

        return model == null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBanner(int id)
    {
        var entity = await _context.Banners.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return NotFound();

        await _fileStorage.DeleteFileAsync(entity.ImageUrl);
        _context.Banners.Remove(entity);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã xóa banner.";
        return RedirectToAction(nameof(Index));
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private async Task PopulatePostDropdownsAsync(CreatePostViewModel model)
    {
        model.AuthorOptions = await _context.Users
            .AsNoTracking()
            .Where(x => x.Role.Name == "Admin" || x.Role.Name == "Teacher")
            .OrderBy(x => x.FullName)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text  = $"{x.FullName} ({x.Username})"
            })
            .ToListAsync();

        // Lấy các danh mục đã có để gợi ý
        model.CategorySuggestions = await _context.Posts
            .AsNoTracking()
            .Where(x => x.Category != null)
            .Select(x => x.Category!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();
    }

    private async Task<bool> IsValidAuthorAsync(int? authorId)
    {
        return authorId.HasValue && await _context.Users.AnyAsync(x =>
            x.Id == authorId.Value &&
            (x.Role.Name == "Admin" || x.Role.Name == "Teacher"));
    }

    private static string NormalizeFilter(string value, IEnumerable<string> allowed)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "all" : value.Trim().ToLowerInvariant();
        return allowed.Contains(normalized) ? normalized : "all";
    }

    private static string NormalizeSlugInput(string slug, string title)
    {
        var source = string.IsNullOrWhiteSpace(slug) ? title : slug;
        if (string.IsNullOrWhiteSpace(source)) return string.Empty;

        var normalized = source.Normalize(NormalizationForm.FormD);
        var builder    = new StringBuilder();

        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }

        var plainText = builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        plainText = plainText.Replace("đ", "d");
        plainText = Regex.Replace(plainText, @"[^a-z0-9]+", "-");
        plainText = Regex.Replace(plainText, @"-+", "-").Trim('-');
        return plainText;
    }

    private static bool IsDuplicateSlug(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException &&
               sqlException.Message.Contains("UQ__Posts__BC7B5FB6", StringComparison.OrdinalIgnoreCase);
    }
}
