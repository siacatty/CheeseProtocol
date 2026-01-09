using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using static CheeseProtocol.CheeseLog;

namespace CheeseProtocol
{
    public static class MeteorApplier
    {
        public static void ApplyMeteorSizeHelper(MeteorRequest meteor, int baseSize)
        {
            float sizeFactor =
                Mathf.Clamp(
                    0.7f
                    + 0.15f * Mathf.Log(1f + meteor.lumpAvg)
                    - 0.25f * Mathf.Log(1f + meteor.score),
                    0.4f,
                    1.0f
                );
            meteor.size = Mathf.RoundToInt(baseSize * sizeFactor);
            meteor.scatterRadius = 0;
            QMsg($"Meteor baseSize = {baseSize}, sizeFactor = {sizeFactor}", Channel.Debug);
        }
        public static void ApplyMeteorTypeHelper(MeteorRequest meteor, float meteorQuality, List<MeteorCandidate> candidates)
        {
            MeteorCandidate chosenType = default;
            if (candidates == null || candidates.Count == 0)
            {
                QWarn("ApplyMeteorHelper: Meteor candidates empty");
                return;
            }
            int bucketSize = 3;
            var sorted = new List<MeteorCandidate>(candidates);
            sorted.Sort((a, b) => a.score.CompareTo(b.score));
            List<int> scoreKeys = new List<int>();
            float prevScoreKey = -1f;
            foreach (MeteorCandidate c in sorted)
            {
                if (c.scoreKey != prevScoreKey && c.scoreKey >= 0)
                {
                    scoreKeys.Add(c.scoreKey);
                    prevScoreKey = c.scoreKey;
                }
            }
            int n = scoreKeys.Count;
            float q = Mathf.Clamp01(meteorQuality);
            int idx = Mathf.Clamp(Mathf.FloorToInt(q * (n - 1)), 0, n - 1);
            var bucketRange = getBucketRange(scoreKeys.Count, idx, bucketSize);

            var result = new List<int>(Math.Max(0, bucketRange.end - bucketRange.start + 1));
            for (int i = bucketRange.start; i <= bucketRange.end; i++) result.Add(scoreKeys[i]);
            int chosenScoreKey = result.RandomElement();
            int count = 0;
            foreach (var c in sorted)
            {
                if (c.scoreKey != chosenScoreKey) continue;
                // reservoir sampling
                if (Rand.RangeInclusive(0, count++) == 0)
                    chosenType = c;
            }
            meteor.lumpAvg = (chosenType.lumpSizeRange.min + chosenType.lumpSizeRange.max) / 2f;
            meteor.score = chosenType.score;
            meteor.def = chosenType.def;
            meteor.type = chosenType.defName;
            meteor.label = chosenType.label;
        }
        private static (int start, int end) getBucketRange(int count, int centerIdx, int bucketSize)
        {
            if (count <= 0) return (0, -1);
            if ((uint)centerIdx >= (uint)count) throw new ArgumentOutOfRangeException(nameof(centerIdx));
            if (bucketSize <= 0) return (centerIdx, centerIdx);

            int take = Math.Min(bucketSize, count);

            int leftNeed = (take - 1) / 2;
            int rightNeed = (take - 1) - leftNeed;

            int leftAvail = centerIdx;
            int rightAvail = (count - 1) - centerIdx;

            int leftTake = Math.Min(leftNeed, leftAvail);
            int rightTake = Math.Min(rightNeed, rightAvail);

            int remaining = (take - 1) - (leftTake + rightTake);
            if (remaining > 0)
            {
                int addRight = Math.Min(remaining, rightAvail - rightTake);
                rightTake += addRight;
                remaining -= addRight;
            }
            if (remaining > 0)
            {
                int addLeft = Math.Min(remaining, leftAvail - leftTake);
                leftTake += addLeft;
                remaining -= addLeft;
            }

            int start = centerIdx - leftTake;
            int end = centerIdx + rightTake;
            return (start, end);
        }
        public static List<ThingDef> CollectMineableMeteorDefs()
        {
            var result = new List<ThingDef>(128);

            var all = DefDatabase<ThingDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                var def = all[i];
                if (def == null) continue;
                var b = def.building;
                if (b == null) continue;
                if (b.mineableThing == null) continue;

                result.Add(def);
            }

            return result;
        }
        public static List<MeteorCandidate> BuildCatalogMeteorCandidates()
        {
            var list = new List<MeteorCandidate>(512);

            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def == null) continue;
                var b = def.building;
                if (b == null) continue;
                if (b.mineableThing == null) continue;
                if (b.mineablePreventMeteorite) continue;
                if (def.defName.StartsWith("Smoothed", StringComparison.OrdinalIgnoreCase)) continue;

                list.Add(new MeteorCandidate(
                    def,
                    def.defName,
                    def.LabelCap,
                    b.mineableThing.defName,
                    b.mineableThing.LabelCap,
                    b.mineableThing.BaseMarketValue,
                    b.mineablePreventMeteorite,
                    b.mineableDropChance,
                    b.mineableScatterCommonality,
                    b.mineableScatterLumpSizeRange
                ));
            }
            return list;
        }
        public static void BuildPools(
            List<MeteorCandidate> catalog,
            List<string> meteorKeys,
            out List<MeteorCandidate> allowed,
            out List<MeteorCandidate> disallowed
            )
        {
            allowed = new List<MeteorCandidate>();
            disallowed = new List<MeteorCandidate>();

            var allowedSet = (meteorKeys != null && meteorKeys.Count > 0)
                ? new HashSet<string>(meteorKeys)
                : null;
            foreach (var c in catalog)
            {
                bool isAllowed = allowedSet != null && allowedSet.Contains(c.key);
                if (isAllowed) allowed.Add(c);
                else disallowed.Add(c);
            }
        }

        
    }
}
