using System.Text.Json;

namespace STEM.Web.Services.AI;

public class AIRequestResult<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }

    public static AIRequestResult<T> Ok(T data) => new() { Success = true, Data = data };
    public static AIRequestResult<T> Fail(string message) => new() { Success = false, Message = message };
}

public class RefineNoteResult
{
    public string Suggestion { get; set; } = string.Empty;
}

public class VideoAnalysisResult
{
    public string Score { get; set; } = string.Empty;
    public List<string> Strengths { get; set; } = new();
    public List<string> Weaknesses { get; set; } = new();
    public string Suggestion { get; set; } = string.Empty;

    public static VideoAnalysisResult FromJson(JsonElement element)
    {
        var result = new VideoAnalysisResult();
        
        if (element.TryGetProperty("score", out var scoreProp))
            result.Score = scoreProp.GetString() ?? "";
            
        if (element.TryGetProperty("suggestion", out var sugProp))
            result.Suggestion = sugProp.GetString() ?? "";
            
        if (element.TryGetProperty("strengths", out var strProp) && strProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in strProp.EnumerateArray())
                if (item.GetString() is string s) result.Strengths.Add(s);
        }
        
        if (element.TryGetProperty("weaknesses", out var weakProp) && weakProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in weakProp.EnumerateArray())
                if (item.GetString() is string w) result.Weaknesses.Add(w);
        }
        
        return result;
    }
}
