namespace CheckmkDesktopNotifier.Core;

public enum ParentLookup
{
    Content = 0,
    VisualTree = 1,
    Logical = 2
}

public static class WpfParentKind
{
    public static ParentLookup For(bool isContentElement, bool isVisual, bool isVisual3D)
    {
        if (isContentElement)
        {
            return ParentLookup.Content;
        }

        if (isVisual || isVisual3D)
        {
            return ParentLookup.VisualTree;
        }

        return ParentLookup.Logical;
    }
}
