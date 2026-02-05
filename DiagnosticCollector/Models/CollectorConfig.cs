using System.Text.Json.Serialization;

namespace DiagnosticCollector.Models;

public class CollectorConfig
{
    [JsonPropertyName("repos")]
    public Dictionary<string, RepoConfig> Repos { get; set; } = new();
}

public class RepoConfig
{
    [JsonPropertyName("prefixes")]
    public Dictionary<string, PrefixConfig> Prefixes { get; set; } = new();
}

public class PrefixConfig
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    [JsonPropertyName("errorUrl")]
    public string? ErrorUrl { get; set; }

    [JsonPropertyName("markdownUrl")]
    public string? MarkdownUrl { get; set; }

    [JsonPropertyName("indexUrl")]
    public string? IndexUrl { get; set; }

    public string? GetErrorUrl(string id) =>
        ErrorUrl?.Replace("{id}", id.ToLowerInvariant());

    public string? GetUrl(string id) =>
        MarkdownUrl?.Replace("{id}", id.ToLowerInvariant());
}
