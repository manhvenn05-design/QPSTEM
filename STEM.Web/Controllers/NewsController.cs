using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Data;
using STEM.Web.Models;

namespace STEM.Web.Controllers;

public class NewsController : Controller
{
    private const int PageSize = 6;
    private const string DefaultImage = "/images/courses/default.jpg";

    private readonly ApplicationDbContext _context;

    public NewsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // ─── Index ────────────────────────────────────────────────────────────────

    public async Task<IActionResult> Index(int page = 1, string? q = null, string? category = null)
    {
        var query = _context.Posts
            .AsNoTracking()
            .Where(p => p.IsPublished);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(p => p.Title.Contains(q) || (p.Excerpt != null && p.Excerpt.Contains(q)));

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category == category);

        var totalItems = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var posts = await query
            .OrderByDescending(p => p.PublishedAt)
            .ThenByDescending(p => p.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(p => new
            {
                p.Slug,
                p.Title,
                p.Excerpt,
                p.ImageUrl,
                p.Category,
                AuthorName  = p.Author.FullName,
                PublishedAt = p.PublishedAt
            })
            .ToListAsync();

        var articles = posts.Select(p => new NewsArticleCardViewModel
        {
            Slug            = p.Slug,
            Title           = p.Title,
            Excerpt         = p.Excerpt ?? string.Empty,
            ImageUrl        = !string.IsNullOrEmpty(p.ImageUrl) ? p.ImageUrl : DefaultImage,
            Category        = p.Category ?? "STEM",
            AuthorName      = p.AuthorName,
            AuthorAvatarUrl = string.Empty,
            PublishedDateText = p.PublishedAt.HasValue
                ? p.PublishedAt.Value.ToString("dd/MM/yyyy")
                : string.Empty,
            ReadTimeText = EstimateReadTime(p.Excerpt ?? string.Empty)
        }).ToList();

        // Sidebar: 4 bài mới nhất (bài featured)
        var featuredPosts = await _context.Posts
            .AsNoTracking()
            .Where(p => p.IsPublished)
            .OrderByDescending(p => p.PublishedAt)
            .ThenByDescending(p => p.Id)
            .Take(4)
            .Select(p => new
            {
                p.Slug,
                p.Title,
                p.ImageUrl,
                PublishedAt = p.PublishedAt
            })
            .ToListAsync();

        var featured = featuredPosts.Select(p => new NewsArticleCardViewModel
        {
            Slug            = p.Slug,
            Title           = p.Title,
            ImageUrl        = !string.IsNullOrEmpty(p.ImageUrl) ? p.ImageUrl : DefaultImage,
            AuthorName      = string.Empty,
            AuthorAvatarUrl = string.Empty,
            PublishedDateText = p.PublishedAt.HasValue
                ? p.PublishedAt.Value.ToString("dd/MM/yyyy")
                : string.Empty,
            ReadTimeText = string.Empty
        }).ToList();

        // Danh mục từ DB
        var categories = await _context.Posts
            .AsNoTracking()
            .Where(p => p.IsPublished && p.Category != null)
            .GroupBy(p => p.Category!)
            .Select(g => new NewsCategoryItemViewModel { Name = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToListAsync();

        var model = new NewsIndexViewModel
        {
            Articles        = articles,
            FeaturedArticles = featured,
            Categories      = categories,
            Tags            = ["#STEM", "#Robotics", "#LậpTrình", "#KhoaHọc", "#GiáoDục", "#AI"],
            CurrentPage     = page,
            TotalPages      = totalPages,
            SearchTerm      = q?.Trim(),
            CategoryFilter  = category?.Trim()
        };

        return View(model);
    }

    // ─── Details ──────────────────────────────────────────────────────────────

    public async Task<IActionResult> Details(string id)
    {
        var post = await _context.Posts
            .AsNoTracking()
            .Where(p => p.Slug == id && p.IsPublished)
            .Select(p => new
            {
                p.Id,
                p.Slug,
                p.Title,
                p.Excerpt,
                p.ImageUrl,
                p.Category,
                p.Content,
                p.PublishedAt,
                AuthorName = p.Author.FullName,
            })
            .FirstOrDefaultAsync();

        if (post == null) return NotFound();

        var article = new NewsArticleCardViewModel
        {
            Slug            = post.Slug,
            Title           = post.Title,
            Excerpt         = post.Excerpt ?? string.Empty,
            ImageUrl        = !string.IsNullOrEmpty(post.ImageUrl) ? post.ImageUrl : DefaultImage,
            Category        = post.Category ?? "STEM",
            AuthorName      = post.AuthorName,
            AuthorAvatarUrl = string.Empty,
            PublishedDateText = post.PublishedAt.HasValue
                ? post.PublishedAt.Value.ToString("dd 'Tháng' MM, yyyy")
                : string.Empty,
            ReadTimeText = EstimateReadTime(post.Content)
        };

        // Bài liên quan: cùng category, khác bài hiện tại
        var relatedPosts = await _context.Posts
            .AsNoTracking()
            .Where(p => p.IsPublished && p.Id != post.Id &&
                        (p.Category == post.Category || post.Category == null))
            .OrderByDescending(p => p.PublishedAt)
            .ThenByDescending(p => p.Id)
            .Take(3)
            .Select(p => new
            {
                p.Slug,
                p.Title,
                p.ImageUrl,
                p.Category,
                AuthorName  = p.Author.FullName,
                PublishedAt = p.PublishedAt
            })
            .ToListAsync();

        var related = relatedPosts.Select(p => new NewsArticleCardViewModel
        {
            Slug            = p.Slug,
            Title           = p.Title,
            ImageUrl        = !string.IsNullOrEmpty(p.ImageUrl) ? p.ImageUrl : DefaultImage,
            Category        = p.Category ?? "STEM",
            AuthorName      = p.AuthorName,
            AuthorAvatarUrl = string.Empty,
            PublishedDateText = p.PublishedAt.HasValue
                ? p.PublishedAt.Value.ToString("dd/MM/yyyy")
                : string.Empty,
            ReadTimeText = string.Empty
        }).ToList();

        // Sidebar featured
        var featuredPosts = await _context.Posts
            .AsNoTracking()
            .Where(p => p.IsPublished && p.Id != post.Id)
            .OrderByDescending(p => p.PublishedAt)
            .ThenByDescending(p => p.Id)
            .Take(4)
            .Select(p => new
            {
                p.Slug,
                p.Title,
                p.ImageUrl,
                PublishedAt = p.PublishedAt
            })
            .ToListAsync();

        var featured = featuredPosts.Select(p => new NewsArticleCardViewModel
        {
            Slug            = p.Slug,
            Title           = p.Title,
            ImageUrl        = !string.IsNullOrEmpty(p.ImageUrl) ? p.ImageUrl : DefaultImage,
            AuthorName      = string.Empty,
            AuthorAvatarUrl = string.Empty,
            PublishedDateText = p.PublishedAt.HasValue
                ? p.PublishedAt.Value.ToString("dd/MM/yyyy")
                : string.Empty,
            ReadTimeText = string.Empty
        }).ToList();

        // Tags từ category
        var tags = await _context.Posts
            .AsNoTracking()
            .Where(p => p.IsPublished && p.Category != null)
            .Select(p => "#" + p.Category!)
            .Distinct()
            .Take(10)
            .ToListAsync();

        if (tags.Count == 0)
            tags = ["#STEM", "#Robotics", "#LậpTrình", "#KhoaHọc"];

        ViewData["Title"] = post.Title;

        var viewModel = new NewsDetailViewModel
        {
            Article          = article,
            HeroImageUrl     = article.ImageUrl,
            ContentHtml      = post.Content,
            AuthorBio        = string.Empty,
            FeaturedArticles = featured,
            Tags             = tags,
            RelatedArticles  = related
        };

        return View(viewModel);
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    /// <summary>Ước tính thời gian đọc: ~200 từ/phút</summary>
    private static string EstimateReadTime(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return "1 phút đọc";
        var wordCount = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var minutes   = Math.Max(1, (int)Math.Ceiling(wordCount / 200.0));
        return $"{minutes} phút đọc";
    }
}
