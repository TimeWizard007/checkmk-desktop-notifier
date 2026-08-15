using CheckmkDesktopNotifier.Infrastructure.Configuration;

namespace CheckmkDesktopNotifier.Infrastructure.Tests;

public sealed class CheckmkOptionsValidatorTests
{
    [Fact]
    public void Mock_mode_does_not_require_connection_settings()
    {
        var options = new CheckmkOptions { Mode = ClientMode.Mock, PollIntervalSeconds = 60 };

        CheckmkOptionsValidator.Validate(options);
    }

    [Fact]
    public void Real_mode_requires_url_site_username_and_secret()
    {
        var options = new CheckmkOptions
        {
            Mode = ClientMode.Real,
            BaseUrl = "https://checkmk.example.invalid",
            Site = "mysite",
            Username = "automation",
            Secret = "test-secret-not-for-production",
            PollIntervalSeconds = 60
        };

        CheckmkOptionsValidator.Validate(options);
    }

    [Fact]
    public void Rejects_poll_interval_below_minimum()
    {
        var options = new CheckmkOptions { Mode = ClientMode.Mock, PollIntervalSeconds = 9 };

        var ex = Assert.Throws<CheckmkOptionsValidationException>(() => CheckmkOptionsValidator.Validate(options));
        Assert.Contains("PollIntervalSeconds", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_credentials_embedded_in_base_url()
    {
        var options = new CheckmkOptions
        {
            Mode = ClientMode.Real,
            BaseUrl = "https://user:secret@checkmk.example.invalid",
            Site = "mysite",
            Username = "automation",
            Secret = "test-secret-not-for-production"
        };

        var ex = Assert.Throws<CheckmkOptionsValidationException>(() => CheckmkOptionsValidator.Validate(options));
        Assert.Contains("username or password", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("ftp://checkmk.example.invalid")]
    public void Rejects_invalid_base_url(string? baseUrl)
    {
        var options = new CheckmkOptions
        {
            Mode = ClientMode.Real,
            BaseUrl = baseUrl,
            Site = "mysite",
            Username = "automation",
            Secret = "test-secret-not-for-production"
        };

        Assert.Throws<CheckmkOptionsValidationException>(() => CheckmkOptionsValidator.Validate(options));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("site/extra")]
    public void Rejects_invalid_site(string? site)
    {
        var options = new CheckmkOptions
        {
            Mode = ClientMode.Real,
            BaseUrl = "https://checkmk.example.invalid",
            Site = site,
            Username = "automation",
            Secret = "test-secret-not-for-production"
        };

        Assert.Throws<CheckmkOptionsValidationException>(() => CheckmkOptionsValidator.Validate(options));
    }

    [Fact]
    public void Rejects_missing_secret_without_echoing_it()
    {
        var options = new CheckmkOptions
        {
            Mode = ClientMode.Real,
            BaseUrl = "https://checkmk.example.invalid",
            Site = "mysite",
            Username = "automation",
            Secret = "  "
        };

        var ex = Assert.Throws<CheckmkOptionsValidationException>(() => CheckmkOptionsValidator.Validate(options));
        Assert.Contains("Secret", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("test-secret", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Builds_verified_api_base_uri()
    {
        var options = new CheckmkOptions
        {
            Mode = ClientMode.Real,
            BaseUrl = "https://checkmk.example.invalid/",
            Site = "itssrv",
            Username = "automation",
            Secret = "test-secret-not-for-production"
        };

        Assert.Equal(
            "https://checkmk.example.invalid/itssrv/check_mk/api/1.0/",
            options.CreateApiBaseUri().ToString());
    }
}
