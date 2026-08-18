using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Acknowledgements;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure.Notifications;
using CheckmkDesktopNotifier.Infrastructure.Polling;

namespace CheckmkDesktopNotifier.Infrastructure;

public sealed class CheckmkTakeService : ITakeService
{
    private readonly ICheckmkAcknowledgementClient _acknowledgements;
    private readonly IProblemPoller _poller;
    private readonly IAlertStateService _alerts;
    private readonly IUserPreferences _preferences;
    private readonly TakeSessionState _session;

    public CheckmkTakeService(
        ICheckmkAcknowledgementClient acknowledgements,
        IProblemPoller poller,
        IAlertStateService alerts,
        IUserPreferences preferences,
        TakeSessionState session)
    {
        _acknowledgements = acknowledgements ?? throw new ArgumentNullException(nameof(acknowledgements));
        _poller = poller ?? throw new ArgumentNullException(nameof(poller));
        _alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public async Task<TakeOperationResult> TakeAsync(
        MonitoredObjectId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        try
        {
            if (_session.AcknowledgeForbidden)
            {
                return TakeOperationResult.Forbidden;
            }

            if (!_preferences.TakeEnabled)
            {
                return TakeOperationResult.FeatureDisabled;
            }

            var displayName = TakeDisplayName.Normalize(_preferences.TakeDisplayName);
            if (displayName is null)
            {
                return TakeOperationResult.FeatureDisabled;
            }

            var existing = _alerts.GetOpenIncidents()
                .FirstOrDefault(incident => incident.ObjectId.Equals(id));
            if (existing?.IsAcknowledgedInCheckmk == true)
            {
                return TakeOperationResult.AlreadyAcknowledged;
            }

            AcknowledgementWriteResult write;
            if (id.Kind == ObjectKind.Host)
            {
                write = await _acknowledgements
                    .AcknowledgeHostAsync(id.HostName, displayName, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                write = await _acknowledgements
                    .AcknowledgeServiceAsync(
                        id.HostName,
                        id.ServiceDescription ?? string.Empty,
                        displayName,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (write.Status == AcknowledgementWriteStatus.Forbidden)
            {
                _session.MarkAcknowledgeForbidden();
            }

            if (write.Status != AcknowledgementWriteStatus.Success)
            {
                return ResultFor(TakeWorkflow.AfterWrite(write.Status, refreshSucceeded: false, snapshotAcknowledged: false));
            }

            var refreshSucceeded = false;
            try
            {
                await _poller.RefreshWhenIdleAsync(cancellationToken).ConfigureAwait(false);
                refreshSucceeded = true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
            }

            var confirmed = refreshSucceeded
                && _alerts.GetOpenIncidents()
                    .Any(incident => incident.ObjectId.Equals(id) && incident.IsAcknowledgedInCheckmk);
            return ResultFor(TakeWorkflow.AfterWrite(write.Status, refreshSucceeded, confirmed));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return TakeOperationResult.Unavailable;
        }
    }

    public async Task<TakeOperationResult> ReleaseAsync(
        MonitoredObjectId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        try
        {
            if (_session.AcknowledgeForbidden)
            {
                return TakeOperationResult.Forbidden;
            }

            await TryRefreshAsync(cancellationToken).ConfigureAwait(false);

            var current = Find(id);
            if (current is null || !TakeEligibility.IsCdnTake(current.IsAcknowledgedInCheckmk, current.IsTakenByNotifier))
            {
                return TakeOperationResult.NotEligible;
            }

            AcknowledgementWriteResult write;
            if (id.Kind == ObjectKind.Host)
            {
                write = await _acknowledgements
                    .DeleteHostAsync(id.HostName, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                write = await _acknowledgements
                    .DeleteServiceAsync(
                        id.HostName,
                        id.ServiceDescription ?? string.Empty,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (write.Status == AcknowledgementWriteStatus.Forbidden)
            {
                _session.MarkAcknowledgeForbidden();
            }

            var refreshSucceeded = await TryRefreshAsync(cancellationToken).ConfigureAwait(false);
            var after = Find(id);
            var stillTaken = TakeEligibility.IsCdnTake(
                after?.IsAcknowledgedInCheckmk == true,
                after?.IsTakenByNotifier == true);
            return ResultFor(TakeWorkflow.AfterDelete(write.Status, refreshSucceeded, stillTaken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return TakeOperationResult.Unavailable;
        }
    }

    private OpenIncident? Find(MonitoredObjectId id) =>
        _alerts.GetOpenIncidents().FirstOrDefault(incident => incident.ObjectId.Equals(id));

    private async Task<bool> TryRefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _poller.RefreshWhenIdleAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static TakeOperationResult ResultFor(TakeOperationStatus status) =>
        status switch
        {
            TakeOperationStatus.Confirmed => TakeOperationResult.Confirmed,
            TakeOperationStatus.SentAwaitingRefresh => TakeOperationResult.SentAwaitingRefresh,
            TakeOperationStatus.Unauthorized => TakeOperationResult.Unauthorized,
            TakeOperationStatus.Forbidden => TakeOperationResult.Forbidden,
            TakeOperationStatus.InvalidRequest => TakeOperationResult.InvalidRequest,
            TakeOperationStatus.FeatureDisabled => TakeOperationResult.FeatureDisabled,
            TakeOperationStatus.AlreadyAcknowledged => TakeOperationResult.AlreadyAcknowledged,
            TakeOperationStatus.NotEligible => TakeOperationResult.NotEligible,
            _ => TakeOperationResult.Unavailable
        };
}
