namespace DiagnosticCollector.Models;

public class PrefixFile
{
    public required string Prefix { get; set; }
    public required string Repo { get; set; }
    public required string Description { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public List<DiagnosticInfo> Diagnostics { get; set; } = new();
}
