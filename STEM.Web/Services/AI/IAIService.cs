namespace STEM.Web.Services.AI;

public interface IAIService
{
    Task<AIRequestResult<RefineNoteResult>> RefineTeacherNoteAsync(
        string rawNote, CancellationToken ct = default);

    Task<AIRequestResult<VideoAnalysisResult>> AnalyzePresentationVideoAsync(
        string absoluteFilePath, string mimeType, CancellationToken ct = default);
}
