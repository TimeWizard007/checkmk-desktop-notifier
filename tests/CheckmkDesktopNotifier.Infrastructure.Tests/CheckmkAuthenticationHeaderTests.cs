using CheckmkDesktopNotifier.Infrastructure.Authentication;
using CheckmkDesktopNotifier.Infrastructure.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Infrastructure.Tests;

public sealed class CheckmkAuthenticationHeaderTests
{
    [Fact]
    public void Builds_bearer_username_secret_value()
    {
        var value = CheckmkAuthenticationHeader.CreateValue("automation", TestOptions.Secret);

        Assert.Equal($"Bearer automation {TestOptions.Secret}", value);
        Assert.Equal("Authorization", CheckmkAuthenticationHeader.HeaderName);
    }

    [Fact]
    public void Trims_username_and_secret()
    {
        var value = CheckmkAuthenticationHeader.CreateValue("  automation  ", $"  {TestOptions.Secret}  ");

        Assert.Equal($"Bearer automation {TestOptions.Secret}", value);
    }

    [Theory]
    [InlineData(null, "secret")]
    [InlineData("", "secret")]
    [InlineData("   ", "secret")]
    [InlineData("user", null)]
    [InlineData("user", "")]
    [InlineData("user", "  ")]
    public void Rejects_empty_credentials(string? username, string? secret)
    {
        Assert.Throws<ArgumentException>(() => CheckmkAuthenticationHeader.CreateValue(username!, secret!));
    }
}
