using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Infrastructure.Polling;

public sealed class PollDiagnosticsWriter
{
    private readonly string _filePath;

    public PollDiagnosticsWriter(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Diagnostics path must not be empty.", nameof(filePath));
        }

        _filePath = filePath;
    }

    public void WriteSuccess(DateTimeOffset utcNow, IReadOnlyList<MonitoredProblem> problems)
    {
        var hosts = problems.Count(p => p.Id.Kind == ObjectKind.Host);
        var services = problems.Count(p => p.Id.Kind == ObjectKind.Service);
        Write(
            $"""
            TimeUtc: {utcNow:O}
            Success: true
            Problems: {problems.Count}
            Hosts: {hosts}
            Services: {services}
            ErrorKind:
            """);
    }

    public void WriteFailure(DateTimeOffset utcNow, SnapshotErrorKind? errorKind, string? errorSummary)
    {
        var safe = errorSummary ?? string.Empty;
        if (safe.Contains("Bearer ", StringComparison.OrdinalIgnoreCase)
            || safe.Contains("Authorization", StringComparison.OrdinalIgnoreCase))
        {
            safe = "The Checkmk request failed.";
        }

        Write(
            $"""
            TimeUtc: {utcNow:O}
            Success: false
            Problems:
            Hosts:
            Services:
            ErrorKind: {errorKind}
            Error: {safe}
            """);
    }

    private void Write(string contents)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_filePath, contents + Environment.NewLine);
    }
}
