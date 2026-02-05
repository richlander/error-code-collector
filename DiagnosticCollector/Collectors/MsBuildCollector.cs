using System.Text.RegularExpressions;
using System.Xml.Linq;
using DiagnosticCollector.Models;

namespace DiagnosticCollector.Collectors;

public class MsBuildCollector : ICollector
{
    private readonly string _msbuildPath;

    public MsBuildCollector(string msbuildPath)
    {
        _msbuildPath = msbuildPath;
    }

    public Task<IReadOnlyList<CollectorResult>> CollectAsync()
    {
        var results = new List<CollectorResult>();

        // Collect MSBxxxx codes from .resx files
        var msbDiagnostics = CollectMsbCodes();
        if (msbDiagnostics.Count > 0)
        {
            results.Add(new CollectorResult(
                Prefix: "MSB",
                Repo: "msbuild",
                Description: "MSBuild errors and warnings",
                Pattern: @"^MSB\d{4}$",
                Diagnostics: msbDiagnostics
            ));
        }

        // Collect BCxxxx codes from BuildCheck
        var bcDiagnostics = CollectBuildCheckCodes();
        if (bcDiagnostics.Count > 0)
        {
            results.Add(new CollectorResult(
                Prefix: "BC",
                Repo: "msbuild",
                Description: "MSBuild BuildCheck diagnostics",
                Pattern: @"^BC\d{4}$",
                Diagnostics: bcDiagnostics
            ));
        }

        return Task.FromResult<IReadOnlyList<CollectorResult>>(results);
    }

    private List<DiagnosticInfo> CollectMsbCodes()
    {
        var result = new List<DiagnosticInfo>();
        var srcDir = Path.Combine(_msbuildPath, "src");

        if (!Directory.Exists(srcDir))
        {
            Console.WriteLine($"  Warning: MSBuild src directory not found");
            return result;
        }

        Console.WriteLine("  Parsing MSBuild .resx files...");
        var resxFiles = Directory.GetFiles(srcDir, "Strings.resx", SearchOption.AllDirectories);

        foreach (var file in resxFiles)
        {
            var diags = ParseResxForMsbCodes(file);
            result.AddRange(diags);
        }

        // Also check shared resources
        var sharedResx = Path.Combine(srcDir, "Shared", "Resources", "Strings.shared.resx");
        if (File.Exists(sharedResx))
        {
            var diags = ParseResxForMsbCodes(sharedResx);
            result.AddRange(diags);
        }

        // Deduplicate and sort
        result = result
            .GroupBy(d => d.Id)
            .Select(g => g.First())
            .OrderBy(d => d.Id)
            .ToList();

        Console.WriteLine($"    Found {result.Count} MSB diagnostics");
        return result;
    }

    private static List<DiagnosticInfo> ParseResxForMsbCodes(string path)
    {
        var result = new List<DiagnosticInfo>();

        try
        {
            var doc = XDocument.Load(path);
            var msbRegex = new Regex(@"^(MSB\d{4}):\s*(.+)$");

            foreach (var data in doc.Descendants("data"))
            {
                var value = data.Element("value")?.Value;
                if (value == null) continue;

                var match = msbRegex.Match(value);
                if (match.Success)
                {
                    var id = match.Groups[1].Value;
                    var message = match.Groups[2].Value;
                    var name = data.Attribute("name")?.Value;

                    // Determine category based on ID range
                    var number = int.Parse(id.Substring(3));
                    var category = number switch
                    {
                        >= 1000 and < 2000 => "CommandLine",
                        >= 2000 and < 3000 => "Conversion",
                        >= 3000 and < 4000 => "Task",
                        >= 4000 and < 5000 => "Engine",
                        >= 5000 and < 6000 => "Shared",
                        >= 6000 and < 7000 => "Utilities",
                        _ => "Unknown"
                    };

                    result.Add(new DiagnosticInfo
                    {
                        Id = id,
                        Category = category,
                        Name = name,
                        Message = message.Length > 200 ? message.Substring(0, 200) + "..." : message,
                        HelpUrl = $"https://learn.microsoft.com/visualstudio/msbuild/errors/{id.ToLowerInvariant()}"
                    });
                }
            }
        }
        catch
        {
            // Ignore parse errors
        }

        return result;
    }

    private List<DiagnosticInfo> CollectBuildCheckCodes()
    {
        var result = new List<DiagnosticInfo>();

        // Parse Codes.md documentation
        var codesFile = Path.Combine(_msbuildPath, "documentation", "specs", "BuildCheck", "Codes.md");
        if (File.Exists(codesFile))
        {
            Console.WriteLine("  Parsing BuildCheck Codes.md...");
            var content = File.ReadAllText(codesFile);

            // Match patterns like: ## BC0101 - ConflictingOutputPath
            var regex = new Regex(@"##\s*(BC\d{4})\s*-\s*(.+)$", RegexOptions.Multiline);

            foreach (Match match in regex.Matches(content))
            {
                var id = match.Groups[1].Value;
                var name = match.Groups[2].Value.Trim();

                result.Add(new DiagnosticInfo
                {
                    Id = id,
                    Category = "BuildCheck",
                    Name = name,
                    HelpUrl = $"https://learn.microsoft.com/visualstudio/msbuild/errors/{id.ToLowerInvariant()}"
                });
            }
        }

        // Also search for BC codes in Check classes
        var checksDir = Path.Combine(_msbuildPath, "src", "Build", "BuildCheck", "Checks");
        if (Directory.Exists(checksDir))
        {
            var checkFiles = Directory.GetFiles(checksDir, "*.cs");
            foreach (var file in checkFiles)
            {
                var diags = ParseBuildCheckFile(file);
                foreach (var diag in diags)
                {
                    if (!result.Any(r => r.Id == diag.Id))
                    {
                        result.Add(diag);
                    }
                }
            }
        }

        result = result.OrderBy(d => d.Id).ToList();
        Console.WriteLine($"    Found {result.Count} BC diagnostics");

        return result;
    }

    private static List<DiagnosticInfo> ParseBuildCheckFile(string path)
    {
        var result = new List<DiagnosticInfo>();
        var content = File.ReadAllText(path);

        // Match patterns like: private const string RuleId = "BC0101";
        var regex = new Regex(@"""(BC\d{4})""");

        foreach (Match match in regex.Matches(content))
        {
            var id = match.Groups[1].Value;
            result.Add(new DiagnosticInfo
            {
                Id = id,
                Category = "BuildCheck"
            });
        }

        return result;
    }
}
