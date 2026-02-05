using System.Text.RegularExpressions;
using DiagnosticCollector.Models;

namespace DiagnosticCollector.Collectors;

public class AspNetCoreCollector : ICollector
{
    private readonly string _aspNetCorePath;

    private static readonly (string Prefix, string Description, string Pattern, string[] SearchPaths)[] PrefixConfigs = new[]
    {
        ("ASP", "ASP.NET Core framework analyzers", @"^ASP\d{4}$", new[] { "src/Framework/AspNetCoreAnalyzers" }),
        ("BL", "Blazor component diagnostics", @"^BL\d{4}$", new[] { "src/Components/Analyzers" }),
        ("MVC", "MVC analyzers", @"^MVC\d{4}$", new[] { "src/Mvc/Mvc.Analyzers" }),
        ("API", "MVC API analyzers", @"^API\d{4}$", new[] { "src/Mvc/Mvc.Api.Analyzers" }),
        ("RDG", "Request Delegate Generator diagnostics", @"^RDG\d{3}$", new[] { "src/Http/Http.Extensions/gen" }),
        ("SSG", "SignalR Source Generator diagnostics", @"^SSG\d{4}$", new[] { "src/SignalR/clients/csharp/Client.SourceGenerator" }),
    };

    public AspNetCoreCollector(string aspNetCorePath)
    {
        _aspNetCorePath = aspNetCorePath;
    }

    public Task<IReadOnlyList<CollectorResult>> CollectAsync()
    {
        var results = new List<CollectorResult>();

        foreach (var (prefix, description, pattern, searchPaths) in PrefixConfigs)
        {
            var diagnostics = new List<DiagnosticInfo>();

            foreach (var searchPath in searchPaths)
            {
                var fullPath = Path.Combine(_aspNetCorePath, searchPath);
                if (!Directory.Exists(fullPath))
                    continue;

                var files = Directory.GetFiles(fullPath, "*.cs", SearchOption.AllDirectories)
                    .Where(f => f.Contains("Diagnostic", StringComparison.OrdinalIgnoreCase));

                foreach (var file in files)
                {
                    var fileDiagnostics = ParseDiagnosticDescriptors(file, prefix);
                    diagnostics.AddRange(fileDiagnostics);
                }
            }

            if (diagnostics.Count > 0)
            {
                // Deduplicate and sort
                diagnostics = diagnostics
                    .GroupBy(d => d.Id)
                    .Select(g => g.First())
                    .OrderBy(d => d.Id)
                    .ToList();

                Console.WriteLine($"    Found {diagnostics.Count} {prefix} diagnostics");

                results.Add(new CollectorResult(
                    Prefix: prefix,
                    Repo: "aspnetcore",
                    Description: description,
                    Pattern: pattern,
                    Diagnostics: diagnostics
                ));
            }
        }

        return Task.FromResult<IReadOnlyList<CollectorResult>>(results);
    }

    private static List<DiagnosticInfo> ParseDiagnosticDescriptors(string path, string prefix)
    {
        var result = new List<DiagnosticInfo>();
        var content = File.ReadAllText(path);

        // Match diagnostic ID patterns like: "ASP0001" or Prefix + "0001"
        var idPattern = $@"""{prefix}(\d+)""";
        var idRegex = new Regex(idPattern);

        // Also look for const definitions like: public const string DiagnosticId = "ASP0001";
        var constPattern = $@"const\s+string\s+(\w+)\s*=\s*""{prefix}(\d+)""";
        var constRegex = new Regex(constPattern);

        var foundIds = new HashSet<string>();

        // Find const definitions first (they have names)
        foreach (Match match in constRegex.Matches(content))
        {
            var name = match.Groups[1].Value;
            var number = match.Groups[2].Value;
            var id = $"{prefix}{number}";
            
            if (foundIds.Add(id))
            {
                result.Add(new DiagnosticInfo
                {
                    Id = id,
                    Category = "Analyzer",
                    Name = name,
                    HelpUrl = GetHelpUrl(prefix, id)
                });
            }
        }

        // Find any other ID references
        foreach (Match match in idRegex.Matches(content))
        {
            var number = match.Groups[1].Value;
            var id = $"{prefix}{number}";
            
            if (foundIds.Add(id))
            {
                result.Add(new DiagnosticInfo
                {
                    Id = id,
                    Category = "Analyzer",
                    HelpUrl = GetHelpUrl(prefix, id)
                });
            }
        }

        return result;
    }

    private static string? GetHelpUrl(string prefix, string id)
    {
        return prefix switch
        {
            "RDG" => $"https://learn.microsoft.com/aspnet/core/fundamentals/aot/request-delegate-generator/diagnostics/{id.ToLowerInvariant()}",
            _ => "https://aka.ms/aspnet/analyzers"
        };
    }
}
