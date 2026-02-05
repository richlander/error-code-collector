namespace DiagnosticCollector.Models;

public class DiagnosticInfo
{
    public required string Id { get; set; }
    public string? Category { get; set; }
    public string? Name { get; set; }
    public string? Message { get; set; }
    public string? HelpUrl { get; set; }
    public string? LongUrl { get; set; }
}
