using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace AntigravityTokenOrb
{
    public class OrbWindow : Window
    {
        private readonly AppSettings _settings;
        private readonly TokenMonitorEngine _engine;
        private readonly DashboardWindow _dashboard;
        private readonly Action _onExitRequested;

        private readonly DispatcherTimer _dataTimer;
        private readonly DispatcherTimer _animTimer;

        private ThemePalette _theme;
        private SettingsWindow _settingsWindow;

        // Orb mode UI elements
        private Grid _orbRoot;
        private Ellipse _orbGlowRing;
        private Ellipse _orbBody;
        private Ellipse _orbTrackRing;
        private Path _progressArc;
        private Path _spinnerArc;
        private RotateTransform _spinnerRotate;
        private Ellipse _orbLiveDot;
        private TextBlock _orbStateLabel;
        private TextBlock _orbSpeedText;
        private Border _orbBottomPill;
        private TextBlock _orbTotalText;

        // Pill mode UI elements
        private Border _pillRoot;
        private Ellipse _pillLiveDot;
        private TextBlock _pillSpeedText;
        private Border _pillDivider;
        private TextBlock _pillTotalText;

        // Interaction state
        private bool _isDragging;
        private bool _dragMoved;
        private Point _dragStartScreen;
        private double _winStartLeft;
        private double _winStartTop;

        // Animation state
        private double _spinAngle;
        private double _targetProgress;
        private double _currentProgress;
        private double _pulsePhase;
        private bool _isActive;
        private bool _hasAlert;
        private Color _accentColor = (Color)ColorConverter.ConvertFromString("#38BDF8");

        public event Action<TokenSnapshot, AlertStatus> SnapshotUpdated;

        public OrbWindow(AppSettings settings, TokenMonitorEngine engine, Action onExitRequested)
        {
            _settings = settings;
            _engine = engine;
            _onExitRequested = onExitRequested;
            _theme = ThemeManager.GetTheme(_settings.ThemeId);

            Title = "Antigravity Token 悬浮球";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = _settings.TopMost;
            Opacity = _settings.Opacity;

            _dashboard = new DashboardWindow(
                _settings,
                _engine.BrainDir,
                () =>
                {
                    SettingsManager.Save(_settings);
                    RefreshNow();
                },
                ToggleDisplayMode,
                OpenSettingsWindow);

            _dashboard.PinnedConversationChanged += (convId) => RefreshNow();

            BuildUi();
            ApplyThemeToOrb();
            BuildContextMenu();
            ApplyDisplayMode();
            RestorePosition();

            MouseLeftButtonDown += OnOrbMouseDown;
            MouseMove += OnOrbMouseMove;
            MouseLeftButtonUp += OnOrbMouseUp;

            _dataTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _dataTimer.Tick += (s, e) => RefreshNow();
            _dataTimer.Start();

            _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _animTimer.Tick += OnAnimationTick;
            _animTimer.Start();

            Loaded += (s, e) => RefreshNow();
        }

        public void OpenSettingsWindow()
        {
            if (_settingsWindow != null && _settingsWindow.IsVisible)
            {
                _settingsWindow.Activate();
                return;
            }
            _settingsWindow = new SettingsWindow(_settings, () =>
            {
                ApplyAllSettings();
            });
            _settingsWindow.Closed += (s, e) => _settingsWindow = null;
            _settingsWindow.Show();
            _settingsWindow.Activate();
        }

        public void ApplyAllSettings()
        {
            _theme = ThemeManager.GetTheme(_settings.ThemeId);
            Topmost = _settings.TopMost;
            Opacity = _settings.Opacity;
            ApplyThemeToOrb();
            _dashboard.RebuildUi();
            BuildContextMenu();
            RefreshNow();
        }

        private void BuildUi()
        {
            var container = new Grid();
            Content = container;

            // === 1. CIRCULAR ORB VIEW (116x116) ===
            _orbRoot = new Grid { Width = 116, Height = 116, Cursor = Cursors.Hand };

            _orbGlowRing = new Ellipse
            {
                Width = 96,
                Height = 96,
                Stroke = new SolidColorBrush(Color.FromArgb(95, 56, 189, 248)),
                StrokeThickness = 2.5,
                Fill = Brushes.Transparent,
                Effect = new DropShadowEffect
                {
                    Color = (Color)ColorConverter.ConvertFromString("#38BDF8"),
                    BlurRadius = 16,
                    ShadowDepth = 0,
                    Opacity = 0.55
                }
            };
            _orbRoot.Children.Add(_orbGlowRing);

            _orbBody = new Ellipse
            {
                Width = 92,
                Height = 92,
                StrokeThickness = 1,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 14,
                    ShadowDepth = 3,
                    Opacity = 0.55
                }
            };
            _orbRoot.Children.Add(_orbBody);

            var highlight = new Ellipse
            {
                Width = 64,
                Height = 28,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 15, 0, 0),
                IsHitTestVisible = false,
                Fill = new LinearGradientBrush(
                    Color.FromArgb(46, 255, 255, 255),
                    Color.FromArgb(0, 255, 255, 255),
                    90)
            };
            _orbRoot.Children.Add(highlight);

            var ringCanvas = new Canvas { Width = 116, Height = 116, IsHitTestVisible = false };

            _orbTrackRing = new Ellipse
            {
                Width = 86,
                Height = 86,
                StrokeThickness = 3.2
            };
            Canvas.SetLeft(_orbTrackRing, 15);
            Canvas.SetTop(_orbTrackRing, 15);
            ringCanvas.Children.Add(_orbTrackRing);

            _progressArc = new Path
            {
                StrokeThickness = 3.4,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            ringCanvas.Children.Add(_progressArc);

            _spinnerRotate = new RotateTransform(0, 58, 58);
            _spinnerArc = new Path
            {
                StrokeThickness = 2.4,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                RenderTransform = _spinnerRotate,
                Data = BuildArcGeometry(58, 58, 43, 0, 75),
                Opacity = 0.0
            };
            ringCanvas.Children.Add(_spinnerArc);
            _orbRoot.Children.Add(ringCanvas);

            var centerStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsHitTestVisible = false
            };

            var statusRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 1)
            };
            _orbLiveDot = new Ellipse
            {
                Width = 5.5,
                Height = 5.5,
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _orbStateLabel = new TextBlock
            {
                Text = "IDLE",
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"),
                FontSize = 8.5,
                FontWeight = FontWeights.SemiBold
            };
            statusRow.Children.Add(_orbLiveDot);
            statusRow.Children.Add(_orbStateLabel);
            centerStack.Children.Add(statusRow);

            _orbSpeedText = new TextBlock
            {
                Text = "0/m",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, -1, 0, 1)
            };
            centerStack.Children.Add(_orbSpeedText);

            _orbBottomPill = new Border
            {
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(6, 1, 6, 1),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _orbTotalText = new TextBlock
            {
                Text = "今 0",
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
                FontSize = 9.5,
                FontWeight = FontWeights.Medium
            };
            _orbBottomPill.Child = _orbTotalText;
            centerStack.Children.Add(_orbBottomPill);

            _orbRoot.Children.Add(centerStack);
            container.Children.Add(_orbRoot);

            // === 2. MINI PILL VIEW ===
            _pillRoot = new Border
            {
                Width = 182,
                Height = 40,
                Margin = new Thickness(6),
                CornerRadius = new CornerRadius(20),
                BorderThickness = new Thickness(1.2),
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 12,
                    ShadowDepth = 2,
                    Opacity = 0.5
                }
            };

            var pillRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            _pillLiveDot = new Ellipse
            {
                Width = 7,
                Height = 7,
                Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _pillSpeedText = new TextBlock
            {
                Text = "0/m",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            _pillDivider = new Border
            {
                Width = 1,
                Height = 14,
                Margin = new Thickness(9, 0, 9, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _pillTotalText = new TextBlock
            {
                Text = "今 0",
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
                FontSize = 11.5,
                FontWeight = FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center
            };

            pillRow.Children.Add(_pillLiveDot);
            pillRow.Children.Add(_pillSpeedText);
            pillRow.Children.Add(_pillDivider);
            pillRow.Children.Add(_pillTotalText);
            _pillRoot.Child = pillRow;
            container.Children.Add(_pillRoot);
        }

        private void ApplyThemeToOrb()
        {
            var t = _theme;
            var bodyBrush = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.35, 0.25),
                Center = new Point(0.45, 0.4),
                RadiusX = 0.72,
                RadiusY = 0.72
            };
            bodyBrush.GradientStops.Add(new GradientStop(t.ColorOf(t.OrbCoreTop), 0.0));
            bodyBrush.GradientStops.Add(new GradientStop(t.ColorOf(t.OrbCoreMid), 0.65));
            bodyBrush.GradientStops.Add(new GradientStop(t.ColorOf(t.OrbCoreEdge), 1.0));

            _orbBody.Fill = bodyBrush;
            _orbBody.Stroke = t.Brush(t.OrbBorder);
            _orbTrackRing.Stroke = t.Brush(t.OrbRingTrack);
            _orbSpeedText.Foreground = t.Brush(t.TextPrimary);
            _orbBottomPill.Background = t.Brush(t.OrbBottomPillBg);
            _orbTotalText.Foreground = t.Brush(_settings.OrbBottomMetric == "cost" ? t.AccentCost : t.AccentPrimary);

            var sec = t.ColorOf(t.AccentSecondary);
            var pri = t.ColorOf(t.AccentPrimary);
            _spinnerArc.Stroke = new LinearGradientBrush(
                Color.FromArgb(240, sec.R, sec.G, sec.B),
                Color.FromArgb(0, pri.R, pri.G, pri.B),
                0);

            _pillRoot.Background = t.Brush(t.PillBg);
            _pillRoot.BorderBrush = t.Brush(t.PillBorder);
            _pillSpeedText.Foreground = t.Brush(t.TextPrimary);
            _pillDivider.Background = t.Brush(t.CardBorder);
            _pillTotalText.Foreground = t.Brush(_settings.OrbBottomMetric == "cost" ? t.AccentCost : t.AccentPrimary);
        }

        private void BuildContextMenu()
        {
            var menu = new ContextMenu();

            var miToggleDash = new MenuItem { Header = "展开 / 收起详情面板" };
            miToggleDash.Click += (s, e) => ToggleDashboard();
            menu.Items.Add(miToggleDash);

            var miSettings = new MenuItem { Header = "⚙ 设置中心 (主题 / 单价 / 预警 / 自启)..." };
            miSettings.Click += (s, e) => OpenSettingsWindow();
            menu.Items.Add(miSettings);

            menu.Items.Add(new Separator());

            var miStyle = new MenuItem { Header = "切换外观 (圆形悬浮球 / 迷你胶囊)" };
            miStyle.Click += (s, e) => ToggleDisplayMode();
            menu.Items.Add(miStyle);

            // Theme submenu
            var miTheme = new MenuItem { Header = "配色主题" };
            foreach (var tp in ThemeManager.AllThemes)
            {
                string capturedId = tp.Id;
                var item = new MenuItem
                {
                    Header = tp.Name,
                    IsCheckable = true,
                    IsChecked = string.Equals(_settings.ThemeId, tp.Id, StringComparison.OrdinalIgnoreCase)
                };
                item.Click += (s, e) =>
                {
                    _settings.ThemeId = capturedId;
                    SettingsManager.Save(_settings);
                    ApplyAllSettings();
                };
                miTheme.Items.Add(item);
            }
            menu.Items.Add(miTheme);

            // Bottom metric submenu
            var miBottom = new MenuItem { Header = "悬浮球副指标" };
            var bItems = new[]
            {
                Tuple.Create("今日 Token 总量", "today"),
                Tuple.Create("当前会话总量", "active"),
                Tuple.Create("今日预估费用", "cost")
            };
            foreach (var bi in bItems)
            {
                string key = bi.Item2;
                var item = new MenuItem
                {
                    Header = bi.Item1,
                    IsCheckable = true,
                    IsChecked = string.Equals(_settings.OrbBottomMetric, key, StringComparison.OrdinalIgnoreCase)
                };
                item.Click += (s, e) =>
                {
                    _settings.OrbBottomMetric = key;
                    SettingsManager.Save(_settings);
                    ApplyThemeToOrb();
                    BuildContextMenu();
                    RefreshNow();
                };
                miBottom.Items.Add(item);
            }
            menu.Items.Add(miBottom);

            var miMetric = new MenuItem
            {
                Header = _settings.IncludeCacheInPrimary ? "当前口径：含缓存 (点击切为仅新增)" : "当前口径：仅新增 (点击切为含缓存)"
            };
            miMetric.Click += (s, e) =>
            {
                _settings.IncludeCacheInPrimary = !_settings.IncludeCacheInPrimary;
                _dashboard.UpdateMetricToggleVisual();
                SettingsManager.Save(_settings);
                BuildContextMenu();
                RefreshNow();
            };
            menu.Items.Add(miMetric);

            menu.Items.Add(new Separator());

            var miAutoStart = new MenuItem
            {
                Header = "Windows 开机自动启动",
                IsCheckable = true,
                IsChecked = SettingsManager.IsAutoStartEnabled()
            };
            miAutoStart.Click += (s, e) =>
            {
                SettingsManager.SetAutoStart(miAutoStart.IsChecked);
            };
            menu.Items.Add(miAutoStart);

            var miTopMost = new MenuItem { Header = "窗口始终置顶", IsCheckable = true, IsChecked = _settings.TopMost };
            miTopMost.Click += (s, e) =>
            {
                _settings.TopMost = miTopMost.IsChecked;
                Topmost = _settings.TopMost;
                SettingsManager.Save(_settings);
            };
            menu.Items.Add(miTopMost);

            var miOpacity = new MenuItem { Header = "透明度" };
            foreach (double op in new[] { 1.0, 0.95, 0.80, 0.65 })
            {
                double captured = op;
                var item = new MenuItem { Header = string.Format("{0:0}%", captured * 100) };
                item.Click += (s, e) =>
                {
                    _settings.Opacity = captured;
                    Opacity = captured;
                    SettingsManager.Save(_settings);
                };
                miOpacity.Items.Add(item);
            }
            menu.Items.Add(miOpacity);

            var miResetPos = new MenuItem { Header = "重置悬浮球位置" };
            miResetPos.Click += (s, e) => ResetPositionToCorner();
            menu.Items.Add(miResetPos);

            var miOpenLogs = new MenuItem { Header = "打开 Antigravity 日志目录" };
            miOpenLogs.Click += (s, e) =>
            {
                try
                {
                    if (System.IO.Directory.Exists(_engine.BrainDir))
                        Process.Start("explorer.exe", _engine.BrainDir);
                }
                catch { }
            };
            menu.Items.Add(miOpenLogs);

            menu.Items.Add(new Separator());

            var miHide = new MenuItem { Header = "隐藏到系统托盘" };
            miHide.Click += (s, e) =>
            {
                _dashboard.Hide();
                Hide();
            };
            menu.Items.Add(miHide);

            var miExit = new MenuItem { Header = "退出" };
            miExit.Click += (s, e) =>
            {
                if (_onExitRequested != null) _onExitRequested();
            };
            menu.Items.Add(miExit);

            ContextMenu = menu;
        }

        public void ToggleDisplayMode()
        {
            _settings.CompactPillMode = !_settings.CompactPillMode;
            ApplyDisplayMode();
            ClampToWorkArea();
            SettingsManager.Save(_settings);
            if (_dashboard.IsVisible)
                _dashboard.PositionNearOrb(this);
        }

        private void ApplyDisplayMode()
        {
            if (_settings.CompactPillMode)
            {
                _orbRoot.Visibility = Visibility.Collapsed;
                _pillRoot.Visibility = Visibility.Visible;
                Width = 194;
                Height = 52;
            }
            else
            {
                _pillRoot.Visibility = Visibility.Collapsed;
                _orbRoot.Visibility = Visibility.Visible;
                Width = 116;
                Height = 116;
            }
        }

        public void ResetPositionToCorner()
        {
            var wa = SystemParameters.WorkArea;
            Left = wa.Right - Width - 20;
            Top = wa.Bottom - Height - 32;
            _settings.Left = Left;
            _settings.Top = Top;
            _settings.HasSavedPosition = true;
            SettingsManager.Save(_settings);
            if (_dashboard.IsVisible)
                _dashboard.PositionNearOrb(this);
        }

        private void RestorePosition()
        {
            if (_settings.HasSavedPosition)
            {
                Left = _settings.Left;
                Top = _settings.Top;
                ClampToWorkArea();
            }
            else
            {
                ResetPositionToCorner();
            }
        }

        private void ClampToWorkArea()
        {
            double vLeft = SystemParameters.VirtualScreenLeft;
            double vTop = SystemParameters.VirtualScreenTop;
            double vRight = vLeft + SystemParameters.VirtualScreenWidth;
            double vBottom = vTop + SystemParameters.VirtualScreenHeight;

            if (Left < vLeft - 10) Left = vLeft + 8;
            if (Top < vTop - 10) Top = vTop + 8;
            if (Left + Width > vRight + 10) Left = vRight - Width - 8;
            if (Top + Height > vBottom + 10) Top = vBottom - Height - 8;
        }

        public void ToggleDashboard()
        {
            if (_dashboard.IsVisible) _dashboard.Hide();
            else ShowDashboard();
        }

        public void ShowDashboard()
        {
            RefreshNow();
            _dashboard.Show();
            _dashboard.PositionNearOrb(this);
            _dashboard.Activate();
        }

        public void CloseChildWindows()
        {
            try { if (_settingsWindow != null) _settingsWindow.Close(); } catch { }
            try { _dashboard.Close(); } catch { }
        }

        private void OnOrbMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            _isDragging = true;
            _dragMoved = false;
            _dragStartScreen = PointToScreen(e.GetPosition(this));
            _winStartLeft = Left;
            _winStartTop = Top;
            CaptureMouse();
        }

        private void OnOrbMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;
            Point curScreen = PointToScreen(e.GetPosition(this));
            double dx = curScreen.X - _dragStartScreen.X;
            double dy = curScreen.Y - _dragStartScreen.Y;

            var source = PresentationSource.FromVisual(this);
            double scaleX = 1.0, scaleY = 1.0;
            if (source != null && source.CompositionTarget != null)
            {
                scaleX = source.CompositionTarget.TransformToDevice.M11;
                scaleY = source.CompositionTarget.TransformToDevice.M22;
            }
            dx /= scaleX;
            dy /= scaleY;

            if (Math.Abs(dx) > 4 || Math.Abs(dy) > 4)
                _dragMoved = true;

            if (_dragMoved)
            {
                Left = _winStartLeft + dx;
                Top = _winStartTop + dy;
                if (_dashboard.IsVisible)
                    _dashboard.PositionNearOrb(this);
            }
        }

        private void OnOrbMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging) return;
            _isDragging = false;
            ReleaseMouseCapture();

            if (!_dragMoved)
            {
                ToggleDashboard();
            }
            else
            {
                var wa = SystemParameters.WorkArea;
                if (Math.Abs(Left - wa.Left) < 16) Left = wa.Left + 4;
                if (Math.Abs((Left + Width) - wa.Right) < 16) Left = wa.Right - Width - 4;
                if (Math.Abs(Top - wa.Top) < 16) Top = wa.Top + 4;
                if (Math.Abs((Top + Height) - wa.Bottom) < 16) Top = wa.Bottom - Height - 4;

                _settings.Left = Left;
                _settings.Top = Top;
                _settings.HasSavedPosition = true;
                SettingsManager.Save(_settings);

                if (_dashboard.IsVisible)
                    _dashboard.PositionNearOrb(this);
            }
        }

        public void RefreshNow()
        {
            var snap = _engine.RefreshAndBuildSnapshot(_dashboard.PinnedConversationId);
            var alert = AlertStatus.Evaluate(snap, _settings);
            UpdateOrbVisuals(snap, alert);
            if (_dashboard.IsVisible)
                _dashboard.UpdateData(snap);
            if (SnapshotUpdated != null)
                SnapshotUpdated(snap, alert);
        }

        private void UpdateOrbVisuals(TokenSnapshot snap, AlertStatus alert)
        {
            if (snap == null) return;
            var t = _theme;
            _isActive = snap.IsActive;
            _hasAlert = alert != null && alert.HasAny;

            bool incCache = _settings.IncludeCacheInPrimary;
            double rate1m = incCache ? snap.Rate1mAllPerMin : snap.Rate1mEffectivePerMin;
            double todayCostUsd = TokenFormat.EstimateCostUsd(
                snap.Today.InputTokens, snap.Today.OutputTokens, snap.Today.CacheReadTokens, _settings);

            string rateStr = TokenFormat.FormatRatePerMin(rate1m);
            _orbSpeedText.Text = rateStr;
            _pillSpeedText.Text = rateStr;

            // Secondary bottom label
            string totalStr;
            if (_settings.OrbBottomMetric == "cost")
            {
                totalStr = TokenFormat.FormatCostCompact(todayCostUsd, _settings);
                _orbTotalText.Foreground = t.Brush(t.AccentCost);
                _pillTotalText.Foreground = t.Brush(t.AccentCost);
            }
            else if (_settings.OrbBottomMetric == "active")
            {
                long v = incCache ? snap.ActiveConversation.TotalAllTokens : snap.ActiveConversation.EffectiveTokens;
                totalStr = "会 " + TokenFormat.FormatCount(v);
                _orbTotalText.Foreground = t.Brush(t.AccentPrimary);
                _pillTotalText.Foreground = t.Brush(t.AccentPrimary);
            }
            else
            {
                long v = incCache ? snap.Today.TotalAllTokens : snap.Today.EffectiveTokens;
                totalStr = "今 " + TokenFormat.FormatCount(v);
                _orbTotalText.Foreground = t.Brush(t.AccentPrimary);
                _pillTotalText.Foreground = t.Brush(t.AccentPrimary);
            }
            _orbTotalText.Text = totalStr;
            _pillTotalText.Text = totalStr;

            // Speed intensity & color
            double fullScale = incCache ? 400000.0 : 60000.0;
            _targetProgress = rate1m <= 0
                ? 0.05
                : Math.Min(1.0, Math.Max(0.12, Math.Log10(1.0 + rate1m / (fullScale * 0.05)) / Math.Log10(21.0)));

            if (_hasAlert)
            {
                _accentColor = t.ColorOf(t.AccentDanger);
                _orbStateLabel.Text = alert.ShortBadge;
                _orbStateLabel.Foreground = t.Brush(t.AccentDanger);
                _orbLiveDot.Fill = t.Brush(t.AccentDanger);
                _pillLiveDot.Fill = t.Brush(t.AccentDanger);
                _pillRoot.BorderBrush = t.Brush(t.AccentDanger);
            }
            else if (!_isActive && rate1m <= 0.1)
            {
                _accentColor = t.ColorOf(t.AccentPrimary);
                _orbStateLabel.Text = "IDLE";
                _orbStateLabel.Foreground = t.Brush(t.TextMuted);
                _orbLiveDot.Fill = t.Brush(t.TextMuted);
                _pillLiveDot.Fill = t.Brush(t.TextMuted);
                _pillRoot.BorderBrush = t.Brush(t.PillBorder);
            }
            else
            {
                if (rate1m >= fullScale * 0.85)
                    _accentColor = t.ColorOf(t.AccentDanger);
                else if (rate1m >= fullScale * 0.35)
                    _accentColor = t.ColorOf(t.AccentWarn);
                else
                    _accentColor = t.ColorOf(t.AccentActive);

                _orbStateLabel.Text = "LIVE";
                _orbStateLabel.Foreground = new SolidColorBrush(_accentColor);
                _orbLiveDot.Fill = new SolidColorBrush(_accentColor);
                _pillLiveDot.Fill = new SolidColorBrush(_accentColor);
                _pillRoot.BorderBrush = t.Brush(t.PillBorder);
            }

            _progressArc.Stroke = new SolidColorBrush(_accentColor);
            _orbGlowRing.Stroke = new SolidColorBrush(Color.FromArgb(
                (_hasAlert || _isActive) ? (byte)155 : (byte)55, _accentColor.R, _accentColor.G, _accentColor.B));
            var eff = _orbGlowRing.Effect as DropShadowEffect;
            if (eff != null) eff.Color = _accentColor;

            string alertLine = _hasAlert ? ("\n" + alert.BannerMessage) : "";
            ToolTip = string.Format(
                "Antigravity Token 监控\n实时速率: {0} ({1})\n今日新增: {2} (含缓存 {3})\n今日估算费用: {4}{5}\n点击展开详情 · 拖动调整位置 · 右键菜单",
                rateStr,
                TokenFormat.FormatRatePerSec(rate1m),
                TokenFormat.FormatCount(snap.Today.EffectiveTokens),
                TokenFormat.FormatCount(snap.Today.TotalAllTokens),
                TokenFormat.FormatCost(todayCostUsd, _settings),
                alertLine);
        }

        private void OnAnimationTick(object sender, EventArgs e)
        {
            _currentProgress += (_targetProgress - _currentProgress) * 0.14;
            double sweep = Math.Max(8.0, Math.Min(359.0, _currentProgress * 359.0));
            _progressArc.Data = BuildArcGeometry(58, 58, 43, -90, -90 + sweep);

            if (_hasAlert)
            {
                _pulsePhase += 0.24;
                double pulse = 0.45 + 0.55 * Math.Abs(Math.Sin(_pulsePhase));
                _orbGlowRing.Opacity = pulse;
                _orbLiveDot.Opacity = pulse;
                _pillLiveDot.Opacity = pulse;
                _spinAngle = (_spinAngle + 6.0) % 360.0;
                _spinnerRotate.Angle = _spinAngle;
                _spinnerArc.Opacity = 0.7;
            }
            else if (_isActive)
            {
                _orbGlowRing.Opacity = 1.0;
                _spinAngle = (_spinAngle + 4.5) % 360.0;
                _spinnerRotate.Angle = _spinAngle;
                _spinnerArc.Opacity = 0.85;

                _pulsePhase += 0.14;
                double dotOpacity = 0.55 + 0.45 * Math.Sin(_pulsePhase);
                _orbLiveDot.Opacity = dotOpacity;
                _pillLiveDot.Opacity = dotOpacity;
            }
            else
            {
                _orbGlowRing.Opacity = 0.8;
                _spinnerArc.Opacity = Math.Max(0, _spinnerArc.Opacity - 0.05);
                _orbLiveDot.Opacity = 0.7;
                _pillLiveDot.Opacity = 0.7;
            }
        }

        private static Geometry BuildArcGeometry(double cx, double cy, double r, double startDeg, double endDeg)
        {
            double startRad = startDeg * Math.PI / 180.0;
            double endRad = endDeg * Math.PI / 180.0;

            Point p1 = new Point(cx + r * Math.Cos(startRad), cy + r * Math.Sin(startRad));
            Point p2 = new Point(cx + r * Math.Cos(endRad), cy + r * Math.Sin(endRad));

            bool isLargeArc = (endDeg - startDeg) > 180.0;
            var fig = new PathFigure { StartPoint = p1, IsClosed = false };
            fig.Segments.Add(new ArcSegment(p2, new Size(r, r), 0, isLargeArc, SweepDirection.Clockwise, true));

            var geo = new PathGeometry();
            geo.Figures.Add(fig);
            return geo;
        }
    }
}
