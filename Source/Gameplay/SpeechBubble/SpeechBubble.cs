using Verse;

namespace CheeseProtocol
{
    public sealed class SpeechBubble
    {
        public string text;
        public Pawn pawn;

        public float startRealtime;
        public float expireRealtime;
        public bool isNPC;

        public SpeechBubble() { }

        public SpeechBubble(string text, Pawn pawn, float startRealtime, float expireRealtime, bool isNPC)
        {
            this.text = text;
            this.pawn = pawn;
            this.startRealtime = startRealtime;
            this.expireRealtime = expireRealtime;
            this.isNPC = isNPC;
        }

        public bool Expired(float now) => now >= expireRealtime;
    }
}