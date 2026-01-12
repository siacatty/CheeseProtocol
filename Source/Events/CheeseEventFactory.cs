using System;
using System.Collections.Generic;
using Verse;

namespace CheeseProtocol
{
    public static class CheeseEventFactory
    {
        private static string NewId() => $"SIM-{Guid.NewGuid():N}";

        /// <summary>
        /// Chat 시뮬레이션: isDonation=false, amount=0
        /// </summary>
        /// 
        public static CheeseEvent MakeChatEvent(string user, string message, long msgTimeMs, CheeseCommand cmd)
        {
            //string simDonationId = NewId();
            long receivedAtUtcMsNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return new CheeseEvent
            {
                donationId = null,
                username = user,
                message = message,
                amount = 0,
                isDonation = false,
                cmd = cmd,
                msgTimeMs = msgTimeMs,
                receivedAtUtcMs = receivedAtUtcMsNow,
                dedupeKey = $"{receivedAtUtcMsNow}|{user}|{0}|{message}"
            };
        }
        public static CheeseEvent MakeDonationEvent(string user, string message, long msgTimeMs, int amount, string donationType, string donationId, CheeseCommand cmd)
        {
            long receivedAtUtcMsNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return new CheeseEvent
            {
                donationId = donationId,
                username = user,
                message = message,
                amount = amount,
                isDonation = true,
                donationType = donationType,
                msgTimeMs = msgTimeMs,
                receivedAtUtcMs = receivedAtUtcMsNow,
                cmd = cmd,
                dedupeKey = !string.IsNullOrWhiteSpace(donationId)
                            ? "don:" + donationId
                            : $"{receivedAtUtcMsNow}|{user}|{amount}|{message}"
            };
        }

        public static CheeseEvent CreateCheeseEvent(Dictionary<string, object> msgObj, bool isDonation)
        {   
            if (msgObj == null) return null;

            var msg = msgObj.TryGetValue("msg", out var m) ? m?.ToString() : null;
            if (string.IsNullOrWhiteSpace(msg)) return null;
            CheeseCommand cmd = CheeseCommandParser.Parse(msg, out string optionalArgs);
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

                return MakeDonationEvent(username, msg, msgTimeMs, amount, donationType, donationId, cmd);
            }
            username = ExtractNickname(msgObj);
            if (username.NullOrEmpty())
                username = "Unknown";

            return MakeChatEvent(username, msg, msgTimeMs, cmd);
        }

        private static string ExtractNickname(Dictionary<string, object> msgObj)
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
    }
}