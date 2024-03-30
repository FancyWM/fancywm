using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;

using FancyWM.Models;
using FancyWM.Utilities;
using FancyWM.Windows;

using Microsoft.Extensions.DependencyInjection;

using Serilog;

namespace FancyWM
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static new App Current => (App)Application.Current;

        private readonly Lazy<AppState> m_appState = new(() => new AppState());

        private bool m_isProcessingUnhandledException = false;

        internal AppState AppState => m_appState.Value;

        internal IServiceProvider Services { get; }

        internal ILogger Logger { get; }

        internal string? PackageFamilyName
        {
            get
            {
                unsafe
                {
                    uint cBytes = 256;
                    char[] buffer = new char[256];
                    fixed (char* ptr = buffer)
                    {
                        var err = DllImports.PInvoke.GetPackageFamilyName(new(Process.GetCurrentProcess().Handle), &cBytes, new(ptr));
                        if (err == 0)
                        {
                            return new string(ptr);
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
            }
        }

        public bool RestartOnClose { get; internal set; }

        private App()
        {
            throw new NotImplementedException();
        }

        public App(ServiceProvider services)
        {
            Services = services;
            Logger = services.GetRequiredService<ILogger>();
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (Environment.OSVersion.Version.Build >= 22000)
            {
                Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/FancyWM;component/Themes/Fluent/Rounded.xaml", UriKind.RelativeOrAbsolute) });
            }
            else
            {
                Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/FancyWM;component/Themes/Fluent/Squared.xaml", UriKind.RelativeOrAbsolute) });
            }
            Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/FancyWM;component/Themes/Fluent/Generic.xaml", UriKind.RelativeOrAbsolute) });
        }

        internal string GetRealPath(string path)
        {
            if (PackageFamilyName is String pfn)
            {
                var appDataPath = Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData))!;
                var packagedAppDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Packages",
                    pfn,
                    "LocalCache");
                return path.Replace(appDataPath, packagedAppDataPath);
            }
            return path;
        }

        internal void Sponsor()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.paypal.com/donate/?hosted_button_id=NKQ6DKGFVN7S2",
                UseShellExecute = true,
                Verb = "open",
            });
        }

        internal void Terminate()
        {
            Close();
            Shutdown();
        }

        private async void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = (Exception)e.ExceptionObject!;
            while (m_isProcessingUnhandledException)
            {
                await Task.Yield();
            }
            m_isProcessingUnhandledException = true;

            try
            {
                HandleException(exception);
            }
            finally
            {
                m_isProcessingUnhandledException = false;
            }
        }

        private bool DidExplorerJustCrash()
        {
            if (DllImports.PInvoke.GetShellWindow().Value == 0)
            {
                return true;
            }

            bool didExplorerJustRestart = Process.GetProcessesByName("explorer")
                .Where(x => x.MainWindowTitle.Length == 0 && DateTime.Now - x.StartTime < TimeSpan.FromSeconds(1))
                .Any();
            if (didExplorerJustRestart)
            {
                return true;
            }

            return false;
        }

        private void HandleException(Exception exception)
        {
            if (exception is XamlParseException && exception.InnerException is Exception innerException)
            {
                exception = innerException;
            }

            // Special case for COMException: We do not show a dialog for 
            // this type of exception when explorer.exe is closed. This is because
            // explorer.exe is what most-likely caused the crash in the first place.
            if (exception is COMException && exception.StackTrace?.Contains("Win32VirtualDesktopManager") == true && DidExplorerJustCrash())
            {
                exception = new COMException($"Windows Explorer (explorer.exe) just crashed and this caused the virtual desktop service to fail! FancyWM received the following error message: {exception.Message}", exception);
            }

            // Show error dialog
            var result = ShowDialogOnBackgroundThread(() => new ErrorMessageBox()
            {
                ExceptionObject = exception
            }).Result;

            // View log file
            if (result == true)
            {
                Services.GetRequiredService<ILogFileViewer>().Open();
            }

            // Close and dispose of all application windows
            Exception? closingException = null;
            try
            {
                Close();
            }
            catch (Exception ce)
            {
                closingException = ce;
            }

            // Try to write logs
            try
            {
                Logger.Fatal("{Exception}", exception);
                if (closingException != null)
                {
                    Logger.Error("{Exception}", closingException);
                }
            }
            catch (Exception)
            {
                // Things are really bad now (logger failed to initialize or to write to disk)
            }

            // Hard crash
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        private void Close()
        {
            void CloseAppWindows()
            {
                List<Exception> exceptions = new();
                foreach (var window in Windows.Cast<Window>())
                {
                    try
                    {
                        // Dispose and close window
                        if (window is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                        try
                        {
                            window.Close();
                        }
                        catch (InvalidOperationException)
                        {
                            // Already closing...
                        }
                    }
                    catch (Exception e)
                    {
                        exceptions.Add(e);
                    }
                }
                if (exceptions.Count > 0)
                {
                    throw new AggregateException("Application shutdown failed!", exceptions);
                }
            }

            if (Dispatcher.Thread == Thread.CurrentThread)
            {
                CloseAppWindows();
            }
            else
            {
                // Use highest priority to close
                Dispatcher.Invoke(CloseAppWindows, System.Windows.Threading.DispatcherPriority.Send);
            }

            if (Services.GetRequiredService<LowLevelMouseHook>() is LowLevelMouseHook mshk)
            {
                mshk.Dispose();
            }
        }

        private Task<bool?> ShowDialogOnBackgroundThread(Func<Window> windowFactory)
        {
            TaskCompletionSource<bool?> tcs = new();
            Thread thread = new(() =>
            {
                try
                {
                    // Create separate dispatcher
                    _ = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                    var window = windowFactory();
                    tcs.SetResult(window.ShowDialog());
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Name = "DialogWindowBackgroundThread";
            thread.Start();

            return tcs.Task;
        }
    }
}
