using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using System.Text;
using System.Linq;
using Verse;
using RimWorld.BaseGen;

namespace CheeseProtocol
{
    public static class TameApplier
    {
        public static bool TryApplyValueHelper(TameRequest tame, float tameValue01)
        {
            List<TameCandidate> tameCandidates = CheeseProtocolMod.TameCatalog;
            if (tameCandidates == null || tameCandidates.Count == 0)
                return false;

            tameValue01 = Mathf.Clamp01(tameValue01);

            // marketValue 기준 정렬 (낮은 → 높은)
            var sorted = tameCandidates
                .Where(c => !c.disabled)
                .OrderBy(c => c.marketValue)
                .ToList();

            if (sorted.Count == 0)
            {
                Log.Warning("[CheeseProtocol] Tame failed : no animals available in TameCatalog");
                return false;
            }

            int count = sorted.Count;

            // tameValue01 → 중심 인덱스
            int centerIndex = Mathf.RoundToInt(tameValue01 * (count - 1));

            // ramdom window 5%
            int window = Mathf.Max(1, Mathf.RoundToInt(count * 0.05f));

            int min = Mathf.Max(0, centerIndex - window);
            int max = Mathf.Min(count - 1, centerIndex + window);

            int pickIndex = Rand.RangeInclusive(min, max);
            TameCandidate picked = sorted[pickIndex];
            tame.setChosen(picked);
            return true;
        }

        public static List<TameCandidate> BuildCatalogTameCandidates()
        {
            List<TameCandidate> tameList   = new List<TameCandidate>(256);
            foreach (var def in DefDatabase<PawnKindDef>.AllDefsListForReading.OrderBy(d => d.defName))
            {
                if (def?.race == null) continue;

                ThingDef raceDef = def.race;
                RaceProperties rp = raceDef.race;
                if (rp == null) continue;

                if (!rp.Animal || rp.Humanlike || rp.IsMechanoid) continue;
                float mv = raceDef.BaseMarketValue;
                if (mv <= 0f) continue;

                float wild = raceDef.GetStatValueAbstract(StatDefOf.Wildness);
                float cp = def.combatPower;
                float body = rp.baseBodySize;

                bool herd = rp.herdAnimal;
                string train = rp.trainability?.defName ?? "None";
                tameList.Add(new TameCandidate(
                    def,
                    def.LabelCap,
                    mv,
                    wild,
                    cp,
                    body,
                    herd,
                    train
                ));
            }
            return tameList;
        }
    
        public static void DumpAllAnimals()
        {
            var sb = new StringBuilder(64 * 1024);

            foreach (var kind in DefDatabase<PawnKindDef>.AllDefsListForReading.OrderBy(k => k.defName))
            {
                if (kind?.race == null) continue;

                ThingDef raceDef = kind.race;
                RaceProperties rp = raceDef.race;
                if (rp == null) continue;

                // animals only
                if (!rp.Animal || rp.Humanlike || rp.IsMechanoid) continue;

                // your version: wildness is a stat
                float wild = raceDef.GetStatValueAbstract(StatDefOf.Wildness);

                float mv = raceDef.BaseMarketValue; // or raceDef.GetStatValueAbstract(StatDefOf.MarketValue)
                float cp = kind.combatPower;
                float body = rp.baseBodySize;

                bool herd = rp.herdAnimal;

                // trainability
                string train = rp.trainability?.defName ?? "None";

                sb.AppendLine(
                    $"[Animal] kind={kind.defName} label={kind.LabelCap} race={raceDef.defName} " +
                    $"mv={mv:0.##} cp={cp:0.##} wild={wild:0.###} body={body:0.###} " +
                    $"herd={herd} train={train}"
                );
            }

            Log.Message(sb.ToString());
        }
    }
}