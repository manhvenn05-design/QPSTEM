namespace STEM.Web.Models;

public class NewsDetailViewModel
{
    public required NewsArticleCardViewModel Article { get; init; }
    public required string HeroImageUrl { get; init; }
    public required IReadOnlyList<NewsArticleSectionViewModel> Sections { get; init; }
    public required IReadOnlyList<string> KeyPoints { get; init; }
    public required string AuthorBio { get; init; }
    public required IReadOnlyList<NewsSidebarItemViewModel> FeaturedArticles { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }
    public required IReadOnlyList<NewsArticleCardViewModel> RelatedArticles { get; init; }
}

public class NewsArticleSectionViewModel
{
    public required string Heading { get; init; }
    public required IReadOnlyList<string> Paragraphs { get; init; }
}
