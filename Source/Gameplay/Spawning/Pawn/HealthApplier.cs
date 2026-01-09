using System;
using System.Linq;
using System.Collections.Generic;
using Verse;
using UnityEngine;
using RimWorld;
using System.Reflection;
using static CheeseProtocol.CheeseLog;

namespace CheeseProtocol
{
    public static class HealthApplier
    {
        private static readonly Dictionary<string, int> MinAgeByDefName = new()
        {
            { "BadBack", 41 },
            { "Frail", 51 },
            { "Cataract", 49 },
            { "HearingLoss", 49 },
            { "Dementia", 69 },
            { "Alzheimers", 36 },
            { "HeartArteryBlockage", 21 },
        };
        public static void ApplyHealthHelper(Pawn pawn, float healthQuality)
        {
            var hediffs = BuildHealthHediffs(pawn.ageTracker.AgeBiologicalYears, healthQuality);
            
            foreach (var def in hediffs)
            {
                if (def == null) continue;

                BodyPartRecord part = null;
                if (def == HediffDefOf.Cut)
                {
                    part = PickPartForScarCut(pawn);
                    if (part == null)
                    {
                        QWarn("ScarCut: no available part", Channel.Verse);
                        continue;
                    }
                }
                else
                {
                    part = PickPartForAgeDisease(pawn, def);
                    if (part == null && def.defaultInstallPart != null)
                    {
                        part = pawn.health.hediffSet
                            .GetNotMissingParts()
                            .FirstOrDefault(p => p.def == def.defaultInstallPart);
                    }
                    if (PartIsRequired(def) && part == null)
                    {
                        QWarn($"Available body part not found (def={def.defName} label={def.label})", Channel.Verse);
                        continue;
                    }
                }

                Hediff h = (part != null)
                    ? HediffMaker.MakeHediff(def, pawn, part)
                    : HediffMaker.MakeHediff(def, pawn);

                if (def == HediffDefOf.Cut)
                    h.Severity = 3f;
                else if (def.initialSeverity > 0f)
                    h.Severity = def.initialSeverity;

                if (h is Hediff_Injury inj)
                {
                    var ok = TryForceScar(inj);
                }

                pawn.health.AddHediff(h);
                LogHealthPlan(pawn, healthQuality, hediffs);
            }
        }
        private static bool PartIsRequired(HediffDef def)
        {
            if (def == null) return false;

            switch (def.defName)
            {
                case "BadBack":
                case "Cataract":
                case "HearingLoss":
                case "HeartArteryBlockage":
                    return true;

                // whole-body
                case "Frail":
                case "Dementia":
                case "Alzheimers":
                    return false;

                default:
                    // 모르는 건 "part 불필요"로 두는 편이 덜 터짐 (whole-body 생성 가능)
                    return false;
            }
        }
        private static BodyPartRecord PickPartForAgeDisease(Pawn pawn, HediffDef def)
        {
            var parts = pawn.health.hediffSet.GetNotMissingParts();

            IEnumerable<BodyPartRecord> candidates = def.defName switch
            {
                "Cataract" =>
                    parts.Where(p => p.def == BodyPartDefOf.Eye),

                "HearingLoss" =>
                    parts.Where(p => p.def.tags.Contains(BodyPartTagDefOf.HearingSource)),

                "HeartArteryBlockage" =>
                    parts.Where(p => p.def == BodyPartDefOf.Heart),

                "BadBack" =>
                    parts.Where(p =>
                        p.def.tags.Contains(BodyPartTagDefOf.Spine) ||
                        p.def == BodyPartDefOf.Torso),

                // Frail / Dementia / Alzheimers → whole-body
                _ => null
            };

            if (candidates == null)
                return null;

            // 같은 part에 같은 hediff 중복 방지
            candidates = candidates.Where(p =>
                pawn.health.hediffSet.hediffs.All(h => h.def != def || h.Part != p)
            );

            return candidates.TryRandomElement(out var picked) ? picked : null;
        }
        private static BodyPartRecord PickPartForScarCut(Pawn pawn)
        {
            var parts = pawn.health.hediffSet
                .GetNotMissingParts()
                .Where(p => p.coverage > 0f)
                .ToList();

            if (parts.Count == 0) return null;

            // 1순위: injury 없는 파트
            var clean = parts.Where(p => !HasInjuryOnPart(pawn, p)).ToList();
            if (clean.Count > 0) return clean.RandomElement();

            // 전부 injury가 있으면 그냥 아무 파트(최소 1개는 선택되게)

            return parts.RandomElementWithFallback();
        }

        private static bool HasInjuryOnPart(Pawn pawn, BodyPartRecord part)
        {
            if (pawn == null || part == null) return false;
            return pawn.health.hediffSet.hediffs.Any(h =>
                h.Part == part && h is Hediff_Injury
            );
        }
        public static void ClearAllHediffs(Pawn pawn, bool restoreMissingParts = true)
        {
            if (pawn?.health?.hediffSet == null) return;

            try
            {
                // Restore missing parts
                if (restoreMissingParts)
                {
                    // Common ancestors가 가장 안전하게 복구됨 (중복/하위 파트 꼬임 방지)
                    var missing = pawn.health.hediffSet.GetMissingPartsCommonAncestors().ToList();
                    for (int i = 0; i < missing.Count; i++)
                    {
                        var mp = missing[i];
                        if (mp?.Part == null) continue;

                        pawn.health.RestorePart(mp.Part);
                    }
                }
                // Remove hediffs
                var hediffs = pawn.health.hediffSet.hediffs.ToList();
                for (int i = 0; i < hediffs.Count; i++)
                {
                    pawn.health.RemoveHediff(hediffs[i]);
                }
                pawn.health.Notify_HediffChanged(null);
            }
            catch (Exception e)
            {
                QWarn($"ClearAllHediffs failed: {e}", Channel.Verse);
            }
        }
        public static bool IsImplantLike(HediffDef def)
        {
            if (def == null) return false;
            return def.countsAsAddedPartOrImplant
                   || def.addedPartProps != null
                   || def.spawnThingOnRemoved != null
                   || def.defaultInstallPart != null;
        }

        // "좋은 업그레이드"처럼 보이는 임플란트만 걸러냄
        public static bool LooksLikeGoodUpgradeImplant(HediffDef def)
        {
            if (def == null) return false;
            if (!IsImplantLike(def)) return false;

            if (def.stages == null || def.stages.Count == 0) return false;

            var s = def.stages[0];

            if (s.capMods != null && s.capMods.Any(cm => cm.offset > 0f)) return true;
            if (s.statOffsets != null && s.statOffsets.Any(so => so.value > 0.001f)) return true;
            if (s.statFactors != null && s.statFactors.Any(sf => sf.value > 1.001f)) return true;

            return false;
        }
        public static List<HediffDef> BuildHealthHediffs(int ageYears, float healthQuality01)
        {
            // 1) quality -> target hediff count (inverse)
            int targetCount = DecideTotalCount(healthQuality01);

            var result = new List<HediffDef>(capacity: Math.Max(0, targetCount));

            // 3) age diseases first (max 2)
            int ageCount = Math.Min(2, targetCount);
            int pickedAge = PickAgeDiseases(result, ageYears, ageCount);

            // 4) fill remaining with scarification
            int remaining = targetCount - pickedAge;
            for (int i = 0; i < remaining; i++)
            {
                result.Add(HediffDefOf.Cut);
            }

            return result;
        }
        public static bool TryForceScar(Hediff_Injury inj)
        {
            if (inj == null) {
                QWarn("Hediff_Injury is null", Channel.Verse);
                return false;
            }

            var comp = inj.TryGetComp<HediffComp_GetsPermanent>();
            if (comp == null) 
            {
                QWarn("HediffComp_GetsPermanent is null", Channel.Verse);
                return false;
            }
            comp.IsPermanent = true;

            // Try common private field names across versions
            inj.destroysBodyParts = false;          // optional safety
            inj.pawn?.health?.Notify_HediffChanged(inj);
            return true;
        }

        // --- step 1 ---
        private static int DecideTotalCount(float q01)
        {
            q01 = Mathf.Clamp01(q01);
            // low quality --> less hediffs.
            return Mathf.RoundToInt(Mathf.Lerp(GameplayConstants.HediffCountMin, GameplayConstants.HediffCountMax, 1f - q01));
        }

        // --- step 3 ---
        // Returns how many age-diseases were added
        private static int PickAgeDiseases(List<HediffDef> dst, int age, int maxToAdd)
        {
            if (maxToAdd <= 0) return 0;

            float p = AgeDiseaseChance(age);
            int added = 0;

            if (added < maxToAdd && Rand.Chance(p))
            {
                if (TryPickOneAgeDisease(dst, age, out var def1))
                {
                    dst.Add(def1);
                    added++;
                }
            }

            if (added < maxToAdd && Rand.Chance(p * 0.55f))
            {
                if (TryPickOneAgeDisease(dst, age, out var def2, exclude: dst))
                {
                    dst.Add(def2);
                    added++;
                }
            }

            return added;
        }

        private static float AgeDiseaseChance(int age)
        {
            if (age < 49) return 0f;

            // 49~60: 5% -> 21% (선형)
            if (age <= 60)
                return Mathf.Lerp(0.05f, 0.21f, Mathf.InverseLerp(49f, 60f, age));

            // 60~80: 21% -> 92% (가속, 80+는 92%로 캡)
            return Mathf.Lerp(
                0.21f,
                0.92f,
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(60f, 80f, age))
            );
        }

        public static bool TryPickOneAgeDisease(
            List<HediffDef> dst,
            int age,
            out HediffDef picked,
            List<HediffDef> exclude = null
        )
        {
            picked = null;

            // exclude 키셋
            HashSet<string> banned = new HashSet<string>();
            if (dst != null)
                for (int i = 0; i < dst.Count; i++) if (dst[i] != null) banned.Add(dst[i].defName);
            if (exclude != null)
                for (int i = 0; i < exclude.Count; i++) if (exclude[i] != null) banned.Add(exclude[i].defName);

            // 후보 만들기 (현재는 minAge 전부 0)
            List<HediffDef> candidates = new List<HediffDef>(8);

            foreach (var kv in MinAgeByDefName)
            {
                string defName = kv.Key;
                int minAge = kv.Value;

                if (age < minAge) continue;
                if (banned.Contains(defName)) continue;

                HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
                if (def == null) continue;

                candidates.Add(def);
            }

            if (candidates.Count == 0)
                return false;

            picked = candidates.RandomElement();
            return picked != null;
        }
        public static void LogHealthPlan(
            Pawn pawn,
            float healthQuality,
            IEnumerable<HediffDef> plannedHediffs
        )
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("HealthPlan: ");

            if (pawn != null)
            {
                sb.Append(pawn.LabelShortCap);
                sb.Append(" | Age=");
                sb.Append(pawn.ageTracker.AgeBiologicalYears);
            }

            sb.Append($" | healthQ={healthQuality:0.00}");

            int cut = 0;
            var diseases = new List<string>();

            foreach (var def in plannedHediffs)
            {
                if (def == null) continue;

                if (def == HediffDefOf.Cut)
                    cut++;
                else
                    diseases.Add(def.defName);
            }

            sb.Append($" | plan:Cut={cut},Disease={diseases.Count}");

            if (diseases.Count > 0)
            {
                sb.Append(" | Diseases=[");
                sb.Append(string.Join(", ", diseases));
                sb.Append("]");
            }

            QMsg(sb.ToString(), Channel.Debug);
        }
    }
}