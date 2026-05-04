namespace STEM.Web.Models;

public class CourseDetailViewModel
{
    public required CourseSummaryViewModel Course { get; init; }
    public required string HeroImageUrl { get; init; }
    public required string BreadcrumbTitle { get; init; }
    public required string SchoolLevel { get; init; }
    public required string LessonCount { get; init; }
    public required string PriceText { get; init; }
    public required string InstructorName { get; init; }
    public required string InstructorTitle { get; init; }
    public required string InstructorBio { get; init; }
    public required string InstructorImageUrl { get; init; }
    public required IReadOnlyList<CourseBenefitViewModel> Benefits { get; init; }
    public required IReadOnlyList<string> CurriculumItems { get; init; }
    public required IReadOnlyList<CourseSummaryViewModel> RelatedCourses { get; init; }
}

public class CourseBenefitViewModel
{
    public required string Icon { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
}
