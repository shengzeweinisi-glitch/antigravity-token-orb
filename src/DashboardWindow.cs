using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace AntigravityTokenOrb
{
    public class DashboardWindow : Window
    {
        private readonly AppSettings _settings;
        private readonly Action _onSettingsChanged;
        private readonly Action _onToggleOrbStyle;
        private readonly Action _onOpenSettings;
        private readonly string _brainDir;

        private TokenSnapshot _lastSnapshot;
        private string _pinnedConversationId;
        private ThemePalette _theme;

        // Alert banner
        private Border _alertBanner;
        private TextBlock _alertBannerText;

        // Header controls
        private Border _statusPill;
        private Ellipse _statusDot;
        private TextBlock _statusText;
        private Button _metricToggleBtn;
        private TextBlock _metricToggleText;

        // Speed controls
        private TextBlock _rate1mValue;
        private TextBlock _rate1mSub;
        private TextBlock _rate5mValue;
        private TextBlock _rate5mSub;
        private TextBlock _sparkPeakText;
        private Canvas _sparkCanvas;

        // Scope tabs
        private Button _tabToday;
        private Button _tabActive;
        private Button _tabAll;

        // Summary 4 cards
        private TextBlock _cardTotalTitle;
        private TextBlock _cardTotalValue;
        private TextBlock _cardTotalSub;
        private TextBlock _cardInputValue;
        private TextBlock _cardInputSub;
        private TextBlock _cardOutputValue;
        private TextBlock _cardOutputSub;
        private TextBlock _cardCacheValue;
        private TextBlock _cardCacheSub;
        private Border _cacheBarFill;

        // Cost card
        private TextBlock _costMainValue;
        private TextBlock _costBurnRateText;
        private TextBlock _costBreakdownText;

        // Context bar
        private TextBlock _contextTitleText;
        private TextBlock _contextValueText;
        private TextBlock _contextHintText;
        private TextBlock _contextModelBadge;
        private Border _contextBarFill;

        // Lists
        private StackPanel _convListPanel;
        private StackPanel _modelListPanel;
        private TextBlock _footerUpdateText;

        public string PinnedConversationId { get { return _pinnedConversationId; } }
        public event Action<string> PinnedConversationChanged;

        public DashboardWindow(
            AppSettings settings,
            string brainDir,
            Action onSettingsChanged,
            Action onToggleOrbStyle,
            Action onOpenSettings)
        {
            _settings = settings;
            _brainDir = brainDir;
            _onSettingsChanged = onSettingsChanged;
            _onToggleOrbStyle = onToggleOrbStyle;
            _onOpenSettings = onOpenSettings;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            Width = 432;
            SizeToContent = SizeToContent.Height;

            RebuildUi();
        }

        public void RebuildUi()
        {
            _theme = ThemeManager.GetTheme(_settings.ThemeId);
            BuildUi();
            UpdateTabStyles();
            UpdateMetricToggleVisual();
            if (_lastSnapshot != null)
                UpdateData(_lastSnapshot);
        }

        private void BuildUi()
        {
            var t = _theme;
            var outer = new Border
            {
                Margin = new Thickness(12),
                CornerRadius = new CornerRadius(18),
                Background = t.Brush(t.WindowBg),
                BorderBrush = t.Brush(t.WindowBorder),
                BorderThickness = new Thickness(1),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 22,
                    ShadowDepth = 6,
                    Opacity = t.IsLight ? 0.25 : 0.65
                }
            };

            var root = new StackPanel { Margin = new Thickness(16, 14, 16, 12) };
            outer.Child = root;
            Content = outer;

            // === HEADER ===
            var header = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    try { DragMove(); } catch { }
                }
            };

            var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var orbDot = new Ellipse
            {
                Width = 10,
                Height = 10,
                Margin = new Thickness(0, 0, 8, 0),
                Fill = new LinearGradientBrush(t.ColorOf(t.AccentPrimary), t.ColorOf(t.AccentSecondary), 45)
            };
            var titleText = new TextBlock
            {
                Text = "Antigravity Token",
                Foreground = t.Brush(t.TextPrimary),
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };

            _statusPill = new Border
            {
                CornerRadius = new CornerRadius(9),
                Background = t.Brush(t.TrackBg),
                BorderBrush = t.Brush(t.CardBorder),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(7, 2, 7, 2),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var statusInner = new StackPanel { Orientation = Orientation.Horizontal };
            _statusDot = new Ellipse { Width = 6, Height = 6, Fill = t.Brush(t.TextMuted), Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
            _statusText = new TextBlock
            {
                Text = "待机",
                Foreground = t.Brush(t.TextMuted),
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
                FontSize = 10.5,
                VerticalAlignment = VerticalAlignment.Center
            };
            statusInner.Children.Add(_statusDot);
            statusInner.Children.Add(_statusText);
            _statusPill.Child = statusInner;

            titleStack.Children.Add(orbDot);
            titleStack.Children.Add(titleText);
            titleStack.Children.Add(_statusPill);
            Grid.SetColumn(titleStack, 0);
            header.Children.Add(titleStack);

            var headerActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            _metricToggleBtn = CreatePillButton("", () =>
            {
                _settings.IncludeCacheInPrimary = !_settings.IncludeCacheInPrimary;
                UpdateMetricToggleVisual();
                if (_onSettingsChanged != null) _onSettingsChanged();
                if (_lastSnapshot != null) UpdateData(_lastSnapshot);
            });
            _metricToggleText = (TextBlock)_metricToggleBtn.Content;
            _metricToggleBtn.ToolTip = "点击切换统计口径：仅新增(输入+输出) / 全量(含Prompt缓存)";
            headerActions.Children.Add(_metricToggleBtn);

            var settingsBtn = new Button
            {
                Content = "⚙",
                Width = 24,
                Height = 24,
                Margin = new Thickness(5, 0, 0, 0),
                Background = Brushes.Transparent,
                Foreground = t.Brush(t.TextSecondary),
                BorderThickness = new Thickness(0),
                FontSize = 13,
                Cursor = Cursors.Hand,
                ToolTip = "打开设置中心（主题 / 单价 / 预警 / 开机自启）"
            };
            settingsBtn.Template = CreateFlatButtonTemplate(t.Brush(t.HoverBg), 6);
            settingsBtn.Click += (s, e) => { if (_onOpenSettings != null) _onOpenSettings(); };
            headerActions.Children.Add(settingsBtn);

            var closeBtn = new Button
            {
                Content = "✕",
                Width = 24,
                Height = 24,
                Margin = new Thickness(2, 0, 0, 0),
                Background = Brushes.Transparent,
                Foreground = t.Brush(t.TextMuted),
                BorderThickness = new Thickness(0),
                FontSize = 11.5,
                Cursor = Cursors.Hand
            };
            closeBtn.Template = CreateFlatButtonTemplate(t.Brush(t.HoverBg), 6);
            closeBtn.Click += (s, e) => Hide();
            headerActions.Children.Add(closeBtn);

            Grid.SetColumn(headerActions, 1);
            header.Children.Add(headerActions);
            root.Children.Add(header);

            // === ALERT BANNER (Hidden unless exceeded) ===
            _alertBanner = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = t.IsLight ? t.Brush("#FFF1F2") : t.Brush("#35141F"),
                BorderBrush = t.Brush(t.AccentDanger),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 0, 9),
                Visibility = Visibility.Collapsed,
                Cursor = Cursors.Hand
            };
            _alertBannerText = new TextBlock
            {
                Text = "",
                Foreground = t.IsLight ? t.Brush("#BE123C") : t.Brush("#FDA4AF"),
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            };
            _alertBanner.Child = _alertBannerText;
            _alertBanner.ToolTip = "点击打开设置中心调整预警阈值";
            _alertBanner.MouseLeftButtonDown += (s, e) => { if (_onOpenSettings != null) _onOpenSettings(); };
            root.Children.Add(_alertBanner);

            // === SECTION 1: SPEED + 20M SPARKLINE ===
            var speedCard = CreateCardBorder(new Thickness(12, 9, 12, 9), new Thickness(0, 0, 0, 9));
            var speedStack = new StackPanel();
            speedCard.Child = speedStack;

            var speedTop = new Grid { Margin = new Thickness(0, 0, 0, 7) };
            speedTop.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
            speedTop.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var leftRate = new StackPanel();
            leftRate.Children.Add(CreateLabel("实时速率 (1分钟滚动)", t.AccentPrimary, 10.5));
            _rate1mValue = new TextBlock
            {
                Text = "0/m",
                Foreground = t.Brush(t.TextPrimary),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 21,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 1, 0, 1)
            };
            _rate1mSub = CreateLabel("0/s · 输出 0/m · 0 次", t.TextMuted, 10);
            leftRate.Children.Add(_rate1mValue);
            leftRate.Children.Add(_rate1mSub);
            Grid.SetColumn(leftRate, 0);
            speedTop.Children.Add(leftRate);

            var rightRate = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
            var rLabel = CreateLabel("5分钟平均速率", t.AccentSecondary, 10.5);
            rLabel.HorizontalAlignment = HorizontalAlignment.Right;
            _rate5mValue = new TextBlock
            {
                Text = "0/m",
                Foreground = t.Brush(t.TextPrimary),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 16.5,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 4, 0, 2)
            };
            _rate5mSub = CreateLabel("最近步 +0", t.TextMuted, 10);
            _rate5mSub.HorizontalAlignment = HorizontalAlignment.Right;
            rightRate.Children.Add(rLabel);
            rightRate.Children.Add(_rate5mValue);
            rightRate.Children.Add(_rate5mSub);
            Grid.SetColumn(rightRate, 1);
            speedTop.Children.Add(rightRate);

            speedStack.Children.Add(speedTop);

            // Sparkline header + canvas
            var sparkHeader = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            sparkHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sparkHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            sparkHeader.Children.Add(CreateLabel("近 20 分钟消耗趋势 (每分钟)", t.TextMuted, 10));
            _sparkPeakText = CreateLabel("峰值 0/m", t.AccentPrimary, 10);
            Grid.SetColumn(_sparkPeakText, 1);
            sparkHeader.Children.Add(_sparkPeakText);
            speedStack.Children.Add(sparkHeader);

            var sparkBorder = new Border
            {
                Background = t.Brush(t.TrackBg),
                BorderBrush = t.Brush(t.CardBorder),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 5, 6, 4)
            };
            _sparkCanvas = new Canvas { Width = 336, Height = 50, ClipToBounds = true };
            sparkBorder.Child = _sparkCanvas;
            speedStack.Children.Add(sparkBorder);

            root.Children.Add(speedCard);

            // === SECTION 2: SCOPE TABS & 4 METRIC CARDS ===
            var tabBar = new Border
            {
                Background = t.Brush(t.CardBg),
                BorderBrush = t.Brush(t.CardBorder),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(3),
                Margin = new Thickness(0, 0, 0, 8)
            };
            var tabGrid = new Grid();
            tabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _tabToday = CreateTabButton("今日消耗", "today");
            _tabActive = CreateTabButton("当前会话", "active");
            _tabAll = CreateTabButton("全部历史", "all");

            Grid.SetColumn(_tabToday, 0);
            Grid.SetColumn(_tabActive, 1);
            Grid.SetColumn(_tabAll, 2);
            tabGrid.Children.Add(_tabToday);
            tabGrid.Children.Add(_tabActive);
            tabGrid.Children.Add(_tabAll);
            tabBar.Child = tabGrid;
            root.Children.Add(tabBar);

            var statsGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            statsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            statsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var c1 = BuildStatCard("新增总量 (In+Out)", t.AccentPrimary, out _cardTotalTitle, out _cardTotalValue, out _cardTotalSub);
            Grid.SetRow(c1, 0); Grid.SetColumn(c1, 0);
            statsGrid.Children.Add(c1);

            TextBlock dummy;
            var c2 = BuildStatCard("新增输入 (Input)", t.AccentSecondary, out dummy, out _cardInputValue, out _cardInputSub);
            Grid.SetRow(c2, 0); Grid.SetColumn(c2, 2);
            statsGrid.Children.Add(c2);

            var c3 = BuildStatCard("模型输出 (Output)", t.AccentOutput, out dummy, out _cardOutputValue, out _cardOutputSub);
            Grid.SetRow(c3, 2); Grid.SetColumn(c3, 0);
            statsGrid.Children.Add(c3);

            // Card 4: Cache Read
            var c4 = CreateCardBorder(new Thickness(10, 7, 10, 7), new Thickness(0));
            var c4Stack = new StackPanel();
            c4Stack.Children.Add(CreateLabel("缓存读取 (Cache)", t.AccentCache, 10.5));
            _cardCacheValue = new TextBlock
            {
                Text = "0",
                Foreground = t.Brush(t.TextPrimary),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 17.5,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 1, 0, 1)
            };
            _cardCacheSub = CreateLabel("命中率 0.0%", t.TextMuted, 10);
            var barTrack = new Border
            {
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Background = t.Brush(t.TrackBg),
                Margin = new Thickness(0, 4, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            _cacheBarFill = new Border
            {
                Height = 4,
                Width = 0,
                HorizontalAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(2),
                Background = t.Brush(t.AccentCache)
            };
            barTrack.Child = _cacheBarFill;
            c4Stack.Children.Add(_cardCacheValue);
            c4Stack.Children.Add(_cardCacheSub);
            c4Stack.Children.Add(barTrack);
            c4.Child = c4Stack;
            Grid.SetRow(c4, 2); Grid.SetColumn(c4, 2);
            statsGrid.Children.Add(c4);

            root.Children.Add(statsGrid);

            // === COST ESTIMATION STRIP ===
            var costCard = CreateCardBorder(new Thickness(11, 7, 11, 7), new Thickness(0, 0, 0, 8));
            costCard.Cursor = Cursors.Hand;
            costCard.ToolTip = "按自定义单价估算的参考费用 · 点击配置单价与币种";
            costCard.MouseLeftButtonDown += (s, e) => { if (_onOpenSettings != null) _onOpenSettings(); };

            var costStack = new StackPanel();
            var costTop = new Grid();
            costTop.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            costTop.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            costTop.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var costTitle = CreateLabel("估算费用", t.AccentCost, 10.5);
            costTitle.VerticalAlignment = VerticalAlignment.Center;
            costTitle.Margin = new Thickness(0, 0, 8, 0);
            costTop.Children.Add(costTitle);

            _costMainValue = new TextBlock
            {
                Text = "¥0.00",
                Foreground = t.Brush(t.AccentCost),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_costMainValue, 1);
            costTop.Children.Add(_costMainValue);

            _costBurnRateText = CreateLabel("点击调单价 ⚙", t.TextMuted, 10);
            _costBurnRateText.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_costBurnRateText, 2);
            costTop.Children.Add(_costBurnRateText);
            costStack.Children.Add(costTop);

            _costBreakdownText = CreateLabel("输入 ¥0.00 · 输出 ¥0.00 · 缓存 ¥0.00", t.TextMuted, 9.5);
            _costBreakdownText.Margin = new Thickness(0, 2, 0, 0);
            costStack.Children.Add(_costBreakdownText);
            costCard.Child = costStack;
            root.Children.Add(costCard);

            // === CONTEXT WINDOW BAR ===
            var ctxCard = CreateCardBorder(new Thickness(11, 7, 11, 8), new Thickness(0, 0, 0, 9));
            var ctxStack = new StackPanel();
            var ctxTop = new Grid();
            ctxTop.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ctxTop.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _contextTitleText = CreateLabel("当前上下文窗口 (最近一步)", t.TextSecondary, 10.5);
            _contextTitleText.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetColumn(_contextTitleText, 0);
            ctxTop.Children.Add(_contextTitleText);

            var badgeBorder = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = t.Brush(t.TrackBg),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(6, 0, 0, 0)
            };
            _contextModelBadge = CreateLabel("-", t.TextSecondary, 9.5);
            badgeBorder.Child = _contextModelBadge;
            Grid.SetColumn(badgeBorder, 1);
            ctxTop.Children.Add(badgeBorder);
            ctxStack.Children.Add(ctxTop);

            var ctxMid = new Grid { Margin = new Thickness(0, 3, 0, 4) };
            ctxMid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ctxMid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _contextValueText = new TextBlock
            {
                Text = "0 tokens",
                Foreground = t.Brush(t.TextPrimary),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold
            };
            ctxMid.Children.Add(_contextValueText);
            _contextHintText = CreateLabel("预警阈值 150K", t.TextMuted, 9.5);
            _contextHintText.VerticalAlignment = VerticalAlignment.Bottom;
            Grid.SetColumn(_contextHintText, 1);
            ctxMid.Children.Add(_contextHintText);
            ctxStack.Children.Add(ctxMid);

            var ctxTrack = new Border
            {
                Height = 5,
                CornerRadius = new CornerRadius(2.5),
                Background = t.Brush(t.TrackBg)
            };
            _contextBarFill = new Border
            {
                Height = 5,
                Width = 0,
                HorizontalAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(2.5),
                Background = new LinearGradientBrush(t.ColorOf(t.AccentPrimary), t.ColorOf(t.AccentSecondary), 0)
            };
            ctxTrack.Child = _contextBarFill;
            ctxStack.Children.Add(ctxTrack);
            ctxCard.Child = ctxStack;
            root.Children.Add(ctxCard);

            // === SECTION 3: RECENT CONVERSATIONS ===
            var convHeader = new Grid { Margin = new Thickness(2, 0, 2, 4) };
            convHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            convHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            convHeader.Children.Add(CreateLabel("最近活跃会话 (点击切换查看)", t.TextMuted, 10.5));
            var resetAutoBtn = CreateLinkButton("自动跟随最新", () =>
            {
                _pinnedConversationId = null;
                if (PinnedConversationChanged != null) PinnedConversationChanged(null);
            });
            Grid.SetColumn(resetAutoBtn, 1);
            convHeader.Children.Add(resetAutoBtn);
            root.Children.Add(convHeader);

            var convCard = CreateCardBorder(new Thickness(8, 5, 8, 5), new Thickness(0, 0, 0, 9));
            _convListPanel = new StackPanel();
            convCard.Child = _convListPanel;
            root.Children.Add(convCard);

            // === SECTION 4: MODEL BREAKDOWN ===
            var modelHeader = CreateLabel("模型消耗分布", t.TextMuted, 10.5);
            modelHeader.Margin = new Thickness(2, 0, 2, 4);
            root.Children.Add(modelHeader);

            var modelCard = CreateCardBorder(new Thickness(10, 6, 10, 6), new Thickness(0, 0, 0, 9));
            _modelListPanel = new StackPanel();
            modelCard.Child = _modelListPanel;
            root.Children.Add(modelCard);

            // === FOOTER ===
            var footer = new Grid { Margin = new Thickness(2, 0, 2, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _footerUpdateText = CreateLabel("监听中...", t.TextMuted, 10);
            _footerUpdateText.VerticalAlignment = VerticalAlignment.Center;
            footer.Children.Add(_footerUpdateText);

            var footerRight = new StackPanel { Orientation = Orientation.Horizontal };
            var styleBtn = CreateLinkButton("球形/胶囊", () =>
            {
                if (_onToggleOrbStyle != null) _onToggleOrbStyle();
            });
            styleBtn.Margin = new Thickness(0, 0, 9, 0);

            var prefBtn = CreateLinkButton("设置中心", () =>
            {
                if (_onOpenSettings != null) _onOpenSettings();
            });
            prefBtn.Margin = new Thickness(0, 0, 9, 0);

            var folderBtn = CreateLinkButton("日志目录", () =>
            {
                try
                {
                    if (System.IO.Directory.Exists(_brainDir))
                        Process.Start("explorer.exe", _brainDir);
                }
                catch { }
            });
            footerRight.Children.Add(styleBtn);
            footerRight.Children.Add(prefBtn);
            footerRight.Children.Add(folderBtn);
            Grid.SetColumn(footerRight, 1);
            footer.Children.Add(footerRight);
            root.Children.Add(footer);
        }

        private Border BuildStatCard(string title, string accentHex, out TextBlock titleBlock, out TextBlock valueBlock, out TextBlock subBlock)
        {
            var card = CreateCardBorder(new Thickness(10, 7, 10, 7), new Thickness(0));
            var stack = new StackPanel();
            titleBlock = CreateLabel(title, accentHex, 10.5);
            valueBlock = new TextBlock
            {
                Text = "0",
                Foreground = _theme.Brush(_theme.TextPrimary),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 17.5,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 1, 0, 1)
            };
            subBlock = CreateLabel("-", _theme.TextMuted, 10);
            stack.Children.Add(titleBlock);
            stack.Children.Add(valueBlock);
            stack.Children.Add(subBlock);
            card.Child = stack;
            return card;
        }

        private Border CreateCardBorder(Thickness padding, Thickness margin)
        {
            return new Border
            {
                Background = _theme.Brush(_theme.CardBg),
                BorderBrush = _theme.Brush(_theme.CardBorder),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = padding,
                Margin = margin
            };
        }

        private TextBlock CreateLabel(string text, string colorHex, double size)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = _theme.Brush(colorHex),
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
                FontSize = size
            };
        }

        private Button CreateTabButton(string text, string scopeKey)
        {
            var btn = new Button
            {
                Content = text,
                Tag = scopeKey,
                Height = 25,
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
                FontSize = 11.5,
                FontWeight = FontWeights.Medium,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btn.Template = CreateFlatButtonTemplate(_theme.Brush(_theme.HoverBg), 7);
            btn.Click += (s, e) =>
            {
                _settings.SelectedScope = scopeKey;
                UpdateTabStyles();
                if (_onSettingsChanged != null) _onSettingsChanged();
                if (_lastSnapshot != null) UpdateData(_lastSnapshot);
            };
            return btn;
        }

        private Button CreatePillButton(string text, Action onClick)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
                FontSize = 10,
                Foreground = _theme.Brush(_theme.AccentPrimary)
            };
            var btn = new Button
            {
                Content = tb,
                Padding = new Thickness(7, 3, 7, 3),
                Background = _theme.Brush(_theme.TrackBg),
                BorderBrush = _theme.Brush(_theme.CardBorder),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            btn.Template = CreateFlatButtonTemplate(_theme.Brush(_theme.HoverBg), 8);
            btn.Click += (s, e) => { if (onClick != null) onClick(); };
            return btn;
        }

        private Button CreateLinkButton(string text, Action onClick)
        {
            var btn = new Button
            {
                Content = text,
                Background = Brushes.Transparent,
                Foreground = _theme.Brush(_theme.AccentPrimary),
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
                FontSize = 10.5,
                Padding = new Thickness(2, 0, 2, 0),
                Cursor = Cursors.Hand
            };
            btn.Template = CreateFlatButtonTemplate(_theme.Brush(_theme.HoverBg), 4);
            btn.Click += (s, e) => { if (onClick != null) onClick(); };
            return btn;
        }

        private static ControlTemplate CreateFlatButtonTemplate(Brush hoverBrush, double radius)
        {
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border), "bd");
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            borderFactory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background")
            {
                RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent
            });
            borderFactory.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush")
            {
                RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent
            });
            borderFactory.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness")
            {
                RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent
            });
            borderFactory.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding")
            {
                RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent
            });

            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentFactory);
            template.VisualTree = borderFactory;

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hoverBrush, "bd"));
            template.Triggers.Add(hoverTrigger);
            return template;
        }

        private void UpdateTabStyles()
        {
            var tabs = new[] { _tabToday, _tabActive, _tabAll };
            foreach (var t in tabs)
            {
                bool active = string.Equals((string)t.Tag, _settings.SelectedScope, StringComparison.OrdinalIgnoreCase);
                t.Background = active ? _theme.Brush(_theme.HoverBg) : Brushes.Transparent;
                t.Foreground = active ? _theme.Brush(_theme.TextPrimary) : _theme.Brush(_theme.TextMuted);
            }
        }

        public void UpdateMetricToggleVisual()
        {
            if (_settings.IncludeCacheInPrimary)
            {
                _metricToggleText.Text = "口径: 含缓存";
                _metricToggleText.Foreground = _theme.Brush(_theme.AccentCache);
            }
            else
            {
                _metricToggleText.Text = "口径: 新增";
                _metricToggleText.Foreground = _theme.Brush(_theme.AccentPrimary);
            }
        }

        public void PositionNearOrb(Window orb)
        {
            UpdateLayout();
            double panelW = ActualWidth > 0 ? ActualWidth : Width;
            double panelH = ActualHeight > 0 ? ActualHeight : 600;

            var wa = SystemParameters.WorkArea;
            double orbCenterX = orb.Left + orb.Width / 2.0;
            double orbCenterY = orb.Top + orb.Height / 2.0;

            double targetLeft;
            if (orbCenterX > wa.Left + wa.Width / 2.0)
                targetLeft = orb.Left - panelW + 4;
            else
                targetLeft = orb.Left + orb.Width - 4;

            double targetTop = orbCenterY - panelH / 2.0;

            if (targetLeft < wa.Left) targetLeft = wa.Left;
            if (targetLeft + panelW > wa.Right) targetLeft = wa.Right - panelW;
            if (targetTop < wa.Top) targetTop = wa.Top;
            if (targetTop + panelH > wa.Bottom) targetTop = wa.Bottom - panelH;

            Left = targetLeft;
            Top = targetTop;
        }

        public void UpdateData(TokenSnapshot snap)
        {
            _lastSnapshot = snap;
            if (snap == null) return;

            var t = _theme;
            bool incCache = _settings.IncludeCacheInPrimary;
            var alert = AlertStatus.Evaluate(snap, _settings);

            // Alert banner
            if (alert.HasAny)
            {
                _alertBanner.Visibility = Visibility.Visible;
                _alertBannerText.Text = alert.BannerMessage;
            }
            else
            {
                _alertBanner.Visibility = Visibility.Collapsed;
            }

            // Live Status
            if (snap.IsActive)
            {
                _statusDot.Fill = t.Brush(t.AccentActive);
                _statusText.Text = "活跃中";
                _statusText.Foreground = t.Brush(t.AccentActive);
            }
            else
            {
                _statusDot.Fill = t.Brush(t.TextMuted);
                _statusText.Text = "待机中";
                _statusText.Foreground = t.Brush(t.TextMuted);
            }

            // Speed metrics
            double rate1m = incCache ? snap.Rate1mAllPerMin : snap.Rate1mEffectivePerMin;
            double rate5m = incCache ? snap.Rate5mAllPerMin : snap.Rate5mEffectivePerMin;

            _rate1mValue.Text = TokenFormat.FormatRatePerMin(rate1m);
            _rate1mValue.Foreground = alert.RateExceeded ? t.Brush(t.AccentDanger) : t.Brush(t.TextPrimary);
            _rate1mSub.Text = string.Format("{0} · 输出 {1} · {2} 次",
                TokenFormat.FormatRatePerSec(rate1m),
                TokenFormat.FormatRatePerMin(snap.Rate1mOutputPerMin),
                snap.StepsLast1m);

            _rate5mValue.Text = TokenFormat.FormatRatePerMin(rate5m);
            _rate5mSub.Text = string.Format("最近步 +{0} (输出 {1})",
                TokenFormat.FormatCount(snap.LatestStepNewTokens),
                TokenFormat.FormatCount(snap.LatestStepOutputTokens));

            // Sparkline
            long[] series = incCache ? snap.SparklineAll20m : snap.SparklineEffective20m;
            DrawSparkline(series, incCache ? t.AccentCache : t.AccentPrimary);

            // Selected scope
            ScopeAggregate agg;
            List<ModelBreakdown> models;
            if (_settings.SelectedScope == "active")
            {
                agg = snap.ActiveConversation;
                models = snap.TodayModels;
            }
            else if (_settings.SelectedScope == "all")
            {
                agg = snap.AllTime;
                models = snap.AllTimeModels;
            }
            else
            {
                agg = snap.Today;
                models = snap.TodayModels;
            }

            if (incCache)
            {
                _cardTotalTitle.Text = "全量总计 (含Cache)";
                _cardTotalValue.Text = TokenFormat.FormatCount(agg.TotalAllTokens);
                _cardTotalSub.Text = string.Format("新增 In+Out: {0}", TokenFormat.FormatCount(agg.EffectiveTokens));
            }
            else
            {
                _cardTotalTitle.Text = "新增总量 (In+Out)";
                _cardTotalValue.Text = TokenFormat.FormatCount(agg.EffectiveTokens);
                _cardTotalSub.Text = string.Format("含缓存全量: {0}", TokenFormat.FormatCount(agg.TotalAllTokens));
            }

            _cardInputValue.Text = TokenFormat.FormatCount(agg.InputTokens);
            _cardInputSub.Text = string.Format("共 {0} 次模型调用", agg.StepCount);

            _cardOutputValue.Text = TokenFormat.FormatCount(agg.OutputTokens);
            double outPct = agg.EffectiveTokens > 0 ? (double)agg.OutputTokens / agg.EffectiveTokens * 100.0 : 0.0;
            _cardOutputSub.Text = string.Format("占新增 {0:0.0}%", outPct);

            _cardCacheValue.Text = TokenFormat.FormatCount(agg.CacheReadTokens);
            _cardCacheSub.Text = string.Format("缓存命中率 {0:0.1}%", agg.CacheHitRate);
            _cacheBarFill.Width = Math.Max(0, Math.Min(154.0, 154.0 * (agg.CacheHitRate / 100.0)));

            // Cost estimation for selected scope
            double costInUsd = (agg.InputTokens / 1000000.0) * _settings.PriceInputPerMillionUsd;
            double costOutUsd = (agg.OutputTokens / 1000000.0) * _settings.PriceOutputPerMillionUsd;
            double costCacheUsd = (agg.CacheReadTokens / 1000000.0) * _settings.PriceCacheReadPerMillionUsd;
            double totalCostUsd = costInUsd + costOutUsd + costCacheUsd;

            _costMainValue.Text = TokenFormat.FormatCost(totalCostUsd, _settings);
            _costBreakdownText.Text = string.Format("输入 {0} · 输出 {1} · 缓存 {2}",
                TokenFormat.FormatCost(costInUsd, _settings),
                TokenFormat.FormatCost(costOutUsd, _settings),
                TokenFormat.FormatCost(costCacheUsd, _settings));

            // Estimated hourly burn rate from 5m average
            if (snap.Rate5mEffectivePerMin > 1.0)
            {
                double eff5mIn = Math.Max(0, snap.Rate5mEffectivePerMin - snap.Rate5mOutputPerMin);
                double cache5m = Math.Max(0, snap.Rate5mAllPerMin - snap.Rate5mEffectivePerMin);
                double hourlyUsd = TokenFormat.EstimateCostUsd(
                    (long)(eff5mIn * 60.0),
                    (long)(snap.Rate5mOutputPerMin * 60.0),
                    (long)(cache5m * 60.0),
                    _settings);
                _costBurnRateText.Text = string.Format("当前约 {0}/小时", TokenFormat.FormatCost(hourlyUsd, _settings));
            }
            else
            {
                _costBurnRateText.Text = "点击配置单价 ⚙";
            }

            // Context window
            string convLabel = string.IsNullOrEmpty(snap.ActiveConversationTitle) ? "当前上下文窗口" : snap.ActiveConversationTitle;
            _contextTitleText.Text = "上下文: " + convLabel;
            _contextModelBadge.Text = TokenFormat.FormatModelShort(snap.ActiveModel);
            _contextValueText.Text = string.Format("{0} tokens", TokenFormat.FormatCount(snap.LatestContextTokens));
            _contextValueText.Foreground = alert.ContextExceeded ? t.Brush(t.AccentDanger) : t.Brush(t.TextPrimary);

            long ctxCeiling = _settings.ContextAlertThreshold > 0 ? Math.Max(200000L, _settings.ContextAlertThreshold) : 200000L;
            _contextHintText.Text = _settings.ContextAlertThreshold > 0
                ? string.Format("预警线 {0}", TokenFormat.FormatCount(_settings.ContextAlertThreshold))
                : "参考上限 200K";
            double ctxRatio = Math.Min(1.0, (double)snap.LatestContextTokens / ctxCeiling);
            _contextBarFill.Width = Math.Max(0, 350.0 * ctxRatio);
            _contextBarFill.Background = alert.ContextExceeded
                ? (Brush)t.Brush(t.AccentDanger)
                : new LinearGradientBrush(t.ColorOf(t.AccentPrimary), t.ColorOf(t.AccentSecondary), 0);

            // Conversations list (top 4)
            _convListPanel.Children.Clear();
            int shown = 0;
            foreach (var c in snap.Conversations)
            {
                if (shown >= 4) break;
                _convListPanel.Children.Add(BuildConversationRow(c, c.ConversationId == snap.ActiveConversationId, incCache));
                shown++;
            }
            if (shown == 0)
            {
                var empty = CreateLabel("暂未检测到 Antigravity 会话记录", t.TextMuted, 11);
                empty.Margin = new Thickness(4, 6, 4, 6);
                _convListPanel.Children.Add(empty);
            }

            // Models breakdown (top 3)
            _modelListPanel.Children.Clear();
            long modelSum = 0;
            foreach (var m in models)
                modelSum += incCache ? m.TotalAllTokens : m.EffectiveTokens;

            int mShown = 0;
            string[] barColors = { t.AccentPrimary, t.AccentSecondary, t.AccentCache };
            foreach (var m in models)
            {
                if (mShown >= 3) break;
                long val = incCache ? m.TotalAllTokens : m.EffectiveTokens;
                double pct = modelSum > 0 ? (double)val / modelSum * 100.0 : 0.0;
                _modelListPanel.Children.Add(BuildModelRow(m, val, pct, barColors[mShown % barColors.Length]));
                mShown++;
            }
            if (mShown == 0)
            {
                _modelListPanel.Children.Add(CreateLabel("暂无模型数据", t.TextMuted, 11));
            }

            _footerUpdateText.Text = string.Format("更新 {0} · 最近活动 {1}",
                DateTime.Now.ToString("HH:mm:ss"),
                TokenFormat.FormatRelativeTime(snap.LastEventUtc));
        }

        private UIElement BuildConversationRow(ConversationStats c, bool isSelected, bool incCache)
        {
            var t = _theme;
            bool isRecent = (DateTime.UtcNow - c.LastActivityUtc).TotalSeconds < 120;
            double convUsd = TokenFormat.EstimateCostUsd(c.InputTokens, c.OutputTokens, c.CacheReadTokens, _settings);

            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = isSelected ? t.Brush(t.SelectedRowBg) : Brushes.Transparent,
                BorderBrush = isSelected ? t.Brush(t.SelectedRowBorder) : Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 1, 0, 1),
                Cursor = Cursors.Hand
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel();
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            titleRow.Children.Add(new Ellipse
            {
                Width = 6,
                Height = 6,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Fill = isRecent ? t.Brush(t.AccentActive) : t.Brush(t.TextMuted)
            });
            var titleTb = new TextBlock
            {
                Text = c.Title,
                Foreground = isSelected ? t.Brush(t.TextPrimary) : t.Brush(t.TextSecondary),
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
                FontSize = 11.5,
                FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
                MaxWidth = 225,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            titleRow.Children.Add(titleTb);
            left.Children.Add(titleRow);

            var sub = CreateLabel(
                string.Format("{0} · {1}步 · 上下文 {2}",
                    TokenFormat.FormatRelativeTime(c.LastActivityUtc),
                    c.StepCount,
                    TokenFormat.FormatCount(c.LatestContextTokens)),
                t.TextMuted, 9.5);
            sub.Margin = new Thickness(12, 1, 0, 0);
            left.Children.Add(sub);
            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            var right = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            long mainVal = incCache ? c.TotalAllTokens : c.EffectiveTokens;
            var valTb = new TextBlock
            {
                Text = TokenFormat.FormatCount(mainVal),
                Foreground = t.Brush(t.AccentPrimary),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var costTb = CreateLabel(TokenFormat.FormatCost(convUsd, _settings), t.AccentCost, 9.5);
            costTb.HorizontalAlignment = HorizontalAlignment.Right;
            right.Children.Add(valTb);
            right.Children.Add(costTb);
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);

            border.Child = grid;
            border.ToolTip = string.Format("{0}\n输入: {1} | 输出: {2} | 缓存: {3} (命中 {4:0.1}%)\n预估费用: {5}",
                c.Title,
                TokenFormat.FormatCount(c.InputTokens),
                TokenFormat.FormatCount(c.OutputTokens),
                TokenFormat.FormatCount(c.CacheReadTokens),
                c.CacheHitRate,
                TokenFormat.FormatCost(convUsd, _settings));

            border.MouseLeftButtonDown += (s, e) =>
            {
                _pinnedConversationId = c.ConversationId;
                _settings.SelectedScope = "active";
                UpdateTabStyles();
                if (PinnedConversationChanged != null) PinnedConversationChanged(c.ConversationId);
            };
            return border;
        }

        private UIElement BuildModelRow(ModelBreakdown m, long val, double pct, string colorHex)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 2, 0, 3) };
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameTb = CreateLabel(m.DisplayName, _theme.TextSecondary, 10.5);
            Grid.SetColumn(nameTb, 0);
            top.Children.Add(nameTb);

            var valTb = CreateLabel(
                string.Format("{0} ({1:0.#}%) · {2}次", TokenFormat.FormatCount(val), pct, m.StepCount),
                _theme.TextMuted, 10);
            Grid.SetColumn(valTb, 1);
            top.Children.Add(valTb);
            stack.Children.Add(top);

            var track = new Border
            {
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Background = _theme.Brush(_theme.TrackBg),
                Margin = new Thickness(0, 3, 0, 0)
            };
            track.Child = new Border
            {
                Height = 4,
                Width = Math.Max(2, 352.0 * (pct / 100.0)),
                HorizontalAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(2),
                Background = _theme.Brush(colorHex)
            };
            stack.Children.Add(track);
            return stack;
        }

        private void DrawSparkline(long[] buckets, string accentHex)
        {
            _sparkCanvas.Children.Clear();
            if (buckets == null || buckets.Length == 0) return;

            double w = _sparkCanvas.Width;
            double h = _sparkCanvas.Height;
            long maxVal = 0;
            for (int i = 0; i < buckets.Length; i++)
                if (buckets[i] > maxVal) maxVal = buckets[i];

            _sparkPeakText.Text = "峰值 " + TokenFormat.FormatRatePerMin(maxVal);

            var midLine = new Line
            {
                X1 = 0,
                X2 = w,
                Y1 = h * 0.5,
                Y2 = h * 0.5,
                Stroke = _theme.Brush(_theme.CardBorder),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 3, 3 }
            };
            _sparkCanvas.Children.Add(midLine);

            int n = buckets.Length;
            double step = w / n;
            double barW = Math.Max(6, step - 4);
            var accentColor = _theme.ColorOf(accentHex);

            var linePoints = new PointCollection();

            for (int i = 0; i < n; i++)
            {
                double ratio = maxVal > 0 ? (double)buckets[i] / maxVal : 0.0;
                double barH = buckets[i] > 0 ? Math.Max(3.0, ratio * (h - 7)) : 2.0;
                double x = i * step + (step - barW) / 2.0;
                double y = h - barH;

                var rect = new Rectangle
                {
                    Width = barW,
                    Height = barH,
                    RadiusX = 2.5,
                    RadiusY = 2.5,
                    Fill = buckets[i] > 0
                        ? (Brush)new LinearGradientBrush(
                            Color.FromArgb(220, accentColor.R, accentColor.G, accentColor.B),
                            Color.FromArgb(65, accentColor.R, accentColor.G, accentColor.B),
                            90)
                        : _theme.Brush(_theme.CardBorder)
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                _sparkCanvas.Children.Add(rect);

                linePoints.Add(new Point(x + barW / 2.0, y));
            }

            if (maxVal > 0)
            {
                var poly = new Polyline
                {
                    Points = linePoints,
                    Stroke = _theme.Brush(accentHex),
                    StrokeThickness = 1.6,
                    StrokeLineJoin = PenLineJoin.Round,
                    Opacity = 0.85
                };
                _sparkCanvas.Children.Add(poly);
            }
        }
    }
}
