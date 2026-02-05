using System.Text.RegularExpressions;
using DiagnosticCollector.Models;

namespace DiagnosticCollector.Collectors;

public class ExtensionsCollector : ICollector
{
    private readonly string _extensionsPath;

    private static readonly (string Prefix, string Description, string Pattern)[] PrefixConfigs = new[]
    {
        ("LOGGEN", "LoggerMessage source generator diagnostics", @"^LOGGEN\d{3}$"),
        ("METGEN", "Metrics source generator diagnostics", @"^METGEN\d{3}$"),
        ("CTXOPTGEN", "Contextual options generator diagnostics", @"^CTXOPTGEN\d{3}$"),
        ("EA", "Extra analyzers", @"^EA\d{4}$"),
        ("LA", "Local analyzers", @"^LA\d{4}$"),
        ("EXTEXP", "Extensions experimental APIs", @"^EXTEXP\d{4}$"),
        ("EXTOBS", "Extensions obsoletions", @"^EXTOBS\d{4}$"),
        ("MEAI", "Microsoft Extensions AI experimental", @"^MEAI\d{3}$"),
    };

    public ExtensionsCollector(string extensionsPath)
    {
        _extensionsPath = extensionsPath;
    }

    public Task<IReadOnlyList<CollectorResult>> CollectAsync()
    {
        var results = new List<CollectorResult>();

        // First try to parse the central DiagnosticIds.cs
        var diagnosticIdsFile = Path.Combine(_extensionsPath, "src", "Shared", "DiagnosticIds", "DiagnosticIds.cs");
        var allDiagnostics = new List<DiagnosticInfo>();
        
        if (File.Exists(diagnosticIdsFile))
        {
            Console.WriteLine("  Parsing DiagnosticIds.cs...");
            allDiagnostics = ParseDiagnosticIds(diagnosticIdsFile);
        }
        else
        {
            // Search for DiagDescriptors.cs files
            Console.WriteLine("  Searching for DiagDescriptors files...");
            var srcDir = Path.Combine(_extensionsPath, "src");
            if (Directory.Exists(srcDir))
            {
                var files = Directory.GetFiles(srcDir, "DiagDescriptors.cs", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var diags = ParseDiagDescriptors(file);
                    allDiagnostics.AddRange(diags);
                }
            }
        }

        // Group by prefix
        foreach (var (prefix, description, pattern) in PrefixConfigs)
        {
            var prefixDiags = allDiagnostics
                .Where(d => d.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .GroupBy(d => d.Id)
                .Select(g => g.First())
                .OrderBy(d => d.Id)
                .ToList();

            if (prefixDiags.Count > 0)
            {
                Console.WriteLine($"    Found {prefixDiags.Count} {prefix} diagnostics");
                results.Add(new CollectorResult(
                    Prefix: prefix,
                    Repo: "extensions",
                    Description: description,
                    Pattern: pattern,
                    Diagnostics: prefixDiags
                ));
            }
        }

        return Task.FromResult<IReadOnlyList<CollectorResult>>(results);
    }

    private static List<DiagnosticInfo> ParseDiagnosticIds(string path)
    {
        var result = new List<DiagnosticInfo>();
        var content = File.ReadAllText(path);

        // Match patterns like: 
        // internal const string LOGGEN000 = nameof(LOGGEN000);
        // internal const string Resilience = "EXTEXP0001";
        var nameofRegex = new Regex(@"internal const string (\w+)\s*=\s*nameof\((\w+)\)");
        var stringRegex = new Regex(@"internal const string (\w+)\s*=\s*""(\w+\d+)""");

        foreach (Match match in nameofRegex.Matches(content))
        {
            var name = match.Groups[1].Value;
            var id = match.Groups[2].Value;

            result.Add(new DiagnosticInfo
            {
                Id = id,
                Name = name,
                Category = GetCategory(id),
                Url = "https://raw.githubusercontent.com/dotnet/extensions/main/docs/list-of-diagnostics.md",
                ErrorUrl = $"https://aka.ms/dotnet-extensions-warnings/{id}"
            });
        }

        foreach (Match match in stringRegex.Matches(content))
        {
            var name = match.Groups[1].Value;
            var id = match.Groups[2].Value;

            // Skip URL format constant
            if (name == "UrlFormat") continue;

            result.Add(new DiagnosticInfo
            {
                Id = id,
                Name = name,
                Category = GetCategory(id),
                Url = "https://raw.githubusercontent.com/dotnet/extensions/main/docs/list-of-diagnostics.md",
                ErrorUrl = $"https://aka.ms/dotnet-extensions-warnings/{id}"
            });
        }

        return result;
    }

    private static List<DiagnosticInfo> ParseDiagDescriptors(string path)
    {
        var result = new List<DiagnosticInfo>();
        var content = File.ReadAllText(path);

        // Match diagnostic IDs in various formats
        var regex = new Regex(@"""((?:LOGGEN|METGEN|CTXOPTGEN|EA|LA|EXTEXP|EXTOBS)\d+)""");

        foreach (Match match in regex.Matches(content))
        {
            var id = match.Groups[1].Value;
            result.Add(new DiagnosticInfo
            {
                Id = id,
                Category = GetCategory(id),
                Url = "https://raw.githubusercontent.com/dotnet/extensions/main/docs/list-of-diagnostics.md",
                ErrorUrl = $"https://aka.ms/dotnet-extensions-warnings/{id}"
            });
        }

        return result;
    }

    private static string GetCategory(string id)
    {
        if (id.StartsWith("EXTEXP")) return "Experimental";
        if (id.StartsWith("EXTOBS")) return "Obsoletion";
        return "Analyzer";
    }
}
