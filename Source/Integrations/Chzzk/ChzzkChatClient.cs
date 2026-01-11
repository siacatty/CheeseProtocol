using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;
using WebSocket4Net;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using UnityEngine;
using static CheeseProtocol.CheeseLog;

namespace CheeseProtocol
{
    public class ChzzkChatClient
    {
        private readonly ConcurrentQueue<CheeseEvent> cheeseEvtQueue
    = new ConcurrentQueue<CheeseEvent>();
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
        private int droppedEvt = 0;
        private int jsonErrorCount = 0;
        private string lastJsonErrorSample = null;
        private int evtQueueDraining = 0;

        public ChzzkChatClient(CheeseSettings settings)
        {
            this.settings = settings;
        }

        public void Connect()
        {
            Disconnect();
            if (!ChzzkEndpoints.TryExtractChannelId(settings.chzzkStudioUrl, out channelId))
            {
                settings.chzzkStatus = "Disconnected: Invalid Studio URL";
                CheeseGameComponent.Instance?.UpdateUiStatus(s =>
                {
                    s.connectionState = ConnectionState.Disconnected;
                    s.lastError = "Disconnected: Invalid Studio URL";
                });
                QWarn("Invalid studio URL", Channel.Net);
                return;
            }
            if (!ChzzkEndpoints.TryResolveChatChannelId(channelId, out chatChannelId))
            {
                settings.chzzkStatus = "Disconnected: Stream offline";
                CheeseGameComponent.Instance?.UpdateUiStatus(s =>
                {
                    s.connectionState = ConnectionState.Disconnected;
                    s.lastError = "Disconnected: Stream offline";
                });
                QWarn("Failed to resolve chatChannelId (is stream LIVE?)", Channel.Net);
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
                CheeseGameComponent.Instance?.UpdateUiStatus(s =>
                {
                    s.connectionState = ConnectionState.Disconnected;
                    s.lastError = "Disconnected: ChatAccess token fetch failed";
                });
                QWarn("Failed to fetch chatAccessToken", Channel.Net);
                return;
            }

            settings.chzzkStatus = "Connecting: WebSocket connecting...";
            CheeseGameComponent.Instance?.UpdateUiStatus(s =>
            {
                s.connectionState = ConnectionState.Connecting;
            });
            connectSent = false;
            sid = null;
            //ticksSinceHandshake = 0;
            ws = new WebSocket("wss://kr-ss1.chat.naver.com/chat");
            ws.Opened += (_, __) =>
            {
                SendConnect();
            };
            ws.MessageReceived += (_, e) =>
            {
                try { OnMessage(e.Message); }
                catch (Exception ex)
                {
                    QErr("OnMessage crash: " + ex, Channel.Net);
                }
            };
            ws.Error += (_, e) =>
            {
                QErr("Web socket error: " + e.Exception, Channel.Net);
                settings.chzzkStatus = "Disconnected: WS error";
                CheeseGameComponent.Instance?.UpdateUiStatus(s =>
                {
                    s.connectionState = ConnectionState.Disconnected;
                    s.lastError = "Disconnected: WS error";
                });
                OnDisconnected("ws.error");
            };
            ws.Closed += (sender, __) =>
            {
                var sock = sender as WebSocket;
                var state = sock != null ? sock.State.ToString() : "sender-null";
                QWarn("Web socket closed. State=" + state, Channel.Net);

                settings.chzzkStatus = "Disconnected: WS closed";
                CheeseGameComponent.Instance?.UpdateUiStatus(s =>
                {
                    s.connectionState = ConnectionState.Disconnected;
                    s.lastError = "Disconnected: WS closed";
                });

                if (sock != null && closingWs == sock)
                {
                    closingWs = null;
                    return;
                }
                OnDisconnected("ws.closed");
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
        }
        private void ScheduleReconnect()
        {
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
                CheeseGameComponent.Instance?.UpdateUiStatus(s =>
                {
                    s.connectionState = ConnectionState.Connecting;
                });
            }
        }

        public void Tick()
        {
            if (reconnectRequested)
            {
                reconnectRequested = false;
                if (!userDisabledReconnect)
                {
                    RequestConnect("auto");
                    QWarn("ScheduleReconnect reason=" + reconnectReason, Channel.Net);
                }
            }
            if (watchRequested)
            {
                watchRequested = false;
                CheckChannelAndSwitchIfNeeded();
            }
            if (!settings.drainQueue)
                ProcessEventQueues();
        }

        private void SendConnect()
        {
            if (ws == null || connectSent) {
                QWarn("SendConnect() null socket or connect already sent", Channel.Net);
                return;
            }
            if (string.IsNullOrEmpty(chatChannelId) || string.IsNullOrEmpty(chatAccessToken)) {
                QWarn("null chatChannelID/chatAccessToken", Channel.Net);
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
                    QErr("JSON parse error (" + jsonErrorCount + "): " + ex, Channel.Net);
                    QErr("JSON error sample (first 300): " +
                        (raw.Length > 300 ? raw.Substring(0, 300) : raw), Channel.Net);
                }
                return;
            }

            var obj = parsed as Dictionary<string, object>;
            if (obj == null) return;

            if (!obj.TryGetValue("cmd", out var cmdObj)) return;
            var cmd = ParseIntSafe(cmdObj);

            if (cmd == 0)
            {
                QMsg("Heartbeat ping received", Channel.Debug);
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
                        droppedEvt = 0;
                        jsonErrorCount = 0;
                        settings.chzzkStatus = "Connected: waiting for chat/cheese";
                        CheeseGameComponent.Instance?.UpdateUiStatus(s =>
                        {
                            s.connectionState = ConnectionState.Connected;
                        });
                        QMsg("Chat channel connected successfully", Channel.Net);
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
                    CheeseEvent evt = CreateCheeseEvent(msgObj, false);
                    if (evt == null)
                    {
                        //QWarn("CheeseEvent creation failed from chat", Channel.Net);
                        continue;
                    }
                    else if (!TryEnqueueMessage(evt))
                        continue;
                }
                return;
            }
            // donation batch
            if (cmd == 93102)
            {
                if (!obj.TryGetValue("bdy", out var bdyObj)) return;

                var list = bdyObj as List<object>;
                if (list == null || list.Count == 0) return;

                foreach (var item in list)
                {
                    var msgObj = item as Dictionary<string, object>;
                    CheeseEvent evt = CreateCheeseEvent(msgObj, true);
                    if (evt == null)
                    {
                        //QWarn("CheeseEvent creation failed from donation", Channel.Net);
                        continue;
                    }
                    else if (!TryEnqueueMessage(evt))
                        continue;
                }
                return;
            }
            // etc. ignore
        }

        private CheeseEvent CreateCheeseEvent(Dictionary<string, object> msgObj, bool isDonation)
        {   
            if (msgObj == null) return null;

            var msg = msgObj.TryGetValue("msg", out var m) ? m?.ToString() : null;
            if (string.IsNullOrWhiteSpace(msg)) return null;

            if (msg.IndexOf('!') < 0) return null;

            // extras is a JSON string
            Dictionary<string, object> extras = null;
            int amount = 0;
            string username = "Unknown";
            var msgTimeMs = msgObj.TryGetValue("msgTime", out var mt) ? ParseLongSafe(mt) : 0;

            if (isDonation)
            {
                var extrasJson = msgObj.TryGetValue("extras", out var ex) ? ex?.ToString() : null;
                if (string.IsNullOrWhiteSpace(extrasJson)) return null;
                try { extras = MiniJSON.Deserialize(extrasJson) as Dictionary<string, object>; }
                catch { extras = null; }
                if (extras == null) return null;
                //username = extras.TryGetValue("nickname", out var nn) ? nn?.ToString() : "Unknown";
                username = ExtractNickname(msgObj);
                if (username.NullOrEmpty())
                    username = "Unknown";
                if (extras.TryGetValue("payAmount", out var pa))
                    amount = ParseIntSafe(pa);
                var donationId = extras.TryGetValue("donationId", out var did) ? did?.ToString() : null;
                var donationType = extras.TryGetValue("donationType", out var dt) ? dt?.ToString() : null;

                return CheeseEventFactory.MakeDonationEvent(username, msg, msgTimeMs, amount, donationType, donationId);
            }
            username = ExtractNickname(msgObj);
            if (username.NullOrEmpty())
                username = "Unknown";

            return CheeseEventFactory.MakeChatEvent(username, msg, msgTimeMs);
        }

        private bool TryEnqueueMessage(CheeseEvent evt)
        {
            if (cheeseEvtQueue.Count >= MaxQueueSize)
            {
                if (droppedEvt++ % 100 == 0)
                {
                    QWarn($"Queue overflow, dropping. droppedEvt={droppedEvt}");
                    return false;
                }
            }
            else
            {
                cheeseEvtQueue.Enqueue(evt);
            }
            QMsg($"New message: {evt.username} type={evt.donationType} amount={evt.amount} msg={evt.message}", Channel.Debug);
            return true;
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
            }
            catch (Exception e)
            {
                QErr("HEARTBEAT send failed: " + e.Message, Channel.Net);
            }
        }

        public void ProcessEventQueues()
        {
            if (System.Threading.Interlocked.Exchange(ref evtQueueDraining, 1) == 1)
                return;
            try
            {
                var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                int budget = MaxEventsPerTick;

                while (budget > 0 && cheeseEvtQueue.TryDequeue(out var evt))
                {
                    budget--;

                    if (!string.IsNullOrEmpty(evt.dedupeKey) && IsDuplicate(evt.dedupeKey, nowMs))
                        continue;

                    ProtocolRouter.RouteAndExecute(evt);
                }
                CleanupSeen(nowMs);   
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref evtQueueDraining, 0);
            }
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
            // "profile" is a JSON string containing nickname.
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
            return "Unknown";
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
                CheeseGameComponent.Instance?.UpdateUiStatus(s =>
                {
                    s.connectionState = ConnectionState.Disconnected;
                    s.lastError = "Stream may be offline";
                });
                return;
            }

            // 처음이거나 변경 감지
            if (string.IsNullOrWhiteSpace(lastResolvedChatChannelId))
                lastResolvedChatChannelId = currentChatCid;

            if (currentChatCid != lastResolvedChatChannelId)
            {
                QMsg($"chatChannelId changed {lastResolvedChatChannelId} -> {currentChatCid}, reconnecting", Channel.Net);
                settings.chzzkStatus = "Connecting: Chat channel changed";
                CheeseGameComponent.Instance?.UpdateUiStatus(s =>
                {
                    s.connectionState = ConnectionState.Connecting;
                    s.lastError = "Connecting: Chat channel changed";
                });
                lastResolvedChatChannelId = currentChatCid;
                RequestConnect("auto"); // Connect가 Disconnect하고 새 cid로 붙음
                return;
            }

            // LIVE인데 ws가 null/닫힘이면 붙기
            if (ws == null || ws.State != WebSocketState.Open || sid == null)
            {
                QMsg("Channel is live but not connected, reconnecting", Channel.Net);
                settings.chzzkStatus = "Connecting: Chat channel detached";
                CheeseGameComponent.Instance?.UpdateUiStatus(s =>
                {
                    s.connectionState = ConnectionState.Connecting;
                    s.lastError = "Connecting: Chat channel detached";
                });
                RequestConnect("auto");
            }
        }
        public void RunSimulation(string user, string message, long msgTimeMs, bool isDonation, int amount=0, string donationType="", string donationId="")
        {
            if (!Prefs.DevMode) return;
            CheeseEvent evt = null;
            if (isDonation)
            {
                evt = CheeseEventFactory.MakeDonationEvent(user, message, msgTimeMs, amount, donationType, donationId);
            }
            else
            {
                evt = CheeseEventFactory.MakeChatEvent(user, message, msgTimeMs);
            }
            TryEnqueueMessage(evt);
        }

        public void ResetCooldown()
        {
            var cdState = CheeseCooldownState.Current;
            cdState.ResetAllCd();
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
    }
}