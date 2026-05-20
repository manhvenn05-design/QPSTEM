using System.Text;
using System.Text.Json;

namespace STEM.Web.Services;

public static class AiEvaluationFormatter
{
    public static ParsedAiEvaluation? Parse(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            return Parse(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static ParsedAiEvaluation? Parse(JsonElement element)
    {
        var source = element;
        if (TryGetProperty(source, out var nested, "result", "Result", "data", "Data"))
        {
            source = nested;
        }

        var score = GetString(source, "score", "Score");
        var strengths = GetStringArray(source, "strengths", "Strengths");
        var weaknesses = GetStringArray(source, "weaknesses", "Weaknesses");
        var suggestion = GetString(source, "suggestion", "Suggestion");

        if (string.IsNullOrWhiteSpace(score) &&
            strengths.Count == 0 &&
            weaknesses.Count == 0 &&
            string.IsNullOrWhiteSpace(suggestion))
        {
            return null;
        }

        return new ParsedAiEvaluation
        {
            Score = score ?? string.Empty,
            Strengths = strengths,
            Weaknesses = weaknesses,
            Suggestion = suggestion ?? string.Empty
        };
    }

    public static string FormatForDisplay(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return string.Empty;
        }

        var parsed = Parse(rawJson);
        if (parsed == null)
        {
            return rawJson;
        }

        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(parsed.Score))
        {
            builder.AppendLine($"Điểm đánh giá: {parsed.Score}");
            builder.AppendLine();
        }

        if (parsed.Strengths.Count > 0)
        {
            builder.AppendLine("Điểm tốt:");
            foreach (var item in parsed.Strengths)
            {
                builder.AppendLine($"- {item}");
            }

            builder.AppendLine();
        }

        if (parsed.Weaknesses.Count > 0)
        {
            builder.AppendLine("Cần cải thiện:");
            foreach (var item in parsed.Weaknesses)
            {
                builder.AppendLine($"- {item}");
            }

            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(parsed.Suggestion))
        {
            builder.AppendLine("Gợi ý phản hồi:");
            builder.AppendLine(parsed.Suggestion);
        }

        return builder.ToString().Trim();
    }

    private static bool TryGetProperty(JsonElement element, out JsonElement value, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? GetString(JsonElement element, params string[] names)
    {
        return TryGetProperty(element, out var value, names) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static List<string> GetStringArray(JsonElement element, params string[] names)
    {
        if (!TryGetProperty(element, out var value, names) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList();
    }
}

public sealed class ParsedAiEvaluation
{
    public string Score { get; init; } = string.Empty;
    public List<string> Strengths { get; init; } = [];
    public List<string> Weaknesses { get; init; } = [];
    public string Suggestion { get; init; } = string.Empty;
}
