using Verse;

namespace CheeseProtocol
{
    public sealed class SpeechBubble
    {
        public string text;
        public Pawn pawn;

        public float startRealtime;
        public float expireRealtime;
        public SpeakerType speaker;
        public GameFont fontSize;

        public SpeechBubble() { }

        public SpeechBubble(string text, Pawn pawn, float startRealtime, float expireRealtime, SpeakerType speaker=SpeakerType.Player, GameFont fontSize=GameFont.Small)
        {
            this.text = text;
            this.pawn = pawn;
            this.startRealtime = startRealtime;
            this.expireRealtime = expireRealtime;
            this.speaker = speaker;
            this.fontSize = fontSize;
        }
        public bool Expired(float now) => now >= expireRealtime;
    }

    public enum SpeakerType
    {
        Player,
        HostileNPC,
        NonHostileNPC,
    }
}