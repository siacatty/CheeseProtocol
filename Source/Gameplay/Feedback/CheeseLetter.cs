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
            MeteorRequest meteor)
        {
            if (map == null) return;
            if (!impactCell.IsValid) return;

            string letterLabel = $"운석: {meteor.label}";
            string letterText = $"큰 운석이 이 지역에 충돌했습니다. {meteor.label} 더미를 남겼습니다.";
            LookTargets look = new LookTargets(impactCell, map);
            LetterDef def = PickLetterDef(meteor);

            Find.LetterStack.ReceiveLetter(letterLabel, letterText, def, look);
        }
        public static void SendTameSuccessLetter(
            Map map,
            IntVec3 impactCell,
            TameRequest tame)
        {
            if (map == null) return;
            if (!impactCell.IsValid) return;

            string letterLabel = $"애완동물: {tame.label}";
            string letterText = $"새로운 애완동물이 합류합니다. {tame.label}(이)가 반갑게 인사를 합니다.";
            LookTargets look = new LookTargets(impactCell, map);
            LetterDef def = LetterDefOf.PositiveEvent;

            Find.LetterStack.ReceiveLetter(letterLabel, letterText, def, look);
        }
        public static void SendSupplySuccessLetter(
            Map map,
            IntVec3 impactCell,
            SupplyRequest supply)
        {
            if (map == null) return;
            if (!impactCell.IsValid) return;

            string letterLabel = $"보급: {supply.label}";
            string letterText = $"보급이 도착했습니다. {supply.label}{(supply.count>1 ? $" {supply.count}개" : "")}.";
            LookTargets look = new LookTargets(impactCell, map);
            LetterDef def = LetterDefOf.PositiveEvent;

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
        private static LetterDef PickLetterDef(MeteorRequest meteor)
        {
            if (meteor == null) return LetterDefOf.NeutralEvent;

            string k = (meteor.type ?? "").ToLowerInvariant();
            if (k.Contains("mineable")) return LetterDefOf.PositiveEvent;
            else return LetterDefOf.NeutralEvent;
        }
    }
}