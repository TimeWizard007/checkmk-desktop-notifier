using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Authentication;
using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure.Authentication;
using CheckmkDesktopNotifier.Infrastructure.Configuration;

namespace CheckmkDesktopNotifier.Infrastructure.Rest;

public sealed class CheckmkServiceClient : ICheckmkClient
{
    public const string ServiceCollectionPath = "domain-types/service/collections/all";

    private readonly HttpClient _http;
    private readonly CheckmkOptions _options;
    private readonly TimeProvider _clock;

    public int? LastHttpStatusCode { get; private set; }

    public CheckmkServiceClient(HttpClient http, CheckmkOptions options, TimeProvider? clock = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? TimeProvider.System;
        CheckmkOptionsValidator.Validate(_options);
        if (_options.Mode != ClientMode.Real)
        {
            throw new InvalidOperationException("CheckmkServiceClient requires Mode=Real.");
        }
    }

    public async Task<ProblemSnapshot> GetCurrentProblemsAsync(CancellationToken cancellationToken = default)
    {
        LastHttpStatusCode = null;
        var retrievedAt = _clock.GetUtcNow();
        var siteId = new SiteId(_options.Site!);

        using var request = new HttpRequestMessage(HttpMethod.Post, ServiceCollectionPath);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation(
            CheckmkAuthenticationHeader.HeaderName,
            CheckmkAuthenticationHeader.CreateValue(_options.Username!, _options.Secret!));
        request.Content = JsonContent.Create(CheckmkServiceStatusRequest.Verified, options: RestJson.SerializerOptions);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ProblemSnapshot.Failure(retrievedAt, SnapshotErrorKind.Unavailable, "The Checkmk request timed out.", siteId);
        }
        catch (Exception ex) when (ex is HttpRequestException or AuthenticationException)
        {
            var status = HttpFailureClassifier.ClassifyException(ex);
            return ProblemSnapshot.Failure(
                retrievedAt,
                SnapshotErrorKind.Unavailable,
                HttpFailureClassifier.UserMessage(status),
                siteId);
        }

        using (response)
        {
            LastHttpStatusCode = (int)response.StatusCode;
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return ProblemSnapshot.Failure(
                    retrievedAt,
                    SnapshotErrorKind.Authentication,
                    response.StatusCode == HttpStatusCode.Forbidden
                        ? "Checkmk access was forbidden (HTTP 403)."
                        : "Checkmk authentication failed (HTTP 401).",
                    siteId);
            }

            if ((int)response.StatusCode is >= 400 and < 500)
            {
                return ProblemSnapshot.Failure(
                    retrievedAt,
                    SnapshotErrorKind.Configuration,
                    $"Checkmk rejected the service query (HTTP {(int)response.StatusCode}).",
                    siteId);
            }

            if (!response.IsSuccessStatusCode)
            {
                return ProblemSnapshot.Failure(
                    retrievedAt,
                    SnapshotErrorKind.Unavailable,
                    $"Checkmk is unavailable (HTTP {(int)response.StatusCode}).",
                    siteId);
            }

            string json;
            try
            {
                json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException)
            {
                return ProblemSnapshot.Failure(retrievedAt, SnapshotErrorKind.Protocol, "The Checkmk response body could not be read.", siteId);
            }

            try
            {
                var problems = ServiceProblemMapper.MapCollection(json, siteId);
                return ProblemSnapshot.Success(retrievedAt, siteId, problems);
            }
            catch (CheckmkProtocolException ex)
            {
                return ProblemSnapshot.Failure(retrievedAt, SnapshotErrorKind.Protocol, ex.Message, siteId);
            }
        }
    }
}
