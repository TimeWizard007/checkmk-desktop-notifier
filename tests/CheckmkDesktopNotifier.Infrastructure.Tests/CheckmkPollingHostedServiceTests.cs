using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Core.Mock;
using CheckmkDesktopNotifier.Core.Persistence;
using CheckmkDesktopNotifier.Core.State;
using CheckmkDesktopNotifier.Infrastructure;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Polling;
using CheckmkDesktopNotifier.Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CheckmkDesktopNotifier.Infrastructure.Tests;

public sealed class CheckmkPollingHostedServiceTests
{
    [Fact]
    public void Mock_mode_does_not_enable_background_polling_or_demo_in_real_profile()
    {
        Assert.True(CheckmkRuntimeProfile.UseDemoBootstrap(ClientMode.Mock));
        Assert.False(CheckmkRuntimeProfile.UseBackgroundPolling(ClientMode.Mock));
        Assert.False(CheckmkRuntimeProfile.UsePersistentAlertState(ClientMode.Mock));

        Assert.False(CheckmkRuntimeProfile.UseDemoBootstrap(ClientMode.Real));
        Assert.True(CheckmkRuntimeProfile.UseBackgroundPolling(ClientMode.Real));
        Assert.True(CheckmkRuntimeProfile.UsePersistentAlertState(ClientMode.Real));
    }

    [Fact]
    public async Task Mock_hosted_service_does_not_call_checkmk_client()
    {
        var clock = TimeProvider.System;
        var client = new RecordingCheckmkClient(clock)
        {
            Snapshot = ProblemSnapshot.Success(clock.GetUtcNow(), new SiteId("mysite"), [])
        };
        var options = new CheckmkOptions { Mode = ClientMode.Mock, PollIntervalSeconds = 60 };
        var alerts = new AlertStateService(new InMemoryAlertStateStore(), clock);
        var poller = new CheckmkPoller(client, alerts, options, clock);
        var hosted = new CheckmkPollingHostedService(options, poller);

        await hosted.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await hosted.StopAsync(CancellationToken.None);

        Assert.Equal(0, client.Calls);
        Assert.IsType<MockCheckmkClient>(CreateMockClientFromDi());
    }

    [Fact]
    public async Task Real_hosted_service_polls_immediately()
    {
        var clock = TimeProvider.System;
        var started = new TaskCompletionSource();
        var client = new RecordingCheckmkClient(clock)
        {
            Handler = _ =>
            {
                started.TrySetResult();
                return Task.FromResult(ProblemSnapshot.Success(clock.GetUtcNow(), new SiteId("mysite"), []));
            }
        };
        var options = new CheckmkOptions
        {
            Mode = ClientMode.Real,
            BaseUrl = "https://checkmk.example.invalid",
            Site = "mysite",
            Username = "automation",
            Secret = TestOptions.Secret,
            PollIntervalSeconds = 60
        };
        var alerts = new AlertStateService(new InMemoryAlertStateStore(), clock);
        var poller = new CheckmkPoller(client, alerts, options, clock);
        var hosted = new CheckmkPollingHostedService(options, poller);

        await hosted.StartAsync(CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await hosted.StopAsync(CancellationToken.None);

        Assert.True(client.Calls >= 1);
    }

    [Fact]
    public async Task Real_hosted_service_stop_cancels_the_loop()
    {
        var clock = TimeProvider.System;
        var started = new TaskCompletionSource();
        var client = new RecordingCheckmkClient(clock)
        {
            Handler = _ =>
            {
                started.TrySetResult();
                return Task.FromResult(ProblemSnapshot.Success(clock.GetUtcNow(), new SiteId("mysite"), []));
            }
        };
        var options = TestOptions.Real();
        var alerts = new AlertStateService(new InMemoryAlertStateStore(), clock);
        var poller = new CheckmkPoller(client, alerts, options, clock);
        var hosted = new CheckmkPollingHostedService(options, poller);

        await hosted.StartAsync(CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await hosted.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Real_mode_does_not_register_mock_client()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddCheckmkClient(TestOptions.Real());

        Assert.DoesNotContain(services, descriptor => descriptor.ImplementationType == typeof(MockCheckmkClient));
        Assert.False(CheckmkRuntimeProfile.UseDemoBootstrap(ClientMode.Real));
        Assert.True(CheckmkRuntimeProfile.UseBackgroundPolling(ClientMode.Real));
    }

    [Fact]
    public void Http_timeout_is_shorter_than_poll_interval()
    {
        var options = new CheckmkOptions { PollIntervalSeconds = 60 };
        Assert.True(options.CreateHttpTimeout() < options.PollInterval);
        Assert.True(options.CreateHttpTimeout().TotalSeconds >= CheckmkOptions.MinimumHttpTimeoutSeconds);

        var minimum = new CheckmkOptions { PollIntervalSeconds = CheckmkOptions.MinimumPollIntervalSeconds };
        Assert.True(minimum.CreateHttpTimeout() < minimum.PollInterval);
    }

    private static ICheckmkClient CreateMockClientFromDi()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAlertStateStore, InMemoryAlertStateStore>();
        services.AddSingleton<IAlertStateService, AlertStateService>();
        services.AddCheckmkClient(new CheckmkOptions { Mode = ClientMode.Mock, PollIntervalSeconds = 60 });
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<ICheckmkClient>();
    }
}
