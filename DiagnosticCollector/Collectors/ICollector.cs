using DiagnosticCollector.Models;

namespace DiagnosticCollector.Collectors;

public record CollectorResult(
    string Prefix,
    string Repo,
    string Description,
    string Pattern,
    List<DiagnosticInfo> Diagnostics
);

public interface ICollector
{
    Task<IReadOnlyList<CollectorResult>> CollectAsync();
}
