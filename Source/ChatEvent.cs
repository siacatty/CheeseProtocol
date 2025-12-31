using System;

namespace CheeseProtocol
{
    public class ChatEvent
    {
        // When we received it (real time, not ticks)
        public long receivedAtUtcMs;

        // Parsed from CHZZK chat payload
        public string chatChannelId;
        public string uid;        // userIdHash (or uid)
        public string nickname;
        public string message;

        // From payload (if available)
        public long msgTimeMs;    // server message time (epoch ms)

        // A stable-ish dedupe key (set this when enqueuing)
        public string dedupeKey;

        public static long NowUtcMs()
            => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}