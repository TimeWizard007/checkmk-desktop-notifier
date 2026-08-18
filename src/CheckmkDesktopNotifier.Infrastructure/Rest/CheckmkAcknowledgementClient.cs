using System.Net;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Acknowledgements;
using CheckmkDesktopNotifier.Infrastructure.Authentication;
using CheckmkDesktopNotifier.Infrastructure.Configuration;

namespace CheckmkDesktopNotifier.Infrastructure.Rest;

public sealed class CheckmkAcknowledgementClient : ICheckmkAcknowledgementClient
{
    public const string ServiceAcknowledgePath = "domain-types/acknowledge/collections/service";
    public const string HostAcknowledgePath = "domain-types/acknowledge/collections/host";

    private readonly HttpClient _http;
    private readonly CheckmkOptions _options;

    public CheckmkAcknowledgementClient(HttpClient http, CheckmkOptions options)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        CheckmkOptionsValidator.Validate(_options);
        if (_options.Mode != ClientMode.Real)
        {
            throw new InvalidOperationException("CheckmkAcknowledgementClient requires Mode=Real.");
        }
    }

    public Task<AcknowledgementWriteResult> AcknowledgeHostAsync(
        string hostName,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var comment = CdnTakeComment.Format(displayName);
        var body = new AcknowledgeHostRequest
        {
            Sticky = true,
            Persistent = false,
            Notify = false,
            Comment = comment,
            AcknowledgeType = "host",
            HostName = hostName.Trim()
        };
        return SendAsync(HostAcknowledgePath, body, cancellationToken);
    }

    public Task<AcknowledgementWriteResult> AcknowledgeServiceAsync(
        string hostName,
        string serviceDescription,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var comment = CdnTakeComment.Format(displayName);
        var body = new AcknowledgeServiceRequest
        {
            Sticky = true,
            Persistent = false,
            Notify = false,
            Comment = comment,
            AcknowledgeType = "service",
            HostName = hostName.Trim(),
            ServiceDescription = serviceDescription.Trim()
        };
        return SendAsync(ServiceAcknowledgePath, body, cancellationToken);
    }

    private async Task<AcknowledgementWriteResult> SendAsync(
        string relativePath,
        object body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, relativePath);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation(
            CheckmkAuthenticationHeader.HeaderName,
            CheckmkAuthenticationHeader.CreateValue(_options.Username!, _options.Secret!));
        request.Content = new StringContent(
            JsonSerializer.Serialize(body, RestJson.SerializerOptions),
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AcknowledgementWriteResult.Canceled;
        }
        catch (OperationCanceledException)
        {
            return AcknowledgementWriteResult.Unavailable;
        }
        catch (Exception ex) when (ex is HttpRequestException or AuthenticationException)
        {
            return AcknowledgementWriteResult.Unavailable;
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized)
            {
                return AcknowledgementWriteResult.Unauthorized;
            }

            if (response.StatusCode is HttpStatusCode.Forbidden)
            {
                return AcknowledgementWriteResult.Forbidden;
            }

            if ((int)response.StatusCode is 400 or 422)
            {
                return AcknowledgementWriteResult.InvalidRequest;
            }

            if ((int)response.StatusCode is >= 200 and < 300)
            {
                return AcknowledgementWriteResult.Success;
            }

            return AcknowledgementWriteResult.Unavailable;
        }
    }
}

public sealed class DelegatingCheckmkAcknowledgementClient : ICheckmkAcknowledgementClient
{
    private readonly object _gate = new();
    private ICheckmkAcknowledgementClient _inner;

    public DelegatingCheckmkAcknowledgementClient(ICheckmkAcknowledgementClient inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public ICheckmkAcknowledgementClient Inner
    {
        get
        {
            lock (_gate)
            {
                return _inner;
            }
        }
    }

    public void SetInner(ICheckmkAcknowledgementClient inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        lock (_gate)
        {
            _inner = inner;
        }
    }

    public Task<AcknowledgementWriteResult> AcknowledgeHostAsync(
        string hostName,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        return Inner.AcknowledgeHostAsync(hostName, displayName, cancellationToken);
    }

    public Task<AcknowledgementWriteResult> AcknowledgeServiceAsync(
        string hostName,
        string serviceDescription,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        return Inner.AcknowledgeServiceAsync(hostName, serviceDescription, displayName, cancellationToken);
    }
}

public sealed class UnavailableCheckmkAcknowledgementClient : ICheckmkAcknowledgementClient
{
    public Task<AcknowledgementWriteResult> AcknowledgeHostAsync(
        string hostName,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(AcknowledgementWriteResult.NotConfigured);
    }

    public Task<AcknowledgementWriteResult> AcknowledgeServiceAsync(
        string hostName,
        string serviceDescription,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(AcknowledgementWriteResult.NotConfigured);
    }
}
