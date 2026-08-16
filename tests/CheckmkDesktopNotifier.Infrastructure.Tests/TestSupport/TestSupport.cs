using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Rest;

namespace CheckmkDesktopNotifier.Infrastructure.Tests.TestSupport;

internal static class TestOptions
{
    public const string Secret = "test-secret-not-for-production";

    public static CheckmkOptions Real() =>
        new()
        {
            Mode = ClientMode.Real,
            BaseUrl = "https://checkmk.example.invalid",
            Site = "mysite",
            Username = "automation",
            Secret = Secret,
            PollIntervalSeconds = 60
        };

    public static SiteId Site => new("mysite");
}

internal static class FixtureReader
{
    public static string Read(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
}

internal sealed class RecordingHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastBody { get; private set; }

    public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } =
        _ => new HttpResponseMessage(System.Net.HttpStatusCode.OK);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        LastRequest = request;
        if (request.Content is not null)
        {
            LastBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        return Responder(request);
    }
}

internal sealed class InMemoryUserSettingsStore : IUserSettingsStore
{
    public UserSettings? Current { get; set; }

    public bool Exists => Current is not null;

    public UserSettings? Load() => Current;

    public void Save(UserSettings settings) => Current = settings;

    public void Delete() => Current = null;
}

internal sealed class EnvironmentScope : IDisposable
{
    private static readonly string[] Names =
    [
        "CHECKMK_MODE",
        "CHECKMK_BASE_URL",
        "CHECKMK_SITE",
        "CHECKMK_USERNAME",
        "CHECKMK_SECRET",
        "CHECKMK_POLL_INTERVAL_SECONDS",
        "CHECKMK_CONFIG"
    ];

    private readonly Dictionary<string, string?> _previous = new(StringComparer.Ordinal);

    public EnvironmentScope()
    {
        foreach (var name in Names)
        {
            _previous[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    public void Set(string name, string value) => Environment.SetEnvironmentVariable(name, value);

    public void Clear(string name) => Environment.SetEnvironmentVariable(name, null);

    public void Dispose()
    {
        foreach (var pair in _previous)
        {
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }
}
