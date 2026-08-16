using CheckmkDesktopNotifier.Infrastructure.Configuration;

namespace CheckmkDesktopNotifier.Infrastructure.Tests;

public sealed class GuiSettingsValidatorTests
{
    [Fact]
    public void Accepts_valid_https_origin()
    {
        var options = GuiSettingsValidator.CreateOptions(
            "https://checkmk.example.com/",
            "mysite",
            "checkmk-desktop-notifier",
            "secret-value",
            "60",
            requireSecret: true);

        Assert.Equal("https://checkmk.example.com", options.BaseUrl);
        Assert.Equal(60, options.PollIntervalSeconds);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://checkmk.example.com")]
    [InlineData("http://checkmk.example.com")]
    [InlineData("https://user:pass@checkmk.example.com")]
    [InlineData("https://checkmk.example.com/mysite/check_mk/api/1.0/")]
    public void Rejects_malformed_or_http_base_url(string baseUrl)
    {
        Assert.Throws<CheckmkOptionsValidationException>(() =>
            GuiSettingsValidator.CreateOptions(baseUrl, "mysite", "user", "secret", "60", true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("site/extra")]
    public void Rejects_invalid_site(string? site)
    {
        Assert.Throws<CheckmkOptionsValidationException>(() =>
            GuiSettingsValidator.CreateOptions("https://checkmk.example.com", site, "user", "secret", "60", true));
    }

    [Fact]
    public void Rejects_poll_interval_below_minimum()
    {
        Assert.Throws<CheckmkOptionsValidationException>(() =>
            GuiSettingsValidator.CreateOptions("https://checkmk.example.com", "mysite", "user", "secret", "9", true));
    }

    [Fact]
    public void Requires_secret_for_new_configuration()
    {
        Assert.Throws<CheckmkOptionsValidationException>(() =>
            GuiSettingsValidator.CreateOptions("https://checkmk.example.com", "mysite", "user", "", "60", true));
    }

    [Fact]
    public void Allows_missing_secret_when_not_required()
    {
        var options = GuiSettingsValidator.CreateOptions(
            "https://checkmk.example.com",
            "mysite",
            "user",
            secret: null,
            "30",
            requireSecret: false);

        Assert.Equal(30, options.PollIntervalSeconds);
    }
}
