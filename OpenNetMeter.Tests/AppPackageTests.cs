using OpenNetMeter.Services;

namespace OpenNetMeter.Tests;

public class AppPackageTests
{
    [Fact]
    public void IsPackaged_IsFalseUnderTheTestHost()
    {
        Assert.False(AppPackage.IsPackaged);
    }
}
