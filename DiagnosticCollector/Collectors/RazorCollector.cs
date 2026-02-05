using System.Text.RegularExpressions;
using DiagnosticCollector.Models;

namespace DiagnosticCollector.Collectors;

public class RazorCollector : ICollector
{
    private readonly string _razorPath;

    public RazorCollector(string razorPath)
    {
        _razorPath = razorPath;
    }

    public Task<IReadOnlyList<CollectorResult>> CollectAsync()
    {
        var diagnostics = new List<DiagnosticInfo>();

        // Parse RazorDiagnosticFactory.cs for RZ codes
        var razorDiagFactory = Path.Combine(_razorPath, "src", "Compiler", 
            "Microsoft.CodeAnalysis.Razor.Compiler", "src", "Language", "RazorDiagnosticFactory.cs");
        if (File.Exists(razorDiagFactory))
        {
            Console.WriteLine("  Parsing RazorDiagnosticFactory.cs...");
            var diags = ParseDiagnosticFactory(razorDiagFactory);
            diagnostics.AddRange(diags);
        }

        // Parse ComponentDiagnosticFactory.cs
        var componentDiagFactory = Path.Combine(_razorPath, "src", "Compiler",
            "Microsoft.CodeAnalysis.Razor.Compiler", "src", "Language", "Components", "ComponentDiagnosticFactory.cs");
        if (File.Exists(componentDiagFactory))
        {
            Console.WriteLine("  Parsing ComponentDiagnosticFactory.cs...");
            var diags = ParseDiagnosticFactory(componentDiagFactory);
            diagnostics.AddRange(diags);
        }

        // Parse RazorExtensionsDiagnosticFactory.cs
        var extDiagFactory = Path.Combine(_razorPath, "src", "Compiler",
            "Microsoft.CodeAnalysis.Razor.Compiler", "src", "Mvc", "RazorExtensionsDiagnosticFactory.cs");
        if (File.Exists(extDiagFactory))
        {
            Console.WriteLine("  Parsing RazorExtensionsDiagnosticFactory.cs...");
            var diags = ParseDiagnosticFactory(extDiagFactory);
            diagnostics.AddRange(diags);
        }

        // Search for any other diagnostic files
        var srcDir = Path.Combine(_razorPath, "src");
        if (Directory.Exists(srcDir))
        {
            var otherFiles = Directory.GetFiles(srcDir, "*Diagnostic*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("RazorDiagnosticFactory") && 
                           !f.Contains("ComponentDiagnosticFactory") &&
                           !f.Contains("RazorExtensionsDiagnosticFactory"));

            foreach (var file in otherFiles)
            {
                var diags = ParseDiagnosticFile(file);
                diagnostics.AddRange(diags);
            }
        }

        // Deduplicate and sort
        diagnostics = diagnostics
            .GroupBy(d => d.Id)
            .Select(g => g.First())
            .OrderBy(d => d.Id)
            .ToList();

        Console.WriteLine($"    Found {diagnostics.Count} RZ diagnostics");

        if (diagnostics.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<CollectorResult>>(Array.Empty<CollectorResult>());
        }

        var result = new CollectorResult(
            Prefix: "RZ",
            Repo: "razor",
            Description: "Razor compiler and analyzer diagnostics",
            Pattern: @"^RZ\d{4}$",
            Diagnostics: diagnostics
        );

        return Task.FromResult<IReadOnlyList<CollectorResult>>(new[] { result });
    }

    private static List<DiagnosticInfo> ParseDiagnosticFactory(string path)
    {
        var result = new List<DiagnosticInfo>();
        var content = File.ReadAllText(path);

        // Match patterns like: new($"{DiagnosticPrefix}1000", or new("RZ1000",
        var prefixRegex = new Regex(@"new\(\$?""\{?DiagnosticPrefix\}?(\d{4})""");
        var directRegex = new Regex(@"new\(""(RZ\d{4})""");
        var descriptorRegex = new Regex(@"RazorDiagnosticDescriptor\s+(\w+)\s*=");

        var descriptorNames = new Dictionary<int, string>();
        var lineNumber = 0;
        foreach (var line in content.Split('\n'))
        {
            var descMatch = descriptorRegex.Match(line);
            if (descMatch.Success)
            {
                descriptorNames[lineNumber] = descMatch.Groups[1].Value;
            }
            lineNumber++;
        }

        // Find all RZ codes with prefix pattern
        foreach (Match match in prefixRegex.Matches(content))
        {
            var number = match.Groups[1].Value;
            var id = $"RZ{number}";
            var name = FindNearestDescriptorName(content, match.Index, descriptorNames);

            result.Add(new DiagnosticInfo
            {
                Id = id,
                Category = GetCategory(int.Parse(number)),
                Name = name
            });
        }

        // Find all direct RZ codes
        foreach (Match match in directRegex.Matches(content))
        {
            var id = match.Groups[1].Value;
            if (!result.Any(r => r.Id == id))
            {
                var number = int.Parse(id.Substring(2));
                result.Add(new DiagnosticInfo
                {
                    Id = id,
                    Category = GetCategory(number)
                });
            }
        }

        return result;
    }

    private static List<DiagnosticInfo> ParseDiagnosticFile(string path)
    {
        var result = new List<DiagnosticInfo>();
        var content = File.ReadAllText(path);

        // Match any RZ, RSG, RZD, RZS codes
        var regex = new Regex(@"""(RZ\d{4}|RSG\d{3}|RZD\d{3}|RZS\d{4})""");

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

    private static string? FindNearestDescriptorName(string content, int position, Dictionary<int, string> names)
    {
        // Simple heuristic: find the descriptor name defined before this position
        var linesBefore = content.Substring(0, position).Split('\n').Length;
        return names
            .Where(kv => kv.Key <= linesBefore)
            .OrderByDescending(kv => kv.Key)
            .Select(kv => kv.Value)
            .FirstOrDefault();
    }

    private static string GetCategory(int number)
    {
        return number switch
        {
            >= 0 and < 1000 => "General",
            >= 1000 and < 2000 => "Parsing",
            >= 2000 and < 3000 => "Semantic",
            >= 3000 and < 4000 => "SourceGenerator",
            >= 9000 => "Component",
            _ => "Unknown"
        };
    }
}
