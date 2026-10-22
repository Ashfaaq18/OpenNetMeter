using OpenNetMeter.Models;

namespace OpenNetMeter.Tests;

public class PinnedAdapterStatusTests
{
    [Fact]
    public void PinnedAndDisconnected_WarnsWithAdapterName()
    {
        var text = PinnedAdapterStatus.GetWarningText("{A}", "WireGuard", isConnected: false);

        Assert.Equal("Pinned adapter \"WireGuard\" isn't connected \u2014 nothing is being counted", text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void PinnedAndDisconnected_WithoutName_UsesFallbackName(string? name)
    {
        var text = PinnedAdapterStatus.GetWarningText("{A}", name, isConnected: false);

        Assert.Equal("Pinned adapter \"the selected adapter\" isn't connected \u2014 nothing is being counted", text);
    }

    [Fact]
    public void PinnedAndConnected_HasNoWarning()
    {
        Assert.Equal(string.Empty, PinnedAdapterStatus.GetWarningText("{A}", "WireGuard", isConnected: true));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("", true)]
    [InlineData(null, false)]
    public void Automatic_NeverWarns(string? pinnedId, bool isConnected)
    {
        Assert.Equal(string.Empty, PinnedAdapterStatus.GetWarningText(pinnedId, "WireGuard", isConnected));
    }
}
