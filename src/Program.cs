using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace AntigravityTokenOrb
{
    public static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        private static Mutex _singleInstanceMutex;

        [STAThread]
        public static void Main(string[] args)
        {
            try { SetProcessDPIAware(); } catch { }

            bool createdNew;
            _singleInstanceMutex = new Mutex(true, "AntigravityTokenOrb_SingleInstance_Mutex", out createdNew);
            if (!createdNew)
            {
                MessageBox.Show(
                    "Antigravity Token 悬浮球已在运行中（请查看桌面或系统托盘）。",
                    "Antigravity Token 悬浮球",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                var app = new Application();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                var settings = SettingsManager.Load();
                var engine = new TokenMonitorEngine();

                OrbWindow orb = null;
                TrayManager tray = null;

                Action doExit = () =>
                {
                    try { if (tray != null) tray.Dispose(); } catch { }
                    try { if (orb != null) { orb.CloseChildWindows(); orb.Close(); } } catch { }
                    app.Shutdown();
                };

                orb = new OrbWindow(settings, engine, doExit);
                tray = new TrayManager(
                    () =>
                    {
                        orb.Show();
                        orb.Activate();
                    },
                    () =>
                    {
                        if (!orb.IsVisible) orb.Show();
                        orb.ToggleDashboard();
                    },
                    () =>
                    {
                        if (!orb.IsVisible) orb.Show();
                        orb.OpenSettingsWindow();
                    },
                    () => orb.ToggleDisplayMode(),
                    () =>
                    {
                        if (!orb.IsVisible) orb.Show();
                        orb.ResetPositionToCorner();
                    },
                    doExit);

                orb.SnapshotUpdated += (snap, alert) => tray.UpdateStatus(snap, alert, settings);

                orb.Show();

                if (args != null)
                {
                    foreach (string a in args)
                    {
                        if (string.Equals(a, "--open-dashboard", StringComparison.OrdinalIgnoreCase))
                        {
                            orb.ShowDashboard();
                            break;
                        }
                    }
                }

                app.Run();
            }
            finally
            {
                if (_singleInstanceMutex != null)
                {
                    try { _singleInstanceMutex.ReleaseMutex(); } catch { }
                    _singleInstanceMutex.Dispose();
                }
            }
        }
    }
}
