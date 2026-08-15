namespace CheckmkDesktopNotifier.Core.Abstractions;

public interface IAlertStateStore
{
    AlertStateDocument? Load();

    void Save(AlertStateDocument document);
}
