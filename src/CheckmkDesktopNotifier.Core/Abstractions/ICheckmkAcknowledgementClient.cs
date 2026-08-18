using CheckmkDesktopNotifier.Core.Acknowledgements;

namespace CheckmkDesktopNotifier.Core.Abstractions;

/// <summary>
/// Write-only Checkmk acknowledgement adapter. Keep this separate from <see cref="ICheckmkClient"/>.
/// </summary>
public interface ICheckmkAcknowledgementClient
{
    Task<AcknowledgementWriteResult> AcknowledgeHostAsync(
        string hostName,
        string displayName,
        CancellationToken cancellationToken = default);

    Task<AcknowledgementWriteResult> AcknowledgeServiceAsync(
        string hostName,
        string serviceDescription,
        string displayName,
        CancellationToken cancellationToken = default);

    Task<AcknowledgementWriteResult> DeleteHostAsync(
        string hostName,
        CancellationToken cancellationToken = default);

    Task<AcknowledgementWriteResult> DeleteServiceAsync(
        string hostName,
        string serviceDescription,
        CancellationToken cancellationToken = default);
}
