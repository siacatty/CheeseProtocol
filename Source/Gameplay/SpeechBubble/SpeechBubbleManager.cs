using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using static CheeseProtocol.CheeseLog;

namespace CheeseProtocol
{
    public class SpeechBubbleManager : MapComponent
    {
        // --- Tunables ---
        private const float DefaultDurationSec = 5f;
        private const float FadeSec = 0.18f;
        private const float Pad = 6f;
        private const float YOffsetPx = 32f;
        private const float MaxWidth = 320f;
        private const float PawnYOffset = -0.4f;
        private const int MaxBubbles = 8;

        // per-user chat cooldown (realtime seconds)
        private float ChatCooldownSecPerUser = CheeseProtocolMod.Settings?.speechBubbleCD ?? CheeseDefaults.SpeechBubbleCD;
        private const float ChatUserStateStaleSec = 60f * 10f; // 10 min
        private float nextCleanupAt = 0f;

        private static readonly Color BoxColor     = new Color(0.08f, 0.12f, 0.17f, 0.85f);
        private static readonly Color OutlineColor = new Color(0.38f, 0.55f, 0.70f, 0.95f);

        private readonly List<SpeechBubble> bubbles = new List<SpeechBubble>(16);
        private readonly Dictionary<string, float> lastChatTimeByUser = new Dictionary<string, float>(256);

        public SpeechBubbleManager(Map map) : base(map) { }

        public static SpeechBubbleManager Get(Map map) => map?.GetComponent<SpeechBubbleManager>();

        // ---- API ----

        /// <summary>
        /// Non-throttled (use for command results; you already have command cooldown elsewhere).
        /// One bubble per pawn: replaces existing bubble for that pawn.
        /// </summary>
        public void Add(string text, Pawn pawn, float durationSec = DefaultDurationSec)
        {
            if (map == null) return;
            if (pawn == null) return;
            if (string.IsNullOrWhiteSpace(text)) return;

            float now = Time.realtimeSinceStartup;
            float dur = Mathf.Max(0.05f, durationSec);

            ReplaceBubbleForPawn(pawn, text, now, now + dur);
        }

        /// <summary>
        /// Per-user throttled chat bubble.
        /// One bubble per pawn: replaces existing bubble for that pawn.
        /// </summary>
        public bool AddChat(string username, string text, Pawn pawn, float durationSec = DefaultDurationSec)
        {
            if (map == null) 
            {
                return false;
            }
            if (pawn == null) 
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(text)) return false;

            float now = Time.realtimeSinceStartup;

            if (lastChatTimeByUser.TryGetValue(username, out float last) &&
                now - last < (CheeseProtocolMod.Settings?.speechBubbleCD ?? CheeseDefaults.SpeechBubbleCD))
                return false;

            lastChatTimeByUser[username] = now;

            float dur = Mathf.Max(0.05f, durationSec);

            ReplaceBubbleForPawn(pawn, text, now, now + dur);

            CleanupChatUserState(now);

            return true;
        }

        public void ClearAll()
        {
            bubbles.Clear();
            lastChatTimeByUser.Clear();
        }

        // ---- Lifecycle ----

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % 60 != 0) return;
            CleanupExpired(Time.realtimeSinceStartup);
        }
        public override void MapComponentOnGUI()
        {
            if (map == null || bubbles.Count == 0) return;
            if (Current.ProgramState != ProgramState.Playing) return;
            if (Event.current == null) return;

            float now = Time.realtimeSinceStartup;

            // Pause/3x safe: clean here too
            CleanupExpired(now);

            // UI state save
            var prevColor = GUI.color;
            var prevAnchor = Text.Anchor;
            var prevFont = Text.Font;
            bool prevWrap = Text.WordWrap;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.WordWrap = true;

            for (int i = 0; i < bubbles.Count; i++)
            {
                var b = bubbles[i];
                if (b == null) continue;

                float alpha = ComputeAlpha(now, b.startRealtime, b.expireRealtime);
                if (alpha <= 0f) continue;

                Vector2 screen = GenMapUI.LabelDrawPosFor(b.pawn, PawnYOffset);

                Rect rect = MakeBubbleRectAt(screen.x, screen.y - YOffsetPx, b.text);
                rect = ClampToScreen(rect, 6f);

                GUI.color = new Color(1f, 1f, 1f, alpha);

                Widgets.DrawBoxSolidWithOutline(
                    rect,
                    new Color(BoxColor.r, BoxColor.g, BoxColor.b, BoxColor.a * alpha),
                    new Color(OutlineColor.r, OutlineColor.g, OutlineColor.b, OutlineColor.a * alpha)
                );
                Widgets.Label(rect, b.text);
            }

            // restore
            Text.WordWrap = prevWrap;
            Text.Font = prevFont;
            Text.Anchor = prevAnchor;
            GUI.color = prevColor;
        }

        // ---- helpers ----

        /// <summary>
        /// Enforces one-bubble-per-pawn. If a bubble for this pawn exists, remove it first.
        /// Also enforces a global cap (drop oldest) as a safety net.
        /// </summary>
        private void ReplaceBubbleForPawn(Pawn pawn, string text, float start, float expire)
        {
            // Rule #2: replace existing bubble for same pawn
            for (int i = 0; i < bubbles.Count; i++)
            {
                var b = bubbles[i];
                if (b != null && b.pawn == pawn)
                {
                    b.text = text;
                    b.startRealtime = start;
                    b.expireRealtime = expire;
                    return;
                }
            }
            bubbles.Add(new SpeechBubble(text, pawn, start, expire));
        }

        private void CleanupExpired(float now)
        {
            if (bubbles.Count == 0) return;
            int removed = bubbles.RemoveAll(b => b == null || b.Expired(now));
        }

        private void CleanupChatUserState(float now)
        {
            if (now < nextCleanupAt) return;
            nextCleanupAt = now + 30f; // every 30 sec

            List<string> dead = null;

            foreach (var kv in lastChatTimeByUser)
            {
                if (now - kv.Value > ChatUserStateStaleSec)
                {
                    dead ??= new List<string>();
                    dead.Add(kv.Key);
                }
            }

            if (dead == null) return;

            for (int i = 0; i < dead.Count; i++)
                lastChatTimeByUser.Remove(dead[i]);
        }

        private static float ComputeAlpha(float now, float start, float expire)
        {
            if (now >= expire) return 0f;

            float a = 1f;

            float inT = now - start;
            if (inT < FadeSec) a *= Mathf.Clamp01(inT / FadeSec);

            float left = expire - now;
            if (left < FadeSec) a *= Mathf.Clamp01(left / FadeSec);

            return a;
        }

        private static Rect MakeBubbleRectAt(float centerX, float anchorY, string text)
        {
            float w = Mathf.Min(MaxWidth, Text.CalcSize(text).x + Pad * 2f);
            float h = Text.CalcHeight(text, w - Pad * 2f) + Pad * 2f;

            return new Rect(centerX - w * 0.5f, anchorY - h, w, h);
        }

        private static Rect ClampToScreen(Rect r, float margin)
        {
            float x = Mathf.Clamp(r.x, margin, UI.screenWidth - margin - r.width);
            float y = Mathf.Clamp(r.y, margin, UI.screenHeight - margin - r.height);
            return new Rect(x, y, r.width, r.height);
        }
    }
}