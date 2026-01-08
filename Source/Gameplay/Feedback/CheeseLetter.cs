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
        public static void SendThrumboSuccessLetter(
            Map map,
            IntVec3 impactCell,
            ThrumboRequest req)
        {
            if (map == null) return;
            if (!impactCell.IsValid) return;

            string letterLabel = "트럼보";
            string letterText = "";
            int count = req.alphaCount + req.thrumboCount;
            if (count < 4)
            {
                letterText += "작은 ";
            }
            else if (count < 8)
            {
                letterText += "큰 ";
            }
            else
            {
                letterText += "굉장히 큰 ";
            }
            letterText += "무리의 트럼보들이 다가옵니다.";
            letterText += $"\n\n총 {count}마리의 트럼보가 관측됩니다.";

            if (req.alphaCount > 0)
            {
                letterLabel = "알파 " + letterLabel;
                letterText += "\n\n이 무리는 알파 트럼보가 이끌고 있습니다. 각별한 주의가 필요합니다.";
            }
            else
            {
                letterLabel = "희귀 " + letterLabel;
            }
            letterText += "\n\n트럼보는 희귀한 동물로, 천성은 순하나 맞설 경우 매우 위험합니다. 트럼보의 뿔과 가죽은 상인들 사이에서 아주 귀중한 재료로 여겨집니다.";
            letterText += "\n\n트럼보는들은 며칠 머무른 뒤 이곳을 떠날 것입니다.";
            LookTargets look = new LookTargets(impactCell, map);
            LetterDef def = LetterDefOf.PositiveEvent;

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