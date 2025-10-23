using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace OpenNetMeter.Utilities
{
    public class UpdateChecker
    {
        private const string owner = "Ashfaaq18";
        private const string repo = "OpenNetMeter";

        public static async Task<(Version? latestVersion, string? downloadUrl)> CheckForUpdates()
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "OpenNetMeter");
                var response = await client.GetAsync($"https://api.github.com/repos/{owner}/{repo}/releases/latest");

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var jsonObject = JObject.Parse(jsonString);
                    string prettyJson = JsonHelper.PrettyPrint(jsonObject);
                    Debug.WriteLine(prettyJson);
                    var latestVersion = jsonObject["tag_name"]?.ToString();
                    if (latestVersion == null) { return (null, null); }

                    var assets = jsonObject["assets"] as JArray;
                    var firstAsset = assets?.FirstOrDefault();
                    var downloadUrl = firstAsset?["browser_download_url"]?.ToString();

                    return (new Version(latestVersion.Substring(1)), downloadUrl);
                }
            }
            return (null, null);
        }
    }
}