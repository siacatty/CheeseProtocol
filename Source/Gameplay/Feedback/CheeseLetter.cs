using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace CheeseProtocol
{
    public static class CheeseLetter
    {
        /// <summary>
        /// Custom meteor success letter. Click -> jump to impact cell.
        /// </summary>
        public static void SendMeteorSuccessLetter(
            Map map,
            IntVec3 impactCell,
            MeteorObject meteor)
        {
            if (map == null) return;
            if (!impactCell.IsValid) return;

            string letterLabel = $"운석: {meteor.label}";
            string letterText = $"큰 운석이 이 지역에 충돌했습니다. {meteor.label} 더미를 남겼습니다.";
            LookTargets look = new LookTargets(impactCell, map);
            LetterDef def = PickLetterDef(meteor);

            Find.LetterStack.ReceiveLetter(letterLabel, letterText, def, look);
        }

        private static string SafeLabel(MeteorCandidate chosen)
        {
            if (!chosen.IsValid) return "Unknown";
            if (!string.IsNullOrWhiteSpace(chosen.yieldLabel)) return chosen.yieldLabel;
            if (!string.IsNullOrWhiteSpace(chosen.label)) return chosen.label;
            return chosen.key?.ToString() ?? "Unknown";
        }

        /// Rule: Stones/chunks -> Neutral (gray)
        /// Valuable/tech -> Positive (blue)
        private static LetterDef PickLetterDef(MeteorObject meteor)
        {
            if (meteor == null) return LetterDefOf.NeutralEvent;

            string k = (meteor.type ?? "").ToLowerInvariant();
            if (k.Contains("mineable")) return LetterDefOf.PositiveEvent;
            else return LetterDefOf.NeutralEvent;
        }
    }
}