using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace AntigravityTokenOrb
{
    public static class Program
    {
        private const string MutexName = "AntigravityTokenOrb_SingleInstance_Mutex";
        private const string WakeEventName = "AntigravityTokenOrb_WakeUp_Event";

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        private static Mutex _singleInstanceMutex;
        private static EventWaitHandle _wakeEvent;

        [STAThread]
        public static void Main(string[] args)
        {
            try { SetProcessDPIAware(); } catch { }

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                LogError("UnhandledException", e.ExceptionObject as Exception);
            };

            bool createdNew;
            _singleInstanceMutex = new Mutex(true, MutexName, out createdNew);
            if (!createdNew)
            {
                // Signal the already-running instance to surface itself & open dashboard
                try
                {
                    using (var existingWake = EventWaitHandle.OpenExisting(WakeEventName))
                    {
                        existingWake.Set();
                        return;
                    }
                }
                catch
                {
                    MessageBox.Show(
                        "Antigravity Token 悬浮球已在运行中（请查看桌面或系统托盘）。",
                        "Antigravity Token 悬浮球",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }
            }

            try
            {
                bool wakeCreated;
                _wakeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, WakeEventName, out wakeCreated);

                var app = new Application();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                app.DispatcherUnhandledException += (s, e) =>
                {
                    LogError("DispatcherUnhandledException", e.Exception);
                    e.Handled = true;
                };

                var settings = SettingsManager.Load();
                var engine = new TokenMonitorEngine();

                OrbWindow orb = null;
                TrayManager tray = null;
                bool shuttingDown = false;

                Action doExit = () =>
                {
                    shuttingDown = true;
                    try { if (_wakeEvent != null) _wakeEvent.Set(); } catch { }
                    try { if (tray != null) tray.Dispose(); } catch { }
                    try { if (orb != null) { orb.CloseChildWindows(); orb.Close(); } } catch { }
                    app.Shutdown();
                };

                orb = new OrbWindow(settings, engine, doExit);
                tray = new TrayManager(
                    () =>
                    {
                        orb.EnsureVisibleOnScreen();
                    },
                    () =>
                    {
                        orb.EnsureVisibleOnScreen();
                        orb.ToggleDashboard();
                    },
                    () =>
                    {
                        orb.EnsureVisibleOnScreen();
                        orb.OpenSettingsWindow();
                    },
                    () => orb.ToggleDisplayMode(),
                    () =>
                    {
                        orb.EnsureVisibleOnScreen();
                        orb.ResetPositionToCorner();
                    },
                    doExit);

                orb.SnapshotUpdated += (snap, alert) => tray.UpdateStatus(snap, alert, settings);

                // Background listener: double-clicking the app icon again brings orb & dashboard to front
                var wakeThread = new Thread(() =>
                {
                    while (!shuttingDown)
                    {
                        try
                        {
                            _wakeEvent.WaitOne();
                            if (shuttingDown) break;
                            app.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                if (orb != null)
                                {
                                    orb.EnsureVisibleOnScreen();
                                    orb.ShowDashboard();
                                }
                            }));
                        }
                        catch
                        {
                            break;
                        }
                    }
                });
                wakeThread.IsBackground = true;
                wakeThread.Start();

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
                if (_wakeEvent != null)
                {
                    try { _wakeEvent.Dispose(); } catch { }
                }
                if (_singleInstanceMutex != null)
                {
                    try { _singleInstanceMutex.ReleaseMutex(); } catch { }
                    _singleInstanceMutex.Dispose();
                }
            }
        }

        private static void LogError(string tag, Exception ex)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AntigravityTokenOrb");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "error.log");
                File.AppendAllText(path,
                    string.Format("[{0}] {1}: {2}\r\n\r\n", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), tag, ex));
            }
            catch { }
        }
    }
}
