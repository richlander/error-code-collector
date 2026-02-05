using System.Text.RegularExpressions;
using System.Xml.Linq;
using DiagnosticCollector.Models;

namespace DiagnosticCollector.Collectors;

public class SdkCollector : ICollector
{
    private readonly string _sdkPath;

    public SdkCollector(string sdkPath)
    {
        _sdkPath = sdkPath;
    }

    public Task<IReadOnlyList<CollectorResult>> CollectAsync()
    {
        var results = new List<CollectorResult>();

        // Collect NETSDK diagnostics
        var netsdkDiagnostics = CollectNetSdk();
        if (netsdkDiagnostics.Count > 0)
        {
            results.Add(new CollectorResult(
                Prefix: "NETSDK",
                Repo: "sdk",
                Description: "SDK build and restore errors",
                Pattern: @"^NETSDK\d{4}$",
                Diagnostics: netsdkDiagnostics
            ));
        }

        // Collect CA diagnostics
        var caDiagnostics = CollectCodeAnalysis();
        if (caDiagnostics.Count > 0)
        {
            results.Add(new CollectorResult(
                Prefix: "CA",
                Repo: "sdk",
                Description: "Code analysis quality rules",
                Pattern: @"^CA\d{4}$",
                Diagnostics: caDiagnostics
            ));
        }

        return Task.FromResult<IReadOnlyList<CollectorResult>>(results);
    }

    private List<DiagnosticInfo> CollectNetSdk()
    {
        var result = new List<DiagnosticInfo>();
        var stringsFile = Path.Combine(_sdkPath, "src", "Tasks", "Common", "Resources", "Strings.resx");

        if (!File.Exists(stringsFile))
        {
            Console.WriteLine($"  Warning: Strings.resx not found at {stringsFile}");
            return result;
        }

        Console.WriteLine("  Parsing SDK Strings.resx...");
        var doc = XDocument.Load(stringsFile);
        
        // Pattern: NETSDK1001: message text
        var netsdkRegex = new Regex(@"^(NETSDK\d{4}):\s*(.+)$");

        foreach (var data in doc.Descendants("data"))
        {
            var value = data.Element("value")?.Value;
            if (value == null) continue;

            var match = netsdkRegex.Match(value);
            if (match.Success)
            {
                var id = match.Groups[1].Value;
                var message = match.Groups[2].Value;

                result.Add(new DiagnosticInfo
                {
                    Id = id,
                    Category = "Build",
                    Message = message,
                    Url = $"https://raw.githubusercontent.com/dotnet/docs/main/docs/core/tools/sdk-errors/{id.ToLowerInvariant()}.md",
                    ErrorUrl = $"https://learn.microsoft.com/dotnet/core/tools/sdk-errors/{id.ToLowerInvariant()}"
                });
            }
        }

        // Deduplicate by ID (keep first occurrence)
        result = result.GroupBy(d => d.Id).Select(g => g.First()).OrderBy(d => d.Id).ToList();
        Console.WriteLine($"    Found {result.Count} NETSDK diagnostics");

        return result;
    }

    private List<DiagnosticInfo> CollectCodeAnalysis()
    {
        var result = new List<DiagnosticInfo>();

        // Look for AnalyzerReleases.Shipped.md files
        var analyzersDir = Path.Combine(_sdkPath, "src", "Microsoft.CodeAnalysis.NetAnalyzers");
        if (!Directory.Exists(analyzersDir))
        {
            Console.WriteLine($"  Warning: NetAnalyzers directory not found at {analyzersDir}");
            return result;
        }

        Console.WriteLine("  Parsing AnalyzerReleases files...");
        var shippedFiles = Directory.GetFiles(analyzersDir, "AnalyzerReleases.Shipped.md", SearchOption.AllDirectories);

        foreach (var file in shippedFiles)
        {
            var diagnostics = ParseAnalyzerReleases(file);
            result.AddRange(diagnostics);
        }

        // Deduplicate by ID
        result = result.GroupBy(d => d.Id).Select(g => g.First()).OrderBy(d => d.Id).ToList();
        Console.WriteLine($"    Found {result.Count} CA diagnostics");

        return result;
    }

    private static List<DiagnosticInfo> ParseAnalyzerReleases(string path)
    {
        var result = new List<DiagnosticInfo>();
        var content = File.ReadAllText(path);

        // Match table rows like: CA1000 | Design | Hidden | DoNotDeclareStaticMembersOnGenericTypes, [Documentation](url)
        var ruleRegex = new Regex(@"^(CA\d{4})\s*\|\s*(\w+)\s*\|\s*(\w+)\s*\|\s*(\w+)", RegexOptions.Multiline);

        foreach (Match match in ruleRegex.Matches(content))
        {
            var id = match.Groups[1].Value;
            var category = match.Groups[2].Value;
            var severity = match.Groups[3].Value;
            var name = match.Groups[4].Value;

            result.Add(new DiagnosticInfo
            {
                Id = id,
                Category = category,
                Name = name,
                Url = $"https://raw.githubusercontent.com/dotnet/docs/main/docs/fundamentals/code-analysis/quality-rules/{id.ToLowerInvariant()}.md",
                ErrorUrl = $"https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/{id.ToLowerInvariant()}"
            });
        }

        return result;
    }
}
