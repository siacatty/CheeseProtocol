using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using static CheeseProtocol.CheeseLog;

namespace CheeseProtocol
{
    public static class TraitApplier
    {
        private static bool IsSexualOrientationTrait(TraitCandidate c)
        {
            var tags = c.exclusionTags;
            if (tags == null || tags.Length == 0) return false;

            for (int i = 0; i < tags.Length; i++)
                if (tags[i] == "SexualOrientation")
                    return true;

            return false;
        }

        private static bool IsCommonalityZero(TraitCandidate c)
        {
            return c.commonality == 0f;
        }

        private static bool SharesExclusionTag(TraitCandidate a, TraitCandidate b)
        {
            if (a.exclusionTags == null || b.exclusionTags == null) return false;

            var aTags = a.exclusionTags;
            var bTags = b.exclusionTags;

            for (int i = 0; i < aTags.Length; i++)
                for (int j = 0; j < bTags.Length; j++)
                    if (aTags[i] == bTags[j])
                        return true;

            return false;
        }

        private static bool ConflictsWithChosen(TraitCandidate cand, List<TraitCandidate> chosen)
        {
            for (int i = 0; i < chosen.Count; i++)
            {
                var c = chosen[i];
                // RimWorld provides ConflictsWith on TraitDef.
                if (cand.def.ConflictsWith(c.def) || c.def.ConflictsWith(cand.def))
                    return true;

                if (SharesExclusionTag(cand, c))
                    return true;
            }
            return false;
        }

        public static List<TraitCandidate> BuildCatalogTraitCandidates()
        {
            var list = new List<TraitCandidate>(512);

            foreach (var def in DefDatabase<TraitDef>.AllDefs)
            {
                if (def == null) continue;
                var defCommonalityObj = TryGet(def, "commonality");
                float defCommonality = defCommonalityObj==null ? -1 : Convert.ToSingle(defCommonalityObj);
                TraitDef[] conflictTraitsArr = def.conflictingTraits?.ToArray() ?? Array.Empty<TraitDef>();
                SkillDef[] conflictPassionsArr = def.conflictingPassions?.ToArray() ?? Array.Empty<SkillDef>();
                string[] exclusionTagsArr = def.exclusionTags?.ToArray() ?? Array.Empty<string>();

                // Spectrum trait: add each degree
                if (def.degreeDatas != null && def.degreeDatas.Count > 0)
                {
                    for (int i = 0; i < def.degreeDatas.Count; i++)
                    {
                        TraitDegreeData d = def.degreeDatas[i];
                        if (d == null) continue;

                        // Degree label is usually d.label (already localized); fallback to def.label/defName
                        string label = !string.IsNullOrEmpty(d.label)
                            ? d.label
                            : (!string.IsNullOrEmpty(def.label) ? def.label : def.defName);

                        list.Add(new TraitCandidate(
                            def,
                            d.degree,
                            label,
                            defCommonality,
                            conflictTraitsArr,
                            conflictPassionsArr,
                            exclusionTagsArr
                        ));
                    }
                }
                else
                {
                    // Singular trait: degree 0
                    string label = !string.IsNullOrEmpty(def.label) ? def.label : def.defName;

                    list.Add(new TraitCandidate(
                        def,
                        0,
                        label,
                        defCommonality,
                        conflictTraitsArr,
                        conflictPassionsArr,
                        exclusionTagsArr
                    ));
                }
            }

            return list;
        }
        public static void BuildPools(
            List<TraitCandidate> catalog,
            List<string> positiveKeys,
            List<string> negativeKeys,
            out List<TraitCandidate> pos,
            out List<TraitCandidate> neu,
            out List<TraitCandidate> neg)
        {
            pos = new List<TraitCandidate>();
            neu = new List<TraitCandidate>();
            neg = new List<TraitCandidate>();

            var posSet = (positiveKeys != null && positiveKeys.Count > 0)
                ? new HashSet<string>(positiveKeys)
                : null;

            var negSet = (negativeKeys != null && negativeKeys.Count > 0)
                ? new HashSet<string>(negativeKeys)
                : null;
            foreach (var c in catalog)
            {
                bool isPos = posSet != null && posSet.Contains(c.key);
                bool isNeg = negSet != null && negSet.Contains(c.key);

                if (isPos) pos.Add(c);
                else if (isNeg) neg.Add(c);
                else neu.Add(c);
            }
        }

        private static bool IsEligibleForRoll(
            TraitCandidate c,
            HashSet<string> positiveSet,
            HashSet<string> negativeSet)
        {
            // 유저가 명시적으로 넣은 건 예외 허용(원하면)
            bool forced = (positiveSet != null && positiveSet.Contains(c.key))
                    || (negativeSet != null && negativeSet.Contains(c.key));

            if (!forced)
            {
                if (c.isSexualOrientation) return false;
                if (c.isCommonalityZero) return false; // 지금은 정책 유보라 했지만, "추첨에서 관리"면 여기서 on/off 가능
            }

            return true;
        }
        private static int DecideNegCount(int traitCount, float traitQuality, int negAvailable)
        {
            if (traitCount <= 0 || negAvailable <= 0) return 0;

            int maxNeg = Mathf.FloorToInt(Mathf.Lerp(2f, 0f, traitQuality));
            maxNeg = Mathf.Clamp(maxNeg, 0, traitCount);
            maxNeg = Mathf.Min(maxNeg, negAvailable);

            int negCount = 0;
            float pSpendNeg = 1f - traitQuality;         // high quality => near 0
            // pSpendNeg *= pSpendNeg; // (optional) stronger protection

            for (int i = 0; i < maxNeg; i++)
                if (Rand.Value < pSpendNeg) negCount++;

            return Mathf.Clamp(negCount, 0, maxNeg);
        }

        // Step 4: decide posCount from traitQuality, after negCount fixed
        private static int DecidePosCount(int traitCount, int negCount, float traitQuality, int posAvailable)
        {
            int slots = traitCount - negCount;
            if (slots <= 0 || posAvailable <= 0) return 0;

            int maxPos = Mathf.Min(slots, posAvailable);

            int posCount = 0;
            float pSpendPos = traitQuality;              // high quality => near 1
            // If you want pos to still appear sometimes at low quality, keep as-is.
            // If you want stricter, use: pSpendPos = Mathf.Clamp01((traitQuality - 0.2f) / 0.8f);

            for (int i = 0; i < maxPos; i++)
                if (Rand.Value < pSpendPos) posCount++;

            return Mathf.Clamp(posCount, 0, maxPos);
        }

        // Step 5: pick from pools in order POS -> NEU -> NEG, with conflict/exclusion checks
        private static int PickFromPool(
            List<TraitCandidate> pool,
            int count,
            List<TraitCandidate> chosen,
            int maxAttemptsPerPick = 50,
            bool allowSpecial = false)
        {
            if (count <= 0 || pool.Count == 0) return 0;

            int picked = 0;

            // mutate a local working list to avoid repeats.
            var work = new List<TraitCandidate>(pool);

            for (int need = 0; need < count; need++)
            {
                bool gotOne = false;

                int attempts = 0;
                while (attempts++ < maxAttemptsPerPick && work.Count > 0)
                {
                    int idx = Rand.Range(0, work.Count);
                    var cand = work[idx];

                    if (!allowSpecial && (IsSexualOrientationTrait(cand) || IsCommonalityZero(cand)))
                    {
                        work.RemoveAt(idx);
                        continue;
                    }
                    if (ConflictsWithChosen(cand, chosen))
                    {
                        work.RemoveAt(idx);
                        continue;
                    }

                    chosen.Add(cand);
                    work.RemoveAt(idx);
                    picked++;
                    gotOne = true;
                    break;
                }

                if (!gotOne) break; // can't satisfy further
            }

            return picked;
        }

        // Step 6: fill shortfalls according to priority POS > NEU > NEG
        private static void FillShortfalls(
            List<TraitCandidate> posPool,
            List<TraitCandidate> neuPool,
            List<TraitCandidate> negPool,
            List<TraitCandidate> chosen,
            ref int posTarget,
            ref int neuTarget,
            ref int negTarget)
        {
            // After initial picking, we try to reach total targets in priority order.
            // Priority is implemented by attempting extra picks from higher-priority pools first.

            int totalTarget = posTarget + neuTarget + negTarget;

            // Compute how many already chosen from each category? (Not strictly needed.)
            // We'll just try to pick more until totalTarget met.

            int safety = 0;
            while (chosen.Count < totalTarget && safety++ < 1000)
            {
                // Try POS first
                int before = chosen.Count;
                PickFromPool(posPool, 1, chosen, maxAttemptsPerPick: 30, allowSpecial: true);
                if (chosen.Count > before) continue;

                // Then NEU
                before = chosen.Count;
                PickFromPool(neuPool, 1, chosen, maxAttemptsPerPick: 30);
                if (chosen.Count > before) continue;

                // Then NEG
                before = chosen.Count;
                PickFromPool(negPool, 1, chosen, maxAttemptsPerPick: 30);
                if (chosen.Count > before) continue;

                // Nowhere to pick from -> reduce targets (final traitCount shrinks)
                break;
            }

            // If we couldn't hit targets, effectively shrink traitCount to chosen.Count.
            // Caller can just apply whatever is chosen.
        }

        // === Your entry point ===
        public static void ApplyTraitsHelper(Pawn pawn, float traitQuality)
        {
            var joinAdvSettings = CheeseProtocolMod.Settings.GetAdvSetting<JoinAdvancedSettings>(CheeseCommand.Join);
            traitQuality = Mathf.Clamp01(traitQuality);

            int traitCount = SampleTraitCount(traitQuality); // your function

            // 1) Build pools
            //BuildPools(pawn, positiveSet, negativeSet, out var posCandidates, out var neuCandidates, out var negCandidates);
            List<TraitCandidate> posList = joinAdvSettings.positiveCandidates;
            List<TraitCandidate> neuList = joinAdvSettings.neutralCandidates;
            List<TraitCandidate> negList = joinAdvSettings.negativeCandidates;
            // 2) Decide counts (NEG first, then POS, rest NEU)
            int negCount = DecideNegCount(traitCount, traitQuality, negList.Count);
            int posCount = DecidePosCount(traitCount, negCount, traitQuality, posList.Count);
            int neuCount = Mathf.Max(0, traitCount - negCount - posCount);
            
            // Clamp by available (soft clamp; we'll also try fill shortfalls later)
            neuCount = Mathf.Min(neuCount, neuList.Count);

            // 3) Pick in order POS -> NEU -> NEG
            var chosen = new List<TraitCandidate>(traitCount);

            PickFromPool(posList, posCount, chosen, allowSpecial: true);
            PickFromPool(neuList, neuCount, chosen);
            PickFromPool(negList, negCount, chosen);

            // 4) Fill shortfalls with priority POS > NEU > NEG (optional but recommended)
            if (chosen.Count < traitCount)
            {
                FillShortfalls(posList, neuList, negList, chosen,
                    ref posCount, ref neuCount, ref negCount);
            }

            // 5) Apply
            if (pawn.story?.traits != null)
            {
                for (int i = 0; i < chosen.Count; i++)
                {
                    var c = chosen[i];
                    pawn.story.traits.GainTrait(new Trait(c.def, c.degree));
                }
            }

            QMsg($"Traits tq={traitQuality:0.00} count={traitCount}, negcount={negCount}, poscount={posCount} -> chosen={chosen.Count} (posPool={posList.Count}, neuPool={neuList.Count}, negPool={negList.Count})", Channel.Debug);
            
        }
        // Placeholder: you already have this.
        private static int SampleTraitCount(float tq)
        {
            tq = Mathf.Clamp01(tq);

            // w1: high quality에서 거의 0으로
            float w1 = Mathf.Pow(1f - tq, 3f);          // tq=1 -> 0, tq=0 -> 1

            // w3: high quality에서 커지게
            float w3 = Mathf.Pow(tq, 2f);
            float w2 = 1.2f + 0.6f * (1f - Mathf.Abs(2f * tq - 1f));
            float sum = w1 + w2 + w3;
            float r = Rand.Value * sum;
            if (r < w1) return 1;
            r -= w1;
            if (r < w2) return 2;
            return 3;
        }

        private static object TryGet(object obj, string name)
        {
            if (obj == null) return null;
            var t = obj.GetType();

            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null) return f.GetValue(obj);

            var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.GetIndexParameters().Length == 0)
            {
                try { return p.GetValue(obj); } catch { }
            }
            return null;
        }
    }
}