using System;
using System.Collections.Generic;

namespace AntigravityTokenOrb
{
    public class TokenStepRecord
    {
        public string ConversationId { get; set; }
        public int StepIndex { get; set; }
        public string Type { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string Model { get; set; }
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
        public long CacheReadTokens { get; set; }

        public long EffectiveTokens { get { return InputTokens + OutputTokens; } }
        public long TotalAllTokens { get { return InputTokens + OutputTokens + CacheReadTokens; } }
        public long ContextWindowTokens { get { return InputTokens + CacheReadTokens; } }
    }

    public class ConversationStats
    {
        public string ConversationId { get; set; }
        public string Title { get; set; }
        public string LastModel { get; set; }
        public DateTime LastActivityUtc { get; set; }
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
        public long CacheReadTokens { get; set; }
        public int StepCount { get; set; }
        public long LatestContextTokens { get; set; }
        public long LatestStepNewTokens { get; set; }
        public long LatestStepOutputTokens { get; set; }

        public long EffectiveTokens { get { return InputTokens + OutputTokens; } }
        public long TotalAllTokens { get { return InputTokens + OutputTokens + CacheReadTokens; } }
        public double CacheHitRate
        {
            get
            {
                long denom = InputTokens + CacheReadTokens;
                return denom > 0 ? (double)CacheReadTokens / denom * 100.0 : 0.0;
            }
        }
    }

    public class ModelBreakdown
    {
        public string ModelName { get; set; }
        public string DisplayName { get; set; }
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
        public long CacheReadTokens { get; set; }
        public int StepCount { get; set; }

        public long EffectiveTokens { get { return InputTokens + OutputTokens; } }
        public long TotalAllTokens { get { return InputTokens + OutputTokens + CacheReadTokens; } }
    }

    public class ScopeAggregate
    {
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
        public long CacheReadTokens { get; set; }
        public int StepCount { get; set; }

        public long EffectiveTokens { get { return InputTokens + OutputTokens; } }
        public long TotalAllTokens { get { return InputTokens + OutputTokens + CacheReadTokens; } }
        public double CacheHitRate
        {
            get
            {
                long denom = InputTokens + CacheReadTokens;
                return denom > 0 ? (double)CacheReadTokens / denom * 100.0 : 0.0;
            }
        }
    }

    public class TokenSnapshot
    {
        public DateTime GeneratedAtUtc { get; set; }
        public bool IsActive { get; set; }
        public DateTime LastEventUtc { get; set; }

        // Rolling 1-minute rates (tokens per minute)
        public double Rate1mEffectivePerMin { get; set; }
        public double Rate1mAllPerMin { get; set; }
        public double Rate1mOutputPerMin { get; set; }
        public int StepsLast1m { get; set; }

        // Rolling 5-minute average rates (tokens per minute)
        public double Rate5mEffectivePerMin { get; set; }
        public double Rate5mAllPerMin { get; set; }
        public double Rate5mOutputPerMin { get; set; }
        public int StepsLast5m { get; set; }

        // 20-minute sparkline buckets (oldest to newest, 1 minute each)
        public long[] SparklineEffective20m { get; set; }
        public long[] SparklineAll20m { get; set; }
        public long[] SparklineOutput20m { get; set; }

        // Scope aggregates
        public ScopeAggregate Today { get; set; }
        public ScopeAggregate ActiveConversation { get; set; }
        public ScopeAggregate AllTime { get; set; }

        // Active conversation metadata
        public string ActiveConversationId { get; set; }
        public string ActiveConversationTitle { get; set; }
        public string ActiveModel { get; set; }
        public long LatestContextTokens { get; set; }
        public long LatestStepNewTokens { get; set; }
        public long LatestStepOutputTokens { get; set; }

        // Lists
        public List<ConversationStats> Conversations { get; set; }
        public List<ModelBreakdown> TodayModels { get; set; }
        public List<ModelBreakdown> AllTimeModels { get; set; }

        public TokenSnapshot()
        {
            SparklineEffective20m = new long[20];
            SparklineAll20m = new long[20];
            SparklineOutput20m = new long[20];
            Today = new ScopeAggregate();
            ActiveConversation = new ScopeAggregate();
            AllTime = new ScopeAggregate();
            Conversations = new List<ConversationStats>();
            TodayModels = new List<ModelBreakdown>();
            AllTimeModels = new List<ModelBreakdown>();
        }
    }

    public static class TokenFormat
    {
        public static string FormatCount(long value)
        {
            if (value < 0) value = 0;
            if (value >= 1000000000L)
                return string.Format("{0:0.##}B", value / 1000000000.0);
            if (value >= 1000000L)
                return string.Format("{0:0.##}M", value / 1000000.0);
            if (value >= 10000L)
                return string.Format("{0:0.#}K", value / 1000.0);
            if (value >= 1000L)
                return string.Format("{0:0.##}K", value / 1000.0);
            return value.ToString("N0");
        }

        public static string FormatCompact(double value)
        {
            if (value < 0) value = 0;
            if (value >= 1000000.0)
                return string.Format("{0:0.#}M", value / 1000000.0);
            if (value >= 10000.0)
                return string.Format("{0:0.#}K", value / 1000.0);
            if (value >= 1000.0)
                return string.Format("{0:0.#}K", value / 1000.0);
            if (value >= 10.0)
                return string.Format("{0:0}", value);
            if (value > 0.0)
                return string.Format("{0:0.#}", value);
            return "0";
        }

        public static string FormatRatePerMin(double perMin)
        {
            if (perMin <= 0.1) return "0/m";
            return FormatCompact(perMin) + "/m";
        }

        public static string FormatRatePerSec(double perMin)
        {
            double perSec = perMin / 60.0;
            if (perSec <= 0.05) return "0/s";
            if (perSec < 10.0) return string.Format("{0:0.0}/s", perSec);
            return FormatCompact(perSec) + "/s";
        }

        public static string FormatModelShort(string model)
        {
            if (string.IsNullOrEmpty(model)) return "Unknown";
            string m = model.Trim();
            if (m.StartsWith("gemini-", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = m.Split('-');
                var pretty = new List<string>();
                for (int i = 0; i < parts.Length; i++)
                {
                    if (string.IsNullOrEmpty(parts[i])) continue;
                    string p = parts[i];
                    if (p.Equals("tiered", StringComparison.OrdinalIgnoreCase)) continue;
                    pretty.Add(char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p.Substring(1) : ""));
                }
                return string.Join(" ", pretty.ToArray());
            }
            return m;
        }

        public static string FormatRelativeTime(DateTime utcTime)
        {
            if (utcTime == DateTime.MinValue) return "无记录";
            TimeSpan diff = DateTime.UtcNow - utcTime;
            if (diff.TotalSeconds < 15) return "刚刚";
            if (diff.TotalSeconds < 60) return string.Format("{0}秒前", (int)diff.TotalSeconds);
            if (diff.TotalMinutes < 60) return string.Format("{0}分钟前", (int)diff.TotalMinutes);
            if (diff.TotalHours < 24) return string.Format("{0}小时前", (int)diff.TotalHours);
            return string.Format("{0}天前", (int)diff.TotalDays);
        }

        public static double EstimateCostUsd(long inputTokens, long outputTokens, long cacheReadTokens, AppSettings settings)
        {
            if (settings == null) return 0.0;
            double inCost = (inputTokens / 1000000.0) * settings.PriceInputPerMillionUsd;
            double outCost = (outputTokens / 1000000.0) * settings.PriceOutputPerMillionUsd;
            double cacheCost = (cacheReadTokens / 1000000.0) * settings.PriceCacheReadPerMillionUsd;
            return Math.Max(0.0, inCost + outCost + cacheCost);
        }

        public static string FormatCost(double usdAmount, AppSettings settings)
        {
            bool isCny = settings != null && string.Equals(settings.Currency, "CNY", StringComparison.OrdinalIgnoreCase);
            double rate = (settings != null && settings.UsdToCnyRate > 0.1) ? settings.UsdToCnyRate : 7.25;
            double val = isCny ? usdAmount * rate : usdAmount;
            string sym = isCny ? "¥" : "$";
            if (val < 0.005 && val > 0) return sym + "<0.01";
            if (val >= 1000.0) return string.Format("{0}{1:N0}", sym, val);
            if (val >= 100.0) return string.Format("{0}{1:0.0}", sym, val);
            return string.Format("{0}{1:0.00}", sym, val);
        }

        public static string FormatCostCompact(double usdAmount, AppSettings settings)
        {
            bool isCny = settings != null && string.Equals(settings.Currency, "CNY", StringComparison.OrdinalIgnoreCase);
            double rate = (settings != null && settings.UsdToCnyRate > 0.1) ? settings.UsdToCnyRate : 7.25;
            double val = isCny ? usdAmount * rate : usdAmount;
            string sym = isCny ? "¥" : "$";
            if (val >= 100.0) return string.Format("{0}{1:0}", sym, val);
            if (val >= 10.0) return string.Format("{0}{1:0.0}", sym, val);
            return string.Format("{0}{1:0.00}", sym, val);
        }
    }

    public class AlertStatus
    {
        public bool ContextExceeded { get; set; }
        public bool RateExceeded { get; set; }
        public bool TodayExceeded { get; set; }
        public string ShortBadge { get; set; }
        public string BannerMessage { get; set; }

        public bool HasAny { get { return ContextExceeded || RateExceeded || TodayExceeded; } }

        public static AlertStatus Evaluate(TokenSnapshot snap, AppSettings settings)
        {
            var st = new AlertStatus();
            if (snap == null || settings == null || !settings.AlertsEnabled) return st;

            var msgs = new List<string>();
            if (settings.ContextAlertThreshold > 0 && snap.LatestContextTokens >= settings.ContextAlertThreshold)
            {
                st.ContextExceeded = true;
                msgs.Add(string.Format("上下文 {0} 超阈值 {1} (建议 /compact)",
                    TokenFormat.FormatCount(snap.LatestContextTokens),
                    TokenFormat.FormatCount(settings.ContextAlertThreshold)));
            }
            if (settings.Rate1mAlertThreshold > 0 && snap.Rate1mEffectivePerMin >= settings.Rate1mAlertThreshold)
            {
                st.RateExceeded = true;
                msgs.Add(string.Format("速率 {0} 超阈值 {1}",
                    TokenFormat.FormatRatePerMin(snap.Rate1mEffectivePerMin),
                    TokenFormat.FormatRatePerMin(settings.Rate1mAlertThreshold)));
            }
            if (settings.TodayTotalAlertThreshold > 0 && snap.Today.EffectiveTokens >= settings.TodayTotalAlertThreshold)
            {
                st.TodayExceeded = true;
                msgs.Add(string.Format("今日新增 {0} 超阈值 {1}",
                    TokenFormat.FormatCount(snap.Today.EffectiveTokens),
                    TokenFormat.FormatCount(settings.TodayTotalAlertThreshold)));
            }

            if (st.ContextExceeded) st.ShortBadge = "⚠ 上下文";
            else if (st.RateExceeded) st.ShortBadge = "⚠ 高速";
            else if (st.TodayExceeded) st.ShortBadge = "⚠ 达限";

            if (msgs.Count > 0)
                st.BannerMessage = "⚠ 预警: " + string.Join(" · ", msgs.ToArray());

            return st;
        }
    }
}
