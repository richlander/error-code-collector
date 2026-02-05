using System.Text.RegularExpressions;
using DiagnosticCollector.Models;

namespace DiagnosticCollector.Collectors;

public class AspireCollector : ICollector
{
    private readonly string _aspirePath;

    public AspireCollector(string aspirePath)
    {
        _aspirePath = aspirePath;
    }

    public Task<IReadOnlyList<CollectorResult>> CollectAsync()
    {
        var diagnostics = new List<DiagnosticInfo>();

        // Search for Diagnostics.cs files in analyzers
        var analyzersDir = Path.Combine(_aspirePath, "src");
        if (Directory.Exists(analyzersDir))
        {
            Console.WriteLine("  Searching for Aspire diagnostics...");
            var diagnosticFiles = Directory.GetFiles(analyzersDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => f.Contains("Diagnostic", StringComparison.OrdinalIgnoreCase));

            foreach (var file in diagnosticFiles)
            {
                var fileDiags = ParseDiagnosticsFile(file);
                diagnostics.AddRange(fileDiags);
            }
        }

        // Deduplicate and sort
        diagnostics = diagnostics
            .GroupBy(d => d.Id)
            .Select(g => g.First())
            .OrderBy(d => d.Id)
            .ToList();

        Console.WriteLine($"    Found {diagnostics.Count} ASPIRE diagnostics");

        if (diagnostics.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<CollectorResult>>(Array.Empty<CollectorResult>());
        }

        var result = new CollectorResult(
            Prefix: "ASPIRE",
            Repo: "aspire",
            Description: "Aspire hosting and configuration diagnostics",
            Pattern: @"^ASPIRE\d{3}$",
            Diagnostics: diagnostics
        );

        return Task.FromResult<IReadOnlyList<CollectorResult>>(new[] { result });
    }

    private static List<DiagnosticInfo> ParseDiagnosticsFile(string path)
    {
        var result = new List<DiagnosticInfo>();
        var content = File.ReadAllText(path);

        // Match patterns like: "ASPIRE006" or const string ... = "ASPIRE007"
        var regex = new Regex(@"""(ASPIRE\d{3})""");

        foreach (Match match in regex.Matches(content))
        {
            var id = match.Groups[1].Value;
            result.Add(new DiagnosticInfo
            {
                Id = id,
                Category = "Analyzer",
                Url = $"https://raw.githubusercontent.com/microsoft/aspire.dev/main/src/frontend/src/content/docs/diagnostics/{id.ToLowerInvariant()}.mdx",
                ErrorUrl = $"https://aka.ms/aspire/diagnostics/{id}"
            });
        }

        return result;
    }
}
