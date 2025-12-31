using System;

namespace CheeseProtocol
{
    public class DonationEvent
    {
        // When we received it (real time, not ticks)
        public string donationType; // e.g. "CHAT", "VIDEO"
        public long receivedAtUtcMs;

        // Donation content
        public bool isDonation;
        public string donor;
        public int amount;
        public string message;

        // If the platform provides a unique donation id, store it here (best dedupe key)
        public string donationId;

        // A stable-ish dedupe key (set this when enqueuing)
        public string dedupeKey;

        public static long NowUtcMs()
            => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
        public override string ToString()
            => $"donor={donor}, amount={amount}, message={message}";
    }
}