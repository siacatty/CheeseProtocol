using System.Collections.Generic;
using Verse;

namespace CheeseProtocol
{
    public static class CheeseLog
    {
        // ----------------------------
        // Public config
        // ----------------------------

        /// <summary>Default prefix added to every log line.</summary>
        public static string Signature = "[CheeseProtocol]";

        public enum Channel
        {
            General,
            Verse,   // RimWorld/Verse-related
            Net,     // WebSocket/network
            Debug,   // Dev/test
        }

        public enum Level
        {
            Trace = 0,
            Message = 1,
            Warning = 2,
            Error = 3,
        }

        /// <summary>Logs below this level are suppressed (applies to queued + immediate helpers here).</summary>
        public static Level MinLevel = Level.Message;

        /// <summary>Coalesce only consecutive identical messages (same channel+level+formatted text).</summary>
        public static bool EnableDuplicateCoalescing = true;

        /// <summary>Enable per-key rate limiting helpers (Q*RL).</summary>
        public static bool EnableRateLimit = false;

        /// <summary>Max queued entries (hard cap). When exceeded, lines are dropped and summarized.</summary>
        public static int MaxQueue = 3000;

        // ----------------------------
        // Internal state
        // ----------------------------

        private struct Entry
        {
            public Channel channel;
            public Level level;
            public string msg;   // already formatted with signature + channel tag
            public int count;    // duplicate coalescing multiplier
            public int firstTick;
        }

        private static readonly object _lock = new object();
        private static readonly Queue<Entry> _q = new Queue<Entry>(256);
        private static int _dropped;

        // consecutive coalescing buffer
        private static bool _hasLast;
        private static Entry _last;

        // Per-channel enable flags
        private static readonly Dictionary<Channel, bool> _channelEnabled = new Dictionary<Channel, bool>
        {
            { Channel.General, true },
            { Channel.Verse,   true },
            { Channel.Net,     true },
            { Channel.Debug,   false }, // default off; enable when Prefs.DevMode if you like
        };

        // Optional rate limit
        private struct RateState { public int windowStartTick; public int used; public int suppressed; }
        private static readonly Dictionary<string, RateState> _rate = new Dictionary<string, RateState>(64);

        // ----------------------------
        // Public API: enable/disable channels
        // ----------------------------

        public static void SetChannel(Channel ch, bool enabled)
        {
            lock (_lock) { _channelEnabled[ch] = enabled; }
        }

        public static bool IsChannelEnabled(Channel ch)
        {
            lock (_lock)
            {
                return _channelEnabled.TryGetValue(ch, out var enabled) ? enabled : true;
            }
        }

        // ----------------------------
        // Public API: queued logging (thread-safe)
        // ----------------------------

        public static void QTrace(string msg, Channel ch = Channel.Debug)   => Enqueue(ch, Level.Trace, msg);
        public static void QMsg(string msg, Channel ch = Channel.General)   => Enqueue(ch, Level.Message, msg);
        public static void QWarn(string msg, Channel ch = Channel.General)  => Enqueue(ch, Level.Warning, msg);
        public static void QErr(string msg, Channel ch = Channel.General)   => Enqueue(ch, Level.Error, msg);

        /// <summary>
        /// Rate-limited enqueue. key별로 windowTicks 동안 maxCount까지만 허용, 나머지는 suppressed 카운트만 누적.
        /// EnableRateLimit=false면 일반 Enqueue처럼 동작.
        /// </summary>
        public static void QWarnRL(string key, string msg, Channel ch = Channel.General, int windowTicks = 60, int maxCount = 3)
            => EnqueueRateLimited(ch, Level.Warning, key, msg, windowTicks, maxCount);

        public static void QMsgRL(string key, string msg, Channel ch = Channel.General, int windowTicks = 60, int maxCount = 5)
            => EnqueueRateLimited(ch, Level.Message, key, msg, windowTicks, maxCount);

        // ----------------------------
        // Public API: immediate logging (main-thread recommended)
        // (still respects MinLevel + channel enable)
        // ----------------------------

        public static void Trace(string msg, Channel ch = Channel.Debug)
        {
            if (!ShouldLog(ch, Level.Trace)) return;
            Log.Message(Format(ch, msg));
        }

        public static void Msg(string msg, Channel ch = Channel.General)
        {
            if (!ShouldLog(ch, Level.Message)) return;
            Log.Message(Format(ch, msg));
        }

        public static void Warn(string msg, Channel ch = Channel.General)
        {
            if (!ShouldLog(ch, Level.Warning)) return;
            Log.Warning(Format(ch, msg));
        }

        public static void Err(string msg, Channel ch = Channel.General)
        {
            if (!ShouldLog(ch, Level.Error)) return;
            Log.Error(Format(ch, msg));
        }

        // ----------------------------
        // Flush / lifecycle
        // ----------------------------

        /// <summary>Clear queued logs and counters. Call on load if you don't want previous-run leftovers.</summary>
        public static void Clear()
        {
            lock (_lock)
            {
                _q.Clear();
                _dropped = 0;
                _hasLast = false;
                _rate.Clear();
            }
        }

        /// <summary>
        /// MUST be called on main thread (e.g. GameComponentTick).
        /// Prints up to maxPerFlush queued lines.
        /// </summary>
        public static void Flush(int maxPerFlush = 200)
        {
            // finalize last coalesced entry first
            lock (_lock)
            {
                if (EnableDuplicateCoalescing && _hasLast)
                {
                    EnqueueInternal(_last);
                    _hasLast = false;
                }
            }

            // overflow summary
            int dropped;
            lock (_lock) { dropped = _dropped; _dropped = 0; }
            if (dropped > 0)
                Log.Warning($"{Signature} Log queue overflow: dropped {dropped} lines.");

            int emitted = 0;
            while (emitted < maxPerFlush)
            {
                Entry e;
                lock (_lock)
                {
                    if (_q.Count == 0) break;
                    e = _q.Dequeue();
                }

                string text = e.count > 1 ? $"{e.msg} (x{e.count})" : e.msg;

                switch (e.level)
                {
                    case Level.Trace:
                    case Level.Message:
                        Log.Message(text);
                        break;
                    case Level.Warning:
                        Log.Warning(text);
                        break;
                    case Level.Error:
                        Log.Error(text);
                        break;
                }

                emitted++;
            }

            // rate-limit suppressed summaries (lightweight)
            if (EnableRateLimit)
            {
                // Emit summaries when a window has likely rolled over.
                // (We only do work if there's something to report.)
                int tick = SafeTick();
                List<string> keysToReport = null;

                lock (_lock)
                {
                    foreach (var kv in _rate)
                    {
                        var st = kv.Value;
                        if (st.suppressed > 0 && tick - st.windowStartTick >= 60)
                        {
                            if (keysToReport == null) keysToReport = new List<string>();
                            keysToReport.Add(kv.Key);
                        }
                    }

                    if (keysToReport != null)
                    {
                        foreach (var key in keysToReport)
                        {
                            var st = _rate[key];
                            EnqueueInternal(new Entry
                            {
                                channel = Channel.Debug,
                                level = Level.Warning,
                                msg = $"{Signature}[RateLimit] suppressed {st.suppressed} logs (key={key})",
                                count = 1,
                                firstTick = tick
                            });
                            st.suppressed = 0;
                            _rate[key] = st;
                        }
                    }
                }
            }
        }

        // ----------------------------
        // Internal helpers
        // ----------------------------

        private static string Format(Channel ch, string msg)
        {
            // Example: [CheeseProtocol][Net] Connected
            return $"{Signature}[{ch}] {msg}";
        }

        private static bool ShouldLog(Channel ch, Level level)
        {
            if (level < MinLevel)
                return false;

            if (ch == Channel.Debug && !Prefs.DevMode)
                return false;

            lock (_lock)
            {
                if (_channelEnabled.TryGetValue(ch, out var enabled))
                    return enabled;
            }

            return true;
        }

        private static void Enqueue(Channel ch, Level level, string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            if (!ShouldLog(ch, level)) return;

            int tick = SafeTick();
            string formatted = Format(ch, msg);

            lock (_lock)
            {
                if (EnableDuplicateCoalescing)
                {
                    // coalesce only consecutive identical messages
                    if (_hasLast && _last.channel == ch && _last.level == level && _last.msg == formatted)
                    {
                        _last.count++;
                        return;
                    }

                    // push previous buffered entry
                    if (_hasLast)
                        EnqueueInternal(_last);

                    _last = new Entry { channel = ch, level = level, msg = formatted, count = 1, firstTick = tick };
                    _hasLast = true;
                    return;
                }

                EnqueueInternal(new Entry { channel = ch, level = level, msg = formatted, count = 1, firstTick = tick });
            }
        }

        private static void EnqueueRateLimited(Channel ch, Level level, string key, string msg, int windowTicks, int maxCount)
        {
            if (string.IsNullOrEmpty(msg)) return;

            if (!EnableRateLimit)
            {
                Enqueue(ch, level, msg);
                return;
            }

            if (string.IsNullOrEmpty(key)) key = $"{ch}:{level}:{msg}";

            int tick = SafeTick();

            lock (_lock)
            {
                if (!_rate.TryGetValue(key, out var st))
                    st = new RateState { windowStartTick = tick, used = 0, suppressed = 0 };

                if (tick - st.windowStartTick >= windowTicks)
                {
                    st.windowStartTick = tick;
                    st.used = 0;
                    // st.suppressed 유지 -> Flush에서 요약 출력 후 0으로 리셋
                }

                if (st.used < maxCount)
                {
                    st.used++;
                    _rate[key] = st;

                    // keep coalescing buffer consistent:
                    if (EnableDuplicateCoalescing && _hasLast)
                    {
                        EnqueueInternal(_last);
                        _hasLast = false;
                    }

                    // rate-limited allowed: enqueue directly (already formatted & filtered)
                    if (!ShouldLog(ch, level)) return;

                    string formatted = Format(ch, msg);
                    EnqueueInternal(new Entry { channel = ch, level = level, msg = formatted, count = 1, firstTick = tick });
                }
                else
                {
                    st.suppressed++;
                    _rate[key] = st;
                }
            }
        }

        private static void EnqueueInternal(Entry e)
        {
            if (_q.Count >= MaxQueue)
            {
                _dropped++;
                return;
            }
            _q.Enqueue(e);
        }
        private static int SafeTick()
        {
            try
            {
                return Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}