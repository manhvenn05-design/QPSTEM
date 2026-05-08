using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Data;
using STEM.Web.Models;

namespace STEM.Web.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        // Lấy danh sách banner đang active, sắp xếp theo SortOrder
        var banners = await _context.Banners
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.SortOrder)
            .ThenByDescending(b => b.Id)
            .Select(b => new BannerViewModel
            {
                Title = b.Title ?? string.Empty,
                ImageUrl = b.ImageUrl,
                LinkUrl = b.LinkUrl
            })
            .ToListAsync();

        // Lấy 3 bài viết mới nhất cho landing page
        var latestNews = await _context.Posts
            .AsNoTracking()
            .Where(p => p.IsPublished)
            .OrderByDescending(p => p.PublishedAt)
            .ThenByDescending(p => p.Id)
            .Take(3)
            .Select(p => new NewsArticleCardViewModel
            {
                Slug = p.Slug,
                Title = p.Title,
                Excerpt = p.Excerpt ?? string.Empty,
                ImageUrl = !string.IsNullOrEmpty(p.ImageUrl) ? p.ImageUrl : "/images/courses/default.jpg",
                Category = p.Category ?? "STEM",
                AuthorName = p.Author.FullName,
                AuthorAvatarUrl = string.Empty,
                PublishedDateText = p.PublishedAt.HasValue ? p.PublishedAt.Value.ToString("dd/MM/yyyy") : string.Empty,
                ReadTimeText = string.Empty
            })
            .ToListAsync();

        var model = new HomeIndexViewModel
        {
            Banners = banners,
            LatestNews = latestNews
        };

        return View(model);
    }

    public IActionResult About() => View();

    public IActionResult Contact() => View();
}
