using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace AntigravityTokenOrb
{
    public class AppSettings
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public bool HasSavedPosition { get; set; }
        public bool TopMost { get; set; }
        public double Opacity { get; set; }
        public bool IncludeCacheInPrimary { get; set; }
        public bool CompactPillMode { get; set; }
        public string OrbBottomMetric { get; set; } // "today", "active", "cost"
        public string SelectedScope { get; set; }   // "today", "active", "all"

        // Theme
        public string ThemeId { get; set; } // "neon", "emerald", "sunset", "frost"

        // Pricing estimation (USD per 1M tokens)
        public double PriceInputPerMillionUsd { get; set; }
        public double PriceOutputPerMillionUsd { get; set; }
        public double PriceCacheReadPerMillionUsd { get; set; }
        public string Currency { get; set; } // "USD" or "CNY"
        public double UsdToCnyRate { get; set; }

        // Alerts
        public bool AlertsEnabled { get; set; }
        public bool BalloonNotificationsEnabled { get; set; }
        public long ContextAlertThreshold { get; set; }     // tokens (0 = off)
        public long Rate1mAlertThreshold { get; set; }      // effective tok/min (0 = off)
        public long TodayTotalAlertThreshold { get; set; }  // effective tokens today (0 = off)

        public AppSettings()
        {
            HasSavedPosition = false;
            TopMost = true;
            Opacity = 0.95;
            IncludeCacheInPrimary = false;
            CompactPillMode = false;
            OrbBottomMetric = "today";
            SelectedScope = "today";
            ThemeId = "neon";

            // Sensible Gemini Pro/High tier default pricing ($/1M tokens)
            PriceInputPerMillionUsd = 1.25;
            PriceOutputPerMillionUsd = 10.00;
            PriceCacheReadPerMillionUsd = 0.125;
            Currency = "CNY";
            UsdToCnyRate = 7.25;

            // Sensible alert thresholds
            AlertsEnabled = true;
            BalloonNotificationsEnabled = true;
            ContextAlertThreshold = 150000;
            Rate1mAlertThreshold = 60000;
            TodayTotalAlertThreshold = 2000000;
        }
    }

    public static class SettingsManager
    {
        private const string StartupRegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupAppName = "AntigravityTokenOrb";

        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AntigravityTokenOrb");

        private static readonly string ConfigPath = Path.Combine(ConfigDir, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath, Encoding.UTF8);
                    var ser = new JavaScriptSerializer();
                    var loaded = ser.Deserialize<AppSettings>(json);
                    if (loaded != null)
                    {
                        if (loaded.Opacity < 0.35 || loaded.Opacity > 1.0) loaded.Opacity = 0.95;
                        if (string.IsNullOrEmpty(loaded.OrbBottomMetric)) loaded.OrbBottomMetric = "today";
                        if (string.IsNullOrEmpty(loaded.SelectedScope)) loaded.SelectedScope = "today";
                        if (string.IsNullOrEmpty(loaded.ThemeId)) loaded.ThemeId = "neon";
                        if (string.IsNullOrEmpty(loaded.Currency)) loaded.Currency = "CNY";
                        if (loaded.UsdToCnyRate <= 0.1) loaded.UsdToCnyRate = 7.25;
                        if (loaded.PriceInputPerMillionUsd < 0) loaded.PriceInputPerMillionUsd = 1.25;
                        if (loaded.PriceOutputPerMillionUsd < 0) loaded.PriceOutputPerMillionUsd = 10.0;
                        if (loaded.PriceCacheReadPerMillionUsd < 0) loaded.PriceCacheReadPerMillionUsd = 0.125;
                        return loaded;
                    }
                }
            }
            catch { }
            return new AppSettings();
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                    Directory.CreateDirectory(ConfigDir);
                var ser = new JavaScriptSerializer();
                string json = ser.Serialize(settings);
                File.WriteAllText(ConfigPath, json, Encoding.UTF8);
            }
            catch { }
        }

        public static bool IsAutoStartEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(StartupRegPath, false))
                {
                    if (key == null) return false;
                    object val = key.GetValue(StartupAppName);
                    return val != null && !string.IsNullOrEmpty(val.ToString());
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool SetAutoStart(bool enable)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(StartupRegPath, true))
                {
                    if (key == null) return false;
                    if (enable)
                    {
                        string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        key.SetValue(StartupAppName, "\"" + exePath + "\"");
                    }
                    else
                    {
                        if (key.GetValue(StartupAppName) != null)
                            key.DeleteValue(StartupAppName, false);
                    }
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
