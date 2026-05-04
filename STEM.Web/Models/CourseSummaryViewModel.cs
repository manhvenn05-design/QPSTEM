namespace STEM.Web.Models;

public class CourseSummaryViewModel
{
    public required int Id { get; init; }
    public required string Slug { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required string ImageUrl { get; init; }
    public required string Category { get; init; }
    public required string Level { get; init; }
    public required string DurationText { get; init; }
    public required string PriceText { get; init; }
}
