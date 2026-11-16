using OpenNetMeter.Services;

namespace OpenNetMeter.Tests;

public class StartupRegistrationTests
{
    private const string RootedPath = @"C:\Program Files (x86)\OpenNetMeter\OpenNetMeter.exe";

    [Fact]
    public void RootedPathThatNoLongerExistsIsStale()
    {
        Assert.True(WindowsStartupRegistrationService.IsActionPathStale(RootedPath, _ => false));
    }

    [Fact]
    public void RootedPathThatExistsIsNotStale()
    {
        Assert.False(WindowsStartupRegistrationService.IsActionPathStale(RootedPath, _ => true));
    }

    [Fact]
    public void CheckedPathIsTheOneGiven()
    {
        string? checkedPath = null;

        WindowsStartupRegistrationService.IsActionPathStale(RootedPath, path =>
        {
            checkedPath = path;
            return true;
        });

        Assert.Equal(RootedPath, checkedPath);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BareCommandIsNeverStaleWhateverTheFileCheckSays(bool fileExists)
    {
        // Dev runs register "dotnet" plus the managed entry point; that must never be rewritten.
        Assert.False(WindowsStartupRegistrationService.IsActionPathStale("dotnet", _ => fileExists));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyPathIsNotStale(string? actionPath)
    {
        Assert.False(WindowsStartupRegistrationService.IsActionPathStale(actionPath, _ => false));
    }
}
