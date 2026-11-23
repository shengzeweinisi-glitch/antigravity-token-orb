using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AntigravityTokenOrb
{
    public class TokenMonitorEngine
    {
        private class ConversationCache
        {
            public string ConversationId;
            public string TranscriptPath;
            public string AnnotationPath;
            public string Title;
            public DateTime TitleLastWriteUtc;
            public long LastReadOffset;
            public DateTime LastWriteUtc;
            public Dictionary<int, TokenStepRecord> Steps = new Dictionary<int, TokenStepRecord>();
            public int FallbackStepCounter;
        }

        public string AntigravityRoot { get; private set; }
        public string BrainDir { get; private set; }
        public string AnnotationsDir { get; private set; }

        private readonly Dictionary<string, ConversationCache> _cache =
            new Dictionary<string, ConversationCache>(StringComparer.OrdinalIgnoreCase);

        private readonly object _lock = new object();
        private DateTime _lastFileModifiedUtc = DateTime.MinValue;

        public TokenMonitorEngine(string customRoot = null)
        {
            if (!string.IsNullOrEmpty(customRoot))
            {
                AntigravityRoot = customRoot;
            }
            else
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                AntigravityRoot = Path.Combine(Path.Combine(userProfile, ".gemini"), "antigravity");
            }
            BrainDir = Path.Combine(AntigravityRoot, "brain");
            AnnotationsDir = Path.Combine(AntigravityRoot, "annotations");
        }

        public TokenSnapshot RefreshAndBuildSnapshot(string pinnedConversationId = null)
        {
            lock (_lock)
            {
                ScanBrainDirectory();
                return BuildSnapshot(pinnedConversationId);
            }
        }

        private void ScanBrainDirectory()
        {
            if (!Directory.Exists(BrainDir)) return;

            string[] dirs;
            try
            {
                dirs = Directory.GetDirectories(BrainDir);
            }
            catch
            {
                return;
            }

            foreach (string dir in dirs)
            {
                string convId = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(convId)) continue;
                if (convId.Equals("tempmediaStorage", StringComparison.OrdinalIgnoreCase)) continue;

                string transcriptPath = Path.Combine(
                    Path.Combine(Path.Combine(dir, ".system_generated"), "logs"),
                    "transcript.jsonl");

                if (!File.Exists(transcriptPath)) continue;

                ConversationCache entry;
                if (!_cache.TryGetValue(convId, out entry))
                {
                    entry = new ConversationCache
                    {
                        ConversationId = convId,
                        TranscriptPath = transcriptPath,
                        AnnotationPath = Path.Combine(AnnotationsDir, convId + ".pbtxt")
                    };
                    _cache[convId] = entry;
                }

                RefreshTitleIfNeeded(entry);
                RefreshTranscriptIfNeeded(entry);
            }
        }

        private void RefreshTitleIfNeeded(ConversationCache entry)
        {
            try
            {
                if (!File.Exists(entry.AnnotationPath)) return;
                DateTime wt = File.GetLastWriteTimeUtc(entry.AnnotationPath);
                if (wt != entry.TitleLastWriteUtc)
                {
                    string title = JsonLineParser.ReadAnnotationTitle(entry.AnnotationPath);
                    if (!string.IsNullOrEmpty(title))
                        entry.Title = title;
                    entry.TitleLastWriteUtc = wt;
                }
            }
            catch { }
        }

        private void RefreshTranscriptIfNeeded(ConversationCache entry)
        {
            try
            {
                var fi = new FileInfo(entry.TranscriptPath);
                if (!fi.Exists) return;

                long fileLen = fi.Length;
                DateTime writeUtc = fi.LastWriteTimeUtc;

                if (fileLen == entry.LastReadOffset && writeUtc == entry.LastWriteUtc)
                    return;

                if (writeUtc > _lastFileModifiedUtc)
                    _lastFileModifiedUtc = writeUtc;

                bool fullReset = fileLen < entry.LastReadOffset;
                long startOffset = fullReset ? 0 : entry.LastReadOffset;
                if (fullReset)
                {
                    entry.Steps.Clear();
                    entry.FallbackStepCounter = 0;
                    entry.LastReadOffset = 0;
                }

                // Read only complete newline-terminated chunks from startOffset
                using (var fs = new FileStream(
                    entry.TranscriptPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                {
                    if (startOffset > fs.Length)
                    {
                        startOffset = 0;
                        entry.Steps.Clear();
                    }

                    long bytesToRead = fs.Length - startOffset;
                    if (bytesToRead <= 0)
                    {
                        entry.LastWriteUtc = writeUtc;
                        return;
                    }

                    fs.Seek(startOffset, SeekOrigin.Begin);
                    byte[] buffer = new byte[bytesToRead];
                    int totalRead = 0;
                    while (totalRead < buffer.Length)
                    {
                        int r = fs.Read(buffer, totalRead, buffer.Length - totalRead);
                        if (r <= 0) break;
                        totalRead += r;
                    }

                    // Find last newline to avoid parsing half-written trailing JSON line
                    int lastNewline = -1;
                    for (int i = totalRead - 1; i >= 0; i--)
                    {
                        if (buffer[i] == (byte)'\n')
                        {
                            lastNewline = i;
                            break;
                        }
                    }

                    int validBytes = lastNewline >= 0 ? (lastNewline + 1) : totalRead;
                    // If file does not end with newline yet, check if the chunk ends with '}'
                    if (lastNewline < 0)
                    {
                        int trimEnd = totalRead - 1;
                        while (trimEnd >= 0 && (buffer[trimEnd] == ' ' || buffer[trimEnd] == '\r' || buffer[trimEnd] == '\t'))
                            trimEnd--;
                        if (trimEnd < 0 || buffer[trimEnd] != (byte)'}')
                        {
                            // Wait for next poll when line completes
                            return;
                        }
                    }

                    string chunk = Encoding.UTF8.GetString(buffer, 0, validBytes);
                    int lineStart = 0;
                    for (int i = 0; i <= chunk.Length; i++)
                    {
                        if (i == chunk.Length || chunk[i] == '\n')
                        {
                            int lineLen = i - lineStart;
                            if (lineLen > 0)
                            {
                                string line = chunk.Substring(lineStart, lineLen).Trim();
                                if (line.Length > 2 && line[0] == '{')
                                {
                                    TokenStepRecord rec;
                                    if (JsonLineParser.TryParseTokenStep(line, entry.ConversationId, out rec))
                                    {
                                        int key = rec.StepIndex >= 0 ? rec.StepIndex : (1000000 + entry.FallbackStepCounter++);
                                        entry.Steps[key] = rec;
                                    }
                                }
                            }
                            lineStart = i + 1;
                        }
                    }

                    entry.LastReadOffset = startOffset + validBytes;
                    entry.LastWriteUtc = writeUtc;
                }
            }
            catch { }
        }

        private TokenSnapshot BuildSnapshot(string pinnedConversationId)
        {
            DateTime nowUtc = DateTime.UtcNow;
            DateTime todayStartUtc = DateTime.Today.ToUniversalTime();
            DateTime win5hStart = nowUtc.AddHours(-5);
            DateTime win7dStart = nowUtc.AddDays(-7);
            DateTime win1mStart = nowUtc.AddSeconds(-60);
            DateTime win5mStart = nowUtc.AddMinutes(-5);
            DateTime win20mStart = nowUtc.AddMinutes(-20);

            var snap = new TokenSnapshot
            {
                GeneratedAtUtc = nowUtc,
                LastEventUtc = DateTime.MinValue
            };

            long w1Eff = 0, w1All = 0, w1Out = 0;
            long w5Eff = 0, w5All = 0, w5Out = 0;
            int w1Steps = 0, w5Steps = 0;

            var todayModels = new Dictionary<string, ModelBreakdown>(StringComparer.OrdinalIgnoreCase);
            var allModels = new Dictionary<string, ModelBreakdown>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in _cache)
            {
                ConversationCache conv = kv.Value;
                if (conv.Steps.Count == 0) continue;

                var cstat = new ConversationStats
                {
                    ConversationId = conv.ConversationId,
                    Title = !string.IsNullOrEmpty(conv.Title)
                        ? conv.Title
                        : ("会话 " + conv.ConversationId.Substring(0, Math.Min(8, conv.ConversationId.Length))),
                    LastActivityUtc = DateTime.MinValue
                };

                int maxStepIdx = -1;
                DateTime latestStepTime = DateTime.MinValue;

                foreach (var step in conv.Steps.Values)
                {
                    cstat.InputTokens += step.InputTokens;
                    cstat.OutputTokens += step.OutputTokens;
                    cstat.CacheReadTokens += step.CacheReadTokens;
                    cstat.StepCount++;

                    if (step.CreatedAtUtc > cstat.LastActivityUtc)
                        cstat.LastActivityUtc = step.CreatedAtUtc;

                    if (step.CreatedAtUtc > latestStepTime || (step.CreatedAtUtc == latestStepTime && step.StepIndex > maxStepIdx))
                    {
                        latestStepTime = step.CreatedAtUtc;
                        maxStepIdx = step.StepIndex;
                        cstat.LastModel = step.Model;
                        cstat.LatestContextTokens = step.ContextWindowTokens;
                        cstat.LatestStepNewTokens = step.EffectiveTokens;
                        cstat.LatestStepOutputTokens = step.OutputTokens;
                    }

                    if (step.CreatedAtUtc > snap.LastEventUtc)
                        snap.LastEventUtc = step.CreatedAtUtc;

                    // All-time aggregate
                    snap.AllTime.InputTokens += step.InputTokens;
                    snap.AllTime.OutputTokens += step.OutputTokens;
                    snap.AllTime.CacheReadTokens += step.CacheReadTokens;
                    snap.AllTime.StepCount++;
                    AddModelStats(allModels, step);

                    // Last 5 hours rolling window
                    if (step.CreatedAtUtc >= win5hStart && step.CreatedAtUtc <= nowUtc.AddSeconds(5))
                    {
                        snap.Last5Hours.InputTokens += step.InputTokens;
                        snap.Last5Hours.OutputTokens += step.OutputTokens;
                        snap.Last5Hours.CacheReadTokens += step.CacheReadTokens;
                        snap.Last5Hours.StepCount++;
                    }

                    // Last 7 days (weekly) rolling window
                    if (step.CreatedAtUtc >= win7dStart && step.CreatedAtUtc <= nowUtc.AddSeconds(5))
                    {
                        snap.Last7Days.InputTokens += step.InputTokens;
                        snap.Last7Days.OutputTokens += step.OutputTokens;
                        snap.Last7Days.CacheReadTokens += step.CacheReadTokens;
                        snap.Last7Days.StepCount++;
                    }

                    // Today aggregate
                    if (step.CreatedAtUtc >= todayStartUtc)
                    {
                        snap.Today.InputTokens += step.InputTokens;
                        snap.Today.OutputTokens += step.OutputTokens;
                        snap.Today.CacheReadTokens += step.CacheReadTokens;
                        snap.Today.StepCount++;
                        AddModelStats(todayModels, step);
                    }

                    // Rolling 1-minute window
                    if (step.CreatedAtUtc >= win1mStart && step.CreatedAtUtc <= nowUtc.AddSeconds(5))
                    {
                        w1Eff += step.EffectiveTokens;
                        w1All += step.TotalAllTokens;
                        w1Out += step.OutputTokens;
                        w1Steps++;
                    }

                    // Rolling 5-minute window
                    if (step.CreatedAtUtc >= win5mStart && step.CreatedAtUtc <= nowUtc.AddSeconds(5))
                    {
                        w5Eff += step.EffectiveTokens;
                        w5All += step.TotalAllTokens;
                        w5Out += step.OutputTokens;
                        w5Steps++;
                    }

                    // 20-minute sparkline buckets (index 19 = latest minute, 0 = 19..20 mins ago)
                    if (step.CreatedAtUtc >= win20mStart && step.CreatedAtUtc <= nowUtc.AddSeconds(5))
                    {
                        double ageSecs = (nowUtc - step.CreatedAtUtc).TotalSeconds;
                        if (ageSecs < 0) ageSecs = 0;
                        int minsAgo = (int)(ageSecs / 60.0);
                        int bucket = 19 - minsAgo;
                        if (bucket >= 0 && bucket < 20)
                        {
                            snap.SparklineEffective20m[bucket] += step.EffectiveTokens;
                            snap.SparklineAll20m[bucket] += step.TotalAllTokens;
                            snap.SparklineOutput20m[bucket] += step.OutputTokens;
                        }
                    }
                }

                snap.Conversations.Add(cstat);
            }

            snap.Conversations.Sort((a, b) => b.LastActivityUtc.CompareTo(a.LastActivityUtc));

            snap.Rate1mEffectivePerMin = w1Eff;
            snap.Rate1mAllPerMin = w1All;
            snap.Rate1mOutputPerMin = w1Out;
            snap.StepsLast1m = w1Steps;

            snap.Rate5mEffectivePerMin = w5Eff / 5.0;
            snap.Rate5mAllPerMin = w5All / 5.0;
            snap.Rate5mOutputPerMin = w5Out / 5.0;
            snap.StepsLast5m = w5Steps;

            // Active status: step in last 45s or transcript updated in last 15s
            bool recentStep = snap.LastEventUtc != DateTime.MinValue && (nowUtc - snap.LastEventUtc).TotalSeconds <= 45;
            bool recentFile = _lastFileModifiedUtc != DateTime.MinValue && (nowUtc - _lastFileModifiedUtc).TotalSeconds <= 15;
            snap.IsActive = recentStep || recentFile;

            // Pick active (or pinned) conversation
            ConversationStats selectedConv = null;
            if (!string.IsNullOrEmpty(pinnedConversationId))
            {
                foreach (var c in snap.Conversations)
                {
                    if (c.ConversationId.Equals(pinnedConversationId, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedConv = c;
                        break;
                    }
                }
            }
            if (selectedConv == null && snap.Conversations.Count > 0)
                selectedConv = snap.Conversations[0];

            if (selectedConv != null)
            {
                snap.ActiveConversationId = selectedConv.ConversationId;
                snap.ActiveConversationTitle = selectedConv.Title;
                snap.ActiveModel = selectedConv.LastModel;
                snap.LatestContextTokens = selectedConv.LatestContextTokens;
                snap.LatestStepNewTokens = selectedConv.LatestStepNewTokens;
                snap.LatestStepOutputTokens = selectedConv.LatestStepOutputTokens;
                snap.ActiveConversation.InputTokens = selectedConv.InputTokens;
                snap.ActiveConversation.OutputTokens = selectedConv.OutputTokens;
                snap.ActiveConversation.CacheReadTokens = selectedConv.CacheReadTokens;
                snap.ActiveConversation.StepCount = selectedConv.StepCount;
            }

            snap.TodayModels = SortModels(todayModels);
            snap.AllTimeModels = SortModels(allModels);

            return snap;
        }

        private static void AddModelStats(Dictionary<string, ModelBreakdown> map, TokenStepRecord step)
        {
            string m = string.IsNullOrEmpty(step.Model) ? "unknown" : step.Model;
            ModelBreakdown mb;
            if (!map.TryGetValue(m, out mb))
            {
                mb = new ModelBreakdown
                {
                    ModelName = m,
                    DisplayName = TokenFormat.FormatModelShort(m)
                };
                map[m] = mb;
            }
            mb.InputTokens += step.InputTokens;
            mb.OutputTokens += step.OutputTokens;
            mb.CacheReadTokens += step.CacheReadTokens;
            mb.StepCount++;
        }

        private static List<ModelBreakdown> SortModels(Dictionary<string, ModelBreakdown> map)
        {
            var list = new List<ModelBreakdown>(map.Values);
            list.Sort((a, b) => b.EffectiveTokens.CompareTo(a.EffectiveTokens));
            return list;
        }
    }
}
