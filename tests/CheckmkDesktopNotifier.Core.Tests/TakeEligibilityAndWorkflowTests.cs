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
}
