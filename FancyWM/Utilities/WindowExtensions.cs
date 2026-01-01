using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;

using WinMan;
using FancyWM.DllImports;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Interop;
using WinMan.Windows;
using System.IO;
using System.Xml.Linq;
using System.Threading;

namespace FancyWM.Utilities
{
    internal static class WindowExtensions
    {
        private class ProcessInfo(int id, string name)
        {
            public int ID = id;
            public string Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        private static readonly ConditionalWeakTable<IWindow, ProcessInfo> m_processCache = [];
        private static readonly ConditionalWeakTable<IWindow, BitmapSource?> m_icons = [];

        [DllImport("User32", EntryPoint = "GetClassLongW", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern uint GetClassLong32(HWND hWnd, GetClassLong_nIndex nIndex);

        [DllImport("user32.dll", EntryPoint = "GetClassLongPtr")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern IntPtr GetClassLongPtr64(HWND hWnd, GetClassLong_nIndex nIndex);

        private static IntPtr GetClassLongPtr(HWND hWnd, GetClassLong_nIndex nIndex)
        {
            if (Marshal.SizeOf<IntPtr>() == 4)
            {
                return new IntPtr(GetClassLong32(hWnd, nIndex));
            }
            else
            {
                return GetClassLongPtr64(hWnd, nIndex);
            }
        }

        public static BitmapSource? GetCachedIcon(this IWindow window)
        {
            BitmapSource? icon;
            if (m_icons.TryGetValue(window, out icon!))
            {
                return icon;
            }

            try
            {
                icon = GetIcon(window);
            }
            catch
            {
                icon = null;
            }

            m_icons.AddOrUpdate(window, icon);
            return icon;
        }

        private static BitmapSource GetIcon(IWindow window)
        {
            HWND hwnd = new(window.Handle);

            if (window is Win32Window win32Window && win32Window.ClassName == "ApplicationFrameWindow")
            {
                if (TryLoadModernAppShellIcon(window) is BitmapSource bitmap)
                {
                    return bitmap;
                }
            }

            IntPtr hIcon = GetClassLongPtr(hwnd, GetClassLong_nIndex.GCL_HICON);
            if (hIcon == IntPtr.Zero)
            {
                hIcon = GetClassLongPtr(hwnd, GetClassLong_nIndex.GCL_HICONSM);
                if (hIcon == IntPtr.Zero && window.GetProcess().MainModule is ProcessModule pm && pm.FileName is string fileName)
                {
                    return LoadShellIcon(fileName);
                }
            }

            try
            {
                using Bitmap bmp = Icon.FromHandle(hIcon).ToBitmap();
                return Imaging.CreateBitmapSourceFromHBitmap(
                   bmp.GetHbitmap(), IntPtr.Zero, System.Windows.Int32Rect.Empty,
                   BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                PInvoke.DestroyIcon(new(hIcon));
            }
        }

        private static BitmapImage? TryLoadModernAppShellIcon(IWindow window)
        {
            HWND hwndChild = PInvoke.FindWindowEx(new(window.Handle), new HWND(), "Windows.UI.Core.CoreWindow", null);
            for (int i = 0; i < 3; i++)
            {
                if (hwndChild.Value != IntPtr.Zero)
                {
                    break;
                }
                // Sleep for 30ms, 300ms, 3000ms
                Thread.Sleep(3 * (int)Math.Pow(10, (i + 1)));
                hwndChild = PInvoke.FindWindowEx(new(window.Handle), new HWND(), "Windows.UI.Core.CoreWindow", null);
            }
            if (hwndChild.Value == IntPtr.Zero)
            {
                return null;
            }

            var process = window.Workspace.UnsafeCreateFromHandle(hwndChild.Value).GetProcess();
            if (process.MainModule == null)
            {
                return null;
            }

            var processPath = process.MainModule.FileName;
            var directory = Path.GetDirectoryName(processPath)!;
            var manifestPath = Path.Combine(directory, "AppxManifest.xml");
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            using var fs = File.OpenRead(manifestPath);
            var manifest = XDocument.Load(fs);

            const string ns = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
            string? logoName = manifest?.Root?.Element(XName.Get("Properties", ns))?.Element(XName.Get("Logo", ns))?.Value;
            if (logoName == null)
            {
                return null;
            }

            string[] matchingFiles = Directory.GetFiles(directory, Path.GetFileNameWithoutExtension(logoName) + "*" + Path.GetExtension(logoName), SearchOption.AllDirectories);
            if (matchingFiles.Length == 0)
            {
                return null;
            }

            return new BitmapImage(new Uri(matchingFiles[0]));
        }

        private static BitmapSource LoadShellIcon(string fileName)
        {
            SHFILEINFOW shinfo = new();
            unsafe
            {
                _ = (IntPtr)(void*)PInvoke.SHGetFileInfo(fileName, 0, &shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_FLAGS.SHGFI_ICON | SHGFI_FLAGS.SHGFI_LARGEICON);
            }
            try
            {
                using Bitmap bmp = Icon.FromHandle(shinfo.hIcon).ToBitmap();
                return Imaging.CreateBitmapSourceFromHBitmap(
                   bmp.GetHbitmap(), IntPtr.Zero, System.Windows.Int32Rect.Empty,
                   BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                PInvoke.DestroyIcon(shinfo.hIcon);
            }
        }

        public static string GetCachedProcessName(this IWindow window)
        {
            return GetCachedProcessInfo(window).name;
        }

        public static string DebugString(this IWindow window)
        {
            var (id, name) = GetCachedProcessInfo(window);
            return $"{window.Handle:X8}={name}({id})";
        }

        private static (int id, string name) GetCachedProcessInfo(IWindow window)
        {
            var info = GetCachedProcessInfoInternal(window);
            return (info.ID, info.Name);
        }

        private static ProcessInfo GetCachedProcessInfoInternal(IWindow window)
        {
            ProcessInfo? info;
            if (m_processCache.TryGetValue(window, out info!))
            {
                return info;
            }

            try
            {
                var process = window.GetProcess();
                info = new(process.Id, process.ProcessName);
                if (info.Name == "ApplicationFrameHost" && window is Win32Window win32Window && win32Window.ClassName == "ApplicationFrameWindow")
                {
                    HWND hwndChild = PInvoke.FindWindowEx(new(window.Handle), new HWND(), "Windows.UI.Core.CoreWindow", null);
                    if (hwndChild.Value != IntPtr.Zero)
                    {
                        info = GetCachedProcessInfoInternal(window.Workspace.UnsafeCreateFromHandle(hwndChild.Value));
                    }
                }
            }
            catch (InvalidWindowReferenceException)
            {
                info = new(0, "Invalid handle");
            }
            catch (ExternalException)
            {
                info = new(0, "Inaccessible process");
            }
            catch (Exception e) when (e is InvalidOperationException || e is ArgumentException)
            {
                info = new(0, "Dead process");
            }
            m_processCache.AddOrUpdate(window, info);
            return info;
        }

        public static object GetMetadata(this IWindow window)
        {
            return new
            {
                window.Handle,
                window.IsAlive,
                window.IsFocused,
                window.IsTopmost,
                window.MaxSize,
                window.MinSize,
                window.Position,
                window.State,
                Permissions = new
                {
                    window.CanClose,
                    window.CanMaximize,
                    window.CanMinimize,
                    window.CanMove,
                    window.CanReorder,
                    window.CanResize,
                },
            };
        }
    }
}
