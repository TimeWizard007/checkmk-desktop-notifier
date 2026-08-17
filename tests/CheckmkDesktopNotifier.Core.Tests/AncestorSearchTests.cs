using CheckmkDesktopNotifier.Core;

namespace CheckmkDesktopNotifier.Core.Tests;

public sealed class AncestorSearchTests
{
    [Fact]
    public void Visual_source_is_treated_as_bar_background()
    {
        var border = new FakeBorder();

        Assert.True(ShouldHandleAsBarBackground(border));
        Assert.Null(AncestorSearch.Find<FakeNode, FakeButton>(border, ParentOf));
    }

    [Fact]
    public void Run_content_element_source_walks_to_text_block_without_throwing()
    {
        var border = new FakeBorder();
        var text = new FakeTextBlock { Parent = border };
        var run = new FakeRun { Parent = text };

        Assert.Same(text, AncestorSearch.Find<FakeNode, FakeTextBlock>(run, ParentOf));
        Assert.True(ShouldHandleAsBarBackground(run));
    }

    [Fact]
    public void Nested_text_content_is_treated_as_bar_background()
    {
        var grid = new FakeGrid();
        var border = new FakeBorder { Parent = grid };
        var text = new FakeTextBlock { Parent = border };
        var innerRun = new FakeRun { Parent = text };
        var nestedRun = new FakeRun { Parent = innerRun };

        Assert.Same(border, AncestorSearch.Find<FakeNode, FakeBorder>(nestedRun, ParentOf));
        Assert.True(ShouldHandleAsBarBackground(nestedRun));
        Assert.Null(AncestorSearch.Find<FakeNode, FakeButton>(nestedRun, ParentOf));
    }

    [Fact]
    public void Button_descendant_is_ignored_for_bar_gestures()
    {
        var button = new FakeButton();
        var text = new FakeTextBlock { Parent = button };
        var run = new FakeRun { Parent = text };

        Assert.Same(button, AncestorSearch.Find<FakeNode, FakeButton>(run, ParentOf));
        Assert.False(ShouldHandleAsBarBackground(run));
    }

    [Fact]
    public void Settings_gear_button_is_excluded_from_drag_and_toggle()
    {
        var gear = new FakeButton { Name = "Settings" };
        var glyph = new FakeTextBlock { Parent = gear };
        var run = new FakeRun { Parent = glyph };

        Assert.False(ShouldHandleAsBarBackground(run));
        Assert.False(ShouldHandleAsBarBackground(glyph));
        Assert.False(ShouldHandleAsBarBackground(gear));
    }

    [Fact]
    public void Compact_bar_counter_button_is_excluded_from_drag_and_toggle()
    {
        var counter = new FakeButton { Name = "CritCount" };
        var text = new FakeTextBlock { Parent = counter };
        var run = new FakeRun { Parent = text };

        Assert.False(ShouldHandleAsBarBackground(run));
        Assert.False(ShouldHandleAsBarBackground(text));
        Assert.False(ShouldHandleAsBarBackground(counter));
        Assert.True(ShouldHandleAsBarBackground(new FakeTextBlock()));
    }

    [Fact]
    public void Non_interactive_compact_bar_content_is_treated_as_bar_background()
    {
        var grid = new FakeGrid();
        var border = new FakeBorder { Parent = grid };
        var text = new FakeTextBlock { Parent = border };

        Assert.True(ShouldHandleAsBarBackground(border));
        Assert.True(ShouldHandleAsBarBackground(text));
        Assert.True(ShouldHandleAsBarBackground(grid));
    }

    [Fact]
    public void Unknown_or_null_parent_terminates_without_throwing()
    {
        Assert.True(ShouldHandleAsBarBackground(null));
        Assert.Null(AncestorSearch.Find<FakeNode, FakeButton>(null, ParentOf));

        var unknown = new FakeUnknown();
        Assert.True(ShouldHandleAsBarBackground(unknown));
        Assert.Null(AncestorSearch.Find<FakeNode, FakeButton>(unknown, ParentOf));
        Assert.Same(unknown, AncestorSearch.Find<FakeNode, FakeUnknown>(unknown, ParentOf));
    }

    [Fact]
    public void Run_like_content_element_must_not_use_visual_tree_parent()
    {
        Assert.Equal(ParentLookup.Content, WpfParentKind.For(isContentElement: true, isVisual: false, isVisual3D: false));
    }

    [Fact]
    public void Visual_and_visual3d_use_visual_tree_parent()
    {
        Assert.Equal(ParentLookup.VisualTree, WpfParentKind.For(isContentElement: false, isVisual: true, isVisual3D: false));
        Assert.Equal(ParentLookup.VisualTree, WpfParentKind.For(isContentElement: false, isVisual: false, isVisual3D: true));
    }

    [Fact]
    public void Content_element_classification_wins_over_visual_flags()
    {
        Assert.Equal(ParentLookup.Content, WpfParentKind.For(isContentElement: true, isVisual: true, isVisual3D: false));
    }

    [Fact]
    public void Unknown_dependency_object_uses_logical_parent()
    {
        Assert.Equal(ParentLookup.Logical, WpfParentKind.For(isContentElement: false, isVisual: false, isVisual3D: false));
    }

    [Fact]
    public void Parent_lookup_is_not_invoked_after_the_chain_ends()
    {
        var calls = 0;
        FakeNode? GetParent(FakeNode node)
        {
            calls++;
            return node.Parent;
        }

        var node = new FakeUnknown();
        Assert.Null(AncestorSearch.Find<FakeNode, FakeButton>(node, GetParent));
        Assert.Equal(1, calls);
    }

    private static bool ShouldHandleAsBarBackground(FakeNode? source) =>
        !AncestorSearch.IsInside<FakeNode, FakeButton>(source, ParentOf);

    private static FakeNode? ParentOf(FakeNode node) => node.Parent;

    private abstract class FakeNode
    {
        public FakeNode? Parent { get; init; }

        public string? Name { get; init; }
    }

    private sealed class FakeBorder : FakeNode;

    private sealed class FakeGrid : FakeNode;

    private sealed class FakeTextBlock : FakeNode;

    private sealed class FakeRun : FakeNode;

    private sealed class FakeButton : FakeNode;

    private sealed class FakeUnknown : FakeNode;
}
