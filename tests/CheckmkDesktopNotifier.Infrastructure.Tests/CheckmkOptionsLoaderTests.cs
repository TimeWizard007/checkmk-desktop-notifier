using CheckmkDesktopNotifier.Infrastructure.Configuration;

namespace CheckmkDesktopNotifier.Infrastructure.Tests;

[Collection("Environment")]
public sealed class CheckmkOptionsLoaderTests
{
    [Fact]
    public void Parses_nested_checkmk_object()
    {
        var json = """
            {
              "Checkmk": {
                "Mode": "Real",
                "BaseUrl": "https://checkmk.example.invalid",
                "Site": "itssrv",
                "Username": "automation",
                "Secret": "from-file",
                "PollIntervalSeconds": 60
              }
            }
            """;

        var options = CheckmkOptionsLoader.FromJson(json);

        Assert.Equal(ClientMode.Real, options.Mode);
        Assert.Equal("https://checkmk.example.invalid", options.BaseUrl);
        Assert.Equal("itssrv", options.Site);
        Assert.Equal("automation", options.Username);
        Assert.Equal("from-file", options.Secret);
        Assert.Equal(60, options.PollIntervalSeconds);
    }

    [Fact]
    public void Parses_flat_object()
    {
        var options = CheckmkOptionsLoader.FromJson("""{"Mode":"Mock","Site":"mysite"}""");

        Assert.Equal(ClientMode.Mock, options.Mode);
        Assert.Equal("mysite", options.Site);
        Assert.Equal(60, options.PollIntervalSeconds);
    }

    [Fact]
    public void Environment_overrides_file_values()
    {
        using var env = new EnvironmentScope();
        env.Set("CHECKMK_MODE", "Real");
        env.Set("CHECKMK_BASE_URL", "https://from-env.example.invalid");
        env.Set("CHECKMK_SITE", "envsite");
        env.Set("CHECKMK_USERNAME", "envuser");
        env.Set("CHECKMK_SECRET", "env-secret");
        env.Set("CHECKMK_POLL_INTERVAL_SECONDS", "120");

        var options = CheckmkOptionsLoader.ApplyEnvironment(new CheckmkOptions
        {
            Mode = ClientMode.Mock,
            BaseUrl = "https://from-file.example.invalid",
            Site = "filesite",
            Username = "fileuser",
            Secret = "file-secret",
            PollIntervalSeconds = 60
        });

        Assert.Equal(ClientMode.Real, options.Mode);
        Assert.Equal("https://from-env.example.invalid", options.BaseUrl);
        Assert.Equal("envsite", options.Site);
        Assert.Equal("envuser", options.Username);
        Assert.Equal("env-secret", options.Secret);
        Assert.Equal(120, options.PollIntervalSeconds);
    }

    [Fact]
    public void Missing_environment_keeps_file_values()
    {
        using var env = new EnvironmentScope();
        env.Clear("CHECKMK_MODE");
        env.Clear("CHECKMK_BASE_URL");
        env.Clear("CHECKMK_SITE");
        env.Clear("CHECKMK_USERNAME");
        env.Clear("CHECKMK_SECRET");
        env.Clear("CHECKMK_POLL_INTERVAL_SECONDS");

        var options = CheckmkOptionsLoader.ApplyEnvironment(new CheckmkOptions
        {
            Mode = ClientMode.Mock,
            Site = "filesite",
            PollIntervalSeconds = 60
        });

        Assert.Equal(ClientMode.Mock, options.Mode);
        Assert.Equal("filesite", options.Site);
        Assert.Equal(60, options.PollIntervalSeconds);
    }

    private sealed class EnvironmentScope : IDisposable
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
}
