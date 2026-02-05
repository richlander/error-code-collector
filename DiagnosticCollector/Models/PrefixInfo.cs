namespace DiagnosticCollector.Models;

public class PrefixInfo
{
    public required string File { get; set; }
    public required string Repo { get; set; }
    public required string Pattern { get; set; }
    public int Count { get; set; }
    public required string Description { get; set; }
}
