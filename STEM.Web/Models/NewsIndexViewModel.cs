namespace STEM.Web.Models;

public class NewsIndexViewModel
{
    public required IReadOnlyList<NewsArticleCardViewModel> Articles { get; init; }
    public required IReadOnlyList<NewsSidebarItemViewModel> FeaturedArticles { get; init; }
    public required IReadOnlyList<NewsCategoryItemViewModel> Categories { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }
}

public class NewsArticleCardViewModel
{
    public required string Slug { get; init; }
    public required string Title { get; init; }
    public required string Excerpt { get; init; }
    public required string ImageUrl { get; init; }
    public required string Category { get; init; }
    public required string AuthorName { get; init; }
    public required string AuthorAvatarUrl { get; init; }
    public required string PublishedDateText { get; init; }
    public required string ReadTimeText { get; init; }
}

public class NewsSidebarItemViewModel
{
    public required string Title { get; init; }
    public required string ImageUrl { get; init; }
    public required string PublishedDateText { get; init; }
}

public class NewsCategoryItemViewModel
{
    public required string Name { get; init; }
    public required int Count { get; init; }
}
