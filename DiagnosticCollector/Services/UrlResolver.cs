using System.Net;
using DiagnosticCollector.Models;

namespace DiagnosticCollector.Services;

public class UrlResolver : IDisposable
{
    private readonly HttpClient _client;
    private readonly bool _enabled;

    public UrlResolver(bool enabled = true)
    {
        _enabled = enabled;
        
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false
        };
        _client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public async Task ResolveDiagnosticsAsync(IReadOnlyList<DiagnosticInfo> diagnostics)
    {
        if (!_enabled)
            return;

        var toResolve = diagnostics.Where(d => d.HelpUrl != null).ToList();
        if (toResolve.Count == 0)
            return;

        Console.WriteLine($"  Resolving {toResolve.Count} URLs...");
        var resolved = 0;
        var failed = 0;

        foreach (var diag in toResolve)
        {
            try
            {
                var longUrl = await ResolveUrlAsync(diag.HelpUrl!);
                if (longUrl != null && longUrl != diag.HelpUrl)
                {
                    diag.LongUrl = longUrl;
                    resolved++;
                }
            }
            catch
            {
                failed++;
            }

            // Rate limiting
            await Task.Delay(25);
        }

        Console.WriteLine($"    Resolved {resolved} URLs ({failed} failed)");
    }

    private async Task<string?> ResolveUrlAsync(string url)
    {
        try
        {
            var originalUri = new Uri(url);
            var currentUrl = url;
            var maxRedirects = 5;

            for (int i = 0; i < maxRedirects; i++)
            {
                var response = await _client.GetAsync(currentUrl);
                
                if (response.StatusCode == HttpStatusCode.Redirect ||
                    response.StatusCode == HttpStatusCode.MovedPermanently ||
                    response.StatusCode == HttpStatusCode.TemporaryRedirect ||
                    response.StatusCode == HttpStatusCode.PermanentRedirect)
                {
                    var location = response.Headers.Location?.ToString();
                    if (location == null)
                        break;

                    // Handle relative URLs
                    if (!location.StartsWith("http"))
                    {
                        var uri = new Uri(currentUrl);
                        location = new Uri(uri, location).ToString();
                    }

                    currentUrl = location;
                }
                else
                {
                    // Final destination reached
                    break;
                }
            }

            // Clean up query parameters for cleaner output
            var idx = currentUrl.IndexOf("?f1url=");
            if (idx > 0)
                currentUrl = currentUrl[..idx];

            // Also strip ?view= parameters (version-specific)
            idx = currentUrl.IndexOf("?view=");
            if (idx > 0)
                currentUrl = currentUrl[..idx];

            var finalUri = new Uri(currentUrl);

            // If redirected to bing.com, that's an error (short link not configured)
            if (finalUri.Host.Contains("bing.com", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"    Warning: Bad redirect for {url} -> {currentUrl}");
                return null;
            }

            // Only include long_url if it's a different domain
            if (string.Equals(originalUri.Host, finalUri.Host, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Remove locale from path to keep URLs language-neutral
            currentUrl = RemoveLocaleFromUrl(currentUrl);

            return currentUrl;
        }
        catch
        {
            return null;
        }
    }

    private static string RemoveLocaleFromUrl(string url)
    {
        // Remove /en-us/ or similar locale paths from learn.microsoft.com URLs
        // e.g., https://learn.microsoft.com/en-us/dotnet/... -> https://learn.microsoft.com/dotnet/...
        var localePattern = new System.Text.RegularExpressions.Regex(
            @"(https://learn\.microsoft\.com)/[a-z]{2}-[a-z]{2}/", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return localePattern.Replace(url, "$1/");
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
