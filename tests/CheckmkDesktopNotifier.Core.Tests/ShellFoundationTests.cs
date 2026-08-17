using CheckmkDesktopNotifier.Core;

namespace CheckmkDesktopNotifier.Core.Tests;

public sealed class ShellFoundationTests
{
    [Fact]
    public void Repository_uri_is_the_public_github_project()
    {
        Assert.Equal("https://github.com/TimeWizard007/checkmk-desktop-notifier", ProductInfo.RepositoryUrl);
        Assert.Equal("TimeWizard007", ProductInfo.Author);
        Assert.Equal("Desktop monitor and notifier for Checkmk", ProductInfo.Description);
        Assert.Equal(Uri.UriSchemeHttps, ProductInfo.Repository.Scheme);
        Assert.Equal("github.com", ProductInfo.Repository.Host);
        Assert.Equal(ProductInfo.RepositoryUrl, ProductInfo.Repository.AbsoluteUri.TrimEnd('/'));
    }

    [Fact]
    public void Version_comes_from_assembly_metadata_not_a_ui_literal()
    {
        Assert.Equal("0.4.2", ApplicationVersion.From("0.4.2+abc123", new Version(1, 0, 0, 0)));
        Assert.Equal("0.4.0", ApplicationVersion.From(" 0.4.0 ", null));
        Assert.Equal("1.2.3.4", ApplicationVersion.From(null, new Version(1, 2, 3, 4)));
        Assert.Equal("unknown", ApplicationVersion.From("  ", null));
        Assert.NotEqual("1.0.0", ApplicationVersion.From("0.4.0", new Version(1, 0, 0, 0)));
    }

    [Fact]
    public void Initializing_wins_over_persisted_or_poller_connected()
    {
        var label = ShellConnectionLabelMapper.Map(
            ShellPhase.Initializing,
            unconfiguredReal: false,
            SessionPollerKind.Connected);
        Assert.Equal(ShellConnectionLabel.Initializing, label);
    }

    [Fact]
    public void Ready_unconfigured_is_setup_required_not_connected()
    {
        var label = ShellConnectionLabelMapper.Map(
            ShellPhase.Ready,
            unconfiguredReal: true,
            SessionPollerKind.Idle);
        Assert.Equal(ShellConnectionLabel.SetupRequired, label);
    }

    [Fact]
    public void Ready_poller_kinds_map_without_using_historical_success()
    {
        Assert.Equal(
            ShellConnectionLabel.Refreshing,
            ShellConnectionLabelMapper.Map(ShellPhase.Ready, false, SessionPollerKind.Refreshing));
        Assert.Equal(
            ShellConnectionLabel.Connected,
            ShellConnectionLabelMapper.Map(ShellPhase.Ready, false, SessionPollerKind.Connected));
        Assert.Equal(
            ShellConnectionLabel.Error,
            ShellConnectionLabelMapper.Map(ShellPhase.Ready, false, SessionPollerKind.Error));
        Assert.Equal(
            ShellConnectionLabel.None,
            ShellConnectionLabelMapper.Map(ShellPhase.Ready, false, SessionPollerKind.Idle));
    }

    [Fact]
    public void Single_instance_gate_prevents_duplicate_entry()
    {
        var gate = new SingleInstanceGate();
        Assert.True(gate.TryEnter());
        Assert.True(gate.IsOpen);
        Assert.False(gate.TryEnter());
        gate.Exit();
        Assert.False(gate.IsOpen);
        Assert.True(gate.TryEnter());
    }

    [Fact]
    public void Shutdown_gate_is_single_flight()
    {
        var gate = new ShutdownGate();
        Assert.True(gate.TryBegin());
        Assert.True(gate.HasStarted);
        Assert.False(gate.TryBegin());
    }

    [Fact]
    public void Shutdown_step_order_stops_polling_before_ui_and_tray()
    {
        Assert.Equal(
            [
                "PreventNewPolling",
                "CancelMonitoringSession",
                "CloseDialogs",
                "CloseProblemList",
                "DisposeTray",
                "ShutdownApplication"
            ],
            ShutdownSteps.Ordered);
    }

    [Fact]
    public void Tray_and_gear_share_the_same_command_names()
    {
        string[] commands = ["ShowBar", "HideToTray", "ToggleBar", "ShowSettings", "ShowAbout", "Exit"];
        Assert.Contains("ShowSettings", commands);
        Assert.Contains("ShowAbout", commands);
        Assert.Contains("Exit", commands);
        Assert.Contains("ShowBar", commands);
        Assert.Contains("HideToTray", commands);
        Assert.Contains("ToggleBar", commands);
    }
}
