using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace FancyWM.Utilities
{
    // Owns the "run as administrator" lifecycle: the administrator-mode intent marker
    // and a Task Scheduler task (RunLevel=HighestAvailable) that starts FancyWM
    // elevated without a UAC prompt. Registering the task requires elevation, so it is
    // (re)registered whenever an elevated instance runs in administrator mode;
    // non-elevated launches then start the task instead of relaunching with "runas".
    internal static class ElevationTask
    {
        private const string TaskName = "FancyWM";
        // Passed by the scheduled task to the instance it launches. An instance started
        // this way must never trigger the task again: if RunLevel=HighestAvailable
        // resolves to a non-elevated token (user lost admin rights), re-triggering
        // would spawn instances in an unbounded loop.
        internal const string StartArgument = "--from-elevation-task";

        // Absolute paths: WPF file dialogs opened elsewhere in the app can change the
        // process working directory, so CWD-relative paths are not safe here.
        private static readonly string s_appDataPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FancyWM");
        private static readonly string s_markerFile = Path.Combine(s_appDataPath, "administrator-mode");
        // Records the executable path the task was registered with. Reading the path
        // back via schtasks output is avoided because console output encoding can
        // mangle non-ASCII paths. A mismatch (moved/updated executable) falls back to
        // the UAC prompt, after which the elevated instance re-registers the task.
        private static readonly string s_registeredPathFile = Path.Combine(s_appDataPath, "elevation-task");

        private static readonly object s_gate = new();
        private static readonly bool s_isRunningAsUwp = new DesktopBridge.Helpers().IsRunningAsUwp();

        public static bool IsEnabled => File.Exists(s_markerFile);

        public static void Enable()
        {
            File.WriteAllBytes(s_markerFile, []);
            EnsureRegistered();
        }

        public static void Disable()
        {
            File.Delete(s_markerFile);
            System.Threading.Tasks.Task.Run(Unregister);
        }

        public static void EnsureRegistered()
        {
            if (IsEnabled && IsProcessElevated)
            {
                System.Threading.Tasks.Task.Run(Register);
            }
        }

        public static bool TryStart()
        {
            if (s_isRunningAsUwp)
            {
                return false;
            }
            try
            {
                if (!File.Exists(s_registeredPathFile))
                {
                    return false;
                }
                var registeredPath = File.ReadAllText(s_registeredPathFile).Trim();
                if (!string.Equals(registeredPath, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                if (RunSchtasks($"/Run /TN \"{TaskName}\"") != 0)
                {
                    return false;
                }
                // schtasks /Run only queues the start request; it exits 0 even when the
                // action later fails to launch. Confirm the started instance is alive
                // before giving up the runas fallback, otherwise the window manager
                // could silently fail to start at logon. Process enumeration is used
                // because the elevated instance's kernel objects (e.g. the single-
                // instance mutex) cannot be opened from this non-elevated process:
                // their default DACL grants the interactive session only synchronize
                // access, less than Mutex.TryOpenExisting requests.
                var processName = Path.GetFileNameWithoutExtension(Environment.ProcessPath)!;
                for (int i = 0; i < 40; i++)
                {
                    Thread.Sleep(250);
                    var alive = false;
                    foreach (var process in Process.GetProcessesByName(processName))
                    {
                        using (process)
                        {
                            alive |= process.Id != Environment.ProcessId;
                        }
                    }
                    if (alive)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsProcessElevated => new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);

        private static void Register()
        {
            if (s_isRunningAsUwp)
            {
                return;
            }
            try
            {
                lock (s_gate)
                {
                    var exePath = Environment.ProcessPath!;
                    var userSid = WindowsIdentity.GetCurrent().User!.Value;
                    // ExecutionTimeLimit=PT0S: the default (72h) would terminate the app.
                    // Priority=4: the default task priority (7, below normal) would
                    // degrade a window manager's responsiveness.
                    // MultipleInstancesPolicy=Parallel: the task stays "Running" while
                    // the app is alive; a second manual launch must still reach the
                    // single-instance check instead of being ignored.
                    var xml = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>Starts FancyWM with administrator privileges without a UAC prompt.</Description>
  </RegistrationInfo>
  <Principals>
    <Principal id=""Author"">
      <UserId>{userSid}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>Parallel</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>false</AllowHardTerminate>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>4</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{SecurityElement.Escape(exePath)}</Command>
      <Arguments>{StartArgument}</Arguments>
    </Exec>
  </Actions>
</Task>";
                    var xmlPath = Path.Combine(Path.GetTempPath(), $"fancywm-elevation-task-{Guid.NewGuid():N}.xml");
                    File.WriteAllText(xmlPath, xml, Encoding.Unicode);
                    try
                    {
                        if (RunSchtasks($"/Create /TN \"{TaskName}\" /XML \"{xmlPath}\" /F") == 0)
                        {
                            File.WriteAllText(s_registeredPathFile, exePath);
                        }
                    }
                    finally
                    {
                        File.Delete(xmlPath);
                    }
                }
            }
            catch
            {
                // Elevation continues to work through the "runas" fallback.
            }
        }

        private static void Unregister()
        {
            if (s_isRunningAsUwp)
            {
                return;
            }
            try
            {
                lock (s_gate)
                {
                    // Deletion requires elevation and this may run non-elevated. Only
                    // forget the registration when the delete succeeded; if the task
                    // survives, the kept record makes a later re-enable promptless.
                    if (RunSchtasks($"/Delete /TN \"{TaskName}\" /F") == 0)
                    {
                        File.Delete(s_registeredPathFile);
                    }
                }
            }
            catch
            {
            }
        }

        private static int RunSchtasks(string arguments)
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
            })!;
            process.WaitForExit();
            return process.ExitCode;
        }
    }
}
