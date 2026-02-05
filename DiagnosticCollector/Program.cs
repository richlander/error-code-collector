using System.Text.Json;
using System.Text.Json.Serialization;
using DiagnosticCollector.Collectors;
using DiagnosticCollector.Models;
using DiagnosticCollector.Services;

namespace DiagnosticCollector;

class Program
{
    static async Task Main(string[] args)
    {
        var config = ParseArgs(args);
        
        if (config.ShowHelp)
        {
            ShowHelp();
            return;
        }

        // Load config file
        CollectorConfig? collectorConfig = null;
        if (config.ConfigPath != null && File.Exists(config.ConfigPath))
        {
            var configJson = await File.ReadAllTextAsync(config.ConfigPath);
            collectorConfig = JsonSerializer.Deserialize<CollectorConfig>(configJson);
            Console.WriteLine($"Loaded config from {config.ConfigPath}");
        }

        var collectors = new List<ICollector>();

        if (config.RoslynPath != null)
        {
            Console.WriteLine($"Adding Roslyn collector: {config.RoslynPath}");
            collectors.Add(new RoslynCollector(config.RoslynPath, config.DocsPath));
        }

        if (config.RuntimePath != null)
        {
            Console.WriteLine($"Adding Runtime collector: {config.RuntimePath}");
            collectors.Add(new RuntimeCollector(config.RuntimePath));
        }

        if (config.SdkPath != null)
        {
            Console.WriteLine($"Adding SDK collector: {config.SdkPath}");
            collectors.Add(new SdkCollector(config.SdkPath));
        }

        if (config.AspNetCorePath != null)
        {
            Console.WriteLine($"Adding ASP.NET Core collector: {config.AspNetCorePath}");
            collectors.Add(new AspNetCoreCollector(config.AspNetCorePath));
        }

        if (config.EfCorePath != null)
        {
            Console.WriteLine($"Adding EF Core collector: {config.EfCorePath}");
            collectors.Add(new EfCoreCollector(config.EfCorePath));
        }

        if (config.AspirePath != null)
        {
            Console.WriteLine($"Adding Aspire collector: {config.AspirePath}");
            collectors.Add(new AspireCollector(config.AspirePath));
        }

        if (config.ExtensionsPath != null)
        {
            Console.WriteLine($"Adding Extensions collector: {config.ExtensionsPath}");
            collectors.Add(new ExtensionsCollector(config.ExtensionsPath));
        }

        if (config.MsBuildPath != null)
        {
            Console.WriteLine($"Adding MSBuild collector: {config.MsBuildPath}");
            collectors.Add(new MsBuildCollector(config.MsBuildPath));
        }

        if (config.RazorPath != null)
        {
            Console.WriteLine($"Adding Razor collector: {config.RazorPath}");
            collectors.Add(new RazorCollector(config.RazorPath));
        }

        if (collectors.Count == 0)
        {
            Console.Error.WriteLine("Error: No repositories specified. Use --help for usage.");
            return;
        }

        // Collect all diagnostics
        var allResults = new List<CollectorResult>();
        foreach (var collector in collectors)
        {
            var results = await collector.CollectAsync();
            allResults.AddRange(results);
        }

        Console.WriteLine($"\nCollected {allResults.Count} prefix groups");

        // Validate markdown URLs exist (check for 404s)
        if (config.ValidateUrls && collectorConfig != null)
        {
            Console.WriteLine("\nValidating markdown URLs...");
            using var validator = new MarkdownValidator(collectorConfig);
            foreach (var result in allResults)
            {
                await validator.ValidateDiagnosticsAsync(result.Prefix, result.Diagnostics);
            }

            if (validator.Warnings.Count > 0)
            {
                Console.WriteLine($"\n⚠ {validator.Warnings.Count} documentation issues found");
            }
        }

        // Ensure output directory exists
        Directory.CreateDirectory(config.OutputDir);

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // Write per-prefix files and build index
        var index = new IndexFile();
        var generatedAt = DateTime.UtcNow;

        foreach (var result in allResults)
        {
            var fileName = $"{result.Prefix.ToLowerInvariant()}.json";
            var filePath = Path.Combine(config.OutputDir, fileName);

            var prefixFile = new PrefixFile
            {
                Prefix = result.Prefix,
                Repo = result.Repo,
                Description = result.Description,
                GeneratedAt = generatedAt,
                Diagnostics = result.Diagnostics
            };

            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(prefixFile, jsonOptions));
            Console.WriteLine($"  Wrote {fileName} ({result.Diagnostics.Count} diagnostics)");

            index.Prefixes[result.Prefix] = new PrefixInfo
            {
                File = fileName,
                Repo = result.Repo,
                Pattern = result.Pattern,
                Count = result.Diagnostics.Count,
                Description = result.Description
            };
        }

        // Write index file
        index.GeneratedAt = generatedAt;
        var indexPath = Path.Combine(config.OutputDir, "index.json");
        await File.WriteAllTextAsync(indexPath, JsonSerializer.Serialize(index, jsonOptions));
        Console.WriteLine($"  Wrote index.json ({index.Prefixes.Count} prefixes)");

        Console.WriteLine("\nDone!");
    }

    static Config ParseArgs(string[] args)
    {
        var config = new Config();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help":
                case "-h":
                    config.ShowHelp = true;
                    break;
                case "--roslyn":
                    config.RoslynPath = args[++i];
                    break;
                case "--runtime":
                    config.RuntimePath = args[++i];
                    break;
                case "--sdk":
                    config.SdkPath = args[++i];
                    break;
                case "--aspnetcore":
                    config.AspNetCorePath = args[++i];
                    break;
                case "--efcore":
                    config.EfCorePath = args[++i];
                    break;
                case "--aspire":
                    config.AspirePath = args[++i];
                    break;
                case "--extensions":
                    config.ExtensionsPath = args[++i];
                    break;
                case "--msbuild":
                    config.MsBuildPath = args[++i];
                    break;
                case "--razor":
                    config.RazorPath = args[++i];
                    break;
                case "--docs":
                    config.DocsPath = args[++i];
                    break;
                case "--output":
                case "-o":
                    config.OutputDir = args[++i];
                    break;
                case "--validate-urls":
                    config.ValidateUrls = true;
                    break;
                case "--config":
                    config.ConfigPath = args[++i];
                    break;
            }
        }

        return config;
    }

    static void ShowHelp()
    {
        Console.WriteLine("""
            DiagnosticCollector - Collects diagnostic codes from .NET repositories

            Usage:
              DiagnosticCollector [options]

            Options:
              --roslyn <path>       Path to roslyn repository
              --runtime <path>      Path to runtime repository
              --sdk <path>          Path to sdk repository
              --aspnetcore <path>   Path to aspnetcore repository
              --efcore <path>       Path to efcore repository
              --aspire <path>       Path to aspire repository
              --extensions <path>   Path to extensions repository
              --msbuild <path>      Path to msbuild repository
              --razor <path>        Path to razor repository
              --docs <path>         Path to docs repository (for Roslyn doc links)
              --validate-urls       Validate markdown URLs exist (report 404s)
              --config <path>       Path to config.json file
              --output, -o <path>   Output directory (default: ./errors)
              --help, -h            Show this help

            Examples:
              DiagnosticCollector --roslyn ~/git/roslyn --runtime ~/git/runtime -o ./diagnostics
              DiagnosticCollector --aspnetcore ~/git/aspnetcore --efcore ~/git/efcore
            """);
    }
}

class Config
{
    public bool ShowHelp { get; set; }
    public string? RoslynPath { get; set; }
    public string? RuntimePath { get; set; }
    public string? SdkPath { get; set; }
    public string? AspNetCorePath { get; set; }
    public string? EfCorePath { get; set; }
    public string? AspirePath { get; set; }
    public string? ExtensionsPath { get; set; }
    public string? MsBuildPath { get; set; }
    public string? RazorPath { get; set; }
    public string? DocsPath { get; set; }
    public string? ConfigPath { get; set; }
    public string OutputDir { get; set; } = "./errors";
    public bool ValidateUrls { get; set; }
}
