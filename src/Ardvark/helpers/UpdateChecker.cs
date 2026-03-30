using FoulzExternal.logging.notifications;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FoulzExternal.helpers
{
    public static class UpdateChecker
    {
        private const string ApiUrl = "https://api.github.com/repos/gooneralert/Ardvark-external/commits/main";

        // Store the latest known commit SHA at build time via a file, or compare against a stored local SHA.
        // We use the GitHub API to get the latest commit SHA and compare against the one saved locally.
        private static readonly string LocalShaPath = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "version.txt");

        public static async Task CheckAsync()
        {
            try
            {
                string? latestSha = await GetLatestShaAsync();
                if (latestSha == null) return;

                string? localSha = GetLocalSha();

                if (localSha == null)
                {
                    // First run — save the current SHA so we have a baseline
                    SaveLocalSha(latestSha);
                    return;
                }

                if (!string.Equals(latestSha, localSha, StringComparison.OrdinalIgnoreCase))
                {
                    notify.Notify(
                        "Update available",
                        "A new version of Ardvark is available. Run updater.bat to update.",
                        6000);
                }
            }
            catch
            {
                // Silently ignore — don't crash the app if update check fails
            }
        }

        private static async Task<string?> GetLatestShaAsync()
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Ardvark-UpdateChecker/1.0");
            client.Timeout = TimeSpan.FromSeconds(10);

            var response = await client.GetAsync(ApiUrl);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("sha", out var sha))
                return sha.GetString();

            return null;
        }

        private static string? GetLocalSha()
        {
            if (!System.IO.File.Exists(LocalShaPath)) return null;
            return System.IO.File.ReadAllText(LocalShaPath).Trim();
        }

        public static void SaveLocalSha(string sha)
        {
            System.IO.File.WriteAllText(LocalShaPath, sha);
        }
    }
}
