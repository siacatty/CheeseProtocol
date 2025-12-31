using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;
using WebSocket4Net;
using System.Collections.Concurrent;

namespace CheeseProtocol
{
    public class ChzzkChatClient
    {
        private enum LogLevel
        {
            Message,
            Warning,
            Error
        }
        private readonly Queue<(LogLevel level, string msg)> pendingLogs
    = new Queue<(LogLevel, string)>();
        private readonly ConcurrentQueue<ChatEvent> chatQueue
        = new ConcurrentQueue<ChatEvent>();
        private readonly ConcurrentQueue<DonationEvent> donationQueue
    = new ConcurrentQueue<DonationEvent>();
        private readonly CheeseSettings settings;

        private WebSocket ws;
        private WebSocket closingWs = null;
        private string channelId;
        private string chatChannelId;   // e.g. "N2CCOb"
        private string chatAccessToken; // accTkn in logs
        private string extraToken;      // optional; some flows require it
        private string sid;             // from cmd 10100 handshake

        private int tid = 1;
        private bool userDisabledReconnect = false; // 유저가 의도적으로 끔
        private System.Timers.Timer liveRetryTimer;
        private System.Timers.Timer reconnectTimer;
        private System.Timers.Timer heartbeatTimer;
        private System.Timers.Timer channelWatchTimer;
        private int fastRetryCount = 0;
        private const int FastRetryMax = 3;

        private const double LiveRetryIntervalMs = 30_000; // 30초마다 LIVE 재확인
        private bool connectSent;
        //private int ticksSinceHandshake;
        private bool reconnectRequested = false;
        private string reconnectReason = "";
        private readonly object reconnectLock = new object();
        private bool reconnectTimerArmed = false;
        private const double ReconnectDelayMs = 5000; // 5 seconds delay reconnect
        private readonly object hbLock = new object();
        private const double HeartbeatIntervalMs = 15000; // 15 seconds interval heartbeat
        private readonly Dictionary<string, long> seen = new Dictionary<string, long>();
        private const long SeenTtlMs = 1_000; // ignore duplicate chat/donations within 1 second
        private volatile bool watchRequested = false;
        private readonly object watchLock = new object();
        private const double ChannelWatchIntervalMs = 30_000;
        private string lastResolvedChatChannelId;
        private readonly object connectLock = new object();
        private bool connectInProgress = false;
        private const int MaxEventsPerTick = 10;
        private const int MaxQueueSize = 1000;
        private int droppedChat = 0;
        private int droppedDon = 0;
        private int jsonErrorCount = 0;
        private string lastJsonErrorSample = null;

        public ChzzkChatClient(CheeseSettings settings)
        {
            this.settings = settings;
        }

        public void Connect()
        {
            Disconnect();
            if (!ChzzkEndpoints.TryExtractChannelIdFromStudioUrl(settings.chzzkStudioUrl, out channelId))
            {
                settings.chzzkStatus = "Disconnected: Invalid Studio URL";
                EnqueueWarning("[CheeseProtocol] Invalid studio URL");
                return;
            }
            if (!ChzzkEndpoints.TryResolveChatChannelId(channelId, out chatChannelId))
            {
                settings.chzzkStatus = "Disconnected: Stream offline";
                EnqueueWarning("[CheeseProtocol] Failed to resolve chatChannelId (is stream LIVE?)");
                StartLiveRetryTimer();
                return;
            }
            lastResolvedChatChannelId = chatChannelId;
            StopLiveRetryTimer();
            StartChannelWatch();
            // 2) Fetch chat access token (READ)
            if (!ChzzkEndpoints.TryFetchChatAccessToken(chatChannelId, out chatAccessToken, out extraToken))
            {
                settings.chzzkStatus = "Disconnected: ChatAccess token fetch failed";
                EnqueueWarning("[CheeseProtocol] Failed to fetch chatAccessToken");
                return;
            }

            settings.chzzkStatus = "Connecting: WebSocket connecting...";
            connectSent = false;
            sid = null;
            //ticksSinceHandshake = 0;
            ws = new WebSocket("wss://kr-ss1.chat.naver.com/chat");
            ws.Opened += (_, __) =>
            {
                settings.chzzkStatus = "Connecting: WS opened";
                SendConnect();
            };
            ws.MessageReceived += (_, e) =>
            {
                try { OnMessage(e.Message); }
                catch (Exception ex)
                {
                    EnqueueError("[CheeseProtocol] OnMessage crash: " + ex);
                }
            };
            ws.Error += (_, e) =>
            {
                EnqueueError("[CheeseProtocol] Web socket error: " + e.Exception);
                settings.chzzkStatus = "Disconnected: WS error";
                OnDisconnected("ws.error");
            };
            ws.Closed += (sender, __) =>
            {
                var sock = sender as WebSocket;
                var state = sock != null ? sock.State.ToString() : "sender-null";
                EnqueueWarning("[CheeseProtocol] Web socket closed. State=" + state);

                settings.chzzkStatus = "Disconnected: WS closed";

                if (sock != null && closingWs == sock)
                {
                    closingWs = null;
                    return;
                }
                OnDisconnected("ws.closed");
                //reconnectReason = "ws.closed";
                //ScheduleReconnect();
            };

            ws.Open();

        }

        public void Disconnect()
        {
            StopHeartbeatTimer();
            StopChannelWatch();
            try
            {
                if (ws != null)
                {
                    closingWs = ws;
                    ws.Close();
                }
            }
            catch { }
            ws = null;
            chatChannelId = null;
            chatAccessToken = null;
            extraToken = null;
            sid = null;
            connectSent = false;
            //ticksSinceHandshake = 0;
            /*
            if (settings != null)
                settings.chzzkStatus = "Disconnected: ";
            */
        }
        private void ScheduleReconnect()
        {
            /*if (reconnectScheduled) return;
            reconnectScheduled = true;
            reconnectTickAt = Find.TickManager.TicksGame + ReconnectDelayTicks;
            settings.chzzkStatus = "Chat: reconnect scheduled";
            */
            lock (reconnectLock)
            {
                if (reconnectTimerArmed) return;
                reconnectTimerArmed = true;

                if (reconnectTimer == null)
                {
                    reconnectTimer = new System.Timers.Timer();
                    reconnectTimer.AutoReset = false;
                    reconnectTimer.Elapsed += (_, __) =>
                    {
                        // WS thread / timer thread: just request reconnect, execute in Tick
                        reconnectRequested = true;
                        lock (reconnectLock) { reconnectTimerArmed = false; }
                    };
                }

                reconnectTimer.Interval = ReconnectDelayMs;
                reconnectTimer.Stop();
                reconnectTimer.Start();

                settings.chzzkStatus = "Connecting: Try reconnecting";
            }
        }

        public void Tick()
        {
            lock (pendingLogs)
            {
                while (pendingLogs.Count > 0)
                {
                    var (level, msg) = pendingLogs.Dequeue();
                    switch (level)
                    {
                        case LogLevel.Warning:
                            Log.Warning(msg);
                            break;
                        case LogLevel.Error:
                            Log.Error(msg);
                            break;
                        default:
                            Log.Message(msg);
                            break;
                    }
                }
            }

            if (reconnectRequested)
            {
                reconnectRequested = false;
                if (!userDisabledReconnect)
                {
                    RequestConnect("auto");
                    EnqueueWarning("[CheeseProtocol] ScheduleReconnect reason=" + reconnectReason);
                }
            }
            if (watchRequested)
            {
                watchRequested = false;
                CheckChannelAndSwitchIfNeeded();
            }

            ProcessEventQueues();

            //if (ws == null) return;

            // Handshake silent watchdog
            /*
            if (ws != null && sid != null)
            {
                ticksSinceHandshake++;
                int now = Find.TickManager.TicksGame;
                if (now - lastRecvTick > 60 * 10) // 10초 동안 아무것도 안 오면
                {
                    reconnectReason = "no_messages_10s";
                    settings.chzzkStatus = "Chat: reconnecting (silent)";
                    reconnectRequested = true;
                }
                // ~10 seconds at 60 ticks/sec = 600 ticks
            }
            */
        }

        private void SendConnect()
        {
            if (ws == null || connectSent) {
                EnqueueWarning("[CheeseProtocol] SendConnect() null socket or connect already sent");
                return;
            }
            if (string.IsNullOrEmpty(chatChannelId) || string.IsNullOrEmpty(chatAccessToken)) {
                EnqueueWarning("[CheeseProtocol] null chatChannelID/chatAccessToken");
                return;
            }
            var payload = new Dictionary<string, object>
            {
                ["ver"] = "2",
                ["cmd"] = 100,
                ["svcid"] = "game",
                ["cid"] = chatChannelId,
                ["bdy"] = new Dictionary<string, object>
                {
                    ["uid"] = null,
                    ["devType"] = 2001,
                    ["accTkn"] = chatAccessToken,
                    ["extraTkn"] = extraToken,
                    ["auth"] = "READ",
                },
                ["tid"] = (tid++).ToString(),
            };

            var json = MiniJSON.Serialize(payload);

            ws.Send(json);
            connectSent = true;
            settings.chzzkStatus = "Connecting: CONNECT sent (waiting 10100)";
        }

        private void OnMessage(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;

            // Some server heartbeats are tiny like {"ver":"2","cmd":0}
            // We just parse and react to cmd.
            object parsed = null;
            try
            {
                parsed = MiniJSON.Deserialize(raw);
            }
            catch (Exception ex)
            {
                jsonErrorCount++;
                if (jsonErrorCount == 1 || jsonErrorCount % 100 == 0)
                {
                    lastJsonErrorSample = raw;
                    EnqueueError("[CheeseProtocol] JSON parse error (" + jsonErrorCount + "): " + ex);
                    EnqueueError("[CheeseProtocol] JSON error sample (first 300): " +
                        (raw.Length > 300 ? raw.Substring(0, 300) : raw));
                }
                return;
            }

            var obj = parsed as Dictionary<string, object>;
            if (obj == null) return;

            if (!obj.TryGetValue("cmd", out var cmdObj)) return;
            var cmd = ParseIntSafe(cmdObj);

            //EnqueueWarning("[CheeseProtocol][RAW-CMD] cmd=" + cmd + " raw=" + raw);

            // server heartbeat ping: {"ver":"2","cmd":0}
            if (cmd == 0)
            {
                //EnqueueWarning("[CheeseProtocol] Heartbeat ping received");
                SendHeartbeat();
                return;
            }

            // Handshake ack
            if (cmd == 10100)
            {
                // bdy.sid present
                if (obj.TryGetValue("bdy", out var bdyObj))
                {
                    var bdy = bdyObj as Dictionary<string, object>;
                    if (bdy != null && bdy.TryGetValue("sid", out var sidObj))
                    {
                        sid = sidObj?.ToString();
                        StartHeartbeatTimer();
                        droppedChat = 0;
                        droppedDon = 0;
                        jsonErrorCount = 0;
                        settings.chzzkStatus = "Connected: waiting for chat/cheese";
                        EnqueueMessage("[CheeseProtocol] Chat channel connected successfully");
                    }
                }
                return;
            }

            // Chat message batch
            if (cmd == 93101)
            {
                if (!obj.TryGetValue("bdy", out var bdyObj)) return;

                var list = bdyObj as List<object>;
                if (list == null || list.Count == 0) return;

                foreach (var item in list)
                {
                    var msgObj = item as Dictionary<string, object>;
                    if (msgObj == null) continue;

                    var msg = msgObj.TryGetValue("msg", out var msgText) ? msgText?.ToString() : null;
                    if (string.IsNullOrEmpty(msg)) continue;
                    var chat_evt = new ChatEvent
                    {
                        receivedAtUtcMs = ChatEvent.NowUtcMs(),
                        chatChannelId = chatChannelId,
                        uid = msgObj.TryGetValue("uid", out var uid) ? uid?.ToString() : null,
                        nickname = ExtractNickname(msgObj),
                        message = msg,
                        msgTimeMs = msgObj.TryGetValue("msgTime", out var mt) ? ParseLongSafe(mt) : 0
                    };

                    // dedupe key
                    chat_evt.dedupeKey = $"{chat_evt.msgTimeMs}|{chat_evt.uid}|{chat_evt.message}";

                    // enqueue ONLY (no game logic here)
                    if (chatQueue.Count >= MaxQueueSize)
                    {
                        if (droppedChat++ % 100 == 0)
                            EnqueueWarning($"[CheeseProtocol] Queue overflow, dropping. droppedChat={droppedChat}");
                    }
                    else
                    {
                        chatQueue.Enqueue(chat_evt);
                    }
                    /*
                    var evt = new DonationEvent
                    {
                        donor = string.IsNullOrWhiteSpace(nickname) ? "Unknown" : nickname,
                        amount = 0,
                        message = msg
                    };
                    */

                    EnqueueMessage($"[CheeseProtocol] [{chat_evt.msgTimeMs}]{chat_evt.nickname}: {chat_evt.message}");

                    // Use existing keyword routing (!참여/!습격/!상단)
                    //ProtocolRouter.RouteAndExecute(evt);
                }
                return;
            }
            if (cmd == 93102)
            {
                //EnqueueWarning("[CheeseProtocol][RAW-CMD] cmd=" + cmd + " raw=" + raw);
                if (!obj.TryGetValue("bdy", out var bdyObj)) return;

                var list = bdyObj as List<object>;
                if (list == null || list.Count == 0) return;

                foreach (var item in list)
                {
                    var msgObj = item as Dictionary<string, object>;
                    if (msgObj == null) continue;

                    // extras is a JSON string
                    var extrasJson = msgObj.TryGetValue("extras", out var ex) ? ex?.ToString() : null;
                    if (string.IsNullOrWhiteSpace(extrasJson)) continue;

                    Dictionary<string, object> extras = null;
                    try { extras = MiniJSON.Deserialize(extrasJson) as Dictionary<string, object>; }
                    catch { extras = null; }
                    if (extras == null) continue;

                    var donor = extras.TryGetValue("nickname", out var nn) ? nn?.ToString() : null;

                    int amount = 0;
                    if (extras.TryGetValue("payAmount", out var pa))
                        amount = ParseIntSafe(pa);

                    var donationId = extras.TryGetValue("donationId", out var did) ? did?.ToString() : null;

                    var message = msgObj.TryGetValue("msg", out var m) ? m?.ToString() : null;

                    var donationType = extras.TryGetValue("donationType", out var dt) ? dt?.ToString() : null;

                    // Build DonationEvent and enqueue (no game logic here)
                    var evt = new DonationEvent
                    {
                        receivedAtUtcMs = DonationEvent.NowUtcMs(),
                        donor = string.IsNullOrWhiteSpace(donor) ? "Unknown" : donor,
                        amount = amount,
                        message = message,
                        donationId = donationId,
                        donationType = donationType,
                        isDonation = true,
                        dedupeKey = !string.IsNullOrWhiteSpace(donationId)
                            ? "don:" + donationId
                            : $"don:{donor}|{amount}|{message}"
                    };
                    if (donationQueue.Count >= MaxQueueSize)
                    {
                        if (droppedDon++ % 100 == 0)
                            EnqueueWarning($"[CheeseProtocol]Queue overflow, dropping. droppedDon={droppedDon}");
                    }
                    else
                    {
                        donationQueue.Enqueue(evt);
                    }
                    EnqueueMessage($"[CheeseProtocol][DON] {evt.donor} type={evt.donationType} amount={evt.amount} msg={evt.message}");
                }
                return;
            }


            // Heartbeat etc. ignore
        }

        private void OnDisconnected(string reason)
        {
            StopHeartbeatTimer();

            if (userDisabledReconnect) return;
            reconnectReason = reason;
            // first: fast retries (5s)
            if (fastRetryCount < FastRetryMax)
            {
                fastRetryCount++;
                //settings.chzzkStatus = $"Connecting: fast retry {fastRetryCount}/{FastRetryMax} ({reason})";
                ScheduleReconnect(); // 5초 타이머
                return;
            }

            // fallback: slow live-check loop (30s)
            //settings.chzzkStatus = $"Disconnected: slow retry (30s) ({reason})";
            StartLiveRetryTimer();
        }

        private void SendHeartbeat()
        {
            try
            {
                if (ws == null) return;

                var payload = new Dictionary<string, object>
                {
                    ["ver"] = "2",
                    ["cmd"] = 10000,
                    ["tid"] = (tid++).ToString(),
                    ["svcid"] = "game",
                    ["cid"] = chatChannelId
                };

                var json = MiniJSON.Serialize(payload);
                ws.Send(json);
                // Log.Message("[CheeseProtocol][Chat] HEARTBEAT sent");
            }
            catch (Exception e)
            {
                EnqueueError("[CheeseProtocol] HEARTBEAT send failed: " + e.Message);
            }
        }

        private void ProcessEventQueues()
        {
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            int budget = MaxEventsPerTick;

            while (budget > 0 && donationQueue.TryDequeue(out var don))
            {
                budget--;

                if (!string.IsNullOrEmpty(don.dedupeKey) && IsDuplicate(don.dedupeKey, nowMs))
                    continue;

                //ProtocolRouter.RouteAndExecute(don);
            }

            while (budget > 0 && chatQueue.TryDequeue(out var chat))
            {
                budget--;

                if (!string.IsNullOrEmpty(chat.dedupeKey) && IsDuplicate(chat.dedupeKey, nowMs))
                    continue;

                var evt = new DonationEvent
                {
                    receivedAtUtcMs = chat.receivedAtUtcMs,
                    donor = string.IsNullOrWhiteSpace(chat.nickname) ? "Unknown" : chat.nickname,
                    amount = 0,
                    message = chat.message,
                    dedupeKey = chat.dedupeKey,
                    isDonation = false
                };

                //ProtocolRouter.RouteAndExecute(evt);
            }

            CleanupSeen(nowMs);
        }
        
        private bool IsDuplicate(string key, long nowMs)
        {
            if (string.IsNullOrEmpty(key)) return false;

            if (seen.TryGetValue(key, out var t) && nowMs - t <= SeenTtlMs)
                return true;

            seen[key] = nowMs;
            return false;
        }

        private void CleanupSeen(long nowMs)
        {
            // light cleanup
            if (seen.Count < 512) return;

            var remove = new List<string>();
            foreach (var kv in seen)
                if (nowMs - kv.Value > SeenTtlMs)
                    remove.Add(kv.Key);

            for (int i = 0; i < remove.Count; i++)
                seen.Remove(remove[i]);
        }

        private string ExtractNickname(Dictionary<string, object> msgObj)
        {
            // In your log, "profile" is a JSON string containing nickname.
            if (msgObj.TryGetValue("profile", out var profileObj))
            {
                var profileJson = profileObj?.ToString();
                if (!string.IsNullOrWhiteSpace(profileJson))
                {
                    try
                    {
                        var profile = MiniJSON.Deserialize(profileJson) as Dictionary<string, object>;
                        if (profile != null && profile.TryGetValue("nickname", out var nick))
                            return nick?.ToString();
                    }
                    catch { }
                }
            }

            // fallback: maybe some payloads have "uid"/"name"
            if (msgObj.TryGetValue("uid", out var uid)) return uid?.ToString();
            return null;
        }

        private static int ParseIntSafe(object value)
        {
            if (value == null) return 0;
            switch (value)
            {
                case int i: return i;
                case long l: return l > int.MaxValue ? int.MaxValue : (int)l;
                case double d: return (int)d;
                case string s:
                    if (int.TryParse(s, out var n)) return n;
                    return 0;
                default:
                    return 0;
            }
        }

        private static long ParseLongSafe(object v)
        {
            if (v == null) return 0;
            switch (v)
            {
                case long l: return l;
                case int i: return i;
                case double d: return (long)d;
                case string s when long.TryParse(s, out var n): return n;
                default: return 0;
            }
        }

        private void StartHeartbeatTimer()
        {
            lock (hbLock)
            {
                if (heartbeatTimer == null)
                {
                    heartbeatTimer = new System.Timers.Timer();
                    heartbeatTimer.AutoReset = true;
                    heartbeatTimer.Elapsed += (_, __) =>
                    {
                        try
                        {
                            if (ws == null) return;
                            if (sid == null) return;                  // only after handshake
                            if (ws.State != WebSocketState.Open) return;

                            SendHeartbeat(); // Send is OK from timer thread
                        }
                        catch { }
                    };
                }

                heartbeatTimer.Interval = HeartbeatIntervalMs;
                heartbeatTimer.Stop();
                heartbeatTimer.Start();
            }
        }

        private void StopHeartbeatTimer()
        {
            lock (hbLock)
            {
                try { heartbeatTimer?.Stop(); } catch { }
            }
        }

        private void CheckChannelAndSwitchIfNeeded()
        {
            if (userDisabledReconnect) return;
            if (string.IsNullOrWhiteSpace(channelId)) return;

            // Resolve current chatChannelId (LIVE면 값이 바뀔 수 있음)
            if (!ChzzkEndpoints.TryResolveChatChannelId(channelId, out var currentChatCid))
            {
                // OFFLINE일 가능성: status만 갱신하고 그냥 유지
                settings.chzzkStatus = "Stream offline";
                return;
            }

            // 처음이거나 변경 감지
            if (string.IsNullOrWhiteSpace(lastResolvedChatChannelId))
                lastResolvedChatChannelId = currentChatCid;

            if (currentChatCid != lastResolvedChatChannelId)
            {
                EnqueueWarning($"[CheeseProtocol] chatChannelId changed {lastResolvedChatChannelId} -> {currentChatCid}, reconnecting");
                settings.chzzkStatus = "Connecting: Chat channel changed";
                lastResolvedChatChannelId = currentChatCid;
                RequestConnect("auto"); // Connect가 Disconnect하고 새 cid로 붙음
                return;
            }

            // LIVE인데 ws가 null/닫힘이면 붙기
            if (ws == null || ws.State != WebSocketState.Open || sid == null)
            {
                EnqueueWarning("[CheeseProtocol][Chat] live but not connected, reconnecting");
                settings.chzzkStatus = "Connecting: Chat channel detached";
                RequestConnect("auto");
            }
        }
        public void UserConnect()
        {
            userDisabledReconnect = false;

            // Stop all timers
            StopLiveRetryTimer();
            StopChannelWatch();
            StopHeartbeatTimer();
            StopRescheduleTimer();

            RequestConnect("user");
        }

        public void UserDisconnect()
        {
            userDisabledReconnect = true;

            StopLiveRetryTimer();
            StopChannelWatch();
            StopHeartbeatTimer();
            StopRescheduleTimer();

            Disconnect();
        }

        public void RequestConnect(string reason)
        {
            lock (connectLock)
            {
                if (connectInProgress) return;
                connectInProgress = true;
            }

            try
            {
                Connect();
            }
            finally
            {
                lock (connectLock)
                    connectInProgress = false;
            }
        }
        private void StartChannelWatch()
        {
            lock (watchLock)
            {
                if (channelWatchTimer == null)
                {
                    channelWatchTimer = new System.Timers.Timer();
                    channelWatchTimer.AutoReset = true;
                    channelWatchTimer.Interval = ChannelWatchIntervalMs;
                    channelWatchTimer.Elapsed += (_, __) =>
                    {
                        if (userDisabledReconnect) return;
                        if (string.IsNullOrWhiteSpace(channelId)) return;

                        // timer thread: just set a flag; do actual work in Tick
                        //EnqueueWarning("[CheeseProtocol] startChannelWatch elapsed --> Try reconnecting");
                        watchRequested = true;
                    };
                }

                channelWatchTimer.Stop();
                channelWatchTimer.Start();
            }
        }

        private void StopChannelWatch()
        {
            lock (watchLock)
            {
                try { channelWatchTimer?.Stop(); } catch { }
            }
        }

        private void StartLiveRetryTimer()
        {
            if (userDisabledReconnect) return;
            if (liveRetryTimer == null)
            {
                liveRetryTimer = new System.Timers.Timer();
                liveRetryTimer.AutoReset = true;
                liveRetryTimer.Interval = LiveRetryIntervalMs;
                liveRetryTimer.Elapsed += (_, __) =>
                {
                    // timer thread -> main thread
                    reconnectRequested = true;
                };
            }

            liveRetryTimer.Stop();
            liveRetryTimer.Start();
            settings.chzzkStatus = "Disconnected: retry connecting every 30s";
        }
        private void StopLiveRetryTimer()
        {
            try { liveRetryTimer?.Stop(); } catch { }
        }

        private void StopRescheduleTimer()
        {
            try { reconnectTimer.Stop(); } catch { }
        }

        private void EnqueueLog(LogLevel level, string msg)
        {
            lock (pendingLogs)
            {
                pendingLogs.Enqueue((level, msg));
            }
        }

        private void EnqueueMessage(string msg)
        {
            EnqueueLog(LogLevel.Message, msg);
        }

        private void EnqueueWarning(string msg)
        {
            EnqueueLog(LogLevel.Warning, msg);
        }
        private void EnqueueError(string msg)
        {
            EnqueueLog(LogLevel.Error, msg);
        }
    }
}