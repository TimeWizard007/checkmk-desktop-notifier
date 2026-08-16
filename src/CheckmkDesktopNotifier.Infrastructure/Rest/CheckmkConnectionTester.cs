using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure.Configuration;

namespace CheckmkDesktopNotifier.Infrastructure.Rest;

public sealed class CheckmkConnectionTester
{
    private readonly TimeProvider _clock;
    private readonly HttpMessageHandler? _handler;

    public CheckmkConnectionTester(TimeProvider? clock = null, HttpMessageHandler? handler = null)
    {
        _clock = clock ?? TimeProvider.System;
        _handler = handler;
    }

    public async Task<ConnectionTestResult> TestAsync(
        CheckmkOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            CheckmkOptionsValidator.ValidateGui(options, requireSecret: true);
        }
        catch (CheckmkOptionsValidationException ex)
        {
            return ConnectionTestResult.FromStatus(ConnectionTestStatus.InvalidConfiguration, ex.Message);
        }

        using var http = _handler is null
            ? new HttpClient()
            : new HttpClient(_handler, disposeHandler: false);
        http.BaseAddress = options.CreateApiBaseUri();
        http.Timeout = options.CreateHttpTimeout();
        http.DefaultRequestHeaders.ExpectContinue = false;

        try
        {
            var services = new CheckmkServiceClient(http, options, _clock);
            var serviceSnapshot = await services.GetCurrentProblemsAsync(cancellationToken).ConfigureAwait(false);
            if (!serviceSnapshot.IsSuccess)
            {
                return MapFailure(serviceSnapshot, services.LastHttpStatusCode);
            }

            var hosts = new CheckmkHostClient(http, options, _clock);
            var hostSnapshot = await hosts.GetHardHostProblemsAsync(cancellationToken).ConfigureAwait(false);
            if (!hostSnapshot.IsSuccess)
            {
                return MapFailure(
                    hostSnapshot,
                    hosts.LastHttpStatusCode,
                    servicesReachable: true,
                    serviceCount: serviceSnapshot.Problems.Count);
            }

            return ConnectionTestResult.FromStatus(
                ConnectionTestStatus.Success,
                "Connection successful",
                httpStatus: hosts.LastHttpStatusCode ?? services.LastHttpStatusCode,
                servicesReachable: true,
                hostsReachable: true,
                serviceCount: serviceSnapshot.Problems.Count,
                hostCount: hostSnapshot.Problems.Count);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ConnectionTestResult.FromStatus(
                ConnectionTestStatus.Timeout,
                HttpFailureClassifier.UserMessage(ConnectionTestStatus.Timeout));
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Security.Authentication.AuthenticationException)
        {
            var status = HttpFailureClassifier.ClassifyException(ex);
            return ConnectionTestResult.FromStatus(status, HttpFailureClassifier.UserMessage(status));
        }
    }

    private static ConnectionTestResult MapFailure(
        ProblemSnapshot snapshot,
        int? httpStatus,
        bool servicesReachable = false,
        int? serviceCount = null)
    {
        var status = snapshot.ErrorKind switch
        {
            SnapshotErrorKind.Authentication when httpStatus == 403 => ConnectionTestStatus.Forbidden,
            SnapshotErrorKind.Authentication => ConnectionTestStatus.Unauthorized,
            SnapshotErrorKind.Protocol => ConnectionTestStatus.UnexpectedApiResponse,
            SnapshotErrorKind.Configuration => ConnectionTestStatus.InvalidConfiguration,
            SnapshotErrorKind.Unavailable when snapshot.ErrorMessage?.Contains("timed out", StringComparison.OrdinalIgnoreCase) == true
                => ConnectionTestStatus.Timeout,
            SnapshotErrorKind.Unavailable when snapshot.ErrorMessage?.Contains("TLS", StringComparison.OrdinalIgnoreCase) == true
                => ConnectionTestStatus.TlsError,
            SnapshotErrorKind.Unavailable when snapshot.ErrorMessage?.Contains("cannot be reached", StringComparison.OrdinalIgnoreCase) == true
                => ConnectionTestStatus.Unreachable,
            _ => httpStatus switch
            {
                401 => ConnectionTestStatus.Unauthorized,
                403 => ConnectionTestStatus.Forbidden,
                _ => ConnectionTestStatus.Unavailable
            }
        };

        return ConnectionTestResult.FromStatus(
            status,
            snapshot.ErrorMessage ?? HttpFailureClassifier.UserMessage(status),
            httpStatus,
            servicesReachable,
            hostsReachable: false,
            serviceCount);
    }
}
