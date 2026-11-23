using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace AntigravityTokenOrb
{
    /// <summary>
    /// Fast, allocation-light top-level JSON object scanner for Antigravity transcript.jsonl lines.
    /// Properly handles escaped strings and nested objects/arrays without allocating large content/thinking bodies.
    /// </summary>
    public static class JsonLineParser
    {
        public static bool TryParseTokenStep(string line, string conversationId, out TokenStepRecord record)
        {
            record = null;
            if (string.IsNullOrEmpty(line)) return false;

            int len = line.Length;
            int i = 0;
            SkipWs(line, ref i, len);
            if (i >= len || line[i] != '{') return false;
            i++;

            int stepIndex = -1;
            string type = null;
            string status = null;
            string createdAtStr = null;
            string model = null;
            long inputTokens = 0;
            long outputTokens = 0;
            long cacheReadTokens = 0;
            bool hasTokenField = false;

            while (i < len)
            {
                SkipWs(line, ref i, len);
                if (i >= len || line[i] == '}') break;
                if (line[i] == ',') { i++; continue; }
                if (line[i] != '"') break;

                // Read top-level key
                int keyStart = i + 1;
                string key = ReadJsonString(line, ref i, len);
                if (key == null) break;

                SkipWs(line, ref i, len);
                if (i >= len || line[i] != ':') break;
                i++;
                SkipWs(line, ref i, len);
                if (i >= len) break;

                switch (key)
                {
                    case "step_index":
                        long si;
                        if (ReadJsonLong(line, ref i, len, out si)) stepIndex = (int)si;
                        else SkipJsonValue(line, ref i, len);
                        break;
                    case "type":
                        if (line[i] == '"') type = ReadJsonString(line, ref i, len);
                        else SkipJsonValue(line, ref i, len);
                        break;
                    case "status":
                        if (line[i] == '"') status = ReadJsonString(line, ref i, len);
                        else SkipJsonValue(line, ref i, len);
                        break;
                    case "created_at":
                        if (line[i] == '"') createdAtStr = ReadJsonString(line, ref i, len);
                        else SkipJsonValue(line, ref i, len);
                        break;
                    case "model":
                        if (line[i] == '"') model = ReadJsonString(line, ref i, len);
                        else SkipJsonValue(line, ref i, len);
                        break;
                    case "input_tokens":
                        long it;
                        if (ReadJsonLong(line, ref i, len, out it)) { inputTokens = it; hasTokenField = true; }
                        else SkipJsonValue(line, ref i, len);
                        break;
                    case "output_tokens":
                        long ot;
                        if (ReadJsonLong(line, ref i, len, out ot)) { outputTokens = ot; hasTokenField = true; }
                        else SkipJsonValue(line, ref i, len);
                        break;
                    case "cache_read_tokens":
                        long ct;
                        if (ReadJsonLong(line, ref i, len, out ct)) { cacheReadTokens = ct; hasTokenField = true; }
                        else SkipJsonValue(line, ref i, len);
                        break;
                    default:
                        SkipJsonValue(line, ref i, len);
                        break;
                }
            }

            if (!hasTokenField && inputTokens == 0 && outputTokens == 0 && cacheReadTokens == 0)
                return false;

            DateTime createdUtc = DateTime.MinValue;
            if (!string.IsNullOrEmpty(createdAtStr))
            {
                DateTime parsed;
                if (DateTime.TryParse(
                    createdAtStr,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out parsed))
                {
                    createdUtc = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
                }
            }

            record = new TokenStepRecord
            {
                ConversationId = conversationId,
                StepIndex = stepIndex,
                Type = type ?? "",
                Status = status ?? "",
                CreatedAtUtc = createdUtc,
                Model = model ?? "",
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                CacheReadTokens = cacheReadTokens
            };
            return true;
        }

        private static void SkipWs(string s, ref int i, int len)
        {
            while (i < len)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n') i++;
                else break;
            }
        }

        private static string ReadJsonString(string s, ref int i, int len)
        {
            if (i >= len || s[i] != '"') return null;
            i++; // skip opening quote
            int start = i;
            bool hasEscape = false;
            while (i < len)
            {
                char c = s[i];
                if (c == '\\')
                {
                    hasEscape = true;
                    i += 2;
                    continue;
                }
                if (c == '"')
                {
                    int end = i;
                    i++; // skip closing quote
                    if (!hasEscape)
                        return s.Substring(start, end - start);
                    return UnescapeJsonString(s, start, end);
                }
                i++;
            }
            return null;
        }

        private static string UnescapeJsonString(string s, int start, int end)
        {
            var sb = new StringBuilder(end - start);
            for (int idx = start; idx < end; idx++)
            {
                char c = s[idx];
                if (c == '\\' && idx + 1 < end)
                {
                    idx++;
                    char n = s[idx];
                    switch (n)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (idx + 4 < end)
                            {
                                string hex = s.Substring(idx + 1, 4);
                                int code;
                                if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                                {
                                    sb.Append((char)code);
                                    idx += 4;
                                }
                            }
                            break;
                        default: sb.Append(n); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static bool ReadJsonLong(string s, ref int i, int len, out long value)
        {
            value = 0;
            int start = i;
            if (i < len && s[i] == '-') i++;
            int digitStart = i;
            while (i < len && s[i] >= '0' && s[i] <= '9') i++;
            if (i == digitStart)
            {
                i = start;
                return false;
            }
            string numStr = s.Substring(start, i - start);
            return long.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static void SkipJsonValue(string s, ref int i, int len)
        {
            if (i >= len) return;
            char c = s[i];
            if (c == '"')
            {
                SkipJsonString(s, ref i, len);
                return;
            }
            if (c == '{' || c == '[')
            {
                int depth = 0;
                while (i < len)
                {
                    char ch = s[i];
                    if (ch == '"')
                    {
                        SkipJsonString(s, ref i, len);
                        continue;
                    }
                    if (ch == '{' || ch == '[') depth++;
                    else if (ch == '}' || ch == ']')
                    {
                        depth--;
                        i++;
                        if (depth <= 0) return;
                        continue;
                    }
                    i++;
                }
                return;
            }
            // primitive: number, true, false, null
            while (i < len)
            {
                char ch = s[i];
                if (ch == ',' || ch == '}' || ch == ']' || ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n')
                    break;
                i++;
            }
        }

        private static void SkipJsonString(string s, ref int i, int len)
        {
            if (i >= len || s[i] != '"') return;
            i++;
            while (i < len)
            {
                char c = s[i];
                if (c == '\\')
                {
                    i += 2;
                    continue;
                }
                if (c == '"')
                {
                    i++;
                    return;
                }
                i++;
            }
        }

        /// <summary>
        /// Reads the conversation title from annotations/<id>.pbtxt if available.
        /// </summary>
        public static string ReadAnnotationTitle(string pbtxtPath)
        {
            try
            {
                if (!File.Exists(pbtxtPath)) return null;
                string text = File.ReadAllText(pbtxtPath, Encoding.UTF8);
                int idx = text.IndexOf("title:", StringComparison.Ordinal);
                if (idx < 0) return null;
                idx += 6;
                while (idx < text.Length && (text[idx] == ' ' || text[idx] == '\t')) idx++;
                if (idx >= text.Length || text[idx] != '"') return null;
                idx++;

                // Decode protobuf text string (UTF-8 literal or octal escapes)
                var bytes = new System.Collections.Generic.List<byte>();
                while (idx < text.Length)
                {
                    char c = text[idx];
                    if (c == '"') break;
                    if (c == '\\' && idx + 1 < text.Length)
                    {
                        char next = text[idx + 1];
                        if (next >= '0' && next <= '7' && idx + 3 < text.Length)
                        {
                            string oct = text.Substring(idx + 1, 3);
                            try
                            {
                                bytes.Add(Convert.ToByte(oct, 8));
                                idx += 4;
                                continue;
                            }
                            catch { }
                        }
                        idx++;
                        switch (text[idx])
                        {
                            case 'n': bytes.Add((byte)'\n'); break;
                            case 'r': bytes.Add((byte)'\r'); break;
                            case 't': bytes.Add((byte)'\t'); break;
                            case '"': bytes.Add((byte)'"'); break;
                            case '\\': bytes.Add((byte)'\\'); break;
                            default:
                                byte[] chb = Encoding.UTF8.GetBytes(new char[] { text[idx] });
                                bytes.AddRange(chb);
                                break;
                        }
                        idx++;
                    }
                    else
                    {
                        byte[] chb = Encoding.UTF8.GetBytes(new char[] { c });
                        bytes.AddRange(chb);
                        idx++;
                    }
                }
                string result = Encoding.UTF8.GetString(bytes.ToArray()).Trim();
                return string.IsNullOrEmpty(result) ? null : result;
            }
            catch
            {
                return null;
            }
        }
    }
}
