using System.Text.RegularExpressions;
using DiagnosticCollector.Models;

namespace DiagnosticCollector.Collectors;

public class EfCoreCollector : ICollector
{
    private readonly string _efCorePath;

    public EfCoreCollector(string efCorePath)
    {
        _efCorePath = efCorePath;
    }

    public Task<IReadOnlyList<CollectorResult>> CollectAsync()
    {
        var diagnostics = new List<DiagnosticInfo>();

        // Parse EFDiagnostics.cs for diagnostic IDs
        var efDiagnosticsFile = Path.Combine(_efCorePath, "src", "Shared", "EFDiagnostics.cs");
        if (File.Exists(efDiagnosticsFile))
        {
            Console.WriteLine("  Parsing EFDiagnostics.cs...");
            var diags = ParseEfDiagnostics(efDiagnosticsFile);
            diagnostics.AddRange(diags);
        }

        // Also check AnalyzerReleases for any we might have missed
        var releasesFile = Path.Combine(_efCorePath, "src", "EFCore.Analyzers", "AnalyzerReleases.Shipped.md");
        if (File.Exists(releasesFile))
        {
            Console.WriteLine("  Parsing AnalyzerReleases.Shipped.md...");
            var releasesDiags = ParseAnalyzerReleases(releasesFile);
            MergeDiagnostics(diagnostics, releasesDiags);
        }

        // Deduplicate and sort
        diagnostics = diagnostics
            .GroupBy(d => d.Id)
            .Select(g => g.First())
            .OrderBy(d => d.Id)
            .ToList();

        Console.WriteLine($"    Found {diagnostics.Count} EF diagnostics");

        var result = new CollectorResult(
            Prefix: "EF",
            Repo: "efcore",
            Description: "Entity Framework Core diagnostics",
            Pattern: @"^EF\d{4}$",
            Diagnostics: diagnostics
        );

        return Task.FromResult<IReadOnlyList<CollectorResult>>(new[] { result });
    }

    private static List<DiagnosticInfo> ParseEfDiagnostics(string path)
    {
        var result = new List<DiagnosticInfo>();
        var content = File.ReadAllText(path);

        // Match patterns like: internal const string InternalUsage = "EF1001";
        var regex = new Regex(@"internal const string (\w+)\s*=\s*""(EF\d{4})""");

        foreach (Match match in regex.Matches(content))
        {
            var name = match.Groups[1].Value;
            var id = match.Groups[2].Value;

            // Determine category based on ID range
            var number = int.Parse(id.Substring(2));
            var category = number switch
            {
                >= 1000 and < 8000 => "Analyzer",
                >= 8000 and < 9000 => "Obsoletion",
                >= 9000 => "Experimental",
                _ => "Unknown"
            };

            result.Add(new DiagnosticInfo
            {
                Id = id,
                Category = category,
                Name = name,
                HelpUrl = $"https://learn.microsoft.com/ef/core/what-is-new/ef-core-9.0/breaking-changes"
            });
        }

        return result;
    }

    private static List<DiagnosticInfo> ParseAnalyzerReleases(string path)
    {
        var result = new List<DiagnosticInfo>();
        var content = File.ReadAllText(path);

        // Match patterns like: EF1001 | ... | InternalUsageDiagnosticAnalyzer
        var regex = new Regex(@"^(EF\d{4})\s*\|", RegexOptions.Multiline);

        foreach (Match match in regex.Matches(content))
        {
            var id = match.Groups[1].Value;
            result.Add(new DiagnosticInfo
            {
                Id = id,
                Category = "Analyzer"
            });
        }

        return result;
    }

    private static void MergeDiagnostics(List<DiagnosticInfo> target, List<DiagnosticInfo> source)
    {
        var existing = target.Select(d => d.Id).ToHashSet();
        foreach (var diag in source)
        {
            if (!existing.Contains(diag.Id))
            {
                target.Add(diag);
            }
        }
    }
}
