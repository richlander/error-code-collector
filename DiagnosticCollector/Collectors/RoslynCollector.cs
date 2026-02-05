using System.Text.RegularExpressions;
using System.Xml.Linq;
using DiagnosticCollector.Models;

namespace DiagnosticCollector.Collectors;

public class RoslynCollector : ICollector
{
    private readonly string _roslynPath;
    private readonly string? _docsPath;

    public RoslynCollector(string roslynPath, string? docsPath = null)
    {
        _roslynPath = roslynPath;
        _docsPath = docsPath;
    }

    public Task<IReadOnlyList<CollectorResult>> CollectAsync()
    {
        var errorCodeFile = Path.Combine(_roslynPath, "src", "Compilers", "CSharp", "Portable", "Errors", "ErrorCode.cs");
        var resourceFile = Path.Combine(_roslynPath, "src", "Compilers", "CSharp", "Portable", "CSharpResources.resx");
        var docsDir = _docsPath != null 
            ? Path.Combine(_docsPath, "docs", "csharp", "language-reference", "compiler-messages")
            : null;

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

        HashSet<string>? localDocs = null;
        if (docsDir != null && Directory.Exists(docsDir))
        {
            Console.WriteLine("  Checking local documentation...");
            localDocs = GetLocalDocs(docsDir);
            Console.WriteLine($"    Found {localDocs.Count} local doc files");
        }

        var diagnostics = new List<DiagnosticInfo>();
        foreach (var (enumName, (category, number)) in errorCodes.OrderBy(kv => kv.Value.Number))
        {
            var csCode = $"CS{number:D4}";
            messages.TryGetValue(enumName, out var message);
            var hasLocalDoc = localDocs?.Contains(csCode) ?? false;

            var info = new DiagnosticInfo
            {
                Id = csCode,
                Category = category,
                Name = enumName,
                Message = message,
                Url = hasLocalDoc 
                    ? $"https://raw.githubusercontent.com/dotnet/docs/main/docs/csharp/language-reference/compiler-messages/{csCode.ToLowerInvariant()}.md"
                    : null,
                ErrorUrl = hasLocalDoc 
                    ? $"https://learn.microsoft.com/dotnet/csharp/language-reference/compiler-messages/{csCode.ToLowerInvariant()}"
                    : null
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

    private static HashSet<string> GetLocalDocs(string docsDir)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var file in Directory.GetFiles(docsDir, "cs*.md"))
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            result.Add(fileName.ToUpperInvariant());
        }
        
        return result;
    }
}
