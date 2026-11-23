using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
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
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        private readonly AppSettings _settings;
        private readonly TokenMonitorEngine _engine;
        private readonly DashboardWindow _dashboard;
        private readonly Action _onExitRequested;

        private readonly DispatcherTimer _dataTimer;
        private readonly DispatcherTimer _animTimer;
        private readonly Stopwatch _frameClock = Stopwatch.StartNew();
        private double _lastFrameSeconds;

        private ThemePalette _theme;
        private SettingsWindow _settingsWindow;

        // Orb mode UI elements (inside 164x158 unclipped jelly canvas)
        private Grid _orbRoot;
        private ScaleTransform _orbScale;
        private TranslateTransform _orbTranslate;
        private StackPanel _orbCenterStack;
        private Ellipse _orbGlowRing;
        private Ellipse _orbBody;
        private Ellipse _orbTrackRing;
        private Path _progressArc;
        private Path _spinnerArc;
        private RotateTransform _spinnerRotate;
        private Ellipse _orbLiveDot;
        private TextBlock _orbSpeedLabel;

        // 5h & Weekly direct readouts inside Orb
        private TextBlock _orb5hTag;
        private TextBlock _orb5hValue;
        private Border _orb5hTrack;
        private Border _orb5hFill;
        private TextBlock _orbWeekTag;
        private TextBlock _orbWeekValue;
        private Border _orbWeekTrack;
        private Border _orbWeekFill;

        // Pill mode UI elements (inside 304x78 unclipped jelly canvas)
        private Border _pillRoot;
        private ScaleTransform _pillScale;
        private TranslateTransform _pillTranslate;
        private StackPanel _pillRow;
        private Ellipse _pillLiveDot;
        private TextBlock _pillSpeedText;
        private Border _pillDiv1;
        private TextBlock _pill5hTag;
        private TextBlock _pill5hValue;
        private Border _pill5hTrack;
        private Border _pill5hFill;
        private Border _pillDiv2;
        private TextBlock _pillWeekTag;
        private TextBlock _pillWeekValue;
        private Border _pillWeekTrack;
        private Border _pillWeekFill;

        // Right-edge collapsed vertical strip (flush right inside same window)
        private Grid _dockStripRoot;
        private ScaleTransform _stripScale;
        private TranslateTransform _stripTranslate;
        private Border _stripBody;
        private Border _stripAccentEdge;
        private Ellipse _stripLiveDot;
        private Border _stripBarTrack;
        private Border _stripBarFill;
        private TextBlock _stripChevron;

        // Interaction state
        private bool _isDragging;
        private bool _dragMoved;
        private Point _dragStartScreen;
        private double _winStartLeft;
        private double _winStartTop;

        // === Q-ELASTIC JELLY & MAGNETIC SNAP PHYSICS ===
        // 1) _morphT: 0.0 = expanded orb/pill, 1.0 = docked right-edge strip (allows lively overshoot!)
        private bool _isRightDocked;
        private double _morphT;
        private double _morphVel;
        private double _morphTarget;

        // 2) _jelly: volume-preserving squash & stretch oscillator (>0 = squashed horizontally / stretched vertically)
        private double _jelly;
        private double _jellyVel;

        // 3) _stripJelly: dedicated Q-elastic impact wobble when the vertical strip snaps into the wall
        private double _stripJelly;
        private double _stripJellyVel;
        private bool _stripImpactFired;

        // 4) Magnetic pull-in glide when releasing orb near the right edge
        private bool _isMagneticSnapping;
        private double _magneticTargetLeft;
        private double _magneticVelX;

        private DateTime _mouseAwaySinceUtc = DateTime.MinValue;
        private DateTime _suppressCollapseUntilUtc = DateTime.MinValue;

        // Ring & status animation state
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
            ApplyMorphVisuals();

            MouseLeftButtonDown += OnOrbMouseDown;
            MouseMove += OnOrbMouseMove;
            MouseLeftButtonUp += OnOrbMouseUp;
            MouseEnter += OnOrbMouseEnter;

            _dataTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _dataTimer.Tick += (s, e) => RefreshNow();
            _dataTimer.Start();

            // 60fps real delta-time physics loop
            _lastFrameSeconds = _frameClock.Elapsed.TotalSeconds;
            _animTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(15)
            };
            _animTimer.Tick += OnAnimationTick;
            _animTimer.Start();

            Loaded += (s, e) => RefreshNow();
        }

        public void OpenSettingsWindow()
        {
            ExpandFromRightEdge();
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
            if (!_settings.AutoDockRightEdge && _isRightDocked)
            {
                _isRightDocked = false;
                _settings.IsDockedRight = false;
                _morphTarget = 0.0;
                _jellyVel -= 4.5;
            }
            ApplyThemeToOrb();
            _dashboard.RebuildUi();
            BuildContextMenu();
            RefreshNow();
        }

        private void BuildUi()
        {
            var container = new Grid { Background = Brushes.Transparent };
            Content = container;

            // === 1. CIRCULAR ORB VIEW (right-aligned inside 164x158 canvas so Q-bounce never clips!) ===
            _orbScale = new ScaleTransform(1.0, 1.0);
            _orbTranslate = new TranslateTransform(0.0, 0.0);
            var orbTg = new TransformGroup();
            orbTg.Children.Add(_orbScale);
            orbTg.Children.Add(_orbTranslate);

            _orbRoot = new Grid
            {
                Width = 134,
                Height = 134,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                RenderTransformOrigin = new Point(0.92, 0.5),
                RenderTransform = orbTg
            };

            _orbGlowRing = new Ellipse
            {
                Width = 116,
                Height = 116,
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
                Width = 112,
                Height = 112,
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
                Width = 78,
                Height = 32,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 14, 0, 0),
                IsHitTestVisible = false,
                Fill = new LinearGradientBrush(
                    Color.FromArgb(44, 255, 255, 255),
                    Color.FromArgb(0, 255, 255, 255),
                    90)
            };
            _orbRoot.Children.Add(highlight);

            var ringCanvas = new Canvas { Width = 134, Height = 134, IsHitTestVisible = false };

            _orbTrackRing = new Ellipse
            {
                Width = 104,
                Height = 104,
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

            _spinnerRotate = new RotateTransform(0, 67, 67);
            _spinnerArc = new Path
            {
                StrokeThickness = 2.4,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                RenderTransform = _spinnerRotate,
                Data = BuildArcGeometry(67, 67, 52, 0, 75),
                Opacity = 0.0
            };
            ringCanvas.Children.Add(_spinnerArc);
            _orbRoot.Children.Add(ringCanvas);

            _orbCenterStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsHitTestVisible = false
            };

            var statusRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 3)
            };
            _orbLiveDot = new Ellipse
            {
                Width = 5.5,
                Height = 5.5,
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _orbSpeedLabel = new TextBlock
            {
                Text = "0/m",
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"),
                FontSize = 9.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            statusRow.Children.Add(_orbLiveDot);
            statusRow.Children.Add(_orbSpeedLabel);
            _orbCenterStack.Children.Add(statusRow);

            var row5h = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _orb5hTag = new TextBlock
            {
                Text = "5h",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 3, 2)
            };
            _orb5hValue = new TextBlock
            {
                Text = "0",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 15.5,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            row5h.Children.Add(_orb5hTag);
            row5h.Children.Add(_orb5hValue);
            _orbCenterStack.Children.Add(row5h);

            _orb5hTrack = new Border
            {
                Width = 70,
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 2, 0, 5),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _orb5hFill = new Border
            {
                Width = 4,
                Height = 4,
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _orb5hTrack.Child = _orb5hFill;
            _orbCenterStack.Children.Add(_orb5hTrack);

            var rowWeek = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _orbWeekTag = new TextBlock
            {
                Text = "周",
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 3, 1)
            };
            _orbWeekValue = new TextBlock
            {
                Text = "0",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            rowWeek.Children.Add(_orbWeekTag);
            rowWeek.Children.Add(_orbWeekValue);
            _orbCenterStack.Children.Add(rowWeek);

            _orbWeekTrack = new Border
            {
                Width = 56,
                Height = 3.5,
                CornerRadius = new CornerRadius(1.75),
                Margin = new Thickness(0, 2, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _orbWeekFill = new Border
            {
                Width = 4,
                Height = 3.5,
                CornerRadius = new CornerRadius(1.75),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _orbWeekTrack.Child = _orbWeekFill;
            _orbCenterStack.Children.Add(_orbWeekTrack);

            _orbRoot.Children.Add(_orbCenterStack);
            container.Children.Add(_orbRoot);

            // === 2. MINI PILL VIEW (right-aligned inside 304x78 canvas) ===
            _pillScale = new ScaleTransform(1.0, 1.0);
            _pillTranslate = new TranslateTransform(0.0, 0.0);
            var pillTg = new TransformGroup();
            pillTg.Children.Add(_pillScale);
            pillTg.Children.Add(_pillTranslate);

            _pillRoot = new Border
            {
                Width = 262,
                Height = 42,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                CornerRadius = new CornerRadius(21),
                BorderThickness = new Thickness(1.2),
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed,
                RenderTransformOrigin = new Point(0.95, 0.5),
                RenderTransform = pillTg,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 12,
                    ShadowDepth = 2,
                    Opacity = 0.5
                }
            };

            _pillRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            _pillLiveDot = new Ellipse
            {
                Width = 6.5,
                Height = 6.5,
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _pillSpeedText = new TextBlock
            {
                Text = "0/m",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };

            _pillDiv1 = new Border
            {
                Width = 1,
                Height = 18,
                Margin = new Thickness(8, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var p5Stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var p5Row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            _pill5hTag = new TextBlock
            {
                Text = "5h ",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            _pill5hValue = new TextBlock
            {
                Text = "0",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            p5Row.Children.Add(_pill5hTag);
            p5Row.Children.Add(_pill5hValue);
            _pill5hTrack = new Border
            {
                Width = 58,
                Height = 3,
                CornerRadius = new CornerRadius(1.5),
                Margin = new Thickness(0, 2, 0, 0)
            };
            _pill5hFill = new Border
            {
                Width = 4,
                Height = 3,
                CornerRadius = new CornerRadius(1.5),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _pill5hTrack.Child = _pill5hFill;
            p5Stack.Children.Add(p5Row);
            p5Stack.Children.Add(_pill5hTrack);

            _pillDiv2 = new Border
            {
                Width = 1,
                Height = 18,
                Margin = new Thickness(8, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var pwStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var pwRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            _pillWeekTag = new TextBlock
            {
                Text = "周 ",
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
                FontSize = 9.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            _pillWeekValue = new TextBlock
            {
                Text = "0",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            pwRow.Children.Add(_pillWeekTag);
            pwRow.Children.Add(_pillWeekValue);
            _pillWeekTrack = new Border
            {
                Width = 58,
                Height = 3,
                CornerRadius = new CornerRadius(1.5),
                Margin = new Thickness(0, 2, 0, 0)
            };
            _pillWeekFill = new Border
            {
                Width = 4,
                Height = 3,
                CornerRadius = new CornerRadius(1.5),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _pillWeekTrack.Child = _pillWeekFill;
            pwStack.Children.Add(pwRow);
            pwStack.Children.Add(_pillWeekTrack);

            _pillRow.Children.Add(_pillLiveDot);
            _pillRow.Children.Add(_pillSpeedText);
            _pillRow.Children.Add(_pillDiv1);
            _pillRow.Children.Add(p5Stack);
            _pillRow.Children.Add(_pillDiv2);
            _pillRow.Children.Add(pwStack);
            _pillRoot.Child = _pillRow;
            container.Children.Add(_pillRoot);

            // === 3. RIGHT-EDGE Q-ELASTIC VERTICAL STRIP ===
            _stripScale = new ScaleTransform(1.0, 1.0);
            _stripTranslate = new TranslateTransform(0.0, 0.0);
            var stripTg = new TransformGroup();
            stripTg.Children.Add(_stripScale);
            stripTg.Children.Add(_stripTranslate);

            _dockStripRoot = new Grid
            {
                Width = 26,
                Height = 90,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Opacity = 0.0,
                IsHitTestVisible = false,
                RenderTransformOrigin = new Point(1.0, 0.5),
                RenderTransform = stripTg
            };

            _stripBody = new Border
            {
                Width = 17,
                Height = 78,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(10, 0, 0, 10),
                BorderThickness = new Thickness(1.2, 1.2, 0, 1.2),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 10,
                    ShadowDepth = 0,
                    Opacity = 0.5
                }
            };

            var stripInner = new Grid();
            _stripAccentEdge = new Border
            {
                Width = 2.8,
                Height = 42,
                CornerRadius = new CornerRadius(1.4),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(1.5, 0, 0, 0),
                Opacity = 0.88
            };
            stripInner.Children.Add(_stripAccentEdge);

            var stripStack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2.5, 0, 0, 0)
            };

            _stripLiveDot = new Ellipse
            {
                Width = 5,
                Height = 5,
                Margin = new Thickness(0, 0, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            stripStack.Children.Add(_stripLiveDot);

            _stripBarTrack = new Border
            {
                Width = 4,
                Height = 32,
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _stripBarFill = new Border
            {
                Width = 4,
                Height = 6,
                CornerRadius = new CornerRadius(2),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            _stripBarTrack.Child = _stripBarFill;
            stripStack.Children.Add(_stripBarTrack);

            _stripChevron = new TextBlock
            {
                Text = "‹",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 1, 0, 0)
            };
            stripStack.Children.Add(_stripChevron);

            stripInner.Children.Add(stripStack);
            _stripBody.Child = stripInner;
            _dockStripRoot.Children.Add(_stripBody);
            container.Children.Add(_dockStripRoot);
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

            _orb5hTag.Foreground = t.Brush(t.AccentPrimary);
            _orb5hValue.Foreground = t.Brush(t.TextPrimary);
            _orb5hTrack.Background = t.Brush(t.OrbRingTrack);

            _orbWeekTag.Foreground = t.Brush(t.AccentSecondary);
            _orbWeekValue.Foreground = t.Brush(t.TextSecondary);
            _orbWeekTrack.Background = t.Brush(t.OrbRingTrack);

            var sec = t.ColorOf(t.AccentSecondary);
            var pri = t.ColorOf(t.AccentPrimary);
            _spinnerArc.Stroke = new LinearGradientBrush(
                Color.FromArgb(240, sec.R, sec.G, sec.B),
                Color.FromArgb(0, pri.R, pri.G, pri.B),
                0);

            _pillRoot.Background = t.Brush(t.PillBg);
            _pillRoot.BorderBrush = t.Brush(t.PillBorder);
            _pillSpeedText.Foreground = t.Brush(t.TextPrimary);
            _pillDiv1.Background = t.Brush(t.CardBorder);
            _pillDiv2.Background = t.Brush(t.CardBorder);

            _pill5hTag.Foreground = t.Brush(t.AccentPrimary);
            _pill5hValue.Foreground = t.Brush(t.TextPrimary);
            _pill5hTrack.Background = t.Brush(t.OrbRingTrack);

            _pillWeekTag.Foreground = t.Brush(t.AccentSecondary);
            _pillWeekValue.Foreground = t.Brush(t.TextSecondary);
            _pillWeekTrack.Background = t.Brush(t.OrbRingTrack);

            _stripBody.Background = t.Brush(t.PillBg);
            _stripBody.BorderBrush = t.Brush(t.PillBorder);
            _stripBarTrack.Background = t.Brush(t.OrbRingTrack);
            _stripChevron.Foreground = t.Brush(t.TextMuted);
        }

        private void BuildContextMenu()
        {
            var menu = new ContextMenu();

            var miToggleDash = new MenuItem { Header = "展开 / 收起详情面板" };
            miToggleDash.Click += (s, e) => ToggleDashboard();
            menu.Items.Add(miToggleDash);

            var miSettings = new MenuItem { Header = "⚙ 设置中心 (主题 / 配额 / 单价 / 预警 / 自启)..." };
            miSettings.Click += (s, e) => OpenSettingsWindow();
            menu.Items.Add(miSettings);

            menu.Items.Add(new Separator());

            var miStyle = new MenuItem { Header = "切换外观 (圆形悬浮球 / 迷你胶囊)" };
            miStyle.Click += (s, e) => ToggleDisplayMode();
            menu.Items.Add(miStyle);

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

            var miAutoDock = new MenuItem
            {
                Header = "靠右边缘自动缩成细条（鼠标靠近拉出）",
                IsCheckable = true,
                IsChecked = _settings.AutoDockRightEdge
            };
            miAutoDock.Click += (s, e) =>
            {
                _settings.AutoDockRightEdge = miAutoDock.IsChecked;
                if (!_settings.AutoDockRightEdge && _isRightDocked)
                {
                    _isRightDocked = false;
                    _settings.IsDockedRight = false;
                    _morphTarget = 0.0;
                    _jellyVel -= 4.5;
                }
                SettingsManager.Save(_settings);
            };
            menu.Items.Add(miAutoDock);

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
            foreach (double op in new[] { 1.0, 0.95, 0.90, 0.80, 0.65 })
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
            double centerY = Top + Height / 2.0;
            _settings.CompactPillMode = !_settings.CompactPillMode;
            ApplyDisplayMode();

            var wa = GetCurrentWorkAreaDip();
            if (_isRightDocked)
            {
                Left = wa.Right - Width;
                Top = ClampVertical(centerY - Height / 2.0, Height, wa);
            }
            else
            {
                Top = ClampVertical(centerY - Height / 2.0, Height, wa);
                ClampToWorkArea();
            }

            _jellyVel += 4.2;
            ApplyMorphVisuals();
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
                Width = 304;
                Height = 78;
                _dockStripRoot.Height = 64;
                _stripBody.Height = 48;
                _stripAccentEdge.Height = 24;
                _stripBarTrack.Height = 16;
            }
            else
            {
                _pillRoot.Visibility = Visibility.Collapsed;
                _orbRoot.Visibility = Visibility.Visible;
                Width = 164;
                Height = 158;
                _dockStripRoot.Height = 92;
                _stripBody.Height = 78;
                _stripAccentEdge.Height = 42;
                _stripBarTrack.Height = 32;
            }
        }

        private Rect GetCurrentWorkAreaDip()
        {
            try
            {
                double scaleX = 1.0, scaleY = 1.0;
                var source = PresentationSource.FromVisual(this);
                if (source != null && source.CompositionTarget != null)
                {
                    scaleX = source.CompositionTarget.TransformToDevice.M11;
                    scaleY = source.CompositionTarget.TransformToDevice.M22;
                }
                double cx = (double.IsNaN(Left) ? 100 : Left + Width / 2.0) * scaleX;
                double cy = (double.IsNaN(Top) ? 100 : Top + Height / 2.0) * scaleY;
                var scr = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)cx, (int)cy));
                var wa = scr.WorkingArea;
                return new Rect(wa.Left / scaleX, wa.Top / scaleY, wa.Width / scaleX, wa.Height / scaleY);
            }
            catch
            {
                return SystemParameters.WorkArea;
            }
        }

        private bool TryGetCursorDip(out Point dipPoint)
        {
            dipPoint = new Point(0, 0);
            POINT pt;
            if (!GetCursorPos(out pt)) return false;

            double scaleX = 1.0, scaleY = 1.0;
            var source = PresentationSource.FromVisual(this);
            if (source != null && source.CompositionTarget != null)
            {
                scaleX = source.CompositionTarget.TransformToDevice.M11;
                scaleY = source.CompositionTarget.TransformToDevice.M22;
            }
            dipPoint = new Point(pt.X / scaleX, pt.Y / scaleY);
            return true;
        }

        private static double ClampVertical(double top, double height, Rect wa)
        {
            if (top < wa.Top) return wa.Top;
            if (top + height > wa.Bottom) return wa.Bottom - height;
            return top;
        }

        private static double Clamp01(double v)
        {
            if (v < 0.0) return 0.0;
            if (v > 1.0) return 1.0;
            return v;
        }

        private void ExpandFromRightEdge()
        {
            bool wasCollapsed = _morphT > 0.35 || _morphTarget > 0.5;
            _morphTarget = 0.0;
            _stripImpactFired = false;
            _mouseAwaySinceUtc = DateTime.MinValue;
            _suppressCollapseUntilUtc = DateTime.UtcNow.AddMilliseconds(480);

            if (wasCollapsed)
            {
                // Juicy pop-out impulse: stretches horizontally as it springs out of the screen bezel!
                _morphVel -= 2.2;
                _jellyVel -= 5.8;
            }
        }

        public void ResetPositionToCorner()
        {
            _isRightDocked = false;
            _isMagneticSnapping = false;
            _settings.IsDockedRight = false;
            _morphTarget = 0.0;
            _morphT = 0.0;
            _morphVel = 0.0;
            _jellyVel += 4.5;
            ApplyMorphVisuals();

            var wa = SystemParameters.WorkArea;
            Left = wa.Right - Width - 36;
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

                var wa = GetCurrentWorkAreaDip();
                if (_settings.AutoDockRightEdge && (_settings.IsDockedRight || (Left + Width) >= wa.Right - 36))
                {
                    _isRightDocked = true;
                    Left = wa.Right - Width;
                    _morphT = 0.0;
                    _morphTarget = 0.0;
                    _suppressCollapseUntilUtc = DateTime.UtcNow.AddSeconds(1.2);
                }
            }
            else
            {
                ResetPositionToCorner();
            }
        }

        public void EnsureVisibleOnScreen()
        {
            ExpandFromRightEdge();
            _suppressCollapseUntilUtc = DateTime.UtcNow.AddSeconds(2.0);
            ClampToWorkArea();
            if (!IsVisible) Show();
            WindowState = WindowState.Normal;
            Topmost = true;
            Topmost = _settings.TopMost;
            Activate();
        }

        private void ClampToWorkArea()
        {
            double vLeft = SystemParameters.VirtualScreenLeft;
            double vTop = SystemParameters.VirtualScreenTop;
            double vRight = vLeft + SystemParameters.VirtualScreenWidth;
            double vBottom = vTop + SystemParameters.VirtualScreenHeight;

            if (Left < vLeft - 24) Left = vLeft - 12;
            if (Top < vTop - 10) Top = vTop;
            if (Left + Width > vRight + 6) Left = vRight - Width;
            if (Top + Height > vBottom + 10) Top = vBottom - Height;
        }

        public void ToggleDashboard()
        {
            _jellyVel += 3.6; // tactile jelly tap feedback!
            if (_dashboard.IsVisible)
                _dashboard.Hide();
            else
                ShowDashboard();
        }

        public void ShowDashboard()
        {
            ExpandFromRightEdge();
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

        private void OnOrbMouseEnter(object sender, MouseEventArgs e)
        {
            if (_isRightDocked && _morphTarget > 0.01)
                ExpandFromRightEdge();
        }

        private void OnOrbMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            if (_morphT > 0.55)
            {
                ExpandFromRightEdge();
                return;
            }

            _isMagneticSnapping = false;
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
                var wa = GetCurrentWorkAreaDip();
                if (Math.Abs(Left - wa.Left) < 16) Left = wa.Left;
                if (Math.Abs(Top - wa.Top) < 16) Top = wa.Top;
                if (Math.Abs((Top + Height) - wa.Bottom) < 16) Top = wa.Bottom - Height;

                // Generous magnetic capture zone near the right edge (within 48px!)
                if (_settings.AutoDockRightEdge && (Left + Width) >= wa.Right - 48)
                {
                    _isRightDocked = true;
                    _settings.IsDockedRight = true;
                    _morphTarget = 0.0;
                    _stripImpactFired = false;

                    // Trigger magnetic pull-in glide + juicy Q-elastic wall-kiss squash!
                    _magneticTargetLeft = wa.Right - Width;
                    if (Math.Abs(Left - _magneticTargetLeft) > 2.0)
                    {
                        _isMagneticSnapping = true;
                        _magneticVelX = 280.0; // magnetic pull boost!
                    }
                    else
                    {
                        Left = _magneticTargetLeft;
                        _isMagneticSnapping = false;
                        // Immediate juicy jelly impact against the right wall!
                        _jellyVel += 7.2;
                    }

                    _mouseAwaySinceUtc = DateTime.UtcNow;
                    _suppressCollapseUntilUtc = DateTime.UtcNow.AddMilliseconds(420);
                }
                else
                {
                    _isMagneticSnapping = false;
                    _isRightDocked = false;
                    _settings.IsDockedRight = false;
                    _morphTarget = 0.0;
                    // Subtle settle wobble when dropped freely on desktop
                    _jellyVel += 2.6;
                }

                _settings.Left = _isRightDocked ? (wa.Right - Width) : Left;
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

        private Brush PickQuotaBarBrush(double ratio, string normalHex)
        {
            if (ratio >= 0.95) return _theme.Brush(_theme.AccentDanger);
            if (ratio >= 0.75) return _theme.Brush(_theme.AccentWarn);
            return _theme.Brush(normalHex);
        }

        private void UpdateOrbVisuals(TokenSnapshot snap, AlertStatus alert)
        {
            if (snap == null) return;
            var t = _theme;
            _isActive = snap.IsActive;
            _hasAlert = alert != null && alert.HasAny;

            bool incCache = _settings.IncludeCacheInPrimary;
            double rate1m = incCache ? snap.Rate1mAllPerMin : snap.Rate1mEffectivePerMin;
            long val5h = incCache ? snap.Last5Hours.TotalAllTokens : snap.Last5Hours.EffectiveTokens;
            long valWeek = incCache ? snap.Last7Days.TotalAllTokens : snap.Last7Days.EffectiveTokens;

            long cap5h = _settings.Quota5hTokens > 0 ? _settings.Quota5hTokens : 22000000L * 1000L;
            long capWeek = _settings.QuotaWeekTokens > 0 ? _settings.QuotaWeekTokens : 10000L * 1000L;
            if (incCache)
            {
                cap5h *= 8L;
                capWeek *= 8L;
            }

            double ratio5h = Math.Min(1.0, (double)val5h / cap5h);
            double ratioWeek = Math.Min(1.0, (double)valWeek / capWeek);

            string rateStr = TokenFormat.FormatRatePerMin(rate1m);
            string str5h = TokenFormat.FormatCount(val5h);
            string strWeek = TokenFormat.FormatCount(valWeek);

            _orb5hValue.Text = str5h;
            _orbWeekValue.Text = strWeek;
            _pill5hValue.Text = str5h;
            _pillWeekValue.Text = strWeek;

            var b5h = PickQuotaBarBrush(ratio5h, t.AccentPrimary);
            var bWeek = PickQuotaBarBrush(ratioWeek, t.AccentSecondary);

            _orb5hFill.Background = b5h;
            _orb5hFill.Width = val5h > 0 ? Math.Max(4.0, 70.0 * ratio5h) : 0.0;

            _orbWeekFill.Background = bWeek;
            _orbWeekFill.Width = valWeek > 0 ? Math.Max(4.0, 56.0 * ratioWeek) : 0.0;

            _pill5hFill.Background = b5h;
            _pill5hFill.Width = val5h > 0 ? Math.Max(4.0, 58.0 * ratio5h) : 0.0;

            _pillWeekFill.Background = bWeek;
            _pillWeekFill.Width = valWeek > 0 ? Math.Max(4.0, 58.0 * ratioWeek) : 0.0;

            double stripTrackH = _stripBarTrack.Height;
            _stripBarFill.Background = b5h;
            _stripBarFill.Height = val5h > 0 ? Math.Max(4.0, stripTrackH * ratio5h) : 3.0;

            _pillSpeedText.Text = rateStr;
            _targetProgress = Math.Max(0.05, ratio5h);

            if (_hasAlert)
            {
                _accentColor = t.ColorOf(t.AccentDanger);
                _orbSpeedLabel.Text = alert.ShortBadge;
                _orbSpeedLabel.Foreground = t.Brush(t.AccentDanger);
                _orbLiveDot.Fill = t.Brush(t.AccentDanger);
                _pillLiveDot.Fill = t.Brush(t.AccentDanger);
                _stripLiveDot.Fill = t.Brush(t.AccentDanger);
                _pillRoot.BorderBrush = t.Brush(t.AccentDanger);
                _stripBody.BorderBrush = t.Brush(t.AccentDanger);
            }
            else if (!_isActive && rate1m <= 0.1)
            {
                _accentColor = ratio5h >= 0.95 ? t.ColorOf(t.AccentDanger)
                    : (ratio5h >= 0.75 ? t.ColorOf(t.AccentWarn) : t.ColorOf(t.AccentPrimary));
                _orbSpeedLabel.Text = "IDLE";
                _orbSpeedLabel.Foreground = t.Brush(t.TextMuted);
                _orbLiveDot.Fill = t.Brush(t.TextMuted);
                _pillLiveDot.Fill = t.Brush(t.TextMuted);
                _stripLiveDot.Fill = t.Brush(t.TextMuted);
                _pillRoot.BorderBrush = t.Brush(t.PillBorder);
                _stripBody.BorderBrush = t.Brush(t.PillBorder);
            }
            else
            {
                double fullScale = incCache ? 400000.0 : 60000.0;
                if (rate1m >= fullScale * 0.85 || ratio5h >= 0.95)
                    _accentColor = t.ColorOf(t.AccentDanger);
                else if (rate1m >= fullScale * 0.35 || ratio5h >= 0.75)
                    _accentColor = t.ColorOf(t.AccentWarn);
                else
                    _accentColor = t.ColorOf(t.AccentActive);

                _orbSpeedLabel.Text = rateStr;
                _orbSpeedLabel.Foreground = new SolidColorBrush(_accentColor);
                _orbLiveDot.Fill = new SolidColorBrush(_accentColor);
                _pillLiveDot.Fill = new SolidColorBrush(_accentColor);
                _stripLiveDot.Fill = new SolidColorBrush(_accentColor);
                _pillRoot.BorderBrush = t.Brush(t.PillBorder);
                _stripBody.BorderBrush = t.Brush(t.PillBorder);
            }

            var accentBrush = new SolidColorBrush(_accentColor);
            _progressArc.Stroke = accentBrush;
            _stripAccentEdge.Background = accentBrush;
            _orbGlowRing.Stroke = new SolidColorBrush(Color.FromArgb(
                (_hasAlert || _isActive) ? (byte)155 : (byte)55, _accentColor.R, _accentColor.G, _accentColor.B));
            var eff = _orbGlowRing.Effect as DropShadowEffect;
            if (eff != null) eff.Color = _accentColor;

            double cost5hUsd = TokenFormat.EstimateCostUsd(
                snap.Last5Hours.InputTokens, snap.Last5Hours.OutputTokens, snap.Last5Hours.CacheReadTokens, _settings);
            double costWeekUsd = TokenFormat.EstimateCostUsd(
                snap.Last7Days.InputTokens, snap.Last7Days.OutputTokens, snap.Last7Days.CacheReadTokens, _settings);
            string alertLine = _hasAlert ? ("\n" + alert.BannerMessage) : "";

            ToolTip = string.Format(
                "Antigravity Token 监控 ({0})\n" +
                "实时速率: {1} ({2})\n" +
                "近 5 小时: {3} ({4:0}% · 约 {5})\n" +
                "近 7 天(周): {6} ({7:0}% · 约 {8})\n" +
                "今日新增: {9} (含缓存 {10}){11}\n" +
                "单击展开详情 · 拖动移位 · 靠右 Q 弹磁吸",
                incCache ? "含缓存口径" : "新增口径",
                rateStr,
                TokenFormat.FormatRatePerSec(rate1m),
                str5h, ratio5h * 100.0, TokenFormat.FormatCost(cost5hUsd, _settings),
                strWeek, ratioWeek * 100.0, TokenFormat.FormatCost(costWeekUsd, _settings),
                TokenFormat.FormatCount(snap.Today.EffectiveTokens),
                TokenFormat.FormatCount(snap.Today.TotalAllTokens),
                alertLine);
        }

        private void StepEdgeDockPhysics(double dt)
        {
            if (!IsVisible) return;

            bool needsVisualUpdate = false;

            // 1. Magnetic snap pull-in glide with wall-impact jelly bounce!
            if (_isMagneticSnapping && !_isDragging)
            {
                double dx = _magneticTargetLeft - Left;
                _magneticVelX += dx * 280.0 * dt;
                _magneticVelX *= Math.Max(0.0, 1.0 - 11.0 * dt);
                Left += _magneticVelX * dt;

                // Slight stretch toward the magnet while pulling in
                _jellyVel -= 1.2 * dt;

                if (Left >= _magneticTargetLeft - 1.5)
                {
                    Left = _magneticTargetLeft;
                    _isMagneticSnapping = false;
                    _magneticVelX = 0.0;
                    // BOING!! Juicy wall-kiss squash & wobble on impact!
                    _jellyVel += 8.2;
                }
            }

            // 2. Determine dock expand/collapse target
            if (!_settings.AutoDockRightEdge || !_isRightDocked || _isDragging)
            {
                _morphTarget = 0.0;
            }
            else if (!_isMagneticSnapping)
            {
                var wa = GetCurrentWorkAreaDip();
                if (Math.Abs(Left - (wa.Right - Width)) > 0.5)
                    Left = wa.Right - Width;

                Point cur;
                bool hasCur = TryGetCursorDip(out cur);

                bool busy = _dashboard.IsVisible
                    || (_settingsWindow != null && _settingsWindow.IsVisible)
                    || (ContextMenu != null && ContextMenu.IsOpen);

                bool isCollapsedOrClosing = _morphTarget > 0.5;
                if (isCollapsedOrClosing)
                {
                    if (busy)
                    {
                        ExpandFromRightEdge();
                    }
                    else if (hasCur)
                    {
                        double stripHalfH = _dockStripRoot.Height / 2.0;
                        double centerY = Top + Height / 2.0;
                        bool nearStrip = cur.X >= wa.Right - 32.0
                            && cur.Y >= (centerY - stripHalfH - 18.0)
                            && cur.Y <= (centerY + stripHalfH + 18.0);
                        if (nearStrip)
                        {
                            ExpandFromRightEdge();
                        }
                    }
                }
                else
                {
                    bool cursorNearExpanded = false;
                    if (hasCur)
                    {
                        double orbLeftEdge = Left + (Width - (_settings.CompactPillMode ? 270.0 : 140.0));
                        cursorNearExpanded = cur.X >= orbLeftEdge - 16.0
                            && cur.X <= wa.Right + 6.0
                            && cur.Y >= Top - 12.0
                            && cur.Y <= Top + Height + 12.0;
                    }

                    if (busy || cursorNearExpanded)
                    {
                        _mouseAwaySinceUtc = DateTime.MinValue;
                    }
                    else if (DateTime.UtcNow >= _suppressCollapseUntilUtc)
                    {
                        if (_mouseAwaySinceUtc == DateTime.MinValue)
                        {
                            _mouseAwaySinceUtc = DateTime.UtcNow;
                        }
                        else if ((DateTime.UtcNow - _mouseAwaySinceUtc).TotalMilliseconds >= 460)
                        {
                            _morphTarget = 1.0;
                            _stripImpactFired = false;
                            // Anticipation pull-back before sucking into the wall!
                            _morphVel -= 1.5;
                            _jellyVel -= 3.2;
                        }
                    }
                }
            }

            // 3. Lively Q-elastic Morph Spring (underdamped in BOTH directions!)
            if (Math.Abs(_morphT - _morphTarget) > 0.0005 || Math.Abs(_morphVel) > 0.002)
            {
                bool expanding = _morphTarget < 0.5;
                // Underdamped spring: zeta ~ 0.36 on pop-out, ~ 0.48 on suck-in!
                double stiffness = expanding ? 300.0 : 210.0;
                double damping = expanding ? 12.6 : 14.2;

                double force = (_morphTarget - _morphT) * stiffness;
                _morphVel += (force - _morphVel * damping) * dt;
                _morphT += _morphVel * dt;

                // Allow juicy negative overshoot when popping out (-0.26..1.12)
                if (_morphT < -0.26) { _morphT = -0.26; _morphVel *= -0.25; }
                if (_morphT > 1.12) { _morphT = 1.12; _morphVel *= -0.25; }

                // Trigger strip Q-elastic "bloop" impact the moment orb finishes docking into the right wall!
                if (!expanding && !_stripImpactFired && _morphT >= 0.88)
                {
                    _stripImpactFired = true;
                    _stripJellyVel += 8.6;
                }

                needsVisualUpdate = true;
            }
            else if (_morphT != _morphTarget)
            {
                _morphT = _morphTarget;
                _morphVel = 0.0;
                needsVisualUpdate = true;
            }

            // 4. Independent Orb Jelly Squash & Stretch Spring (stiffness=340, damping=11.5 => super Q-弹!)
            if (Math.Abs(_jelly) > 0.0005 || Math.Abs(_jellyVel) > 0.002)
            {
                double jForce = -_jelly * 340.0;
                _jellyVel += (jForce - _jellyVel * 11.5) * dt;
                _jelly += _jellyVel * dt;
                if (_jelly > 0.32) _jelly = 0.32;
                if (_jelly < -0.32) _jelly = -0.32;
                needsVisualUpdate = true;
            }
            else if (_jelly != 0.0)
            {
                _jelly = 0.0;
                _jellyVel = 0.0;
                needsVisualUpdate = true;
            }

            // 5. Dedicated Strip Impact Jelly Spring (gives the right-edge bar a soft magnetic "bloop" wobble!)
            if (Math.Abs(_stripJelly) > 0.0005 || Math.Abs(_stripJellyVel) > 0.002)
            {
                double sjForce = -_stripJelly * 310.0;
                _stripJellyVel += (sjForce - _stripJellyVel * 10.5) * dt;
                _stripJelly += _stripJellyVel * dt;
                if (_stripJelly > 0.35) _stripJelly = 0.35;
                if (_stripJelly < -0.35) _stripJelly = -0.35;
                needsVisualUpdate = true;
            }
            else if (_stripJelly != 0.0)
            {
                _stripJelly = 0.0;
                _stripJellyVel = 0.0;
                needsVisualUpdate = true;
            }

            if (needsVisualUpdate)
            {
                ApplyMorphVisuals();
            }
        }

        private void ApplyMorphVisuals()
        {
            double rawT = _morphT;
            double t = Clamp01(rawT);

            // Elastic pop-out overshoot when rawT < 0 (spring shoots left & stretches horizontally!)
            double popOut = rawT < 0.0 ? -rawT : 0.0;
            // Smooth S-curve for crossfade
            double smoothT = t * t * (3.0 - 2.0 * t);

            // Volume-preserving jelly squash & stretch:
            // _jelly > 0 => squashed horizontally against wall, stretched vertically!
            // _jelly < 0 => stretched horizontally outward, slightly flattened vertically!
            double jSx = 1.0 - _jelly * 0.65;
            double jSy = 1.0 + _jelly * 0.58;

            double dockRestOffsetX = _isRightDocked ? -5.0 : 0.0;

            // 1. Orb liquid morph + Q-elastic spring + volume-preserving jelly wobble
            double baseOrbSx = Math.Max(0.10, (1.0 - 0.85 * t) + popOut * 0.55);
            double baseOrbSy = Math.Max(0.54, (1.0 - 0.34 * t) - popOut * 0.32);
            double orbTx = dockRestOffsetX * (1.0 - t) + (8.0 * t) - popOut * 34.0 + (_jelly * 7.0);

            _orbScale.ScaleX = Math.Max(0.08, baseOrbSx * jSx);
            _orbScale.ScaleY = Math.Max(0.45, baseOrbSy * jSy);
            _orbTranslate.X = orbTx;

            _orbCenterStack.Opacity = Clamp01(1.0 - t * 2.35);
            _orbRoot.Opacity = Clamp01(1.0 - Math.Max(0.0, (smoothT - 0.44) / 0.46));

            // 2. Mini Pill liquid morph + Q-elastic spring
            double basePillSx = Math.Max(0.06, (1.0 - 0.92 * t) + popOut * 0.35);
            double basePillSy = Math.Max(0.82, (1.0 + 0.12 * t) - popOut * 0.22);
            double pillTx = dockRestOffsetX * (1.0 - t) + (8.0 * t) - popOut * 28.0 + (_jelly * 6.0);

            _pillScale.ScaleX = Math.Max(0.05, basePillSx * (1.0 - _jelly * 0.45));
            _pillScale.ScaleY = Math.Max(0.70, basePillSy * (1.0 + _jelly * 0.50));
            _pillTranslate.X = pillTx;
            _pillRow.Opacity = Clamp01(1.0 - t * 2.5);
            _pillRoot.Opacity = Clamp01(1.0 - Math.Max(0.0, (smoothT - 0.42) / 0.48));

            // 3. Sleek right-edge strip with magnetic "bloop" jelly settle!
            double stripAppear = Clamp01((smoothT - 0.34) / 0.52);
            double overDock = rawT > 1.0 ? (rawT - 1.0) : 0.0;

            double sBaseX = (0.45 + 0.55 * stripAppear) * (1.0 + _stripJelly * 0.85 - overDock * 0.6);
            double sBaseY = (1.28 - 0.28 * stripAppear) * (1.0 - _stripJelly * 0.55 + overDock * 0.7);

            _dockStripRoot.Opacity = stripAppear;
            _stripScale.ScaleX = Math.Max(0.30, sBaseX);
            _stripScale.ScaleY = Math.Max(0.65, sBaseY);
            _stripTranslate.X = (1.0 - stripAppear) * 8.0;

            // Hit-testing handoff so collapsed transparent left region is 100% click-through!
            bool stripOwnsHit = t > 0.65;
            _dockStripRoot.IsHitTestVisible = stripOwnsHit;
            _orbRoot.IsHitTestVisible = !stripOwnsHit && !_settings.CompactPillMode;
            _pillRoot.IsHitTestVisible = !stripOwnsHit && _settings.CompactPillMode;
        }

        private void OnAnimationTick(object sender, EventArgs e)
        {
            double nowSec = _frameClock.Elapsed.TotalSeconds;
            double dt = nowSec - _lastFrameSeconds;
            _lastFrameSeconds = nowSec;
            if (dt <= 0.001 || dt > 0.05) dt = 0.016;

            StepEdgeDockPhysics(dt);

            if (_morphT < 0.92 && !_settings.CompactPillMode)
            {
                _currentProgress += (_targetProgress - _currentProgress) * Math.Min(1.0, dt * 8.5);
                double sweep = Math.Max(8.0, Math.Min(359.0, _currentProgress * 359.0));
                _progressArc.Data = BuildArcGeometry(67, 67, 52, -90, -90 + sweep);
            }

            if (_hasAlert)
            {
                _pulsePhase += dt * 7.5;
                double pulse = 0.45 + 0.55 * Math.Abs(Math.Sin(_pulsePhase));
                _orbGlowRing.Opacity = pulse;
                _orbLiveDot.Opacity = pulse;
                _pillLiveDot.Opacity = pulse;
                _stripLiveDot.Opacity = pulse;
                _stripAccentEdge.Opacity = pulse;
                _spinAngle = (_spinAngle + dt * 185.0) % 360.0;
                _spinnerRotate.Angle = _spinAngle;
                _spinnerArc.Opacity = 0.7;
            }
            else if (_isActive)
            {
                _orbGlowRing.Opacity = 1.0;
                _spinAngle = (_spinAngle + dt * 135.0) % 360.0;
                _spinnerRotate.Angle = _spinAngle;
                _spinnerArc.Opacity = 0.85;

                _pulsePhase += dt * 4.2;
                double dotOpacity = 0.55 + 0.45 * Math.Sin(_pulsePhase);
                _orbLiveDot.Opacity = dotOpacity;
                _pillLiveDot.Opacity = dotOpacity;
                _stripLiveDot.Opacity = dotOpacity;
                _stripAccentEdge.Opacity = 0.65 + 0.35 * Math.Sin(_pulsePhase);
            }
            else
            {
                _orbGlowRing.Opacity = 0.8;
                _spinnerArc.Opacity = Math.Max(0, _spinnerArc.Opacity - dt * 1.6);
                _orbLiveDot.Opacity = 0.7;
                _pillLiveDot.Opacity = 0.7;
                _stripLiveDot.Opacity = 0.65;
                _stripAccentEdge.Opacity = 0.55;
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
