namespace STEM.Web.Services.AI;

public class GoogleAiOptions
{
    public const string Position = "GoogleAI";

    public string ApiKey { get; set; } = string.Empty;
    public string TextModel { get; set; } = "gemini-2.0-flash";
    public int RequestTimeoutSeconds { get; set; } = 90;
    public int MaxRetryCount { get; set; } = 3;
}
