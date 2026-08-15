using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Core.Persistence;

public sealed class InMemoryAlertStateStore : IAlertStateStore
{
    private AlertStateDocument? _document;

    public AlertStateDocument? Load() => _document;

    public void Save(AlertStateDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _document = document;
    }
}
