namespace DiagnosticCollector.Models;

public class DiagnosticInfo
{
    public required string Id { get; set; }
    public string? Category { get; set; }
    public string? Name { get; set; }
    public string? Message { get; set; }
    public string? Url { get; set; }        // Raw markdown URL (best for LLM)
    public string? ErrorUrl { get; set; }   // URL from error message (aka.ms, learn.microsoft.com)
}
