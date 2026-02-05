using System.Text.RegularExpressions;
using System.Xml.Linq;
using DiagnosticCollector.Models;

namespace DiagnosticCollector.Collectors;

public class RoslynCollector : ICollector
{
    private readonly string _roslynPath;

    public RoslynCollector(string roslynPath, string? docsPath = null)
    {
        _roslynPath = roslynPath;
        // docsPath is no longer used - URLs are generated from patterns
    }

    public Task<IReadOnlyList<CollectorResult>> CollectAsync()
    {
        var errorCodeFile = Path.Combine(_roslynPath, "src", "Compilers", "CSharp", "Portable", "Errors", "ErrorCode.cs");
        var resourceFile = Path.Combine(_roslynPath, "src", "Compilers", "CSharp", "Portable", "CSharpResources.resx");

        if (!File.Exists(errorCodeFile))
        {
            Console.Error.WriteLine($"Warning: ErrorCode.cs not found at {errorCodeFile}");
            return Task.FromResult<IReadOnlyList<CollectorResult>>(Array.Empty<CollectorResult>());
        }

        Console.WriteLine("  Parsing Roslyn error codes...");
        var errorCodes = ParseErrorCodes(errorCodeFile);
        Console.WriteLine($"    Found {errorCodes.Count} error codes");

        Console.WriteLine("  Parsing resource strings...");
        var messages = ParseResourceStrings(resourceFile);
        Console.WriteLine($"    Found {messages.Count} message strings");

        var diagnostics = new List<DiagnosticInfo>();
        foreach (var (enumName, (category, number)) in errorCodes.OrderBy(kv => kv.Value.Number))
        {
            var csCode = $"CS{number:D4}";
            messages.TryGetValue(enumName, out var message);

            var info = new DiagnosticInfo
            {
                Id = csCode,
                Category = category,
                Name = enumName,
                Message = message,
                Url = $"https://raw.githubusercontent.com/dotnet/docs/main/docs/csharp/language-reference/compiler-messages/{csCode.ToLowerInvariant()}.md",
                ErrorUrl = $"https://learn.microsoft.com/dotnet/csharp/language-reference/compiler-messages/{csCode.ToLowerInvariant()}"
            };

            diagnostics.Add(info);
        }

        var result = new CollectorResult(
            Prefix: "CS",
            Repo: "roslyn",
            Description: "C# compiler errors and warnings",
            Pattern: @"^CS\d{4}$",
            Diagnostics: diagnostics
        );

        return Task.FromResult<IReadOnlyList<CollectorResult>>(new[] { result });
    }

    private static Dictionary<string, (string Category, int Number)> ParseErrorCodes(string path)
    {
        var result = new Dictionary<string, (string, int)>();
        var content = File.ReadAllText(path);
        
        var regex = new Regex(@"^\s*(ERR|WRN|FTL|HDN|INF)_(\w+)\s*=\s*(\d+)", RegexOptions.Multiline);
        
        foreach (Match match in regex.Matches(content))
        {
            var category = match.Groups[1].Value;
            var name = match.Groups[2].Value;
            var number = int.Parse(match.Groups[3].Value);
            var enumName = $"{category}_{name}";
            result[enumName] = (category, number);
        }
        
        return result;
    }

    private static Dictionary<string, string> ParseResourceStrings(string path)
    {
        var result = new Dictionary<string, string>();
        
        if (!File.Exists(path))
            return result;

        var doc = XDocument.Load(path);
        foreach (var data in doc.Descendants("data"))
        {
            var name = data.Attribute("name")?.Value;
            var value = data.Element("value")?.Value;
            if (name != null && value != null)
            {
                result[name] = value;
            }
        }
        
        return result;
    }

}
