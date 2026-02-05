using System.Net;
using DiagnosticCollector.Models;

namespace DiagnosticCollector.Services;

public class MarkdownValidator : IDisposable
{
    private readonly HttpClient _client;
    private readonly Dictionary<string, PrefixConfig> _prefixConfigs;
    private readonly List<string> _warnings = new();

    public IReadOnlyList<string> Warnings => _warnings;

    public MarkdownValidator(CollectorConfig config)
    {
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        
        // Flatten all prefix configs for easy lookup
        _prefixConfigs = new Dictionary<string, PrefixConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var repo in config.Repos.Values)
        {
            foreach (var (prefix, prefixConfig) in repo.Prefixes)
            {
                _prefixConfigs[prefix] = prefixConfig;
            }
        }
    }

    public async Task ValidateDiagnosticsAsync(string prefix, IReadOnlyList<DiagnosticInfo> diagnostics)
    {
        if (!_prefixConfigs.TryGetValue(prefix, out var config) || config.MarkdownUrl == null)
        {
            return;
        }

        // Skip if it's a shared URL (no {id} placeholder)
        if (!config.MarkdownUrl.Contains("{id}"))
        {
            return;
        }

        Console.WriteLine($"  Validating {prefix} markdown URLs...");
        var notFound = 0;

        foreach (var diag in diagnostics)
        {
            var url = config.GetUrl(diag.Id);
            if (url == null) continue;

            try
            {
                var response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    var warning = $"    404: {diag.Id} - {url}";
                    Console.WriteLine(warning);
                    _warnings.Add(warning);
                    notFound++;
                }
            }
            catch (Exception ex)
            {
                var warning = $"    Error checking {diag.Id}: {ex.Message}";
                Console.WriteLine(warning);
                _warnings.Add(warning);
            }

            // Rate limiting
            await Task.Delay(25);
        }

        if (notFound > 0)
        {
            Console.WriteLine($"    {notFound} missing docs for {prefix}");
        }
        else
        {
            Console.WriteLine($"    All {diagnostics.Count} {prefix} docs validated");
        }
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
