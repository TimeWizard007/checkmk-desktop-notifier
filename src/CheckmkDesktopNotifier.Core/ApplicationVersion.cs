using System.Reflection;

namespace CheckmkDesktopNotifier.Core;

public static class ApplicationVersion
{
    public static string FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        return From(informational, assembly.GetName().Version);
    }

    public static string From(string? informationalVersion, Version? assemblyVersion)
    {
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var value = informationalVersion.Trim();
            var suffix = value.IndexOfAny(['+', ' ']);
            if (suffix >= 0)
            {
                value = value[..suffix];
            }

            if (value.Length > 0)
            {
                return value;
            }
        }

        return assemblyVersion?.ToString() ?? "unknown";
    }
}
