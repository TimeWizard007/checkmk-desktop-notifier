using System.Text;
using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Infrastructure.Rest;

public static class ConnectionTestReport
{
    public static string Format(int? httpStatus, ProblemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var warn = 0;
        var crit = 0;
        var unknown = 0;
        foreach (var problem in snapshot.Problems)
        {
            switch (problem.Severity)
            {
                case Severity.Warning:
                    warn++;
                    break;
                case Severity.Critical:
                    crit++;
                    break;
                case Severity.Unknown:
                    unknown++;
                    break;
            }
        }

        var builder = new StringBuilder();
        builder.AppendLine($"HTTP status: {httpStatus?.ToString() ?? "n/a"}");
        builder.AppendLine($"Service problems: {snapshot.Problems.Count}");
        builder.AppendLine($"WARN: {warn}");
        builder.AppendLine($"CRIT: {crit}");
        builder.AppendLine($"UNKNOWN: {unknown}");
        return builder.ToString();
    }
}
