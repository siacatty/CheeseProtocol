using System;
using System.Reflection;
using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;
using System.Linq;
using System.Collections;
using Verse.Noise;
using static CheeseProtocol.CheeseLog;
using System.Text;

namespace CheeseProtocol
{
    internal static class ColonistSpawner
    {
        public static void Spawn(string donorName, int amount, string message)
        {
            JoinAdvancedSettings joinAdvSetting = CheeseProtocolMod.Settings?.GetAdvSetting<JoinAdvancedSettings>(CheeseCommand.Join);
            if (joinAdvSetting == null) return;
            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;
            
            float quality = QualityEvaluator.evaluateQuality(amount, CheeseCommand.Join);
            CheeseRollTrace trace = new CheeseRollTrace(donorName, CheeseCommand.Join);
            // Generate a player colonist
            Pawn pawn = Generate(quality, trace);
            if (pawn == null || pawn.Destroyed)
            {
                QErr("Pawn invalid (null or destroyed)", Channel.Verse);
                return;
            }
            
            pawn.SetFaction(Faction.OfPlayer);
            if (!string.IsNullOrWhiteSpace(donorName))
                pawn.Name = new NameSingle(TrimName(donorName));

            if (!CheeseParticipantRegistry.Get().TryRegister(donorName, pawn, out string reason))
            {
                QWarn($"CheeseParticipant register failed. reason={reason}");
                CheeseLetter.AlertFail("!참여", $"최대 합류인원을 초과해 {donorName}님이 합류하지 못했습니다.");
                return;
            }

            IntVec3 rootCell = IntVec3.Invalid;
            if (joinAdvSetting.useDropPod)
            {
                rootCell = DropCellFinder.TradeDropSpot(map);
                DropPodUtility.DropThingsNear(rootCell, map, new Thing[] { pawn }, 110, canInstaDropDuringInit: false, leaveSlag: false);
            }
            if (!joinAdvSetting.useDropPod || !rootCell.IsValid)
            {
                rootCell = CellFinderLoose.RandomCellWith(
                    c => c.Standable(map) && !c.Fogged(map),
                    map, 200);
                if (!rootCell.IsValid)
                {
                    QWarn("Available spawn cell for colonist not found", Channel.Verse);
                    CheeseLetter.AlertFail("!참여", $"{donorName}님이 맵에 진입할 수 있는 경로를 찾지 못했습니다.");
                    return;
                }
                GenSpawn.Spawn(pawn, rootCell, map);
            }
            string letterText = $"새로운 동료가 합류합니다.\n<color=#d09b61>{donorName}</color>님이 조심스럽게 인사를 건넵니다.";
            CheeseLetter.SendCheeseLetter(
                CheeseCommand.Join,
                "합류",
                letterText,
                new LookTargets(pawn),
                trace,
                map,
                LetterDefOf.PositiveEvent
            );
        }

        public static Pawn Generate(float quality, CheeseRollTrace trace)
        {
            JoinAdvancedSettings joinAdvSetting = CheeseProtocolMod.Settings?.GetAdvSetting<JoinAdvancedSettings>(CheeseCommand.Join);
            if (joinAdvSetting == null) return null;
            var race = RaceApplier.GetRace();
            var req = new PawnGenerationRequest(
                race,
                context: PawnGenerationContext.PlayerStarter,
                forceGenerateNewPawn: true,
                canGeneratePawnRelations: false,
                forcedXenotype: XenotypeDefOf.Baseliner
            );
            Pawn pawn = PawnGenerator.GeneratePawn(req);
            bool isCustomRace = race != PawnKindDefOf.Colonist;
            cleanPawn(pawn, joinAdvSetting.allowWorkDisable, isCustomRace);
            ApplyPawnCustomization(pawn, quality, trace, isCustomRace);
            //ApplyRace(pawn);
            if (!isCustomRace)
                pawn.skills?.DirtyAptitudes();
            pawn.skills?.Notify_SkillDisablesChanged();
            pawn.Notify_DisabledWorkTypesChanged();
            pawn.workSettings?.Notify_DisabledWorkTypesChanged();
            return pawn;
        }

        private static void ApplyIdeo(Pawn pawn, bool forcePlayerIdeo)
        {
            if (pawn == null) return;
            if (!ModsConfig.IdeologyActive) return;
            if (pawn.ideo == null) return;
            var ideos = Find.IdeoManager?.IdeosListForReading;
            if (ideos == null || ideos.Count == 0) return;
            Ideo chosen = null;
            if (forcePlayerIdeo)
            {
                chosen = Faction.OfPlayer?.ideos?.PrimaryIdeo;
                if (chosen == null)
                    chosen = ideos.RandomElement(); // fallback
            }
            else
            {
                chosen = ideos.RandomElement();
            }

            if (chosen == null) return; // 방어
            pawn.ideo.SetIdeo(chosen);
        }

        private static void ApplyXenotype(Pawn pawn, bool forceHuman)
        {
            if (pawn == null) return;
            if (!ModsConfig.BiotechActive) return;
            var genes = pawn.genes;
            if (genes == null) return;

            if (forceHuman)
            {
                genes.SetXenotype(XenotypeDefOf.Baseliner);
                return;
            }

            XenotypeDef xeno = TryPickRandomXenotypeDef();
            if (xeno == null) return;
            genes.SetXenotype(xeno);
        }

        private static XenotypeDef TryPickRandomXenotypeDef()
        {
            if (!ModsConfig.BiotechActive) return null;
            return DefDatabase<XenotypeDef>.AllDefsListForReading
                .Where(x => x != null)
                .RandomElementWithFallback();
        }

        private static void cleanPawn(Pawn pawn, bool allowWorkDisable, bool isCustomRace)
        {
            if (pawn == null) return;
            foreach (var skill in pawn.skills.skills)
            {
                skill.Level = 0;
                skill.passion = Passion.None;
            }
            if (!isCustomRace)
                ClearGenes(pawn);
            pawn.story.traits.allTraits.Clear();
            if (!allowWorkDisable)
                SetNoDisableBackstories(pawn);
            HealthApplier.ClearAllHediffs(pawn);
            pawn.Notify_DisabledWorkTypesChanged();
        }

        static void ClearGenes(Pawn pawn)
        {
            var genes = pawn?.genes;
            if (genes == null) return;

            // 복사본으로 순회 (cachedGenes 이슈 방지)
            var all = genes.GenesListForReading.ToList();

            foreach (var g in all)
            {
                var cat = g.def.endogeneCategory;

                // ✅ 기본 외형 유전자는 유지
                if (cat == EndogeneCategory.Melanin ||
                    cat == EndogeneCategory.HairColor)
                    continue;

                genes.RemoveGene(g);
            }
        }
        public static void ClearAllGenes(Pawn pawn)
        {
            var genes = pawn?.genes;
            if (genes == null) return;

            var all = genes.GenesListForReading.ToList();
            for (int i = 0; i < all.Count; i++)
                genes.RemoveGene(all[i]);
        }
        private static BackstoryDef PickNoDisableBackstory(BackstorySlot slot)
        {
            return DefDatabase<BackstoryDef>.AllDefs
                .Where(b => b.slot == slot)
                .Where(b => b.workDisables == WorkTags.None)   // "결격 없음"
                .RandomElementWithFallback();
        }

        private static void SetNoDisableBackstories(Pawn pawn)
        {
            var child = PickNoDisableBackstory(BackstorySlot.Childhood);
            if (child != null) pawn.story.Childhood = child;

            var adult = PickNoDisableBackstory(BackstorySlot.Adulthood);
            if (adult != null) pawn.story.Adulthood = adult;
        }

        public static List<SkillDef> GetBlockedSkills(Pawn pawn)
        {
            var result = new HashSet<SkillDef>();

            if (pawn?.RaceProps == null || !pawn.RaceProps.Humanlike) return result.ToList();
            if (pawn.skills?.skills == null) return result.ToList();

            foreach (WorkTypeDef work in DefDatabase<WorkTypeDef>.AllDefs)
            {
                if (!pawn.WorkTypeIsDisabled(work)) continue;

                if (work.relevantSkills == null) continue;

                foreach (SkillDef s in work.relevantSkills)
                    if (s != null) result.Add(s);
            }

            return result.ToList();
        }

        private static void ApplyPawnCustomization(Pawn pawn, float quality, CheeseRollTrace trace, bool isCustomRace)
        {
            if (pawn?.RaceProps == null || !pawn.RaceProps.Humanlike) return;
            var settings = CheeseProtocolMod.Settings;
            JoinAdvancedSettings joinAdvSetting = settings.GetAdvSetting<JoinAdvancedSettings>(CheeseCommand.Join);
            float randomVar = settings.randomVar; //higher values --> bigger noise (lucky/unlucky)
            //float lower_tail = 0.1f; //higher values --> less likely for high amount donation to get unlucky
            ApplyAge(pawn, quality, randomVar, joinAdvSetting.ageRange, trace);
            ApplySkills(pawn, quality, randomVar, joinAdvSetting.skillRange, trace);
            ApplyPassions(pawn, quality, randomVar, joinAdvSetting.passionRange, trace);
            if (!isCustomRace)
                ApplyXenotype(pawn, joinAdvSetting.forceHuman);
            ApplyIdeo(pawn, joinAdvSetting.forcePlayerIdeo);
            ApplyTraits(pawn, quality, randomVar, joinAdvSetting.traitsRange, trace);
            ApplyHealth(pawn, quality, randomVar, joinAdvSetting.healthRange, trace);
            //ApplyApparel(pawn, quality);
            //ApplyWeapon(pawn, quality);
        }

        private static void ApplyAge(Pawn pawn, float quality, float randomVar, QualityRange minMaxRange, CheeseRollTrace trace)
        {
            int baseMin = GameplayConstants.AgeMin;
            int baseMax = GameplayConstants.AgeMax;
            TraceStep traceStep = new TraceStep("나이", isInverse: true);
            float ageQuality = QualityBetaSampler.SampleQualityWeightedBeta(
                quality,
                minMaxRange,
                concentration01: 1f-randomVar,
                traceStep,
                inverseQ: true
            );
            int age = Mathf.Clamp(Mathf.RoundToInt(ageQuality), baseMin, baseMax);
            traceStep.value = age;
            trace.steps.Add(traceStep);
            long bioTicks = age * 3600000L;
            long chronoTicks = age * 3600000L;
            
            pawn.ageTracker.AgeBiologicalTicks = bioTicks;
            pawn.ageTracker.AgeChronologicalTicks = chronoTicks;

            pawn.ageTracker.ResetAgeReversalDemand(Pawn_AgeTracker.AgeReversalReason.Initial, false);
        }

        private static void ApplyTraits(Pawn pawn, float quality, float randomVar, QualityRange minMaxRange, CheeseRollTrace trace)
        {
            TraceStep traceStep = new TraceStep("특성");
            float traitQuality = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    minMaxRange,
                    concentration01: 1f-randomVar,
                    traceStep,
                    debugLog: false
            );
            //Mathf.Lerp(minMaxRange.qMin, minMaxRange.qMax, quality)
            trace.steps.Add(traceStep);
            TraitApplier.ApplyTraitsHelper(pawn, traitQuality);
            trace.traits = pawn.story?.traits?.allTraits
                                ?.Select(t => t.LabelCap)
                                .ToList() ?? new List<string>();    
        }

        private static void ApplySkills(Pawn pawn, float quality, float randomVar, QualityRange minMaxRange, CheeseRollTrace trace)
        {
            if (pawn.skills?.skills == null || pawn.skills.skills.Count == 0) return;
            int baseMin = GameplayConstants.SkillLevelMin;
            int baseMax = GameplayConstants.SkillLevelMax;
            float totalscore = 0;
            float totalskills = 0;
            TraceStep traceStep = new TraceStep("스킬 평균레벨");
            foreach (var skill in pawn.skills.skills)
            {
                float levelF = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    minMaxRange,
                    concentration01: 1f-randomVar,
                    traceStep
                );
                totalscore += traceStep.score;
                totalskills += levelF;
                skill.Level = Mathf.Clamp(Mathf.RoundToInt(levelF), baseMin, baseMax);
            }
            int count = pawn.skills.skills.Count;
            if (count == 0)
                return;
            traceStep.value = totalskills/count;
            traceStep.score = totalscore/count;
            trace.steps.Add(traceStep);
        }

        private static void ApplyHealth(Pawn pawn, float quality, float randomVar, QualityRange minMaxRange, CheeseRollTrace trace)
        {
            TraceStep traceStep = new TraceStep("건강");
            float healthQuality = QualityBetaSampler.SampleQualityWeightedBeta(
                quality,
                minMaxRange,
                concentration01: 1f-randomVar,
                traceStep
            );
            trace.steps.Add(traceStep);
            HealthApplier.ApplyHealthHelper(pawn, healthQuality);
            trace.hediffs = pawn.health?.hediffSet?.hediffs
                            ?.Select(h => h.LabelCap)
                            .ToList() ?? new List<string>();
        }

        private static void ApplyPassions(Pawn pawn, float quality, float randomVar, QualityRange minMaxRange, CheeseRollTrace trace)
        {
            int baseMin = GameplayConstants.PassionMin;
            int baseMax = GameplayConstants.PassionMax;
            TraceStep traceStep = new TraceStep("열정 개수");
            float passionCountF = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    minMaxRange,
                    concentration01: 1f-randomVar,
                    traceStep
                );
            int baseCount = Mathf.Clamp(Mathf.RoundToInt(passionCountF), baseMin, baseMax);                 // floor
            float frac = passionCountF - baseCount;             // [0,1)
            int passionCount = baseCount + (Rand.Value < frac ? 1 : 0);
            traceStep.value = passionCount;
            trace.steps.Add(traceStep);

            if (passionCount <= 0) return;
            if (pawn?.skills?.skills == null) return;

            var pool = pawn.skills.skills;
            var blocked = new HashSet<SkillDef>(GetBlockedSkills(pawn));
            int availableCount = 0;
            for (int i = 0; i < pool.Count; i++)
                if (!blocked.Contains(pool[i].def)) availableCount++;
            if (availableCount <= 0) return;

            int maxBudget = availableCount * 2;
            if (passionCount > maxBudget) passionCount = maxBudget;
            float levelWeightPower = 5f;

            while (passionCount > 0)
            {
                SkillRecord chosen = DrawWeightedByLevel(pool, blocked, levelWeightPower, passionCount);
                if (chosen == null)
                    break; // 👉 구조적으로 종료

                if (passionCount >= 2)
                {
                    float majorChance = Mathf.Lerp(0.3f, 0.80f, Mathf.Clamp01(quality));
                    if (Rand.Value < majorChance)
                    {
                        if (chosen.passion == Passion.None) passionCount -= 2;
                        else if (chosen.passion == Passion.Minor) passionCount -= 1;

                        chosen.passion = Passion.Major;
                        continue;
                    }
                }

                // 여기 오면 None → Minor만 가능
                if (chosen.passion == Passion.None)
                {
                    chosen.passion = Passion.Minor;
                    passionCount -= 1;
                }
            }
        }
        private static SkillRecord DrawWeightedByLevel(
            List<SkillRecord> pool,
            HashSet<SkillDef> blocked,
            float power,
            int passionCount)
        {
            float total = 0f;

            for (int i = 0; i < pool.Count; i++)
            {
                var s = pool[i];
                if (blocked.Contains(s.def)) continue;

                // 🔒 이번 루프에서 "소비 불가능"한 스킬은 후보 제외
                if (passionCount == 1)
                {
                    // 1포인트면 None만 가능
                    if (s.passion != Passion.None) continue;
                }
                else
                {
                    // 2포인트 이상이면 Major 제외
                    if (s.passion == Passion.Major) continue;
                }

                int lvl = Mathf.Max(0, s.Level);
                float w = Mathf.Pow(lvl + 1f, power);
                total += w;
            }

            if (total <= 0f)
                return null; // 👉 더 이상 소비 가능한 후보 없음

            float r = Rand.Value * total;
            float acc = 0f;

            for (int i = 0; i < pool.Count; i++)
            {
                var s = pool[i];
                if (blocked.Contains(s.def)) continue;

                if (passionCount == 1)
                {
                    if (s.passion != Passion.None) continue;
                }
                else
                {
                    if (s.passion == Passion.Major) continue;
                }

                int lvl = Mathf.Max(0, s.Level);
                float w = Mathf.Pow(lvl + 1f, power);
                acc += w;
                if (r <= acc) return s;
            }

            return null;
        }
        private static string TrimName(string s)
        {
            s = s.Trim();
            if (s.Length > 24) s = s.Substring(0, 24);
            return s;
        }
    }
}
