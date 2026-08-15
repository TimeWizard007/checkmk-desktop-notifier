using System.Text.Json;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Rest;

namespace CheckmkDesktopNotifier.ConnectionTest;

internal static class Program
{
    public static async Task<int> Main()
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

        using var http = new HttpClient
        {
            BaseAddress = options.CreateApiBaseUri(),
            Timeout = TimeSpan.FromSeconds(20)
        };

        var client = new CheckmkServiceClient(http, options);
        var snapshot = await client.GetCurrentProblemsAsync().ConfigureAwait(false);
        Console.Write(ConnectionTestReport.Format(client.LastHttpStatusCode, snapshot));
        return snapshot.IsSuccess ? 0 : 1;
    }
}
