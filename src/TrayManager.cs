using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AntigravityTokenOrb
{
    public class TrayManager : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly Icon _customIcon;
        private readonly ToolStripMenuItem _autoStartMenuItem;

        // Per-alert cooldown (3 minutes) so balloons never spam
        private DateTime _lastContextAlertUtc = DateTime.MinValue;
        private DateTime _lastRateAlertUtc = DateTime.MinValue;
        private DateTime _lastTodayAlertUtc = DateTime.MinValue;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        public TrayManager(
            Action onShowOrb,
            Action onToggleDashboard,
            Action onOpenSettings,
            Action onToggleStyle,
            Action onResetPos,
            Action onExit)
        {
            _customIcon = CreateOrbIcon();

            var menu = new ContextMenuStrip();
            menu.Items.Add("显示 / 置前悬浮球", null, (s, e) => { if (onShowOrb != null) onShowOrb(); });
            menu.Items.Add("展开 / 收起详情面板", null, (s, e) => { if (onToggleDashboard != null) onToggleDashboard(); });
            menu.Items.Add("⚙ 设置中心 (主题/单价/预警/自启)...", null, (s, e) => { if (onOpenSettings != null) onOpenSettings(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("切换球形 / 迷你胶囊", null, (s, e) => { if (onToggleStyle != null) onToggleStyle(); });
            menu.Items.Add("重置悬浮球位置", null, (s, e) => { if (onResetPos != null) onResetPos(); });

            _autoStartMenuItem = new ToolStripMenuItem("Windows 开机自动启动")
            {
                CheckOnClick = true,
                Checked = SettingsManager.IsAutoStartEnabled()
            };
            _autoStartMenuItem.Click += (s, e) =>
            {
                SettingsManager.SetAutoStart(_autoStartMenuItem.Checked);
            };
            menu.Opening += (s, e) =>
            {
                _autoStartMenuItem.Checked = SettingsManager.IsAutoStartEnabled();
            };
            menu.Items.Add(_autoStartMenuItem);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出 Antigravity Token 悬浮球", null, (s, e) => { if (onExit != null) onExit(); });

            _notifyIcon = new NotifyIcon
            {
                Icon = _customIcon,
                Text = "Antigravity Token 悬浮球",
                Visible = true,
                ContextMenuStrip = menu
            };

            _notifyIcon.DoubleClick += (s, e) => { if (onShowOrb != null) onShowOrb(); };
        }

        public void UpdateStatus(TokenSnapshot snap, AlertStatus alert, AppSettings settings)
        {
            if (snap == null || settings == null) return;

            double rate = settings.IncludeCacheInPrimary ? snap.Rate1mAllPerMin : snap.Rate1mEffectivePerMin;
            long val5h = settings.IncludeCacheInPrimary ? snap.Last5Hours.TotalAllTokens : snap.Last5Hours.EffectiveTokens;
            long valWeek = settings.IncludeCacheInPrimary ? snap.Last7Days.TotalAllTokens : snap.Last7Days.EffectiveTokens;
            double costUsd = TokenFormat.EstimateCostUsd(
                snap.Today.InputTokens, snap.Today.OutputTokens, snap.Today.CacheReadTokens, settings);

            string text = string.Format("AGY {0} | 5h:{1} 周:{2}",
                TokenFormat.FormatRatePerMin(rate),
                TokenFormat.FormatCount(val5h),
                TokenFormat.FormatCount(valWeek));
            if (text.Length > 63) text = text.Substring(0, 63);
            _notifyIcon.Text = text;

            if (alert == null || !alert.HasAny || !settings.AlertsEnabled || !settings.BalloonNotificationsEnabled)
                return;

            DateTime now = DateTime.UtcNow;
            TimeSpan cooldown = TimeSpan.FromMinutes(3);

            if (alert.ContextExceeded && (now - _lastContextAlertUtc) > cooldown)
            {
                _lastContextAlertUtc = now;
                ShowBalloon("Antigravity 上下文超限预警",
                    string.Format("当前会话上下文已达 {0}（阈值 {1}），建议执行 /compact 压缩上下文。",
                        TokenFormat.FormatCount(snap.LatestContextTokens),
                        TokenFormat.FormatCount(settings.ContextAlertThreshold)));
            }
            else if (alert.RateExceeded && (now - _lastRateAlertUtc) > cooldown)
            {
                _lastRateAlertUtc = now;
                ShowBalloon("Antigravity 消耗速率预警",
                    string.Format("近 1 分钟新增速率达到 {0}（阈值 {1}）。",
                        TokenFormat.FormatRatePerMin(snap.Rate1mEffectivePerMin),
                        TokenFormat.FormatRatePerMin(settings.Rate1mAlertThreshold)));
            }
            else if (alert.TodayExceeded && (now - _lastTodayAlertUtc) > cooldown)
            {
                _lastTodayAlertUtc = now;
                ShowBalloon("Antigravity 今日用量预警",
                    string.Format("今日新增 Token 已达 {0}（预估费用 {1}）。",
                        TokenFormat.FormatCount(snap.Today.EffectiveTokens),
                        TokenFormat.FormatCost(costUsd, settings)));
            }
        }

        private void ShowBalloon(string title, string message)
        {
            try
            {
                _notifyIcon.ShowBalloonTip(5000, title, message, ToolTipIcon.Warning);
            }
            catch { }
        }

        private static Icon CreateOrbIcon()
        {
            using (var bmp = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                using (var bgBrush = new SolidBrush(Color.FromArgb(255, 14, 20, 36)))
                    g.FillEllipse(bgBrush, 2, 2, 28, 28);

                using (var ringBrush = new LinearGradientBrush(
                    new Rectangle(2, 2, 28, 28),
                    Color.FromArgb(255, 56, 189, 248),
                    Color.FromArgb(255, 167, 139, 250),
                    45f))
                using (var pen = new Pen(ringBrush, 3.2f))
                {
                    g.DrawEllipse(pen, 4, 4, 24, 24);
                }

                using (var dotBrush = new SolidBrush(Color.FromArgb(255, 125, 211, 252)))
                    g.FillEllipse(dotBrush, 12, 12, 8, 8);

                IntPtr hIcon = bmp.GetHicon();
                try
                {
                    return (Icon)Icon.FromHandle(hIcon).Clone();
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
        }

        public void Dispose()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            if (_customIcon != null)
                _customIcon.Dispose();
        }
    }
}
