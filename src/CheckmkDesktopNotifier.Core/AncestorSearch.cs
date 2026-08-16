namespace CheckmkDesktopNotifier.Core;

public static class AncestorSearch
{
    public static TAncestor? Find<TNode, TAncestor>(TNode? current, Func<TNode, TNode?> getParent)
        where TNode : class
        where TAncestor : class
    {
        ArgumentNullException.ThrowIfNull(getParent);

        while (current is not null)
        {
            if (current is TAncestor match)
            {
                return match;
            }

            current = getParent(current);
        }

        return null;
    }

    public static bool IsInside<TNode, TAncestor>(TNode? current, Func<TNode, TNode?> getParent)
        where TNode : class
        where TAncestor : class
        => Find<TNode, TAncestor>(current, getParent) is not null;
}
