using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using System.Text;
using System.Linq;
using Verse;
using RimWorld.BaseGen;
using static CheeseProtocol.CheeseLog;

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
                QWarn("Tame failed : no animals available in TameCatalog", Channel.Verse);
                return false;
            }

            int count = sorted.Count;

            // tameValue01 → 중심 인덱스
            int pickIndex = Mathf.RoundToInt(tameValue01 * (count - 1));
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
    }
}