using CheckmkDesktopNotifier.Core.Acknowledgements;

namespace CheckmkDesktopNotifier.Core.Tests;

public sealed class TakeConfirmationTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, false)]
    public void Only_explicit_true_proceeds(bool? dialogResult, bool expected)
    {
        Assert.Equal(expected, TakeConfirmation.ShouldProceed(dialogResult));
    }
}
