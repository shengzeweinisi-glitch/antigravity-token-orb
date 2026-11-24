using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace AntigravityTokenOrb
{
    public class SettingsWindow : Window
    {
        private readonly AppSettings _settings;
        private readonly Action _onApplied;

        private Border _outerBorder;
        private StackPanel _rootPanel;

        // Controls
        private readonly List<Button> _themeButtons = new List<Button>();
        private ComboBox _quotaGroupCombo;
        private ComboBox _quotaModeCombo;
        private TextBox _quota5hKBox;
        private TextBox _quotaWeekKBox;
        private CheckBox _topMostCheck;
        private CheckBox _autoStartCheck;
        private CheckBox _autoDockCheck;
        private Slider _opacitySlider;
        private TextBlock _opacityValText;

        private ComboBox _currencyCombo;
        private TextBox _fxRateBox;
        private TextBox _priceInBox;
        private TextBox _priceOutBox;
        private TextBox _priceCacheBox;

        private CheckBox _alertsEnableCheck;
        private CheckBox _balloonEnableCheck;
        private TextBox _ctxAlertKBox;
        private TextBox _rateAlertKBox;
        private TextBox _todayAlertKBox;
        private TextBlock _statusMsg;

        public SettingsWindow(AppSettings settings, Action onApplied)
        {
            _settings = settings;
            _onApplied = onApplied;

            Title = "Antigravity Token 设置中心";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            Width = 440;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            BuildUi();
        }

        private void BuildUi()
        {
            var theme = ThemeManager.GetTheme(_settings.ThemeId);
            _themeButtons.Clear();

            _outerBorder = new Border
            {
                Margin = new Thickness(12),
                CornerRadius = new CornerRadius(18),
                Background = theme.Brush(theme.WindowBg),
                BorderBrush = theme.Brush(theme.WindowBorder),
                BorderThickness = new Thickness(1),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 22,
                    ShadowDepth = 6,
                    Opacity = theme.IsLight ? 0.25 : 0.65
                }
            };

            _rootPanel = new StackPanel { Margin = new Thickness(18, 14, 18, 16) };
            _outerBorder.Child = _rootPanel;
            Content = _outerBorder;

            // Header
            var header = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    try { DragMove(); } catch { }
                }
            };

            var titleTb = new TextBlock
            {
                Text = "⚙ 设置中心 · 主题 / 费用 / 预警 / 自启",
                Foreground = theme.Brush(theme.TextPrimary),
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            header.Children.Add(titleTb);

            var closeBtn = new Button
            {
                Content = "✕",
                Width = 26,
                Height = 26,
                Background = Brushes.Transparent,
                Foreground = theme.Brush(theme.TextMuted),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            closeBtn.Click += (s, e) => Close();
            Grid.SetColumn(closeBtn, 1);
            header.Children.Add(closeBtn);
            _rootPanel.Children.Add(header);

            // === 1. THEME SECTION ===
            var themeCard = CreateSectionCard(theme, "配色主题 (点击即时预览)");
            var themeGrid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            themeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            themeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            themeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            themeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            themeGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
            themeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int idx = 0; idx < ThemeManager.AllThemes.Count; idx++)
            {
                var tp = ThemeManager.AllThemes[idx];
                bool selected = string.Equals(tp.Id, _settings.ThemeId, StringComparison.OrdinalIgnoreCase);

                var swStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                swStack.Children.Add(new Border
                {
                    Width = 12,
                    Height = 12,
                    CornerRadius = new CornerRadius(6),
                    Background = tp.Brush(tp.AccentPrimary),
                    BorderBrush = tp.Brush(tp.WindowBorder),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 6, 0)
                });
                swStack.Children.Add(new TextBlock
                {
                    Text = tp.Name.Split(' ')[0],
                    Foreground = theme.Brush(selected ? theme.TextPrimary : theme.TextSecondary),
                    FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
                    FontSize = 11.5,
                    FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal
                });

                var btn = new Button
                {
                    Content = swStack,
                    Height = 30,
                    Tag = tp.Id,
                    Background = theme.Brush(selected ? theme.SelectedRowBg : theme.TrackBg),
                    BorderBrush = theme.Brush(selected ? theme.AccentPrimary : theme.CardBorder),
                    BorderThickness = new Thickness(selected ? 1.5 : 1),
                    Cursor = Cursors.Hand
                };
                string capturedId = tp.Id;
                btn.Click += (s, e) =>
                {
                    _settings.ThemeId = capturedId;
                    SettingsManager.Save(_settings);
                    if (_onApplied != null) _onApplied();
                    BuildUi(); // Re-render settings window in new theme
                };
                int r = (idx / 2) * 2;
                int c = (idx % 2) * 2;
                Grid.SetRow(btn, r);
                Grid.SetColumn(btn, c);
                themeGrid.Children.Add(btn);
                _themeButtons.Add(btn);
            }
            ((StackPanel)themeCard.Child).Children.Add(themeGrid);
            _rootPanel.Children.Add(themeCard);

            // === 2. GENERAL & STARTUP ===
            var genCard = CreateSectionCard(theme, "悬浮球显示与系统启动");
            var genStack = (StackPanel)genCard.Child;

            var row1 = new Grid { Margin = new Thickness(0, 6, 0, 6) };
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _autoStartCheck = CreateCheckBox(theme, "Windows 开机自动启动", SettingsManager.IsAutoStartEnabled());
            _topMostCheck = CreateCheckBox(theme, "悬浮球始终置顶", _settings.TopMost);
            Grid.SetColumn(_autoStartCheck, 0);
            Grid.SetColumn(_topMostCheck, 1);
            row1.Children.Add(_autoStartCheck);
            row1.Children.Add(_topMostCheck);
            genStack.Children.Add(row1);

            _autoDockCheck = CreateCheckBox(theme, "靠桌面右边缘自动缩成细条（鼠标靠近拉出）", _settings.AutoDockRightEdge);
            _autoDockCheck.Margin = new Thickness(0, 0, 0, 6);
            genStack.Children.Add(_autoDockCheck);

            var quotaModeRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            quotaModeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            quotaModeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            quotaModeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var qgStack = new StackPanel();
            qgStack.Children.Add(MakeTinyLabel(theme, "官方配额模型分组 (agy /usage)"));
            _quotaGroupCombo = new ComboBox { Height = 25, FontSize = 11 };
            _quotaGroupCombo.Items.Add(new ComboBoxItem { Content = "自动跟随当前模型", Tag = "auto" });
            _quotaGroupCombo.Items.Add(new ComboBoxItem { Content = "固定 Gemini 模型", Tag = "gemini" });
            _quotaGroupCombo.Items.Add(new ComboBoxItem { Content = "固定 Claude / GPT", Tag = "claude" });
            SelectComboByTag(_quotaGroupCombo, _settings.QuotaGroupPreference);
            qgStack.Children.Add(_quotaGroupCombo);
            Grid.SetColumn(qgStack, 0);
            quotaModeRow.Children.Add(qgStack);

            var qmStack = new StackPanel();
            qmStack.Children.Add(MakeTinyLabel(theme, "悬浮球百分比显示方式"));
            _quotaModeCombo = new ComboBox { Height = 25, FontSize = 11 };
            _quotaModeCombo.Items.Add(new ComboBoxItem { Content = "已用百分比 (如 7%)", Tag = "used" });
            _quotaModeCombo.Items.Add(new ComboBoxItem { Content = "剩余百分比 (如 93%)", Tag = "remaining" });
            SelectComboByTag(_quotaModeCombo, _settings.ShowRemainingPercent ? "remaining" : "used");
            qmStack.Children.Add(_quotaModeCombo);
            Grid.SetColumn(qmStack, 2);
            quotaModeRow.Children.Add(qmStack);
            genStack.Children.Add(quotaModeRow);

            var row2 = new Grid { Margin = new Thickness(0, 2, 0, 6) };
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var q5Stack = new StackPanel();
            q5Stack.Children.Add(MakeTinyLabel(theme, "5小时进度条参考上限 (K tok)"));
            _quota5hKBox = CreateInputBox(theme, (_settings.Quota5hTokens / 1000).ToString(CultureInfo.InvariantCulture));
            q5Stack.Children.Add(_quota5hKBox);
            Grid.SetColumn(q5Stack, 0);
            row2.Children.Add(q5Stack);

            var qwStack = new StackPanel();
            qwStack.Children.Add(MakeTinyLabel(theme, "周进度条参考上限 (K tok)"));
            _quotaWeekKBox = CreateInputBox(theme, (_settings.QuotaWeekTokens / 1000).ToString(CultureInfo.InvariantCulture));
            qwStack.Children.Add(_quotaWeekKBox);
            Grid.SetColumn(qwStack, 2);
            row2.Children.Add(qwStack);
            genStack.Children.Add(row2);

            var row3 = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            row3.Children.Add(MakeFieldLabel(theme, "悬浮球透明度"));

            _opacitySlider = new Slider
            {
                Minimum = 0.50,
                Maximum = 1.00,
                Value = _settings.Opacity,
                TickFrequency = 0.05,
                IsSnapToTickEnabled = true,
                VerticalAlignment = VerticalAlignment.Center
            };
            _opacityValText = new TextBlock
            {
                Text = string.Format("{0:0}%", _settings.Opacity * 100),
                Foreground = theme.Brush(theme.TextSecondary),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            _opacitySlider.ValueChanged += (s, e) =>
            {
                _opacityValText.Text = string.Format("{0:0}%", _opacitySlider.Value * 100);
            };
            Grid.SetColumn(_opacitySlider, 1);
            Grid.SetColumn(_opacityValText, 2);
            row3.Children.Add(_opacitySlider);
            row3.Children.Add(_opacityValText);
            genStack.Children.Add(row3);

            _rootPanel.Children.Add(genCard);

            // === 3. COST ESTIMATION ===
            var costCard = CreateSectionCard(theme, "费用单价估算 (美元 / 每百万 Token)");
            var costStack = (StackPanel)costCard.Child;

            // Quick presets
            var presetRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 7) };
            presetRow.Children.Add(CreateSmallPresetBtn(theme, "Gemini Pro", () => SetPrices(1.25, 10.00, 0.125)));
            presetRow.Children.Add(CreateSmallPresetBtn(theme, "Gemini Flash", () => SetPrices(0.15, 0.60, 0.0375)));
            presetRow.Children.Add(CreateSmallPresetBtn(theme, "Claude Sonnet", () => SetPrices(3.00, 15.00, 0.30)));
            costStack.Children.Add(presetRow);

            var priceGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            for (int c = 0; c < 3; c++)
                priceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _priceInBox = CreateMiniLabeledBox(theme, priceGrid, 0, "输入 $/1M", _settings.PriceInputPerMillionUsd.ToString("0.####", CultureInfo.InvariantCulture));
            _priceOutBox = CreateMiniLabeledBox(theme, priceGrid, 1, "输出 $/1M", _settings.PriceOutputPerMillionUsd.ToString("0.####", CultureInfo.InvariantCulture));
            _priceCacheBox = CreateMiniLabeledBox(theme, priceGrid, 2, "缓存读 $/1M", _settings.PriceCacheReadPerMillionUsd.ToString("0.####", CultureInfo.InvariantCulture));
            costStack.Children.Add(priceGrid);

            var curRow = new Grid();
            curRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            curRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            curRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var curLeft = new StackPanel();
            curLeft.Children.Add(MakeTinyLabel(theme, "显示币种"));
            _currencyCombo = new ComboBox { Height = 25, FontSize = 11 };
            _currencyCombo.Items.Add(new ComboBoxItem { Content = "人民币 (¥ CNY)", Tag = "CNY" });
            _currencyCombo.Items.Add(new ComboBoxItem { Content = "美元 ($ USD)", Tag = "USD" });
            SelectComboByTag(_currencyCombo, _settings.Currency);
            curLeft.Children.Add(_currencyCombo);
            Grid.SetColumn(curLeft, 0);
            curRow.Children.Add(curLeft);

            var curRight = new StackPanel();
            curRight.Children.Add(MakeTinyLabel(theme, "USD → CNY 汇率"));
            _fxRateBox = CreateInputBox(theme, _settings.UsdToCnyRate.ToString("0.##", CultureInfo.InvariantCulture));
            curRight.Children.Add(_fxRateBox);
            Grid.SetColumn(curRight, 2);
            curRow.Children.Add(curRight);
            costStack.Children.Add(curRow);

            _rootPanel.Children.Add(costCard);

            // === 4. ALERTS ===
            var alertCard = CreateSectionCard(theme, "超限预警阈值 (填 0 表示关闭该项)");
            var alertStack = (StackPanel)alertCard.Child;

            var alertChkRow = new Grid { Margin = new Thickness(0, 6, 0, 6) };
            alertChkRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            alertChkRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _alertsEnableCheck = CreateCheckBox(theme, "启用视觉超限预警", _settings.AlertsEnabled);
            _balloonEnableCheck = CreateCheckBox(theme, "托盘气泡弹窗提醒", _settings.BalloonNotificationsEnabled);
            Grid.SetColumn(_alertsEnableCheck, 0);
            Grid.SetColumn(_balloonEnableCheck, 1);
            alertChkRow.Children.Add(_alertsEnableCheck);
            alertChkRow.Children.Add(_balloonEnableCheck);
            alertStack.Children.Add(alertChkRow);

            var threshGrid = new Grid();
            for (int c = 0; c < 3; c++)
                threshGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _ctxAlertKBox = CreateMiniLabeledBox(theme, threshGrid, 0, "上下文 (K tok)", (_settings.ContextAlertThreshold / 1000).ToString(CultureInfo.InvariantCulture));
            _rateAlertKBox = CreateMiniLabeledBox(theme, threshGrid, 1, "速率 (K/min)", (_settings.Rate1mAlertThreshold / 1000).ToString(CultureInfo.InvariantCulture));
            _todayAlertKBox = CreateMiniLabeledBox(theme, threshGrid, 2, "今日新增 (K)", (_settings.TodayTotalAlertThreshold / 1000).ToString(CultureInfo.InvariantCulture));
            alertStack.Children.Add(threshGrid);

            _rootPanel.Children.Add(alertCard);

            // === FOOTER ACTIONS ===
            var footer = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _statusMsg = new TextBlock
            {
                Text = "",
                Foreground = theme.Brush(theme.AccentActive),
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            footer.Children.Add(_statusMsg);

            var btnStack = new StackPanel { Orientation = Orientation.Horizontal };
            var cancelBtn = new Button
            {
                Content = "取消",
                Width = 68,
                Height = 30,
                Margin = new Thickness(0, 0, 8, 0),
                Background = theme.Brush(theme.TrackBg),
                Foreground = theme.Brush(theme.TextSecondary),
                BorderBrush = theme.Brush(theme.CardBorder),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            cancelBtn.Click += (s, e) => Close();

            var saveBtn = new Button
            {
                Content = "保存并应用",
                Width = 96,
                Height = 30,
                Background = theme.Brush(theme.AccentPrimary),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            saveBtn.Click += OnSaveClick;

            btnStack.Children.Add(cancelBtn);
            btnStack.Children.Add(saveBtn);
            Grid.SetColumn(btnStack, 1);
            footer.Children.Add(btnStack);
            _rootPanel.Children.Add(footer);
        }

        private void SetPrices(double pIn, double pOut, double pCache)
        {
            _priceInBox.Text = pIn.ToString("0.####", CultureInfo.InvariantCulture);
            _priceOutBox.Text = pOut.ToString("0.####", CultureInfo.InvariantCulture);
            _priceCacheBox.Text = pCache.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            double pIn, pOut, pCache, fx;
            long ctxK, rateK, todayK, q5hK, qWeekK;

            if (!TryParseDouble(_priceInBox.Text, out pIn) || pIn < 0 ||
                !TryParseDouble(_priceOutBox.Text, out pOut) || pOut < 0 ||
                !TryParseDouble(_priceCacheBox.Text, out pCache) || pCache < 0 ||
                !TryParseDouble(_fxRateBox.Text, out fx) || fx <= 0.1 ||
                !TryParseLong(_ctxAlertKBox.Text, out ctxK) || ctxK < 0 ||
                !TryParseLong(_rateAlertKBox.Text, out rateK) || rateK < 0 ||
                !TryParseLong(_todayAlertKBox.Text, out todayK) || todayK < 0 ||
                !TryParseLong(_quota5hKBox.Text, out q5hK) || q5hK <= 0 ||
                !TryParseLong(_quotaWeekKBox.Text, out qWeekK) || qWeekK <= 0)
            {
                _statusMsg.Foreground = ThemeManager.GetTheme(_settings.ThemeId).Brush(ThemeManager.GetTheme(_settings.ThemeId).AccentDanger);
                _statusMsg.Text = "请输入有效的数字（参考配额需大于 0）";
                return;
            }

            _settings.TopMost = _topMostCheck.IsChecked == true;
            _settings.AutoDockRightEdge = _autoDockCheck.IsChecked == true;
            _settings.QuotaGroupPreference = GetSelectedComboTag(_quotaGroupCombo, "auto");
            _settings.ShowRemainingPercent = string.Equals(GetSelectedComboTag(_quotaModeCombo, "used"), "remaining", StringComparison.OrdinalIgnoreCase);
            _settings.Opacity = _opacitySlider.Value;
            _settings.Quota5hTokens = q5hK * 1000L;
            _settings.QuotaWeekTokens = qWeekK * 1000L;

            _settings.PriceInputPerMillionUsd = pIn;
            _settings.PriceOutputPerMillionUsd = pOut;
            _settings.PriceCacheReadPerMillionUsd = pCache;
            _settings.Currency = GetSelectedComboTag(_currencyCombo, "CNY");
            _settings.UsdToCnyRate = fx;

            _settings.AlertsEnabled = _alertsEnableCheck.IsChecked == true;
            _settings.BalloonNotificationsEnabled = _balloonEnableCheck.IsChecked == true;
            _settings.ContextAlertThreshold = ctxK * 1000L;
            _settings.Rate1mAlertThreshold = rateK * 1000L;
            _settings.TodayTotalAlertThreshold = todayK * 1000L;

            SettingsManager.Save(_settings);
            SettingsManager.SetAutoStart(_autoStartCheck.IsChecked == true);

            if (_onApplied != null) _onApplied();
            Close();
        }

        private static bool TryParseDouble(string text, out double val)
        {
            return double.TryParse((text ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out val);
        }

        private static bool TryParseLong(string text, out long val)
        {
            return long.TryParse((text ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out val);
        }

        private static Border CreateSectionCard(ThemePalette theme, string title)
        {
            var card = new Border
            {
                Background = theme.Brush(theme.CardBg),
                BorderBrush = theme.Brush(theme.CardBorder),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 9, 12, 10),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = theme.Brush(theme.AccentPrimary),
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            });
            card.Child = stack;
            return card;
        }

        private static CheckBox CreateCheckBox(ThemePalette theme, string text, bool isChecked)
        {
            return new CheckBox
            {
                Content = text,
                IsChecked = isChecked,
                Foreground = theme.Brush(theme.TextSecondary),
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
                FontSize = 11.5,
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }

        private static TextBlock MakeFieldLabel(ThemePalette theme, string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = theme.Brush(theme.TextSecondary),
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static TextBlock MakeTinyLabel(ThemePalette theme, string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = theme.Brush(theme.TextMuted),
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 3)
            };
        }

        private static TextBox CreateInputBox(ThemePalette theme, string initial)
        {
            return new TextBox
            {
                Text = initial,
                Height = 25,
                Padding = new Thickness(6, 3, 6, 2),
                Background = theme.Brush(theme.TrackBg),
                Foreground = theme.Brush(theme.TextPrimary),
                BorderBrush = theme.Brush(theme.CardBorder),
                BorderThickness = new Thickness(1),
                CaretBrush = theme.Brush(theme.AccentPrimary),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11.5
            };
        }

        private static TextBox CreateMiniLabeledBox(ThemePalette theme, Grid grid, int col, string label, string value)
        {
            var sp = new StackPanel { Margin = new Thickness(col == 0 ? 0 : 4, 0, col == 2 ? 0 : 4, 0) };
            sp.Children.Add(MakeTinyLabel(theme, label));
            var tb = CreateInputBox(theme, value);
            sp.Children.Add(tb);
            Grid.SetColumn(sp, col);
            grid.Children.Add(sp);
            return tb;
        }

        private static Button CreateSmallPresetBtn(ThemePalette theme, string text, Action onClick)
        {
            var btn = new Button
            {
                Content = text,
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 0, 6, 0),
                Background = theme.Brush(theme.TrackBg),
                Foreground = theme.Brush(theme.TextSecondary),
                BorderBrush = theme.Brush(theme.CardBorder),
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"),
                FontSize = 10.5,
                Cursor = Cursors.Hand
            };
            btn.Click += (s, e) => { if (onClick != null) onClick(); };
            return btn;
        }

        private static void SelectComboByTag(ComboBox cb, string tag)
        {
            for (int i = 0; i < cb.Items.Count; i++)
            {
                var item = cb.Items[i] as ComboBoxItem;
                if (item != null && string.Equals(item.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
                {
                    cb.SelectedIndex = i;
                    return;
                }
            }
            if (cb.Items.Count > 0) cb.SelectedIndex = 0;
        }

        private static string GetSelectedComboTag(ComboBox cb, string fallback)
        {
            var item = cb.SelectedItem as ComboBoxItem;
            return item != null && item.Tag != null ? item.Tag.ToString() : fallback;
        }
    }
}
