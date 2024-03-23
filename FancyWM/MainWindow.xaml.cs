using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Diagnostics;
using System.Reactive.Linq;

using FancyWM.DllImports;

using FancyWM.Utilities;
using FancyWM.Windows;
using FancyWM.Models;
using WinMan;
using WinMan.Windows;
using FancyWM.Controls;
using ModernWpf;
using Hardcodet.Wpf.TaskbarNotification;
using Windows.System;
using System.Reactive.Disposables;
using System.Reactive;
using Serilog;
using FancyWM.Toasts;
using System.Threading;
using FancyWM.Pages.Settings;
using System.Media;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Media.Animation;
using System.Text;
using FancyWM.Resources;

using Strings = FancyWM.Resources.Strings;

namespace FancyWM
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        private readonly IWorkspace m_workspace;
        private ITilingService m_tiling;
        private readonly CompositeDisposable m_subscriptions;
        private readonly IToastService m_toasts;

        private readonly TimeSpan ToastDurationCommandSequence = TimeSpan.FromMilliseconds(3000);
        private readonly TimeSpan ToastDurationCommandSequenceWithContextHints = TimeSpan.FromMilliseconds(9000);
        private readonly TimeSpan ToastDurationLong = TimeSpan.FromMilliseconds(3000);
        private readonly TimeSpan ToastDurationShort = TimeSpan.FromMilliseconds(1000);
        private readonly TimeSpan RateReviewDelay = TimeSpan.FromMinutes(30);
        private readonly double ResizeDisplayPercentage = 1.0 / 12.0;

        private readonly ILogger m_logger;

        private IVirtualDesktop? m_prevDesktop = null;
        private IDisplay? m_prevDisplay = null;
        private IDisplay? m_currentDisplay = null;

        private LowLevelHotkey[]? m_cmdHks;

        private readonly TaskbarIcon m_notifyIcon;
        private readonly CountdownTimer m_hideCountdownTimer;
        private readonly ContextMenu m_contextMenu;
        private readonly LowLevelKeyboardHook m_llkbdHook;
        private KeybindingDictionary m_keybindings;
        private readonly IntPtr m_hwnd;
        private readonly IAnimationThread m_animationThread;
        private bool m_enableRateReviewRequests;
        private DateTime m_reviewTooltipShown;
        private bool m_showContextHints;
        private bool m_soundOnFailure;
        private readonly Stopwatch m_stopwatch;
        private readonly IMicaProvider m_micaProvider;
        private readonly ModifierWindowMover m_mvm;
        private long m_cmdSequenceId = 0;
        private bool m_showFocusDuringAction;
        private bool m_autoCollapse;
        private bool m_notifyVirtualDesktopServiceIncompatibility;
        private GlobalHotkey[] m_directHks = new GlobalHotkey[0];

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public MainWindow()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            InitializeComponent();

            m_logger = App.Current.Logger;

            m_hwnd = new WindowInteropHelper(this).EnsureHandle();

            m_workspace = new Win32Workspace();
            m_workspace.UnhandledException += OnWorkspaceUnhandledException;

            m_workspace.Open();

            m_toasts = new ToastService(m_workspace);

            m_animationThread = new AnimationThread(m_workspace.DisplayManager.Displays.Select(x => x.RefreshRate).Max());

            m_workspace.VirtualDesktopManager.CurrentDesktopChanged += OnVirtualDesktopChanged;
            m_workspace.VirtualDesktopManager.DesktopRemoved += OnVirtualDesktopRemoved;
            m_workspace.FocusedWindowChanged += OnFocusedWindowChanged;

            m_mvm = new ModifierWindowMover(m_workspace, App.Current.Services.GetRequiredService<LowLevelMouseHook>());

            var settings = App.Current.AppState.Settings
                .Do(x => m_keybindings = x.Keybindings)
                .Do(x => m_enableRateReviewRequests = x.RemindToRateReview)
                .Do(x => m_showContextHints = x.ShowContextHints)
                .Do(x => m_soundOnFailure = x.SoundOnFailure)
                .Do(x => m_showFocusDuringAction = x.ShowFocusDuringAction)
                .Do(x => m_autoCollapse = x.AutoCollapsePanels)
                .Do(x => m_notifyVirtualDesktopServiceIncompatibility = x.NotifyVirtualDesktopServiceIncompatibility)
                .Do(x =>
                {
                    m_mvm.IsEnabled = x.ModifierMoveWindow;
                    m_mvm.AutoFocus = x.ModifierMoveWindowAutoFocus;
                })
                .DistinctUntilChanged()
                .Do(async x =>
                {
                    await Dispatcher.InvokeAsync(async () =>
                    {
                        if (x.OverrideAccentColor)
                        {
                            ThemeManager.Current.AccentColor = x.CustomAccentColor;
                        }
                        else
                        {
                            ThemeManager.Current.AccentColor = null;
                        }
                    });
                });

            var startupSettings = settings
                .Take(1)
                .Do(x =>
                {
                    if (x.ShowStartupWindow)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            new StartupWindow(this, new ViewModels.SettingsViewModel(App.Current.AppState.Settings)).Show();
                        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    }
                });

            var activationHotkeySettings = settings
                .Select(x => x.ActivationHotkey)
                .DistinctUntilChanged()
                .Do(_ => Dispatcher.BeginInvoke(() => RebindActivationHotkey(_)));

            var keybindingsSettings = settings
                .Select(x => x.Keybindings)
                .DistinctUntilChanged()
                .Do(_ => Dispatcher.BeginInvoke(() => RebindDirectHotkeys(_)));

            var multiMonitorObservable = settings
                .Select(x => x.MultiMonitorSupport)
                .Take(1)
                .Do(async multiMonitorSupport => await Dispatcher.InvokeAsync(() =>
                {
                    if (multiMonitorSupport)
                    {
                        m_tiling = new MultiDisplayTilingService(m_workspace, m_animationThread, settings);
                    }
                    else
                    {
                        m_tiling = new TilingService(m_workspace, m_workspace.DisplayManager.PrimaryDisplay, m_animationThread, settings, true);
                    }

                    m_tiling.AutoCollapse = m_autoCollapse;
                    m_tiling.PlacementFailed += OnTilingFailed;
                    m_tiling.Start();
                }))
                .Select(_ => Unit.Default);

            var exclusionListSettings = settings
                .DistinctUntilChanged(x => (x.ProcessIgnoreList, x.ClassIgnoreList))
                .Do(async x => await Dispatcher.InvokeAsync(() =>
                {
                    var processMatchers = x.ProcessIgnoreList.Select(x => new ByProcessNameMatcher(x));
                    var classMatchers = x.ClassIgnoreList.Select(x => new ByClassNameMatcher(x));
                    m_tiling!.ExclusionMatchers = m_tiling.ExclusionMatchers
                        .Where(m => m is not ByProcessNameMatcher && m is not ByClassNameMatcher)
                        .Concat(processMatchers)
                        .Concat(classMatchers)
                        .ToArray();
                }))
                .Select(_ => Unit.Default);

            var autoCollapseSettings = settings
                .DistinctUntilChanged(x => x.AutoCollapsePanels)
                .Do(async _ => await Dispatcher.InvokeAsync(() =>
                {
                    if (m_tiling != null)
                    {
                        m_tiling.AutoCollapse = m_autoCollapse;
                    }
                }));

            m_llkbdHook = new LowLevelKeyboardHook();

            m_subscriptions = new CompositeDisposable
            {
                settings.Subscribe(new NotifyUnhandledObserver<Settings>()),
                startupSettings.Subscribe(new NotifyUnhandledObserver<Settings>()),
                activationHotkeySettings.Subscribe(new NotifyUnhandledObserver<ActivationHotkey>()),
                keybindingsSettings.Subscribe(new NotifyUnhandledObserver<KeybindingDictionary>()),
                autoCollapseSettings.Subscribe(new NotifyUnhandledObserver<Settings>()),
                multiMonitorObservable
                    .Concat(exclusionListSettings)
                    .Subscribe(new NotifyUnhandledObserver<Unit>()),
            };

            m_notifyIcon = new TaskbarIcon
            {
                Icon = Files.Icon
            };
            m_notifyIcon.TrayLeftMouseDown += OnNotifyIconLeftMouseDown;
            m_notifyIcon.TrayRightMouseDown += OnNotifyIconRightMouseDown;
            m_notifyIcon.Visibility = Visibility.Visible;
            m_notifyIcon.TrayBalloonTipClicked += OnBalloonTipClicked;

            m_hideCountdownTimer = new CountdownTimer();
            m_contextMenu = (ContextMenu)FindResource("NotifierContextMenu");

            m_stopwatch = new Stopwatch();
            m_stopwatch.Start();

            _ = PInvoke.SetWindowLong(new(m_hwnd), GetWindowLongPtr_nIndex.GWL_EXSTYLE,
                (int)(WINDOWS_EX_STYLE.WS_EX_TOOLWINDOW | WINDOWS_EX_STYLE.WS_EX_TOPMOST));

            if (App.Current.Services.GetService<IMicaProvider>() is IMicaProvider micaProvider)
            {
                m_micaProvider = micaProvider;
                App.Current.Resources["MicaPrimaryColor"] = m_micaProvider.PrimaryColor;
                m_micaProvider.PrimaryColorChanged += OnMicaProviderPrimaryColorChanged;
            }

            Loaded += OnLoaded;
            Show();

            ((HwndSource)PresentationSource.FromVisual(this)).AddHook(WndProc);
        }

        private void OnFocusedWindowChanged(object? sender, FocusedWindowChangedEventArgs e)
        {
            if (e.NewFocusedWindow != null)
            {
                var currentDisplay = m_workspace.DisplayManager.Displays.FirstOrDefault(x => x.Bounds.Contains(e.NewFocusedWindow.Position.Center));
                if (currentDisplay != null)
                {
                    OnCurrentDisplayChanged(currentDisplay);
                }
            }
        }

        private void OnCurrentDisplayChanged(IDisplay currentDisplay)
        {
            if (m_currentDisplay != currentDisplay)
            {
                m_prevDisplay = m_currentDisplay;
                m_currentDisplay = currentDisplay;
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Constants.WM_COPYDATA)
            {
                byte[] message = WindowCopyDataHelper.Receive(lParam);
                _ = Dispatcher.InvokeAsync(async () =>
                {
                    var actionName = Encoding.Default.GetString(message);
                    await ExecuteActionFromStringAsync(actionName);
                });
            }
            handled = false;
            return IntPtr.Zero;
        }

        private void OnMicaProviderPrimaryColorChanged(object? sender, MicaOptionsChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                App.Current.Resources["MicaPrimaryColor"] = m_micaProvider.PrimaryColor;
            });
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (m_workspace.VirtualDesktopManager.GetType().Name == "DummyVirtualDesktopManager" && Environment.OSVersion.Version.Build >= 17661 && m_notifyVirtualDesktopServiceIncompatibility)
            {
                if (new Windows.MessageBox { IconGlyph = "\xF1AD", Title = Strings.Messages_WindowsVersionNotSupported_Caption + " OS Build: " + Environment.OSVersion.Version.Build, Message = Strings.Messages_WindowsVersionNotSupported_Description }.ShowDialog() == true)
                {
                    m_logger.Warning("Unsupported OS Version: " + Environment.OSVersion.Version.ToString());
                }
            }

#if !DEBUG
            await Task.Delay(10000);
#endif
        }

        private void OnWorkspaceUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Dispatcher.RethrowOnDispatcher((Exception)e.ExceptionObject!);
        }

        private IEnumerable<(KeyCode[] KeyA, KeyCode KeyB)> CreateSideInsensitiveHotkeyVariants(ActivationHotkey hk)
        {
            KeyCode GetLeftVersion(KeyCode k)
            {
                return k switch
                {
                    KeyCode.RightShift => KeyCode.LeftShift,
                    KeyCode.RightCtrl => KeyCode.LeftCtrl,
                    KeyCode.RWin => KeyCode.LWin,
                    KeyCode.RightAlt => KeyCode.LeftAlt,
                    _ => k,
                };
            }

            KeyCode GetRightVersion(KeyCode k)
            {
                return k switch
                {
                    KeyCode.LeftShift => KeyCode.RightShift,
                    KeyCode.LeftCtrl => KeyCode.RightCtrl,
                    KeyCode.LWin => KeyCode.RWin,
                    KeyCode.LeftAlt => KeyCode.RightAlt,
                    _ => k,
                };
            }

            var keyA = GetLeftVersion(hk.KeyA);
            var keyB = GetLeftVersion(hk.KeyB);
            var rKeyA = GetRightVersion(hk.KeyA);
            var rKeyB = GetRightVersion(hk.KeyB);

            yield return (new[] { keyA }, keyB);
            yield return (new[] { keyB }, keyA);
            yield return (new[] { keyA }, rKeyB);
            yield return (new[] { keyB }, rKeyA);
            yield return (new[] { rKeyA }, keyB);
            yield return (new[] { rKeyB }, keyA);
            yield return (new[] { rKeyA }, rKeyB);
            yield return (new[] { rKeyB }, rKeyA);
        }

        private void RebindActivationHotkey(ActivationHotkey hk)
        {
            if (m_cmdHks != null)
            {
                foreach (var cmdHk in m_cmdHks)
                {
                    cmdHk.Pressed -= OnCmdSequenceBegin;
                }
            }

            m_cmdHks = CreateSideInsensitiveHotkeyVariants(hk)
                .Select(hk => new LowLevelHotkey(m_llkbdHook, hk.KeyA, hk.KeyB)
                {
                    HideKeyPress = false,
                    ScanOnRelease = true,
                }).ToArray();

            foreach (var cmdHk in m_cmdHks)
            {
                cmdHk.Pressed += OnCmdSequenceBegin;
            }
        }

        private async void RebindDirectHotkeys(KeybindingDictionary keybindings)
        {
            foreach (var hk in m_directHks)
            {
                hk.Dispose();
            }

            var newHotkeys = new List<GlobalHotkey>();
            var failedHotkeys = new List<Keybinding>();
            foreach (var x in keybindings
                .Where(x => x.Value?.IsDirectMode == true))
            {
                try
                {
                    var (modifiers, key) = KeyCodeHelper.GetModifierAndKeyCode(x.Value!.Keys);
                    var hk = new GlobalHotkey(m_hwnd, modifiers, key);
                    hk.Pressed += delegate { OnDirectHotkeyPressed(x.Key); };
                    hk.Register();
                    newHotkeys.Add(hk);
                }
                catch (Win32Exception)
                {
                    failedHotkeys.Add(x.Value!);
                }
                catch (ArgumentException)
                {
                    // Failed because bad keys
                    failedHotkeys.Add(x.Value!);
                }
            }

            if (failedHotkeys.Count > 0)
            {
                OnDirectHotkeyRegistrationFailed(failedHotkeys);
            }
            m_directHks = newHotkeys.ToArray();
        }

        private async void OnDirectHotkeyRegistrationFailed(IEnumerable<Keybinding> hotkeys)
        {
            string text = string.Join(", ", hotkeys.Select(x => string.Join(" + ", x.Keys.Select(KeyDescriptions.GetDescription))));
            await ShowToastAsync(Strings.Messages_DirectHotkeyRegistrationFailed, text, ToastDurationLong);
        }

        private async void OnDirectHotkeyPressed(BindableAction action)
        {
            string? friendlyActionName = null;
            try
            {
                ExecuteAction(action, ref friendlyActionName);
            }
            catch (TilingFailedException e)
            {
                await HandleCommandExceptionAsync(e, friendlyActionName);
            }
        }

        private void OnTilingFailed(object? sender, TilingFailedEventArgs e)
        {
            if (e.FailReason == TilingError.NoValidPlacementExists)
            {
                if (e.FailSource == null)
                {
                    throw new ArgumentException($"{nameof(e.FailSource)} is required for {nameof(e.FailReason)}!");
                }

                PlayBeepSound();
                _ = ShowToastAsync($"{Strings.Messages_FloatingModeEnabledFor} {e.FailSource.Title}", Strings.Messages_CannotBeResizedToFit, ToastDurationLong);
            }
            else
            {
                var reason = GetTilingErrorText(e.FailReason) ?? Strings.Messages_OperationFailed;
                var hint = e.FailSource != null
                    ? e.FailSource.GetCachedProcessName() + " " + Strings.Common_Window
                    : null;

                if (e.FailReason != TilingError.TargetCannotFit)
                {
                    PlayBeepSound();
                }
                _ = ShowToastAsync(reason, hint, ToastDurationLong);
            }
        }

        private async void OnCommandKey(IReadOnlySet<KeyCode> keys)
        {
            // This is to allow for focus to return to the window before movement
            await Task.Delay(50);

            var matches = m_keybindings.Where(x => x.Value != null && x.Value.Keys.SetEqualsSideInsensitive(keys));
            var action = matches.FirstOrDefault().Key;
            string? friendlyActionName = null;
            try
            {
                if (matches.Any())
                {
                    ExecuteAction(action, ref friendlyActionName);
                    return;
                }
                else if (keys.Count == 1)
                {
                    var key = keys.First();
                    switch (key)
                    {
                        case KeyCode.F1:
                            OpenHelp();
                            return;
                        case KeyCode.Snapshot:
                            Debugger.Break();
                            return;
                    }
                }
                else if (keys.SetEquals(new[] { KeyCode.LeftShift, KeyCode.Snapshot }))
                {
                    throw new Exception("Program break requested!");
                }

                if (keys.Count() == 2 && keys.Contains(KeyCode.LeftAlt)
                    && (keys.Contains(KeyCode.LeftShift) || keys.Contains(KeyCode.LeftCtrl) || keys.Contains(KeyCode.LeftAlt)
                    || keys.Contains(KeyCode.RightShift) || keys.Contains(KeyCode.RightCtrl) || keys.Contains(KeyCode.RightAlt)))
                {
                    return;
                }

                PlayBeepSound();
                await ShowToastAsync(Strings.Messages_UnrecognizedKeybinding, $"{Strings.Messages_NothingIsAssignedTo} [{keys.ToPrettyString()}]!", ToastDurationLong);
            }
            catch (TilingFailedException e)
            {
                await HandleCommandExceptionAsync(e, friendlyActionName);
            }
        }

        private async Task HandleCommandExceptionAsync(TilingFailedException e, string? friendlyActionName)
        {
            var messageText = friendlyActionName != null
                ? Strings.Messages_CouldNot + " " + friendlyActionName + "!"
                : Strings.Messages_OperationFailed;
            var hintText = GetTilingErrorText(e.FailReason);

            PlayBeepSound();
            await ShowToastAsync(messageText, hintText, ToastDurationLong);
        }

        private async Task ExecuteActionFromStringAsync(string actionName)
        {
            BindableAction action;
            try
            {
                action = (BindableAction)Enum.Parse(typeof(BindableAction), actionName);
            }
            catch (ArgumentException)
            {
                _ = ShowToastAsync($"{Strings.Messages_ReceivedInvalidCommand} \"{actionName}\"!", ToastDurationLong);
                return;
            }

            string? friendlyActionName = null;
            try
            {
                ExecuteAction(action, ref friendlyActionName);
            }
            catch (TilingFailedException e)
            {
                await HandleCommandExceptionAsync(e, friendlyActionName);
            }
        }

        private void ExecuteAction(BindableAction action, ref string? friendlyActionName)
        {
            switch (action)
            {
                case BindableAction.CreateVerticalPanel:
                    friendlyActionName = "wrap in vertical panel";
                    OnSplitVHotkeyPressed();
                    return;
                case BindableAction.CreateHorizontalPanel:
                    friendlyActionName = "wrap in horizontal panel";
                    OnSplitHHotkeyPressed();
                    //_ = ShowInfoToastAsync("Horizontal Panel", ToastDurationShort);
                    return;
                case BindableAction.CreateStackPanel:
                    friendlyActionName = "wrap in stack panel";
                    //_ = ShowInfoToastAsync("Stack Panel", ToastDurationShort);
                    m_tiling.Stack();
                    return;
                case BindableAction.RefreshWorkspace:
                    friendlyActionName = "refresh workspace";
                    m_tiling.Refresh();
                    //_ = ShowInfoToastAsync("Refresh Workspace", ToastDurationShort);
                    return;
                case BindableAction.ShowDesktop:
                    friendlyActionName = "show desktop";
                    m_tiling.ToggleDesktop();
                    return;
                case BindableAction.ToggleFloatingMode:
                    friendlyActionName = "float window";
                    m_tiling.Float();
                    //_ = ShowInfoToastAsync($"Floating Mode: {(state ? "On" : "off")}", ToastDurationShort);
                    return;
                case BindableAction.MoveFocusLeft:
                    friendlyActionName = "move focus left";
                    m_tiling.MoveFocus(Layouts.Tiling.TilingDirection.Left);
                    return;
                case BindableAction.MoveFocusRight:
                    friendlyActionName = "move focus right";
                    m_tiling.MoveFocus(Layouts.Tiling.TilingDirection.Right);
                    return;
                case BindableAction.MoveFocusUp:
                    friendlyActionName = "move focus up";
                    m_tiling.MoveFocus(Layouts.Tiling.TilingDirection.Up);
                    return;
                case BindableAction.MoveFocusDown:
                    friendlyActionName = "move focus down";
                    m_tiling.MoveFocus(Layouts.Tiling.TilingDirection.Down);
                    return;
                case BindableAction.PullWindowUp:
                    friendlyActionName = "move to upper level";
                    m_tiling.PullUp();
                    //_ = ShowInfoToastAsync("Pull Up", ToastDurationShort);
                    return;
                case BindableAction.MoveLeft:
                    friendlyActionName = "move left";
                    m_tiling.MoveWindow(Layouts.Tiling.TilingDirection.Left);
                    return;
                case BindableAction.MoveUp:
                    friendlyActionName = "move up";
                    m_tiling.MoveWindow(Layouts.Tiling.TilingDirection.Up);
                    return;
                case BindableAction.MoveRight:
                    friendlyActionName = "move right";
                    m_tiling.MoveWindow(Layouts.Tiling.TilingDirection.Right);
                    return;
                case BindableAction.MoveDown:
                    friendlyActionName = "move down";
                    m_tiling.MoveWindow(Layouts.Tiling.TilingDirection.Down);
                    return;
                case BindableAction.SwapLeft:
                    friendlyActionName = "swap left";
                    m_tiling.SwapFocus(Layouts.Tiling.TilingDirection.Left);
                    return;
                case BindableAction.SwapUp:
                    friendlyActionName = "swap up";
                    m_tiling.SwapFocus(Layouts.Tiling.TilingDirection.Up);
                    return;
                case BindableAction.SwapRight:
                    friendlyActionName = "swap right";
                    m_tiling.SwapFocus(Layouts.Tiling.TilingDirection.Right);
                    return;
                case BindableAction.SwapDown:
                    friendlyActionName = "swap down";
                    m_tiling.SwapFocus(Layouts.Tiling.TilingDirection.Down);
                    return;
                case BindableAction.DecreaseWidth:
                    friendlyActionName = "decrease width";
                    m_tiling.Resize(Layouts.Tiling.PanelOrientation.Horizontal, -ResizeDisplayPercentage);
                    return;
                case BindableAction.DecreaseHeight:
                    friendlyActionName = "decrease height";
                    m_tiling.Resize(Layouts.Tiling.PanelOrientation.Vertical, -ResizeDisplayPercentage);
                    return;
                case BindableAction.IncreaseWidth:
                    friendlyActionName = "increase width";
                    m_tiling.Resize(Layouts.Tiling.PanelOrientation.Horizontal, ResizeDisplayPercentage);
                    return;
                case BindableAction.IncreaseHeight:
                    friendlyActionName = "increase height";
                    m_tiling.Resize(Layouts.Tiling.PanelOrientation.Vertical, ResizeDisplayPercentage);
                    return;
                case BindableAction.ToggleManager:
                    friendlyActionName = "toggle window management";
                    if (m_tiling.Active)
                    {
                        m_tiling.Stop();
                        //_ = ShowInfoToastAsync("Window Management: Off", ToastDurationShort);
                    }
                    else
                    {
                        m_tiling.Start();
                        //_ = ShowInfoToastAsync("Window Management: On", ToastDurationShort);
                    }
                    return;
                case BindableAction.Cancel:
                    return;
                case BindableAction.SwitchToPreviousDesktop:
                    OnPreviousDesktopHotkeyPressed();
                    return;
                case BindableAction.SwitchToDesktop1:
                    OnDesktopHotkeyPressed(0);
                    return;
                case BindableAction.SwitchToDesktop2:
                    OnDesktopHotkeyPressed(1);
                    return;
                case BindableAction.SwitchToDesktop3:
                    OnDesktopHotkeyPressed(2);
                    return;
                case BindableAction.SwitchToDesktop4:
                    OnDesktopHotkeyPressed(3);
                    return;
                case BindableAction.SwitchToDesktop5:
                    OnDesktopHotkeyPressed(4);
                    return;
                case BindableAction.SwitchToDesktop6:
                    OnDesktopHotkeyPressed(5);
                    return;
                case BindableAction.SwitchToDesktop7:
                    OnDesktopHotkeyPressed(6);
                    return;
                case BindableAction.SwitchToDesktop8:
                    OnDesktopHotkeyPressed(7);
                    return;
                case BindableAction.SwitchToDesktop9:
                    OnDesktopHotkeyPressed(8);
                    return;
                case BindableAction.MoveToPreviousDesktop:
                    OnMoveToPreviousDesktopHotkeyPressed();
                    return;
                case BindableAction.MoveToDesktop1:
                    OnMoveToDesktopHotkeyPressed(0);
                    return;
                case BindableAction.MoveToDesktop2:
                    OnMoveToDesktopHotkeyPressed(1);
                    return;
                case BindableAction.MoveToDesktop3:
                    OnMoveToDesktopHotkeyPressed(2);
                    return;
                case BindableAction.MoveToDesktop4:
                    OnMoveToDesktopHotkeyPressed(3);
                    return;
                case BindableAction.MoveToDesktop5:
                    OnMoveToDesktopHotkeyPressed(4);
                    return;
                case BindableAction.MoveToDesktop6:
                    OnMoveToDesktopHotkeyPressed(5);
                    return;
                case BindableAction.MoveToDesktop7:
                    OnMoveToDesktopHotkeyPressed(6);
                    return;
                case BindableAction.MoveToDesktop8:
                    OnMoveToDesktopHotkeyPressed(7);
                    return;
                case BindableAction.MoveToDesktop9:
                    OnMoveToDesktopHotkeyPressed(8);
                    return;
                case BindableAction.SwitchToPreviousDisplay:
                    OnPreviousDisplayHotkeyPressed();
                    return;
                case BindableAction.SwitchToDisplay1:
                    OnSwitchToDisplayHotkeyPressed(0);
                    return;
                case BindableAction.SwitchToDisplay2:
                    OnSwitchToDisplayHotkeyPressed(1);
                    return;
                case BindableAction.SwitchToDisplay3:
                    OnSwitchToDisplayHotkeyPressed(2);
                    return;
                case BindableAction.SwitchToDisplay4:
                    OnSwitchToDisplayHotkeyPressed(3);
                    return;
                case BindableAction.SwitchToDisplay5:
                    OnSwitchToDisplayHotkeyPressed(4);
                    return;
                case BindableAction.SwitchToDisplay6:
                    OnSwitchToDisplayHotkeyPressed(5);
                    return;
                case BindableAction.SwitchToDisplay7:
                    OnSwitchToDisplayHotkeyPressed(6);
                    return;
                case BindableAction.SwitchToDisplay8:
                    OnSwitchToDisplayHotkeyPressed(7);
                    return;
                case BindableAction.SwitchToDisplay9:
                    OnSwitchToDisplayHotkeyPressed(8);
                    return;
                case BindableAction.MoveToPreviousDisplay:
                    OnMoveToPreviousDisplayHotkeyPressed();
                    return;
                case BindableAction.MoveToDisplay1:
                    OnMoveToDisplayHotkeyPressed(0);
                    return;
                case BindableAction.MoveToDisplay2:
                    OnMoveToDisplayHotkeyPressed(1);
                    return;
                case BindableAction.MoveToDisplay3:
                    OnMoveToDisplayHotkeyPressed(2);
                    return;
                case BindableAction.MoveToDisplay4:
                    OnMoveToDisplayHotkeyPressed(3);
                    return;
                case BindableAction.MoveToDisplay5:
                    OnMoveToDisplayHotkeyPressed(4);
                    return;
                case BindableAction.MoveToDisplay6:
                    OnMoveToDisplayHotkeyPressed(5);
                    return;
                case BindableAction.MoveToDisplay7:
                    OnMoveToDisplayHotkeyPressed(6);
                    return;
                case BindableAction.MoveToDisplay8:
                    OnMoveToDisplayHotkeyPressed(7);
                    return;
                case BindableAction.MoveToDisplay9:
                    OnMoveToDisplayHotkeyPressed(8);
                    return;
                default:
                    throw new NotImplementedException(action.ToString());
            }
        }

        private void PlayBeepSound()
        {
            if (m_soundOnFailure)
            {
                SystemSounds.Beep.Play();
            }
        }

        private string? GetTilingErrorText(TilingError e)
        {
            switch (e)
            {
                case TilingError.NoValidPlacementExists:
                    return Strings.TilingError_NoValidPlacementExists;
                case TilingError.CausesRecursiveNesting:
                    return Strings.TilingError_CausesRecursiveNesting;
                case TilingError.ModifiesTopLevelPanel:
                    return Strings.TilingError_ModifiesTopLevelPanel;
                case TilingError.PullsBeyondTopLevelPanel:
                    return Strings.TilingError_PullsBeyondTopLevelPanel;
                case TilingError.MissingAdjacentWindow:
                    return Strings.TilingError_MissingAdjacentWindow;
                case TilingError.MissingTarget:
                    return Strings.TilingError_MissingTarget;
                case TilingError.TargetCannotFit:
                    return Strings.TilingError_TargetCannotFit;
                case TilingError.InvalidTarget:
                    return Strings.TilingError_InvalidTarget;
                case TilingError.NestingInStackPanel:
                    return Strings.TilingError_NestingInStackPanel;
                case TilingError.Failed:
                default:
                    return null;
            }
        }

        internal void OpenSettings()
        {
            if (App.Current.Windows.OfType<SettingsWindow>().Any())
            {
                App.Current.Windows.OfType<SettingsWindow>().First().Activate();
            }
            else
            {
                new SettingsWindow(new ViewModels.SettingsViewModel(App.Current.AppState.Settings)).Show();
            }
        }

        internal void OpenHelp()
        {
            if (App.Current.Windows.OfType<SettingsWindow>().Any())
            {
                var window = App.Current.Windows.OfType<SettingsWindow>().First();
                window.GoToPage(typeof(HelpPage));
                window.Activate();
            }
            else
            {
                var window = new SettingsWindow(new ViewModels.SettingsViewModel(App.Current.AppState.Settings));
                window.GoToPage(typeof(HelpPage));
                window.Show();
            }
        }

        private async void OnBalloonTipClicked(object? sender, RoutedEventArgs e)
        {
            if (DateTime.UtcNow - m_reviewTooltipShown < TimeSpan.FromSeconds(10))
            {
                await Launcher.LaunchUriAsync(new Uri(Publishing.StoreReviewProtocolLink));
                await App.Current.AppState.Settings.SaveAsync(x =>
                {
                    x.RemindToRateReview = false;
                    return x;
                });
            }
        }

        private async void OnCmdSequenceBegin(object? sender, EventArgs e)
        {
            m_logger.Debug("Command sequence started...");
            if (m_stopwatch.Elapsed >= RateReviewDelay && m_enableRateReviewRequests)
            {
                m_enableRateReviewRequests = false;
                m_reviewTooltipShown = DateTime.UtcNow;
                m_notifyIcon.ShowBalloonTip(Strings.Messages_EnjoyingFancyWM, Strings.Messages_AskForReview, BalloonIcon.None);
            }

            await Dispatcher.InvokeAsync(async () =>
            {
                var cts = new CancellationTokenSource(m_showContextHints ? ToastDurationCommandSequenceWithContextHints : ToastDurationCommandSequence);
                var toast = Dispatcher.RunAsync(async () =>
                {
                    try
                    {
                        // Yield so that the UI elements for the action toast are created on the next event loop tick,
                        // without slowing down LowLevelKeyPatternListener hooking.
                        await Task.Yield();
                        await ShowWaitingForActionToast(m_showContextHints, cts.Token);
                    }
                    catch (Exception e)
                    {
                        Dispatcher.RethrowOnDispatcher(e);
                    }
                });

                using var keyListener = new LowLevelKeyPatternListener(m_llkbdHook);
                keyListener.PatternChanged += (s, e) =>
                {
                    cts.Cancel();
                    keyListener.Dispose();
                    OnCommandKey(e.Keys);
                };

                long currentId = ++m_cmdSequenceId;
                bool showFocus = m_showFocusDuringAction;
                if (showFocus)
                {
                    m_tiling.ShowFocus = true;
                }
                await toast;
                if (showFocus)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(300));
                    if (m_cmdSequenceId == currentId)
                    {
                        m_tiling.ShowFocus = false;
                    }
                }
            });
        }

        private Task ShowToastAsync(string messageText, TimeSpan duration)
        {
            return ShowToastAsync(messageText, null, duration);
        }

        private async Task ShowToastAsync(string messageText, string? hintText, TimeSpan duration)
        {
            await Dispatcher.RunAsync(() => m_toasts.ShowToastAsync(new MessageBoxContent
            {
                Text = messageText,
                HintText = hintText,
            }, new CancellationTokenSource(duration).Token));
        }

        private async Task ShowToastAsync(string messageText, string? hintText, CancellationToken cancellationToken)
        {
            await Dispatcher.RunAsync(() => m_toasts.ShowToastAsync(new MessageBoxContent
            {
                Text = messageText,
                HintText = hintText,
            }, cancellationToken));
        }

        private async Task ShowWaitingForActionToast(bool showContextHints, CancellationToken cancellationToken)
        {
            var container = new StackPanel
            {
                Orientation = Orientation.Vertical,
            };
            if (showContextHints)
            {
                var extraContent = new ScrollViewer
                {
                    Visibility = Visibility.Collapsed,
                    Content = GetContextHintsExtraContent(),
                    VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                };

                _ = Dispatcher.RunAsync(async () =>
                {
                    await Task.Delay(ToastDurationShort);

                    extraContent.Visibility = Visibility.Visible;
                    extraContent.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    var extraContentHeight = extraContent.DesiredSize.Height + 32;
                    DoubleAnimation opacityAnimation = new(0, 1, TimeSpan.FromMilliseconds(200));
                    DoubleAnimation heightAnimation = new(0, extraContentHeight, TimeSpan.FromMilliseconds(200));
                    extraContent.BeginAnimation(OpacityProperty, opacityAnimation);
                    extraContent.BeginAnimation(MaxHeightProperty, heightAnimation);
                });

                container.Children.Add(extraContent);
            }

            container.Children.Add(new MessageBoxContent
            {
                Text = Strings.Messages_WaitingForAction,
                HintText = Strings.Messages_PressF1ForHelp,
            });
            await m_toasts.ShowToastAsync(container, cancellationToken);
        }


        private FrameworkElement GetContextHintsExtraContent()
        {
            var availableActions = GetAvailableActions().ToHashSet();
            var availableKeybindings = new KeybindingDictionary(m_keybindings.Where(x => availableActions.Contains(x.Key)));
            if (availableKeybindings.Any())
            {
                return new KeybindingList
                {
                    Keybindings = availableKeybindings,
                };
            }
            else
            {
                var border = new Border
                {
                    Child = new TextBlock
                    {
                        Text = Strings.Messages_NothingToDo,
                    },
                    Margin = new Thickness(0, 5, 0, 5),
                };
                border.Style = FindResource("SettingsItemStyle") as Style;
                return border;
            }
        }

        private IEnumerable<BindableAction> GetAvailableActions()
        {
            if (m_tiling.CanSplit(vertical: true))
            {
                yield return BindableAction.CreateVerticalPanel;
            }
            if (m_tiling.CanSplit(vertical: false))
            {
                yield return BindableAction.CreateHorizontalPanel;
            }
            if (m_tiling.CanStack())
            {
                yield return BindableAction.CreateStackPanel;
            }
            //yield return BindableAction.RefreshWorkspace;
            if (m_tiling.CanFloat())
            {
                yield return BindableAction.ToggleFloatingMode;
            }
            if (m_tiling.CanMoveFocus(Layouts.Tiling.TilingDirection.Left))
            {
                yield return BindableAction.MoveFocusLeft;
            }
            if (m_tiling.CanMoveFocus(Layouts.Tiling.TilingDirection.Up))
            {
                yield return BindableAction.MoveFocusUp;
            }
            if (m_tiling.CanMoveFocus(Layouts.Tiling.TilingDirection.Right))
            {
                yield return BindableAction.MoveFocusRight;
            }
            if (m_tiling.CanMoveFocus(Layouts.Tiling.TilingDirection.Down))
            {
                yield return BindableAction.MoveFocusDown;
            }
            if (m_tiling.CanPullUp())
            {
                yield return BindableAction.PullWindowUp;
            }
            if (m_tiling.CanMoveFocus(Layouts.Tiling.TilingDirection.Left))
            {
                yield return BindableAction.MoveFocusLeft;
            }
            if (m_tiling.CanMoveFocus(Layouts.Tiling.TilingDirection.Up))
            {
                yield return BindableAction.MoveFocusUp;
            }
            if (m_tiling.CanMoveFocus(Layouts.Tiling.TilingDirection.Right))
            {
                yield return BindableAction.MoveFocusRight;
            }
            if (m_tiling.CanMoveFocus(Layouts.Tiling.TilingDirection.Down))
            {
                yield return BindableAction.MoveFocusDown;
            }
            if (m_tiling.CanMoveWindow(Layouts.Tiling.TilingDirection.Left))
            {
                yield return BindableAction.MoveLeft;
            }
            if (m_tiling.CanMoveWindow(Layouts.Tiling.TilingDirection.Up))
            {
                yield return BindableAction.MoveUp;
            }
            if (m_tiling.CanMoveWindow(Layouts.Tiling.TilingDirection.Right))
            {
                yield return BindableAction.MoveRight;
            }
            if (m_tiling.CanMoveWindow(Layouts.Tiling.TilingDirection.Down))
            {
                yield return BindableAction.MoveDown;
            }
            if (m_tiling.CanSwapFocus(Layouts.Tiling.TilingDirection.Left))
            {
                yield return BindableAction.SwapLeft;
            }
            if (m_tiling.CanSwapFocus(Layouts.Tiling.TilingDirection.Up))
            {
                yield return BindableAction.SwapUp;
            }
            if (m_tiling.CanSwapFocus(Layouts.Tiling.TilingDirection.Right))
            {
                yield return BindableAction.SwapRight;
            }
            if (m_tiling.CanSwapFocus(Layouts.Tiling.TilingDirection.Down))
            {
                yield return BindableAction.SwapDown;
            }
            if (m_tiling.CanResize(Layouts.Tiling.PanelOrientation.Horizontal, ResizeDisplayPercentage))
            {
                yield return BindableAction.IncreaseWidth;
            }
            if (m_tiling.CanResize(Layouts.Tiling.PanelOrientation.Horizontal, -ResizeDisplayPercentage))
            {
                yield return BindableAction.DecreaseWidth;
            }
            if (m_tiling.CanResize(Layouts.Tiling.PanelOrientation.Vertical, ResizeDisplayPercentage))
            {
                yield return BindableAction.IncreaseHeight;
            }
            if (m_tiling.CanResize(Layouts.Tiling.PanelOrientation.Vertical, -ResizeDisplayPercentage))
            {
                yield return BindableAction.DecreaseHeight;
            }
            if (m_prevDesktop != null)
            {
                yield return BindableAction.SwitchToPreviousDesktop;
            }
            if (m_prevDesktop != null && m_tiling.CanFloat())
            {
                yield return BindableAction.MoveToPreviousDesktop;
            }
            if (m_prevDisplay != null)
            {
                yield return BindableAction.SwitchToPreviousDisplay;
            }
            if (m_prevDisplay != null && m_tiling.CanFloat())
            {
                yield return BindableAction.MoveToPreviousDisplay;
            }

            yield return BindableAction.ShowDesktop;
            yield return BindableAction.Cancel;
            //yield return BindableAction.ToggleManager;
        }

        private void OnSplitVHotkeyPressed()
        {
            m_logger.Debug("Creating a vertical split...");
            m_tiling.Split(vertical: true);
        }

        private void OnSplitHHotkeyPressed()
        {
            m_logger.Debug("Creating a horizontal split...");
            m_tiling.Split(vertical: false);
        }

        private void OnNotifyIconLeftMouseDown(object? sender, RoutedEventArgs e)
        {
            m_contextMenu.IsOpen = false;
            OpenSettings();
        }

        private void OnNotifyIconRightMouseDown(object? sender, RoutedEventArgs e)
        {
            m_contextMenu.IsOpen = true;
        }

        private async void OnVirtualDesktopChanged(object? sender, CurrentDesktopChangedEventArgs e)
        {
            m_logger.Debug("Virtual desktop changed...");
            m_prevDesktop = e.OldDesktop;
            await ShowToastAsync(e.NewDesktop.Name, ToastDurationShort);
        }

        private void OnVirtualDesktopRemoved(object? sender, DesktopChangedEventArgs e)
        {
            if (m_prevDesktop == e.Source)
            {
                m_prevDesktop = null;
            }
        }

        private void SwitchToDesktop(IVirtualDesktop desktop)
        {
            desktop.SwitchTo();
            // Make sure keyboard focus is bound to the foreground window.
            foreach (var window in m_workspace.GetCurrentDesktopSnapshot())
            {
                if (window.IsFocused)
                {
                    FocusHelper.ForceActivate(window.Handle);
                    break;
                }
            }
        }

        private async Task MoveToDesktopAsync(IVirtualDesktop desktop, IWindow window)
        {
            var formattedTitle = window.Title.Length > 15
                ? $"{window.Title.Substring(0, 15)}..."
                : window.Title;
            if (desktop.HasWindow(window))
            {
                PlayBeepSound();
                await ShowToastAsync($"\"{formattedTitle}\" {Strings.Messages_IsAlreadyOn} {desktop.Name}!", ToastDurationShort);
            }
            else
            {
                m_logger.Debug("Moving window to designated desktop...");
                desktop.MoveWindow(window);
                m_tiling.Refresh();
                await ShowToastAsync($"{Strings.Common_Moved} \"{formattedTitle}\" {Strings.Common_To} {desktop.Name}.", ToastDurationShort);
            }
        }

        private async void OnPreviousDesktopHotkeyPressed()
        {
            if (m_prevDesktop == null)
            {
                PlayBeepSound();
                await ShowToastAsync(Strings.Messages_NoPreviousDesktop, ToastDurationShort);
                return;
            }

            m_logger.Debug("Switching to previous desktop...");
            SwitchToDesktop(m_prevDesktop);
        }

        private async void OnMoveToPreviousDesktopHotkeyPressed()
        {
            if (m_workspace.FocusedWindow is not IWindow window)
            {
                PlayBeepSound();
                await ShowToastAsync(Strings.Messages_NoFocusedWindow, ToastDurationShort);
                return;
            }
            if (m_prevDesktop == null)
            {
                PlayBeepSound();
                await ShowToastAsync(Strings.Messages_NoPreviousDesktop, ToastDurationShort);
                return;
            }
            await MoveToDesktopAsync(m_prevDesktop, window);
        }

        private void OnDesktopHotkeyPressed(int desktopIndex)
        {
            m_logger.Debug("Switching to designated desktop...");
            var desktops = m_workspace.VirtualDesktopManager.Desktops;
            if (desktopIndex < desktops.Count)
            {
                SwitchToDesktop(desktops[desktopIndex]);
            }
            else
            {
                PlayBeepSound();
            }
        }

        private async void OnMoveToDesktopHotkeyPressed(int desktopIndex)
        {
            if (m_workspace.FocusedWindow is not IWindow window)
            {
                PlayBeepSound();
                await ShowToastAsync(Strings.Messages_NoFocusedWindow, ToastDurationShort);
                return;
            }
            var desktops = m_workspace.VirtualDesktopManager.Desktops;
            if (desktopIndex < desktops.Count)
            {
                var desktop = desktops[desktopIndex];
                var formattedTitle = window.Title.Length > 15
                    ? $"{window.Title.Substring(0, 15)}..."
                    : window.Title;
                if (desktop.HasWindow(window))
                {
                    PlayBeepSound();
                    await ShowToastAsync($"\"{formattedTitle}\" {Strings.Messages_IsAlreadyOn} {desktop.Name}!", ToastDurationShort);
                }
                else
                {
                    m_logger.Debug("Moving window to designated desktop...");
                    desktop.MoveWindow(window);
                    m_tiling.Refresh();
                    await ShowToastAsync($"{Strings.Common_Moved} \"{formattedTitle}\" {Strings.Common_To} {desktop.Name}.", ToastDurationShort);
                }
            }
            else
            {
                PlayBeepSound();
            }
        }

        private void SwitchToDisplay(IDisplay display)
        {
            var comparer = m_workspace.CreateSnapshotZOrderComparer();
            var mainWindow = m_workspace.GetCurrentDesktopSnapshot()
                .Where(x => x.State != WinMan.WindowState.Minimized)
                .Where(x => display.Bounds.Contains(x.Position.Center))
                .OrderBy(x => x, comparer)
                .FirstOrDefault();

            if (mainWindow == null)
            {
                PlayBeepSound();
            }
            else
            {
                FocusHelper.ForceActivate(mainWindow.Handle);
                m_tiling.Refresh();
                OnCurrentDisplayChanged(display);
            }
        }

        private async Task MoveToDisplayAsync(IDisplay display, IWindow window)
        {
            var formattedTitle = window.Title.Length > 15
                ? $"{window.Title.Substring(0, 15)}..."
                : window.Title;
            if (display.Bounds.Contains(window.Position.Center))
            {
                PlayBeepSound();
                await ShowToastAsync($"\"{formattedTitle}\" {Strings.Messages_IsAlreadyOn} {Strings.Common_Display} {display.Index() + 1}!", ToastDurationShort);
            }
            else
            {
                m_logger.Debug("Moving window to designated display...");
                var position = window.Position;
                var displayCenter = display.Bounds.Center;
                window.SetPosition(Rectangle.OffsetAndSize(displayCenter.X - position.Width / 2, displayCenter.Y - position.Height / 2, position.Width, position.Height));

                m_tiling.Refresh();
                OnCurrentDisplayChanged(display);

                await ShowToastAsync($"{Strings.Common_Moved} \"{formattedTitle}\" {Strings.Common_To} {Strings.Common_Display} {display.Index() + 1}.", ToastDurationShort);
            }
        }

        private async void OnPreviousDisplayHotkeyPressed()
        {
            if (m_prevDisplay == null)
            {
                PlayBeepSound();
                await ShowToastAsync(Strings.Messages_NoPreviousDisplay, ToastDurationShort);
                return;
            }

            m_logger.Debug("Switching to previous display...");
            SwitchToDisplay(m_prevDisplay);
        }

        private async void OnMoveToPreviousDisplayHotkeyPressed()
        {
            if (m_workspace.FocusedWindow is not IWindow window)
            {
                PlayBeepSound();
                await ShowToastAsync(Strings.Messages_NoFocusedWindow, ToastDurationShort);
                return;
            }
            if (m_prevDisplay == null)
            {
                PlayBeepSound();
                await ShowToastAsync(Strings.Messages_NoPreviousDisplay, ToastDurationShort);
                return;
            }
            await MoveToDisplayAsync(m_prevDisplay, window);
        }

        private void OnSwitchToDisplayHotkeyPressed(int displayIndex)
        {
            var displays = m_workspace.DisplayManager.Displays;
            if (displayIndex < displays.Count)
            {
                var display = displays[displayIndex];
                SwitchToDisplay(display);
            }
            else
            {
                PlayBeepSound();
            }
        }

        private async void OnMoveToDisplayHotkeyPressed(int displayIndex)
        {
            if (m_workspace.FocusedWindow is not IWindow window)
            {
                PlayBeepSound();
                await ShowToastAsync(Strings.Messages_NoFocusedWindow, ToastDurationShort);
                return;
            }

            var displays = m_workspace.DisplayManager.Displays;
            if (displayIndex < displays.Count)
            {
                var display = displays[displayIndex];
                await MoveToDisplayAsync(display, window);
            }
            else
            {
                PlayBeepSound();
            }
        }

        private void OnSettingsClick(object? sender, RoutedEventArgs e)
        {
            m_contextMenu.IsOpen = false;
            OpenSettings();
        }

        private void OnExitClick(object? sender, RoutedEventArgs e)
        {
            m_logger.Debug("Application exit requested!");
            m_contextMenu.IsOpen = false;
            App.Current.Terminate();
        }

        private void OnSponsorClick(object? sender, RoutedEventArgs e)
        {
            App.Current.Sponsor();
        }

        private void OnAboutClick(object? sender, RoutedEventArgs e)
        {
            m_logger.Debug("Opening about window...");
            m_contextMenu.IsOpen = false;
            new AboutWindow().Show();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            e.Cancel = true;
        }

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
        public void Dispose()
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
        {
            m_workspace.VirtualDesktopManager.CurrentDesktopChanged -= OnVirtualDesktopChanged;
            m_workspace.VirtualDesktopManager.DesktopRemoved -= OnVirtualDesktopRemoved;
            m_workspace.FocusedWindowChanged -= OnFocusedWindowChanged;

            m_logger.Debug($"Disposing of {nameof(MainWindow)}...");

            m_llkbdHook?.Dispose();

            if (m_tiling?.Active == true)
            {
                m_tiling.Stop();
            }

            m_animationThread?.Dispose();

            foreach (var hk in m_directHks)
            {
                hk.Dispose();
            }

            m_logger.Debug($"Stopping the tiling window manager...");
            m_tiling?.Dispose();
            m_logger.Debug($"Closing the workspace...");
            m_workspace?.Dispose();
            m_logger.Debug($"Closing all other subscriptions...");
            m_subscriptions?.Dispose();

            m_notifyIcon?.Dispose();
        }
    }
}
