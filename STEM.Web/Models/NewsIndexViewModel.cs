namespace STEM.Web.Models;

// ─── Index page ──────────────────────────────────────────────────────────────

public class NewsIndexViewModel
{
    public required IReadOnlyList<NewsArticleCardViewModel> Articles { get; init; }
    public required IReadOnlyList<NewsArticleCardViewModel> FeaturedArticles { get; init; }
    public required IReadOnlyList<NewsCategoryItemViewModel> Categories { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }

    // Phân trang
    public int CurrentPage { get; init; }
    public int TotalPages { get; init; }
    public string? SearchTerm { get; init; }
    public string? CategoryFilter { get; init; }
}

// ─── Article card (dùng cho cả list và related) ───────────────────────────────

public class NewsArticleCardViewModel
{
    public required string Slug { get; init; }
    public required string Title { get; init; }
    public string Excerpt { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = "/images/courses/default.jpg";
    public string Category { get; init; } = "STEM";
    public string AuthorName { get; init; } = string.Empty;
    public string AuthorAvatarUrl { get; init; } = string.Empty;
    public string PublishedDateText { get; init; } = string.Empty;
    public string ReadTimeText { get; init; } = string.Empty;
}

// ─── Sidebar items ────────────────────────────────────────────────────────────

public class NewsSidebarItemViewModel
{
    public required string Slug { get; init; }
    public required string Title { get; init; }
    public string ImageUrl { get; init; } = string.Empty;
    public required string PublishedDateText { get; init; }
}

// ─── Category filter ──────────────────────────────────────────────────────────

public class NewsCategoryItemViewModel
{
    public required string Name { get; init; }
    public required int Count { get; init; }
}
