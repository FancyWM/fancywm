using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Media;

using FancyWM.DllImports;

namespace FancyWM.Utilities
{
    internal class FauxMicaProvider : IMicaProvider
    {
        public event EventHandler<MicaOptionsChangedEventArgs>? PrimaryColorChanged;

        public Color PrimaryColor
        {
            get
            {
                lock (m_syncRoot)
                {
                    return m_primaryColor;
                }
            }
        }

        private readonly object m_syncRoot = new();
        private Color m_primaryColor;
        private DateTime m_lastCheckTime;
        private string? m_lastWallpaperPath;
        private readonly Thread m_thread;
        private readonly TimeSpan m_checkingInterval;
        private volatile bool m_disposed;

        public FauxMicaProvider(TimeSpan checkingInterval)
        {
            m_thread = new Thread(ThreadMain)
            {
                Name = "FauxMicaProviderThread",
                Priority = ThreadPriority.BelowNormal,
                IsBackground = true,
            };
            m_thread.Start();
            m_checkingInterval = checkingInterval;
        }

        private void ThreadMain()
        {
            while (!m_disposed)
            {
                try
                {
                    bool changed = false;
                    var current = SystemWallpaper.GetCurrent();
                    if (current.WallpaperPath is string wallpaperPath)
                    {
                        var lastUpdateTime = File.GetLastWriteTimeUtc(wallpaperPath);
                        var isWallpaperChanged = m_lastWallpaperPath != wallpaperPath || lastUpdateTime > m_lastCheckTime;
                        m_lastCheckTime = DateTime.UtcNow;
                        m_lastWallpaperPath = wallpaperPath;
                        if (isWallpaperChanged)
                        {
                            try
                            {
                                IntPtr hwnd = GetWallpaperHWND();

                                PInvoke.GetWindowRect(new(hwnd), out var rect);
                                using var originalImage = new System.Drawing.Bitmap(rect.right - rect.left, rect.bottom - rect.top);

                                {
                                    using var graphics = System.Drawing.Graphics.FromImage(originalImage);
                                    const PrintWindow_nFlags PW_RENDERFULLCONTENT = (PrintWindow_nFlags)0x00000002;
                                    bool success = PInvoke.PrintWindow(new(hwnd), new HDC(graphics.GetHdc()), PW_RENDERFULLCONTENT);
                                    graphics.ReleaseHdc();
                                }

                                using var pixelImage = new System.Drawing.Bitmap(originalImage, new(2, 2));
                                var colors = new[]
                                {
                                    pixelImage.GetPixel(0, 0),
                                    pixelImage.GetPixel(0, 1),
                                    pixelImage.GetPixel(1, 0),
                                    pixelImage.GetPixel(1, 1),
                                };

                                var primaryColor = TransformColor(AverageColor(colors));

                                lock (m_syncRoot)
                                {
                                    m_primaryColor = ToMediaColor(primaryColor);
                                }
                                changed = true;
                            }
                            catch (Exception e)
                            {
                                throw new AggregateException($"Could not load image!", e);
                            }
                        }
                    }
                    else if (current.RGB is byte[] rgb)
                    {
                        m_lastWallpaperPath = null;
                        var newColor = new Color { A = 255, R = rgb[0], G = rgb[1], B = rgb[2] };
                        lock (m_syncRoot)
                        {
                            changed = true;
                            if (newColor != m_primaryColor)
                            {
                                m_primaryColor = newColor;
                            }
                        }
                    }
                    else
                    {
                        m_lastWallpaperPath = null;
                        lock (m_syncRoot)
                        {
                            m_primaryColor = Colors.Transparent;
                        }
                    }

                    if (changed)
                    {
                        PrimaryColorChanged?.Invoke(this, new MicaOptionsChangedEventArgs());
                    }

                    Thread.Sleep(m_checkingInterval);
                }
                catch (Exception e)
                {
                    App.Current.Logger.Warning(e, "Exception thrown while restoring the original window layout!");
                    return;
                }
            }
        }

        private IntPtr GetWallpaperHWND()
        {
            IntPtr hwndDefView = IntPtr.Zero;
            unsafe
            {
                PInvoke.EnumWindows((HWND hwnd, LPARAM lParam) =>
                {
                    IntPtr hwndDefView = PInvoke.FindWindowEx(hwnd, new HWND(), "SHELLDLL_DefView", null);
                    if (hwndDefView != IntPtr.Zero)
                    {
                        *((IntPtr*)lParam.Value) = hwndDefView;
                        return false;
                    }
                    return true;
                }, new LPARAM((nint)(&hwndDefView)));
            }

            if (hwndDefView == IntPtr.Zero)
            {
                throw new Exception("Could not find SHELLDLL_DefView!");
            }
            return hwndDefView;
        }

        private static System.Drawing.Color TransformColor(System.Drawing.Color color)
        {
            var h = color.GetHue();
            var l = color.GetBrightness();
            return GDIColorFromHSL(h, 1, l);
        }

        public static System.Drawing.Color GDIColorFromHSL(float h, float s, float l)
        {
            byte r;
            byte g;
            byte b;

            if (s == 0)
            {
                r = g = b = (byte)(l * 255);
            }
            else
            {
                float v1, v2;
                float hue = (float)h / 360;

                v2 = (l < 0.5) ? (l * (1 + s)) : ((l + s) - (l * s));
                v1 = 2 * l - v2;

                r = (byte)(255 * RGBComponent(v1, v2, hue + (1.0f / 3)));
                g = (byte)(255 * RGBComponent(v1, v2, hue));
                b = (byte)(255 * RGBComponent(v1, v2, hue - (1.0f / 3)));
            }

            return System.Drawing.Color.FromArgb(r, g, b);
        }

        private static float RGBComponent(float v1, float v2, float vH)
        {
            if (vH < 0)
                vH += 1;

            if (vH > 1)
                vH -= 1;

            if ((6 * vH) < 1)
                return (v1 + (v2 - v1) * 6 * vH);

            if ((2 * vH) < 1)
                return v2;

            if ((3 * vH) < 2)
                return (v1 + (v2 - v1) * ((2.0f / 3) - vH) * 6);

            return v1;
        }

        private static System.Drawing.Color AverageColor(IEnumerable<System.Drawing.Color> colors)
        {
            int r = 0;
            int g = 0;
            int b = 0;
            int count = 0;
            foreach (var color in colors)
            {
                r += color.R;
                g += color.G;
                b += color.B;
                count += 1;
            }
            return System.Drawing.Color.FromArgb(r / count, g / count, b / count);
        }

        private static Color ToMediaColor(System.Drawing.Color color)
        {
            return new Color
            {
                A = 255,
                R = color.R,
                G = color.G,
                B = color.B,
            };
        }

        public void Dispose()
        {
            m_disposed = true;
            m_thread?.Join(m_checkingInterval * 2);
        }
    }
}
