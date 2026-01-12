using Verse;

namespace CheeseProtocol
{
    public sealed class SpeechBubble
    {
        public string text;
        public Pawn pawn;

        public float startRealtime;
        public float expireRealtime;

        public SpeechBubble() { }

        public SpeechBubble(string text, Pawn pawn, float startRealtime, float expireRealtime)
        {
            this.text = text;
            this.pawn = pawn;
            this.startRealtime = startRealtime;
            this.expireRealtime = expireRealtime;
        }

        public bool Expired(float now) => now >= expireRealtime;
    }
}