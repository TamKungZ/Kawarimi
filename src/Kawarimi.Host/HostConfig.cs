using System.Text.Json;

namespace Kawarimi.Host;

public sealed record HostConfig(string Strategy = "lookup", bool Trace = false, string Online = "visual")
{
    public int OnlineValue => Online.ToLowerInvariant() switch
    {
        "off" => 0,
        "all" => 2,
        _ => 1,
    };

    public int StrategyValue => string.Equals(Strategy, "record", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

    public static HostConfig Parse(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        var r = doc.RootElement;
        var c = new HostConfig();
        if (r.ValueKind != JsonValueKind.Object) return c;
        if (r.TryGetProperty("strategy", out var s) && s.ValueKind == JsonValueKind.String) c = c with { Strategy = s.GetString() ?? "lookup" };
        if (r.TryGetProperty("trace", out var t) && t.ValueKind is JsonValueKind.True or JsonValueKind.False) c = c with { Trace = t.GetBoolean() };
        if (r.TryGetProperty("online", out var o) && o.ValueKind == JsonValueKind.String) c = c with { Online = o.GetString() ?? "visual" };
        return c;
    }

    public static HostConfig Load(string path, Action<string> log)
    {
        if (!File.Exists(path)) return new HostConfig();
        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            log("config.json ignored: " + e.Message);
            return new HostConfig();
        }
    }
}
