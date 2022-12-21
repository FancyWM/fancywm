using System;
using System.IO;
using System.Threading.Tasks;

using Windows.ApplicationModel;

namespace FancyWM.Utilities
{
    internal static class Autostart
    {
        private const string StartupTaskId = "FancyWM";
        private static readonly string s_startupLinkPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup) + "\\FancyWM.lnk";
        private static readonly bool s_isRunningAsUwp = new DesktopBridge.Helpers().IsRunningAsUwp();

        public static async Task<bool> IsEnabledAsync()
        {
            if (s_isRunningAsUwp)
            {
                return await IsEnabledUwpAsync();
            }
            else
            {
                return await IsEnabledLegacyAsync();
            }
        }

        public static async Task<bool> EnableAsync()
        {
            if (s_isRunningAsUwp)
            {
                return await EnableUwpAsync();
            }
            else
            {
                return await EnableLegacyAsync();
            }
        }

        public static async Task<bool> DisableAsync()
        {
            if (s_isRunningAsUwp)
            {
                return await DisableUwpAsync();
            }
            else
            {
                return await DisableLegacyAsync();
            }
        }

        private static Task<bool> IsEnabledLegacyAsync()
        {
            return Task.FromResult(File.Exists(s_startupLinkPath));
        }

        private static async Task<bool> IsEnabledUwpAsync()
        {
            var task = await StartupTask.GetAsync(StartupTaskId);
            return task.State == StartupTaskState.Enabled || task.State == StartupTaskState.EnabledByPolicy;
        }

        private static async Task<bool> EnableLegacyAsync()
        {
            File.WriteAllBytes(s_startupLinkPath, Resources.Files.FancyWM_lnk);
            return await IsEnabledLegacyAsync();
        }

        private static async Task<bool> EnableUwpAsync()
        {
            var task = await StartupTask.GetAsync(StartupTaskId);
            if (task.State != StartupTaskState.Enabled && task.State != StartupTaskState.EnabledByPolicy)
            {
                var newState = await task.RequestEnableAsync();
                if (newState != StartupTaskState.Enabled)
                {
                    return false;
                }
            }
            return true;
        }

        private static async Task<bool> DisableLegacyAsync()
        {
            File.Delete(s_startupLinkPath);
            return !await IsEnabledLegacyAsync();
        }

        private static async Task<bool> DisableUwpAsync()
        {
            var task = await StartupTask.GetAsync(StartupTaskId);
            if (task.State == StartupTaskState.Enabled || task.State == StartupTaskState.EnabledByPolicy)
            {
                task.Disable();
                if (task.State != StartupTaskState.Disabled)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
