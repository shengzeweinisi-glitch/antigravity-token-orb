using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace AntigravityTokenOrb
{
    public class QuotaBucket
    {
        public bool HasData { get; set; }
        public double RemainingPercent { get; set; }
        public double UsedPercent { get { return Math.Max(0.0, Math.Min(100.0, 100.0 - RemainingPercent)); } }
        public DateTime ResetTimeUtc { get; set; }

        public string FormatResetCountdown()
        {
            if (!HasData || ResetTimeUtc == DateTime.MinValue) return "--";
            TimeSpan left = ResetTimeUtc - DateTime.UtcNow;
            if (left.TotalSeconds <= 0) return "即将重置";
            if (left.TotalDays >= 1.0)
                return string.Format("{0}天{1}小时后重置", (int)left.TotalDays, left.Hours);
            if (left.TotalHours >= 1.0)
                return string.Format("{0}小时{1}分后重置", (int)left.TotalHours, left.Minutes);
            return string.Format("{0}分钟后重置", Math.Max(1, (int)left.TotalMinutes));
        }

        public string FormatResetShort()
        {
            if (!HasData || ResetTimeUtc == DateTime.MinValue) return "--";
            TimeSpan left = ResetTimeUtc - DateTime.UtcNow;
            if (left.TotalSeconds <= 0) return "即将重置";
            if (left.TotalDays >= 1.0)
                return string.Format("{0}d{1}h后重置", (int)left.TotalDays, left.Hours);
            if (left.TotalHours >= 1.0)
                return string.Format("{0}h{1}m后重置", (int)left.TotalHours, left.Minutes);
            return string.Format("{0}m后重置", Math.Max(1, (int)left.TotalMinutes));
        }
    }

    public class QuotaGroupSnapshot
    {
        public string GroupName { get; set; }
        public QuotaBucket FiveHour { get; set; }
        public QuotaBucket Weekly { get; set; }

        public QuotaGroupSnapshot(string name)
        {
            GroupName = name;
            FiveHour = new QuotaBucket();
            Weekly = new QuotaBucket();
        }

        public bool HasAny { get { return FiveHour.HasData || Weekly.HasData; } }
    }

    public class OfficialQuotaSnapshot
    {
        public bool Available { get; set; }
        public bool IsRefreshing { get; set; }
        public DateTime LastSyncedUtc { get; set; }
        public string StatusMessage { get; set; }

        public QuotaGroupSnapshot Gemini { get; set; }
        public QuotaGroupSnapshot ClaudeGpt { get; set; }

        public OfficialQuotaSnapshot()
        {
            Gemini = new QuotaGroupSnapshot("Gemini Models");
            ClaudeGpt = new QuotaGroupSnapshot("Claude and GPT models");
            StatusMessage = "等待同步...";
        }

        public QuotaGroupSnapshot PickGroup(string preferredGroup, string activeModel)
        {
            if (string.Equals(preferredGroup, "gemini", StringComparison.OrdinalIgnoreCase))
                return Gemini.HasAny ? Gemini : ClaudeGpt;
            if (string.Equals(preferredGroup, "claude", StringComparison.OrdinalIgnoreCase))
                return ClaudeGpt.HasAny ? ClaudeGpt : Gemini;

            // Auto mode: pick Claude/GPT group if active model is claude/gpt, else Gemini
            if (!string.IsNullOrEmpty(activeModel))
            {
                string m = activeModel.ToLowerInvariant();
                if ((m.Contains("claude") || m.Contains("gpt") || m.Contains("opus") || m.Contains("sonnet")) && ClaudeGpt.HasAny)
                    return ClaudeGpt;
            }
            return Gemini.HasAny ? Gemini : ClaudeGpt;
        }
    }

    /// <summary>
    /// Queries official Antigravity 5-hour and weekly quota percentages via the documented
    /// `agy -p /usage` CLI command asynchronously (zero LLM token cost, non-blocking).
    /// </summary>
    public class QuotaSyncEngine
    {
        private readonly object _lock = new object();
        private OfficialQuotaSnapshot _latest = new OfficialQuotaSnapshot();
        private int _isRunning;
        private DateTime _lastAttemptUtc = DateTime.MinValue;
        private DateTime _lastObservedStepUtc = DateTime.MinValue;

        public OfficialQuotaSnapshot GetSnapshot()
        {
            lock (_lock)
            {
                return _latest;
            }
        }

        public void TriggerSyncIfDue(bool isActive, DateTime latestStepUtc, bool force = false)
        {
            DateTime now = DateTime.UtcNow;

            bool newStepArrived = latestStepUtc != DateTime.MinValue && latestStepUtc > _lastObservedStepUtc;
            if (newStepArrived)
                _lastObservedStepUtc = latestStepUtc;

            if (!force)
            {
                if (_lastAttemptUtc != DateTime.MinValue)
                {
                    double elapsedSec = (now - _lastAttemptUtc).TotalSeconds;
                    // On new step, refresh at most once every 30s; otherwise every 75s when active, 180s when idle
                    double minInterval = newStepArrived ? 30.0 : (isActive ? 75.0 : 180.0);
                    if (elapsedSec < minInterval) return;
                }
            }

            if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
                return;

            _lastAttemptUtc = now;
            lock (_lock) { _latest.IsRefreshing = true; }

            ThreadPool.QueueUserWorkItem(_ => RunAgyUsageWorker());
        }

        public OfficialQuotaSnapshot SyncBlockingForTest()
        {
            RunAgyUsageWorker();
            return GetSnapshot();
        }

        private void RunAgyUsageWorker()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "agy.exe",
                    Arguments = "-p /usage",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };

                string stdout = "";
                string stderr = "";
                int exitCode = -1;

                using (var proc = Process.Start(psi))
                {
                    if (proc == null)
                    {
                        SetFailure("无法启动 agy.exe");
                        return;
                    }

                    // Read stdout & stderr safely to avoid deadlock
                    var outBuilder = new System.Text.StringBuilder();
                    var errBuilder = new System.Text.StringBuilder();
                    proc.OutputDataReceived += (s, e) => { if (e.Data != null) outBuilder.AppendLine(e.Data); };
                    proc.ErrorDataReceived += (s, e) => { if (e.Data != null) errBuilder.AppendLine(e.Data); };
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();

                    if (!proc.WaitForExit(14000))
                    {
                        try { proc.Kill(); } catch { }
                        SetFailure("agy /usage 响应超时");
                        return;
                    }
                    proc.WaitForExit(); // flush async output buffers
                    exitCode = proc.ExitCode;
                    stdout = outBuilder.ToString();
                    stderr = errBuilder.ToString();
                }

                var parsed = ParseUsageOutput(stdout);
                if (parsed.Available)
                {
                    lock (_lock)
                    {
                        _latest = parsed;
                    }
                }
                else
                {
                    string reason = !string.IsNullOrWhiteSpace(stderr)
                        ? stderr.Trim().Split('\n')[0]
                        : string.Format("未解析到配额数据 (exit {0})", exitCode);
                    SetFailure(reason);
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                SetFailure("未检测到 agy CLI（使用本地估算）");
            }
            catch (Exception ex)
            {
                SetFailure("同步异常: " + ex.Message);
            }
            finally
            {
                lock (_lock) { _latest.IsRefreshing = false; }
                Interlocked.Exchange(ref _isRunning, 0);
            }
        }

        private void SetFailure(string msg)
        {
            lock (_lock)
            {
                // Keep last successful data if we already had one, just update status message
                _latest.IsRefreshing = false;
                if (!_latest.Available)
                    _latest.StatusMessage = msg;
            }
        }

        public static OfficialQuotaSnapshot ParseUsageOutput(string raw)
        {
            var snap = new OfficialQuotaSnapshot();
            if (string.IsNullOrWhiteSpace(raw)) return snap;

            string[] lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // Expected tab-separated format:
                // Gemini Models\tWeekly Limit Remaining\t98%\t2026-12-01T03:13:08Z
                // Gemini Models\tFive Hour Limit Remaining\t93%\t2026-11-24T08:13:08Z
                string[] parts = line.Split('\t');
                if (parts.Length < 3) continue;

                string group = parts[0].Trim();
                string metric = parts[1].Trim();
                string pctText = parts[2].Trim().TrimEnd('%').Trim();
                string resetText = parts.Length >= 4 ? parts[3].Trim() : "";

                double pct;
                if (!double.TryParse(pctText, NumberStyles.Float, CultureInfo.InvariantCulture, out pct))
                    continue;

                DateTime resetUtc = DateTime.MinValue;
                if (!string.IsNullOrEmpty(resetText))
                {
                    DateTime parsedDt;
                    if (DateTime.TryParse(
                        resetText,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                        out parsedDt))
                    {
                        resetUtc = DateTime.SpecifyKind(parsedDt, DateTimeKind.Utc);
                    }
                }

                string groupLower = group.ToLowerInvariant();
                QuotaGroupSnapshot targetGroup = null;
                if (groupLower.Contains("gemini"))
                    targetGroup = snap.Gemini;
                else if (groupLower.Contains("claude") || groupLower.Contains("gpt"))
                    targetGroup = snap.ClaudeGpt;

                if (targetGroup == null) continue;

                string metricLower = metric.ToLowerInvariant();
                bool isRemaining = metricLower.Contains("remaining");
                double remainingPct = isRemaining ? pct : Math.Max(0.0, 100.0 - pct);

                if (metricLower.Contains("five hour") || metricLower.Contains("5 hour") || metricLower.Contains("5-hour"))
                {
                    targetGroup.FiveHour = new QuotaBucket
                    {
                        HasData = true,
                        RemainingPercent = Math.Max(0.0, Math.Min(100.0, remainingPct)),
                        ResetTimeUtc = resetUtc
                    };
                    snap.Available = true;
                }
                else if (metricLower.Contains("week"))
                {
                    targetGroup.Weekly = new QuotaBucket
                    {
                        HasData = true,
                        RemainingPercent = Math.Max(0.0, Math.Min(100.0, remainingPct)),
                        ResetTimeUtc = resetUtc
                    };
                    snap.Available = true;
                }
            }

            if (snap.Available)
            {
                snap.LastSyncedUtc = DateTime.UtcNow;
                snap.StatusMessage = "官方实时配额已同步";
            }
            return snap;
        }
    }
}
