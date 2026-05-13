namespace STEM.Web.Services.AI;

public interface IAIService
{
    Task<AIRequestResult<RefineNoteResult>> RefineTeacherNoteAsync(string rawNote);
    Task<AIRequestResult<VideoAnalysisResult>> AnalyzePresentationVideoAsync(string absoluteFilePath, string mimeType);
}
