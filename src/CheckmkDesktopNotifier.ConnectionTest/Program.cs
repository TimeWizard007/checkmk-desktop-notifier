using System.Text.Json;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Rest;

namespace CheckmkDesktopNotifier.ConnectionTest;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        CheckmkOptions options;
        try
        {
            options = CheckmkOptionsLoader.Load();
            CheckmkOptionsValidator.Validate(options);
        }
        catch (Exception ex) when (ex is CheckmkOptionsValidationException or JsonException or IOException)
        {
            await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            return 2;
        }

        if (options.Mode != ClientMode.Real)
        {
            await Console.Error.WriteLineAsync(
                    "Real Checkmk mode is required. Copy config/checkmk.local.json.example to config/checkmk.local.json, set Mode to Real, and fill in BaseUrl, Site, Username, and Secret.")
                .ConfigureAwait(false);
            return 2;
        }

        var hosts = args.Any(arg => string.Equals(arg, "--hosts", StringComparison.OrdinalIgnoreCase));
        using var http = new HttpClient
        {
            BaseAddress = options.CreateApiBaseUri(),
            Timeout = TimeSpan.FromSeconds(hosts ? 60 : 20)
        };

        if (hosts)
        {
            var hostClient = new CheckmkHostClient(http, options);
            var verified = await hostClient.ProbeVerifiedAsync().ConfigureAwait(false);
            var documented = await hostClient.ProbeDocumentedColumnsAsync().ConfigureAwait(false);
            Console.Write(HostConnectionTestReport.Format(verified, documented));
            return verified.IsSuccess ? 0 : 1;
        }

        var client = new CheckmkServiceClient(http, options);
        var snapshot = await client.GetCurrentProblemsAsync().ConfigureAwait(false);
        Console.Write(ConnectionTestReport.Format(client.LastHttpStatusCode, snapshot));
        return snapshot.IsSuccess ? 0 : 1;
    }
}
