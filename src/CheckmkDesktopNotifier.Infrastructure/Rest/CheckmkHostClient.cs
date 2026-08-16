using System.Net;
using System.Net.Http.Headers;
using System.Security.Authentication;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure.Authentication;
using CheckmkDesktopNotifier.Infrastructure.Configuration;

namespace CheckmkDesktopNotifier.Infrastructure.Rest;

public sealed class HostCollectionProbeResult
{
    public int? HttpStatusCode { get; init; }

    public bool IsSuccess { get; init; }

    public SnapshotErrorKind? ErrorKind { get; init; }

    public string? ErrorMessage { get; init; }

    public HostCollectionInspection? Inspection { get; init; }
}

public sealed class CheckmkHostClient
{
    public const string HostCollectionPath = CheckmkHostCollectionContract.HostCollectionPath;

    private readonly HttpClient _http;
    private readonly CheckmkOptions _options;
    private readonly TimeProvider _clock;

    public int? LastHttpStatusCode { get; private set; }

    public CheckmkHostClient(HttpClient http, CheckmkOptions options, TimeProvider? clock = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? TimeProvider.System;
        CheckmkOptionsValidator.Validate(_options);
        if (_options.Mode != ClientMode.Real)
        {
            throw new InvalidOperationException("CheckmkHostClient requires Mode=Real.");
        }
    }

    public Task<HostCollectionProbeResult> ProbeVerifiedAsync(CancellationToken cancellationToken = default) =>
        ProbeAsync(HostCollectionPath, cancellationToken);

    public Task<HostCollectionProbeResult> ProbeDocumentedColumnsAsync(CancellationToken cancellationToken = default) =>
        ProbeAsync(CheckmkHostCollectionContract.CreateDocumentedColumnsRelativeUri(), cancellationToken);

    public async Task<ProblemSnapshot> GetHardHostProblemsAsync(CancellationToken cancellationToken = default)
    {
        LastHttpStatusCode = null;
        var retrievedAt = _clock.GetUtcNow();
        var siteId = new SiteId(_options.Site!);
        var raw = await SendGetAsync(
            CheckmkHostCollectionContract.CreateDocumentedColumnsRelativeUri(),
            cancellationToken).ConfigureAwait(false);

        LastHttpStatusCode = raw.HttpStatusCode;
        if (!raw.IsSuccess || raw.Json is null)
        {
            return ProblemSnapshot.Failure(
                retrievedAt,
                raw.ErrorKind ?? SnapshotErrorKind.Unavailable,
                raw.ErrorMessage,
                siteId);
        }

        try
        {
            var problems = HostProblemMapper.MapHardProblems(raw.Json, siteId);
            return ProblemSnapshot.Success(retrievedAt, siteId, problems);
        }
        catch (CheckmkProtocolException ex)
        {
            return ProblemSnapshot.Failure(retrievedAt, SnapshotErrorKind.Protocol, ex.Message, siteId);
        }
    }

    private async Task<HostCollectionProbeResult> ProbeAsync(string relativeUri, CancellationToken cancellationToken)
    {
        var raw = await SendGetAsync(relativeUri, cancellationToken).ConfigureAwait(false);
        LastHttpStatusCode = raw.HttpStatusCode;
        if (!raw.IsSuccess || raw.Json is null)
        {
            return new HostCollectionProbeResult
            {
                HttpStatusCode = raw.HttpStatusCode,
                IsSuccess = false,
                ErrorKind = raw.ErrorKind,
                ErrorMessage = raw.ErrorMessage
            };
        }

        try
        {
            return new HostCollectionProbeResult
            {
                HttpStatusCode = raw.HttpStatusCode,
                IsSuccess = true,
                Inspection = HostCollectionInspector.Inspect(raw.Json)
            };
        }
        catch (CheckmkProtocolException ex)
        {
            return new HostCollectionProbeResult
            {
                HttpStatusCode = raw.HttpStatusCode,
                IsSuccess = false,
                ErrorKind = SnapshotErrorKind.Protocol,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<RawGetResult> SendGetAsync(string relativeUri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, relativeUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation(
            CheckmkAuthenticationHeader.HeaderName,
            CheckmkAuthenticationHeader.CreateValue(_options.Username!, _options.Secret!));

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return RawGetResult.Fail(null, SnapshotErrorKind.Unavailable, "The Checkmk host request timed out.");
        }
        catch (Exception ex) when (ex is HttpRequestException or AuthenticationException)
        {
            var status = HttpFailureClassifier.ClassifyException(ex);
            return RawGetResult.Fail(null, SnapshotErrorKind.Unavailable, HttpFailureClassifier.UserMessage(status));
        }

        using (response)
        {
            var status = (int)response.StatusCode;
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return RawGetResult.Fail(
                    status,
                    SnapshotErrorKind.Authentication,
                    response.StatusCode == HttpStatusCode.Forbidden
                        ? "Checkmk access was forbidden (HTTP 403)."
                        : "Checkmk authentication failed (HTTP 401).");
            }

            if ((int)response.StatusCode is >= 400 and < 500)
            {
                return RawGetResult.Fail(status, SnapshotErrorKind.Configuration, $"Checkmk rejected the host query (HTTP {status}).");
            }

            if (!response.IsSuccessStatusCode)
            {
                return RawGetResult.Fail(status, SnapshotErrorKind.Unavailable, $"Checkmk is unavailable (HTTP {status}).");
            }

            try
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return RawGetResult.Ok(status, json);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException)
            {
                return RawGetResult.Fail(status, SnapshotErrorKind.Protocol, "The Checkmk host response body could not be read.");
            }
        }
    }

    private sealed class RawGetResult
    {
        public int? HttpStatusCode { get; init; }

        public bool IsSuccess { get; init; }

        public string? Json { get; init; }

        public SnapshotErrorKind? ErrorKind { get; init; }

        public string? ErrorMessage { get; init; }

        public static RawGetResult Ok(int status, string json) =>
            new() { HttpStatusCode = status, IsSuccess = true, Json = json };

        public static RawGetResult Fail(int? status, SnapshotErrorKind kind, string message) =>
            new()
            {
                HttpStatusCode = status,
                IsSuccess = false,
                ErrorKind = kind,
                ErrorMessage = message
            };
    }
}
