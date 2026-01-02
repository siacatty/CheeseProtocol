using System;

namespace CheeseProtocol
{
    public class CheeseEvent
    {
        // When we received it (real time, not ticks)
        public string donationType; // e.g. "CHAT", "VIDEO"
        public long receivedAtUtcMs;
        public long msgTimeMs;

        // Donation content
        public bool isDonation;
        public string username;
        public int amount;
        public string message;

        // If the platform provides a unique donation id, store it here (best dedupe key)
        public string donationId;

        // A stable-ish dedupe key (set this when enqueuing)
        public string dedupeKey;

        public static long NowUtcMs()
            => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
        public override string ToString()
            => $"username={username}, amount={amount}, message={message}";
    }
}