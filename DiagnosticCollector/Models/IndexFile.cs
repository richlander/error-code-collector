namespace DiagnosticCollector.Models;

public class IndexFile
{
    public string Version { get; set; } = "1.0";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, PrefixInfo> Prefixes { get; set; } = new();
}
