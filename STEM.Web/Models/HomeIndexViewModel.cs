namespace STEM.Web.Models;

public class HomeIndexViewModel
{
    public required IReadOnlyList<BannerViewModel> Banners { get; init; }
    public required IReadOnlyList<NewsArticleCardViewModel> LatestNews { get; init; }
}

public class BannerViewModel
{
    public required string Title { get; init; }
    public required string ImageUrl { get; init; }
    public string? LinkUrl { get; init; }
}
