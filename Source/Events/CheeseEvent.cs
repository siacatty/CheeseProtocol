using System;
using Verse;

namespace CheeseProtocol
{
    public class CheeseEvent : IExposable
    {
        // Meta
        public string donationType; // e.g. "CHAT", "VIDEO"
        public long receivedAtUtcMs;
        public long msgTimeMs;

        // Donation content
        public bool isDonation;
        public string username;
        public int amount;
        public string message;
        public CheeseCommand cmd;

        // Deduplication
        public string donationId;
        public string dedupeKey;

        public CheeseEvent() { }

        public static long NowUtcMs()
            => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public void ExposeData()
        {
            Scribe_Values.Look(ref donationType, "donationType");
            Scribe_Values.Look(ref receivedAtUtcMs, "receivedAtUtcMs");
            Scribe_Values.Look(ref msgTimeMs, "msgTimeMs");

            Scribe_Values.Look(ref isDonation, "isDonation");
            Scribe_Values.Look(ref username, "username");
            Scribe_Values.Look(ref amount, "amount");
            Scribe_Values.Look(ref message, "message");
            Scribe_Values.Look(ref cmd, "cmd");

            Scribe_Values.Look(ref donationId, "donationId");
            Scribe_Values.Look(ref dedupeKey, "dedupeKey");
        }

        public override string ToString()
            => $"username={username}, amount={amount}, message={message}";
    }
}