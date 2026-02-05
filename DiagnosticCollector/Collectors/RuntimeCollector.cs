using System.Text.RegularExpressions;
using DiagnosticCollector.Models;

namespace DiagnosticCollector.Collectors;

public class RuntimeCollector : ICollector
{
    private readonly string _runtimePath;

    public RuntimeCollector(string runtimePath)
    {
        _runtimePath = runtimePath;
    }

    public Task<IReadOnlyList<CollectorResult>> CollectAsync()
    {
        var diagnostics = new List<DiagnosticInfo>();

        // Parse list-of-diagnostics.md as the authoritative source
        var listOfDiagnostics = Path.Combine(_runtimePath, "docs", "project", "list-of-diagnostics.md");
        if (File.Exists(listOfDiagnostics))
        {
            Console.WriteLine("  Parsing list-of-diagnostics.md...");
            var mdDiagnostics = ParseDiagnosticsMarkdown(listOfDiagnostics);
            Console.WriteLine($"    Found {mdDiagnostics.Count} diagnostics from markdown");
            diagnostics.AddRange(mdDiagnostics);
        }

        // Supplement with Obsoletions.cs for additional message details
        var obsoletionsFile = Path.Combine(_runtimePath, "src", "libraries", "Common", "src", "System", "Obsoletions.cs");
        if (File.Exists(obsoletionsFile))
        {
            Console.WriteLine("  Parsing Obsoletions.cs...");
            var obsoletions = ParseObsoletions(obsoletionsFile);
            Console.WriteLine($"    Found {obsoletions.Count} obsoletions");
            MergeMessages(diagnostics, obsoletions);
        }

        // Supplement with Experimentals.cs
        var experimentalsFile = Path.Combine(_runtimePath, "src", "libraries", "Common", "src", "System", "Experimentals.cs");
        if (File.Exists(experimentalsFile))
        {
            Console.WriteLine("  Parsing Experimentals.cs...");
            var experimentals = ParseExperimentals(experimentalsFile);
            Console.WriteLine($"    Found {experimentals.Count} experimentals");
            MergeMessages(diagnostics, experimentals);
        }

        // Sort by ID
        diagnostics = diagnostics.OrderBy(d => d.Id).ToList();

        var result = new CollectorResult(
            Prefix: "SYSLIB",
            Repo: "runtime",
            Description: "Runtime obsoletions, analyzers, and experimental APIs",
            Pattern: @"^SYSLIB\d{4}$",
            Diagnostics: diagnostics
        );

        return Task.FromResult<IReadOnlyList<CollectorResult>>(new[] { result });
    }

    private static List<DiagnosticInfo> ParseDiagnosticsMarkdown(string path)
    {
        var result = new List<DiagnosticInfo>();
        var content = File.ReadAllText(path);

        // Match table rows like: |  __`SYSLIB0001`__ | The UTF-7 encoding... |
        var tableRegex = new Regex(@"\|\s*__`(SYSLIB\d{4})`__\s*\|\s*(.+?)\s*\|", RegexOptions.Multiline);
        
        foreach (Match match in tableRegex.Matches(content))
        {
            var id = match.Groups[1].Value;
            var description = match.Groups[2].Value.Trim();

            // Skip reserved/placeholder entries
            if (description.StartsWith("_") && description.EndsWith("_"))
                continue;

            // Determine category based on ID range
            var number = int.Parse(id.Substring(6));
            var category = number switch
            {
                >= 1 and <= 999 => "Obsoletion",
                >= 1001 and <= 1999 => "Analyzer",
                >= 5001 and <= 5999 => "Experimental",
                _ => "Unknown"
            };

            result.Add(new DiagnosticInfo
            {
                Id = id,
                Category = category,
                Message = description,
                Url = $"https://raw.githubusercontent.com/dotnet/docs/main/docs/fundamentals/syslib-diagnostics/{id.ToLowerInvariant()}.md",
                ErrorUrl = $"https://aka.ms/dotnet-warnings/{id.ToLowerInvariant()}"
            });
        }

        return result;
    }

    private static Dictionary<string, (string Name, string? Message)> ParseObsoletions(string path)
    {
        var result = new Dictionary<string, (string, string?)>();
        var content = File.ReadAllText(path);

        // Match patterns like: internal const string SystemTextEncodingUTF7DiagId = "SYSLIB0001";
        var diagIdRegex = new Regex(@"internal const string (\w+)DiagId = ""(SYSLIB\d{4})"";");
        var messageRegex = new Regex(@"internal const string (\w+)Message = ""(.+?)"";");

        var messages = new Dictionary<string, string>();
        foreach (Match match in messageRegex.Matches(content))
        {
            messages[match.Groups[1].Value] = match.Groups[2].Value;
        }

        foreach (Match match in diagIdRegex.Matches(content))
        {
            var name = match.Groups[1].Value;
            var id = match.Groups[2].Value;
            messages.TryGetValue(name, out var message);
            result[id] = (name, message);
        }

        return result;
    }

    private static Dictionary<string, (string Name, string? Message)> ParseExperimentals(string path)
    {
        var result = new Dictionary<string, (string, string?)>();
        var content = File.ReadAllText(path);

        // Match patterns like: internal const string TensorTDiagId = "SYSLIB5001";
        var diagIdRegex = new Regex(@"internal const string (\w+)DiagId = ""(SYSLIB\d{4})"";");

        foreach (Match match in diagIdRegex.Matches(content))
        {
            var name = match.Groups[1].Value;
            var id = match.Groups[2].Value;
            result[id] = (name, null);
        }

        return result;
    }

    private static void MergeMessages(List<DiagnosticInfo> diagnostics, Dictionary<string, (string Name, string? Message)> source)
    {
        var lookup = diagnostics.ToDictionary(d => d.Id);
        
        foreach (var (id, (name, message)) in source)
        {
            if (lookup.TryGetValue(id, out var existing))
            {
                existing.Name ??= name;
                if (message != null && (existing.Message == null || existing.Message.Length < message.Length))
                {
                    existing.Message = message;
                }
            }
        }
    }
}
