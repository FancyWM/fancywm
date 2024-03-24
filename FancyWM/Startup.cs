using System;
using System.Diagnostics;
using System.Windows;
using System.Linq;

using Serilog;
using Serilog.Events;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using FancyWM.Utilities;
using System.Text;
using System.Security.Principal;

namespace FancyWM
{
    public static class Startup
    {
        class Arguments
        {
            public LogEventLevel LogLevel { get; init; }
        }

        private static event Action? CrashCleanup;

        private static event Action? ProgramExit;

        private const string LogFile = "fancywm.log";

        [STAThread]
        public static int Main(string[] args)
        {
            // Set the working path
            string roamingPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string fullPath = $"{roamingPath}\\FancyWM";
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }
            Directory.SetCurrentDirectory(fullPath);

            if (args.Contains("--action"))
            {
                ExecuteAction(args[args.IndexOf("--action") + 1]);
                return 0;
            }

            if (File.Exists("administrator-mode") && !IsAdministrator())
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        Verb = "runas",
                        FileName = Environment.ProcessPath!,
                        UseShellExecute = true,
                    });
                    return 0;
                }
                catch
                {
                    // Something went wrong.
                }
            }

            // Check if other instances are running
            var exists = Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName)
                .Where(x => x.MainWindowHandle != IntPtr.Zero)
                .Any();
            if (exists)
            {
                MessageBox.Show("Another instance of FancyWM is already running!", "FancyWM", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }

            // Parse command line
            var logLevel = args.Contains("-vvv") || args.Contains("--verbose")
                ? LogEventLevel.Verbose
                : args.Contains("-vv") || args.Contains("--debug")
                ? LogEventLevel.Debug
                : args.Contains("-v") || args.Contains("--info")
                ? LogEventLevel.Information
                : LogEventLevel.Warning;
#if DEBUG
            if (logLevel > LogEventLevel.Information)
            {
                logLevel = LogEventLevel.Information;
            }
#endif

            // Create logger configuration
            var logConfig = new LoggerConfiguration();

            IServiceCollection serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection, new Arguments
            {
                LogLevel = logLevel,
            });

            ProgramExit += OnProgramExit;

            var provider = serviceCollection.BuildServiceProvider();

            // Create logger
            var logger = provider.GetRequiredService<ILogger>();
            logger.Warning($"FancyWM v{GetVersionString()} (https://www.microsoft.com/store/apps/9P1741LKHQS9) on {Environment.OSVersion}");

            bool exitNormally = false;
            try
            {
                // Create the app
                var app = new App(provider);
                // We want this running after the one attached in App()
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    logger.Fatal("{Exception}", e.ExceptionObject);
                    CrashCleanup?.Invoke();
                    if (e.IsTerminating)
                    {
                        ProgramExit?.Invoke();
                    }
                };
                // Run the app
                app.InitializeComponent();
                int exitCode = app.Run();
                exitNormally = true;
                logger.Information("Exiting normally with exit code={ExitCode}", exitCode);
                ProgramExit?.Invoke();
                return exitCode;
            }
            finally
            {
                // Write crash log if needed
                if (!exitNormally)
                {
                    CrashCleanup?.Invoke();
                    ProgramExit?.Invoke();
                }
            }
        }

        private static void ExecuteAction(string message)
        {
            var hwnd = FancyWM.DllImports.PInvoke.FindWindow(null, "FancyWMMainWindow").Value;
            if (hwnd == IntPtr.Zero)
            {
                throw new InvalidOperationException("FancyWM is not running!");
            }
            WindowCopyDataHelper.Send(hwnd, Encoding.Default.GetBytes(message));
        }

        private static void OnProgramExit()
        {
            if (Application.Current is App app && app.RestartOnClose)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = @"/c timeout 5 && explorer.exe shell:appsFolder\2203VeselinKaraganev.FancyWM_9x2ndwrcmyd2c!App",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
            }
        }

        private static void ConfigureServices(IServiceCollection services, Arguments args)
        {
            ConfigureLogging(services, args);
            services.AddSingleton<IMicaProvider>(_ => new FauxMicaProvider(TimeSpan.FromSeconds(1)));
            services.AddSingleton(_ => new LowLevelMouseHook());
        }

        private static void ConfigureLogging(IServiceCollection services, Arguments args)
        {
            // Create log file
            bool isUsingTempLogFile = false;
            string logFileName = Path.GetFullPath(LogFile);
            try
            {
                File.WriteAllText(logFileName, "");
            }
            catch
            {
                // Fall back to using a temporary file
                isUsingTempLogFile = true;
                logFileName = Path.GetTempFileName() + ".log";
            }

            // Create logger configuration
            var logConfig = new LoggerConfiguration();
#if DEBUG
            if (Debugger.IsAttached)
            {
                logConfig.WriteTo.Debug(restrictedToMinimumLevel: LogEventLevel.Information);
            }
#endif

            logConfig.WriteTo.File(
                logFileName,
                restrictedToMinimumLevel: args.LogLevel,
                fileSizeLimitBytes: 4 * 1024 * 1024,
                buffered: true,
                flushToDiskInterval: TimeSpan.FromSeconds(5));

            var logBuffer = new CircularBuffer<string>(1000);
            var lineWriter = new LineWriter(logBuffer, '\n');

            logConfig.WriteTo.TextWriter(lineWriter, restrictedToMinimumLevel: LogEventLevel.Information);

            var logger = logConfig.CreateLogger();

            services.AddSingleton<ILogger>(logger);
            services.AddSingleton<ILogFileViewer>(new LogFileViewer(logFileName, logBuffer));

            CrashCleanup += () =>
            {
                logger.Dispose();
                if (!isUsingTempLogFile)
                {
                    // Create the file with a placeholder error message indicating more severe failure
                    var crashFileName = $"fancywm-crash-{DateTimeOffset.Now:yyyyMMddTHHmmss}.log";
                    File.Copy(logFileName, crashFileName, true);
                }
            };

            ProgramExit += () =>
            {
                logger.Dispose();
            };
        }

        private static string GetVersionString()
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetEntryAssembly()!.Location);
            return versionInfo.FileVersion ?? "0.0.0.0";
        }

        private static bool IsAdministrator()
        {
            return (new WindowsPrincipal(WindowsIdentity.GetCurrent()))
                      .IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
