using System;
using System.Reflection;
using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;
using System.Linq;
using System.Collections;
using Verse.Noise;

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
            
            // Generate a player colonist
            var req = new PawnGenerationRequest(
                PawnKindDefOf.Colonist,
                Faction.OfPlayer,
                PawnGenerationContext.PlayerStarter,
                map.Tile,
                forceGenerateNewPawn: true,
                canGeneratePawnRelations: false
            );
            Pawn pawn = PawnGenerator.GeneratePawn(req);

            float quality = QualityEvaluator.evaluateQuality(amount, CheeseCommand.Join);
            if (Prefs.DevMode)
            {
                Log.Message($"[CheeseProtocol] Spawn quality={quality:0.00}");
            }
            if (pawn == null || pawn.Destroyed)
            {
                Log.Error("[CheeseProtocol] Pawn invalid (null or destroyed)");
                return;
            }

            if (!string.IsNullOrWhiteSpace(donorName))
                pawn.Name = new NameSingle(TrimName(donorName));

            cleanPawn(pawn, joinAdvSetting.allowWorkDisable);
            CheeseRollTrace trace = new CheeseRollTrace(donorName, CheeseCommand.Join);
            ApplyPawnCustomization(pawn, quality, trace);
            pawn.skills?.Notify_SkillDisablesChanged();
            pawn.Notify_DisabledWorkTypesChanged();
            pawn.workSettings?.Notify_DisabledWorkTypesChanged();
            IntVec3 rootCell = IntVec3.Invalid;
            if (joinAdvSetting.useDropPod)
            {
                rootCell = DropCellFinder.TradeDropSpot(map);
                DropPodUtility.DropThingsNear(rootCell, map, new Thing[] { pawn }, 110, canInstaDropDuringInit: false, leaveSlag: false);
            }
            else
            {
                rootCell = CellFinderLoose.RandomCellWith(
                    c => c.Standable(map) && !c.Fogged(map),
                    map, 200);

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
            if (forceHuman) return;
            if (pawn == null) return;
            if (!ModsConfig.BiotechActive) return;

            var genes = pawn.genes;
            if (genes == null) return;

            XenotypeDef xeno = TryPickRandomXenotypeDef_NoReflection();
            if (xeno == null) return;
            genes.SetXenotype(xeno);
        }

        private static XenotypeDef TryPickRandomXenotypeDef_NoReflection()
        {
            if (!ModsConfig.BiotechActive) return null;
            return DefDatabase<XenotypeDef>.AllDefsListForReading
                .Where(x => x != null)
                .RandomElementWithFallback();
        }

        private static Def TryPickRandomXenotypeDef()
        {
            if (!ModsConfig.BiotechActive) return null;
            var allXenos = DefDatabase<Def>.AllDefs
                .Where(d => d != null && d.GetType().Name == "XenotypeDef")
                .ToList();

            if (allXenos.Count == 0) return null;

            return allXenos.RandomElement();
        }

        private static void cleanPawn(Pawn pawn, bool allowWorkDisable)
        {
            if (pawn == null) return;
            foreach (var skill in pawn.skills.skills)
            {
                skill.Level = 0;
                skill.passion = Passion.None;
            }
            pawn.story.traits.allTraits.Clear();
            if (!allowWorkDisable)
                SetNoDisableBackstories(pawn);
            HealthApplier.ClearAllHediffs(pawn);
            pawn.Notify_DisabledWorkTypesChanged();
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

        private static void ApplyPawnCustomization(Pawn pawn, float quality, CheeseRollTrace trace)
        {
            if (pawn?.RaceProps == null || !pawn.RaceProps.Humanlike) return;
            if (pawn.skills?.skills == null || pawn.skills.skills.Count == 0) return;
            var settings = CheeseProtocolMod.Settings;
            JoinAdvancedSettings joinAdvSetting = settings.GetAdvSetting<JoinAdvancedSettings>(CheeseCommand.Join);
            float randomVar = settings.randomVar; //higher values --> bigger noise (lucky/unlucky)
            //float lower_tail = 0.1f; //higher values --> less likely for high amount donation to get unlucky
            ApplyAge(pawn, quality, randomVar, joinAdvSetting.ageRange, trace);
            ApplySkills(pawn, quality, randomVar, joinAdvSetting.skillRange, trace);
            ApplyPassions(pawn, quality, randomVar, joinAdvSetting.passionRange, trace);
            ApplyXenotype(pawn, joinAdvSetting.forceHuman);
            ApplyIdeo(pawn, joinAdvSetting.forcePlayerIdeo);
            //Log.Warning($"[CheeseProtocol] traitsRange = {joinSettings.traitsRange.qMin} ~ {joinSettings.traitsRange.qMax}");
            ApplyTraits(pawn, quality, randomVar, joinAdvSetting.traitsRange, trace);
            ApplyHealth(pawn, quality, randomVar, joinAdvSetting.healthRange, trace);
            //ApplyApparel(pawn, quality);
            //ApplyWeapon(pawn, quality);
        }

        private static void ApplyAge(Pawn pawn, float quality, float randomVar, QualityRange minMaxRange, CheeseRollTrace trace)
        {
            int baseMin = GameplayConstants.AgeMin;
            int baseMax = GameplayConstants.AgeMax;
            float ageQuality = QualityBetaSampler.SampleQualityWeightedBeta(
                quality,
                minMaxRange,
                concentration01: 1f-randomVar,
                out float score,
                inverseQ: true
            );
            int age = Mathf.Clamp(Mathf.RoundToInt(ageQuality), baseMin, baseMax);
            trace.steps.Add(new TraceStep("나이", score, minMaxRange.Expected(1-quality), age, isInverse: true));
            long bioTicks = age * 3600000L;
            long chronoTicks = age * 3600000L;
            
            pawn.ageTracker.AgeBiologicalTicks = bioTicks;
            pawn.ageTracker.AgeChronologicalTicks = chronoTicks;

            pawn.ageTracker.ResetAgeReversalDemand(Pawn_AgeTracker.AgeReversalReason.Initial, false);
        }

        private static void ApplyTraits(Pawn pawn, float quality, float randomVar, QualityRange minMaxRange, CheeseRollTrace trace)
        {
            float traitQuality = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    minMaxRange,
                    concentration01: 1f-randomVar,
                    out float score,
                    debugLog: false
            );
            //Mathf.Lerp(minMaxRange.qMin, minMaxRange.qMax, quality)
            trace.steps.Add(new TraceStep("특성", score, minMaxRange.Expected(quality), traitQuality));
            TraitApplier.ApplyTraitsHelper(pawn, traitQuality);    
        }

        private static void ApplySkills(Pawn pawn, float quality, float randomVar, QualityRange minMaxRange, CheeseRollTrace trace)
        {
            int baseMin = GameplayConstants.SkillLevelMin;
            int baseMax = GameplayConstants.SkillLevelMax;
            float totalscore = 0;
            float totalskills = 0;
            foreach (var skill in pawn.skills.skills)
            {
                float levelF = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    minMaxRange,
                    concentration01: 1f-randomVar,
                    out float score
                );
                totalscore += score;
                totalskills += levelF;
                skill.Level = Mathf.Clamp(Mathf.RoundToInt(levelF), baseMin, baseMax);
            }
            int count = pawn.skills.skills.Count;
            if (count == 0)
                return;
            trace.steps.Add(new TraceStep("스킬 평균레벨", totalscore/count, minMaxRange.Expected(quality), totalskills/count));
        }

        private static void ApplyHealth(Pawn pawn, float quality, float randomVar, QualityRange minMaxRange, CheeseRollTrace trace)
        {
            float healthQuality = QualityBetaSampler.SampleQualityWeightedBeta(
                quality,
                minMaxRange,
                concentration01: 1f-randomVar,
                out float score
            );
            trace.steps.Add(new TraceStep("건강", score, minMaxRange.Expected(quality), healthQuality));
            HealthApplier.ApplyHealthHelper(pawn, healthQuality);
        }

        private static void ApplyPassions(Pawn pawn, float quality, float randomVar, QualityRange minMaxRange, CheeseRollTrace trace)
        {
            int baseMin = GameplayConstants.PassionMin;
            int baseMax = GameplayConstants.PassionMax;
            float passionCountF = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    minMaxRange,
                    concentration01: 1f-randomVar,
                    out float score
                );
            int baseCount = Mathf.Clamp(Mathf.RoundToInt(passionCountF), baseMin, baseMax);                 // floor
            float frac = passionCountF - baseCount;             // [0,1)
            int passionCount = baseCount + (Rand.Value < frac ? 1 : 0);
            trace.steps.Add(new TraceStep("열정 개수", score, minMaxRange.Expected(quality), passionCount));

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
        private static BackstoryDef FindBackstoryByTitle(BackstorySlot slot, string contains)
        {
            contains = contains.ToLowerInvariant();

            return DefDatabase<BackstoryDef>.AllDefs
                .Where(b => b.slot == slot)
                .FirstOrDefault(b =>
                    (b.title ?? "").ToLowerInvariant().Contains(contains) ||
                    (b.titleShort ?? "").ToLowerInvariant().Contains(contains) ||
                    b.defName.ToLowerInvariant().Contains(contains)
                );
        }
        private static string TrimName(string s)
        {
            s = s.Trim();
            if (s.Length > 24) s = s.Substring(0, 24);
            return s;
        }
        public static void DumpTraitDefMembers(string defName)
        {
            TraitDef def = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                Log.Warning($"[CheeseProtocol][TraitDump] TraitDef not found: {defName}");
                return;
            }

            Type t = def.GetType();

            var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .OrderBy(f => f.Name)
                        .Select(f =>
                        {
                            object v = null;
                            try { v = f.GetValue(def); } catch { }
                            string vs = v == null ? "null" : v.ToString();
                            return $"{f.FieldType.Name} {f.Name} = {vs}";
                        });

            var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(p => p.GetIndexParameters().Length == 0)
                        .OrderBy(p => p.Name)
                        .Select(p =>
                        {
                            object v = null;
                            try { v = p.GetValue(def); } catch { }
                            string vs = v == null ? "null" : v.ToString();
                            return $"{p.PropertyType.Name} {p.Name} = {vs}";
                        });

            Log.Message("[CheeseProtocol][TraitDump] === Fields ===\n" + string.Join("\n", fields));
            Log.Message("[CheeseProtocol][TraitDump] === Properties ===\n" + string.Join("\n", props));
        }
        public static void LogTraitCandidates(IEnumerable<TraitCandidate> list, string title = null)
        {
            if (!Prefs.DevMode) return;

            if (!string.IsNullOrEmpty(title))
                Log.Message($"[CheeseProtocol] ==== {title} ====");

            int i = 0;
            foreach (var c in list)
            {
                Log.Message(
                    $"[{i++}] {c.key} | label='{c.label}' | commonality={c.commonality:0.###} | " +
                    $"sexual={c.isSexualOrientation} | " +
                    $"conflictsT={c.conflictTraits?.Length ?? 0} | " +
                    $"conflictsP={c.conflictPassions?.Length ?? 0} | " +
                    $"exTags={c.exclusionTags?.Length ?? 0}"
                );
            }

            Log.Message($"[CheeseProtocol] ==== total: {i} ====");
        }
        public static void LogAllTraitsSummary()
        {
            foreach (TraitDef def in DefDatabase<TraitDef>.AllDefs)
            {
                var degreeList = TryGet(def, "degreeDatas") as IList;
                if (degreeList == null || degreeList.Count == 0)
                    continue; // degree 자체가 없는 경우만 제외

                // TraitDef 공통 메타
                string reqTags = (TryGet(def, "requiredWorkTags") ?? "None").ToString();
                string disTags = (TryGet(def, "disabledWorkTags") ?? "None").ToString();
                string conflicts = FormatEnum(TryGet(def, "conflictingTraits"));
                string exclusion = FormatEnum(TryGet(def, "exclusionTags"));

                // TraitDef.commonality (fallback 용)
                string defCommonality = (TryGet(def, "commonality") ?? "").ToString();

                for (int i = 0; i < degreeList.Count; i++)
                {
                    object d = degreeList[i];
                    if (d == null) continue;

                    string degree = (TryGet(d, "degree") ?? i).ToString();

                    // 한글 label은 거의 항상 degreeDatas 쪽에 있음
                    string label =
                        (TryGet(d, "label")
                        ?? TryGet(d, "LabelCap")
                        ?? def.defName).ToString();

                    // degree별 commonality 우선, 없으면 def 쪽
                    string degreeCommonality =
                        (TryGet(d, "commonality") ?? defCommonality).ToString();

                    string desc = (TryGet(d, "description") ?? "").ToString();

                    Log.Message(
                        $"[CheeseProtocol][TraitDegree] " +
                        $"key={def.defName}({degree}) " +
                        $"defName={def.defName} degree={degree} label={label} " +
                        $"def commonality={defCommonality} " +
                        $"commonality={degreeCommonality} " +
                        $"requiredWorkTags={reqTags} disabledWorkTags={disTags} " +
                        $"conflicts=[{conflicts}] exclusionTags=[{exclusion}] " +
                        $"desc={Short(desc, 120)}"
                    );
                }
            }
        }
        private static string Short(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }
        public static void DumpTraitDegree0(string traitDefName)
        {
            var def = DefDatabase<TraitDef>.GetNamedSilentFail(traitDefName);
            if (def == null)
            {
                Log.Warning($"[CheeseProtocol][TraitDegreeDump] TraitDef not found: {traitDefName}");
                return;
            }

            object defLabel = TryGet(def, "label");
            object degreeDatasObj = TryGet(def, "degreeDatas");

            int degreeCount = (degreeDatasObj as ICollection)?.Count ?? (degreeDatasObj as IList)?.Count ?? 0;

            Log.Message($"[CheeseProtocol][TraitDegreeDump] defName={def.defName} def.label={defLabel ?? "null"} degreeDatas.Count={degreeCount}");

            var list = degreeDatasObj as IList;
            if (list == null || list.Count == 0)
            {
                Log.Warning($"[CheeseProtocol][TraitDegreeDump] No degreeDatas for {def.defName}");
                return;
            }

            var d0 = list[0];
            if (d0 == null)
            {
                Log.Warning($"[CheeseProtocol][TraitDegreeDump] degreeDatas[0] is null for {def.defName}");
                return;
            }

            var t = d0.GetType();
            var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .OrderBy(f => f.Name)
                        .Select(f =>
                        {
                            object v = null;
                            try { v = f.GetValue(d0); } catch { }
                            return $"{f.FieldType.Name} {f.Name} = {(v == null ? "null" : v.ToString())}";
                        });

            var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(p => p.GetIndexParameters().Length == 0)
                        .OrderBy(p => p.Name)
                        .Select(p =>
                        {
                            object v = null;
                            try { v = p.GetValue(d0); } catch { }
                            return $"{p.PropertyType.Name} {p.Name} = {(v == null ? "null" : v.ToString())}";
                        });

            Log.Message("[CheeseProtocol][TraitDegreeDump] === degreeDatas[0] Fields ===\n" + string.Join("\n", fields));
            Log.Message("[CheeseProtocol][TraitDegreeDump] === degreeDatas[0] Properties ===\n" + string.Join("\n", props));
        }
        private static string FormatEnum(object o)
        {
            if (o == null) return "n/a";
            if (o is System.Collections.IEnumerable e)
                return "[" + string.Join(",", e.Cast<object>().Select(x => x.ToString())) + "]";
            return o.ToString();
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

        private static T Timed<T>(string label, Func<T> fn)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var r = fn();
                sw.Stop();
                Log.Message($"[CheeseProtocol][SpawnTiming] {label} OK ({sw.ElapsedMilliseconds} ms)");
                return r;
            }
            catch (Exception e)
            {
                sw.Stop();
                Log.Error($"[CheeseProtocol][SpawnTiming] {label} FAIL ({sw.ElapsedMilliseconds} ms)\n{e}");
                throw;
            }
        }

        private static void Timed(string label, Action fn)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                fn();
                sw.Stop();
                Log.Message($"[CheeseProtocol][SpawnTiming] {label} OK ({sw.ElapsedMilliseconds} ms)");
            }
            catch (Exception e)
            {
                sw.Stop();
                Log.Error($"[CheeseProtocol][SpawnTiming] {label} FAIL ({sw.ElapsedMilliseconds} ms)\n{e}");
                throw;
            }
        }
    }
}
