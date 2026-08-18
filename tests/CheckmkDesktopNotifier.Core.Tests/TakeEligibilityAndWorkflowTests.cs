using CheckmkDesktopNotifier.Core.Acknowledgements;

namespace CheckmkDesktopNotifier.Core.Tests;

public sealed class TakeEligibilityAndWorkflowTests
{
    [Fact]
    public void Take_is_disabled_when_feature_is_disabled()
    {
        Assert.False(TakeEligibility.CanOfferTake(
            takeEnabled: false,
            displayName: "Michał",
            isRealMonitoring: true,
            acknowledgeForbidden: false,
            alreadyAcknowledged: false,
            isTaking: false,
            isReady: true));
    }

    [Fact]
    public void Take_is_disabled_when_display_name_is_empty()
    {
        Assert.False(TakeEligibility.CanOfferTake(
            takeEnabled: true,
            displayName: "   ",
            isRealMonitoring: true,
            acknowledgeForbidden: false,
            alreadyAcknowledged: false,
            isTaking: false,
            isReady: true));
    }

    [Fact]
    public void Take_is_unavailable_when_already_acknowledged()
    {
        Assert.False(TakeEligibility.CanOfferTake(
            takeEnabled: true,
            displayName: "Michał",
            isRealMonitoring: true,
            acknowledgeForbidden: false,
            alreadyAcknowledged: true,
            isTaking: false,
            isReady: true));
    }

    [Fact]
    public void Taking_state_disables_duplicate_click()
    {
        Assert.False(TakeEligibility.CanOfferTake(
            takeEnabled: true,
            displayName: "Michał",
            isRealMonitoring: true,
            acknowledgeForbidden: false,
            alreadyAcknowledged: false,
            isTaking: true,
            isReady: true));
    }

    [Fact]
    public void Forbidden_session_disables_take()
    {
        Assert.False(TakeEligibility.CanOfferTake(
            takeEnabled: true,
            displayName: "Michał",
            isRealMonitoring: true,
            acknowledgeForbidden: true,
            alreadyAcknowledged: false,
            isTaking: false,
            isReady: true));
    }

    [Fact]
    public void Eligible_take_is_offered()
    {
        Assert.True(TakeEligibility.CanOfferTake(
            takeEnabled: true,
            displayName: "Michał",
            isRealMonitoring: true,
            acknowledgeForbidden: false,
            alreadyAcknowledged: false,
            isTaking: false,
            isReady: true));
    }

    [Fact]
    public void Success_with_refresh_confirms()
    {
        Assert.Equal(
            TakeOperationStatus.Confirmed,
            TakeWorkflow.AfterWrite(AcknowledgementWriteStatus.Success, refreshSucceeded: true, snapshotAcknowledged: true));
    }

    [Fact]
    public void Success_without_refresh_awaits()
    {
        Assert.Equal(
            TakeOperationStatus.SentAwaitingRefresh,
            TakeWorkflow.AfterWrite(AcknowledgementWriteStatus.Success, refreshSucceeded: false, snapshotAcknowledged: false));
    }

    [Fact]
    public void Failed_take_returns_to_idle_statuses()
    {
        Assert.Equal(
            TakeOperationStatus.Unauthorized,
            TakeWorkflow.AfterWrite(AcknowledgementWriteStatus.Unauthorized, false, false));
        Assert.Equal(
            TakeOperationStatus.Forbidden,
            TakeWorkflow.AfterWrite(AcknowledgementWriteStatus.Forbidden, false, false));
        Assert.Equal(
            TakeOperationStatus.Unavailable,
            TakeWorkflow.AfterWrite(AcknowledgementWriteStatus.Unavailable, false, false));
    }

    [Fact]
    public void Generic_ack_displays_ack()
    {
        Assert.Equal(
            TakeRowVisual.Acknowledged,
            TakeRowPresentation.Classify(
                alreadyAcknowledged: true,
                isTakenByNotifier: false,
                canOfferTake: false,
                isTakingThis: false));
    }

    [Fact]
    public void Cdn_ack_displays_taken()
    {
        Assert.Equal(
            TakeRowVisual.Taken,
            TakeRowPresentation.Classify(
                alreadyAcknowledged: true,
                isTakenByNotifier: true,
                canOfferTake: false,
                isTakingThis: false));
    }

    [Fact]
    public void Taking_visual_while_in_flight()
    {
        Assert.Equal(
            TakeRowVisual.Taking,
            TakeRowPresentation.Classify(
                alreadyAcknowledged: false,
                isTakenByNotifier: false,
                canOfferTake: false,
                isTakingThis: true));
    }

    [Fact]
    public void Display_name_rejects_empty_and_overlong()
    {
        Assert.Null(TakeDisplayName.Normalize("  "));
        Assert.Null(TakeDisplayName.Normalize(new string('x', TakeDisplayName.MaxLength + 1)));
        Assert.Equal("Michał", TakeDisplayName.Normalize("  Michał  "));
    }

    [Fact]
    public void Generic_ack_cannot_release()
    {
        Assert.False(TakeEligibility.CanOfferRelease(
            isAcknowledged: true,
            isTakenByNotifier: false,
            isRealMonitoring: true,
            acknowledgeForbidden: false,
            isBusy: false,
            isReady: true));
    }

    [Fact]
    public void Cdn_take_can_release()
    {
        Assert.True(TakeEligibility.CanOfferRelease(
            isAcknowledged: true,
            isTakenByNotifier: true,
            isRealMonitoring: true,
            acknowledgeForbidden: false,
            isBusy: false,
            isReady: true));
    }

    [Fact]
    public void Another_admins_cdn_take_can_release()
    {
        Assert.True(TakeEligibility.IsCdnTake(isAcknowledged: true, isTakenByNotifier: true));
        Assert.True(TakeEligibility.CanOfferRelease(
            isAcknowledged: true,
            isTakenByNotifier: true,
            isRealMonitoring: true,
            acknowledgeForbidden: false,
            isBusy: false,
            isReady: true));
    }

    [Fact]
    public void Releasing_visual_while_in_flight()
    {
        Assert.Equal(
            TakeRowVisual.Releasing,
            TakeRowPresentation.Classify(
                alreadyAcknowledged: true,
                isTakenByNotifier: true,
                canOfferTake: false,
                isTakingThis: false,
                isReleasingThis: true));
    }

    [Fact]
    public void Delete_success_with_refresh_confirms_when_no_longer_taken()
    {
        Assert.Equal(
            TakeOperationStatus.Confirmed,
            TakeWorkflow.AfterDelete(AcknowledgementWriteStatus.Success, refreshSucceeded: true, stillTaken: false));
    }

    [Fact]
    public void Delete_success_without_refresh_awaits()
    {
        Assert.Equal(
            TakeOperationStatus.SentAwaitingRefresh,
            TakeWorkflow.AfterDelete(AcknowledgementWriteStatus.Success, refreshSucceeded: false, stillTaken: true));
    }

    [Fact]
    public void Concurrent_invalid_request_confirms_when_already_released()
    {
        Assert.Equal(
            TakeOperationStatus.Confirmed,
            TakeWorkflow.AfterDelete(AcknowledgementWriteStatus.InvalidRequest, refreshSucceeded: true, stillTaken: false));
    }

    [Fact]
    public void Successful_or_waiting_take_release_does_not_show_a_dialog()
    {
        Assert.False(TakeCompletionUi.ShowsErrorDialog(TakeOperationStatus.Confirmed));
        Assert.False(TakeCompletionUi.ShowsErrorDialog(TakeOperationStatus.SentAwaitingRefresh));
        Assert.False(TakeCompletionUi.ShowsErrorDialog(TakeOperationStatus.Cancelled));
        Assert.False(TakeCompletionUi.ShowsErrorDialog(TakeOperationStatus.AlreadyAcknowledged));
        Assert.False(TakeCompletionUi.ShowsErrorDialog(TakeOperationStatus.NotEligible));
        Assert.False(TakeCompletionUi.KeepWaitingVisual(TakeOperationStatus.Confirmed));
        Assert.True(TakeCompletionUi.KeepWaitingVisual(TakeOperationStatus.SentAwaitingRefresh));
    }

    [Fact]
    public void Take_release_errors_still_show_a_dialog()
    {
        Assert.True(TakeCompletionUi.ShowsErrorDialog(TakeOperationStatus.Forbidden));
        Assert.True(TakeCompletionUi.ShowsErrorDialog(TakeOperationStatus.Unauthorized));
        Assert.True(TakeCompletionUi.ShowsErrorDialog(TakeOperationStatus.InvalidRequest));
        Assert.True(TakeCompletionUi.ShowsErrorDialog(TakeOperationStatus.Unavailable));
        Assert.False(TakeCompletionUi.KeepWaitingVisual(TakeOperationStatus.Unavailable));
    }
}
