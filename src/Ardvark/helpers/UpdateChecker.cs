using FoulzExternal.logging.notifications;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace FoulzExternal.helpers
{
    public static class UpdateChecker
    {
        private const string RemoteVersionUrl = "https://raw.githubusercontent.com/gooneralert/Ardvark-external/main/version.txt";

        private static readonly string LocalVersionPath = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "version.txt");

        public static async Task CheckAsync()
        {
            try
            {
                string? remoteVersion = await GetRemoteVersionAsync();
                if (remoteVersion == null) return;

                string? localVersion = GetLocalVersion();

                if (localVersion == null)
                {
                    // First run — save so we have a baseline
                    SaveLocalVersion(remoteVersion);
                    return;
                }

                if (!string.Equals(remoteVersion, localVersion, StringComparison.OrdinalIgnoreCase))
                {
                    notify.Notify(
                        $"Update available (v{remoteVersion})",
                        $"You are on v{localVersion}. Run updater.bat to update.",
                        6000);
                }
            }
            catch
            {
                // Silently ignore — don't crash the app if update check fails
            }
        }

        private static async Task<string?> GetRemoteVersionAsync()
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Ardvark-UpdateChecker/1.0");
            client.Timeout = TimeSpan.FromSeconds(10);

            var response = await client.GetAsync(RemoteVersionUrl);
            if (!response.IsSuccessStatusCode) return null;

            return (await response.Content.ReadAsStringAsync()).Trim();
        }

        private static string? GetLocalVersion()
        {
            if (!System.IO.File.Exists(LocalVersionPath)) return null;
            return System.IO.File.ReadAllText(LocalVersionPath).Trim();
        }

        public static void SaveLocalVersion(string version)
        {
            System.IO.File.WriteAllText(LocalVersionPath, version);
        }
    }
}
