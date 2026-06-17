namespace STEM.Web.Models;

public class NewsDetailViewModel
{
    public required NewsArticleCardViewModel Article { get; init; }
    public string HeroImageUrl { get; init; } = string.Empty;

    public string ContentHtml { get; init; } = string.Empty;

    public string AuthorBio { get; init; } = string.Empty;
    public required IReadOnlyList<NewsArticleCardViewModel> FeaturedArticles { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }
    public required IReadOnlyList<NewsArticleCardViewModel> RelatedArticles { get; init; }
}
