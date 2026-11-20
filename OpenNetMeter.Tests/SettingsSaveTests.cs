using System.Text.Json.Nodes;
using OpenNetMeter.Properties;

namespace OpenNetMeter.Tests;

/// <summary>
/// The write behind SettingsManager.Save, through its path seam so the user's real settings.json is never touched.
/// (SaveInBackground itself always writes the real file, so it is covered only through LatestWinsRunnerTests, which
/// test the serialising and latest-wins behaviour it is built from. An unreadable existing file is not tested because
/// Save logs that to the real Windows Event Log.)
/// </summary>
public class SettingsSaveTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"onm-settings-{Guid.NewGuid():N}");

    public SettingsSaveTests()
    {
        Directory.CreateDirectory(directory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string SettingsPath => Path.Combine(directory, "settings.json");

    [Fact]
    public void Save_WritesTheCurrentSettingsToTheGivenPath()
    {
        SettingsManager.Save(SettingsPath);

        var root = Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllText(SettingsPath)));
        Assert.Equal(SettingsManager.Current.WinWidth, root[nameof(AppSettings.WinWidth)]!.GetValue<int>());
        Assert.Equal(SettingsManager.Current.HistoryRetentionDays, root[nameof(AppSettings.HistoryRetentionDays)]!.GetValue<int>());
    }

    [Fact]
    public void Save_KeepsKeysItDoesNotKnow_AndReplacesTheOnesItDoes()
    {
        File.WriteAllText(SettingsPath, "{ \"FromANewerVersion\": 5, \"WinWidth\": -1 }");

        SettingsManager.Save(SettingsPath);

        var root = Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllText(SettingsPath)));
        Assert.Equal(5, root["FromANewerVersion"]!.GetValue<int>());
        Assert.Equal(SettingsManager.Current.WinWidth, root[nameof(AppSettings.WinWidth)]!.GetValue<int>());
    }

    [Fact]
    public async Task ConcurrentSaves_NeverInterleave_AndTheFileIsAlwaysWhole()
    {
        // Eight threads each save 25 times to the same file. If two writes could overlap, one would hit a sharing
        // violation or the file would end up truncated or mixed; serialised, every call succeeds.
        var failures = new List<Exception>();
        var threads = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < 25; i++)
                {
                    try
                    {
                        SettingsManager.Save(SettingsPath);
                    }
                    catch (Exception ex)
                    {
                        lock (failures)
                            failures.Add(ex);
                    }
                }
            }))
            .ToArray();

        await Task.WhenAll(threads);

        Assert.Empty(failures);
        var root = Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllText(SettingsPath)));
        Assert.True(root.ContainsKey(nameof(AppSettings.NetworkAdapterId)));
    }
}
