using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using System.Linq;
using static CheeseProtocol.CheeseLog;
using UnityEngine;
using Verse.AI.Group;
using System.Configuration;

namespace CheeseProtocol
{
    internal static class TeacherSpawner
    {
        public const string ColorPositive = "#2e8032ff";
        public const string ColorNegative = "#aa4040ff";
        public static void Spawn(string donorName, int amount, string message)
        {
            CheeseRollTrace trace = new CheeseRollTrace(donorName, CheeseCommand.Teacher);
            float quality = QualityEvaluator.evaluateQuality(amount, CheeseCommand.Teacher);
            TeacherRequest request = Generate(quality, trace);

            if (request == null || !request.IsValid) return;
            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;
            GenerateTeacher(request, map);
            if (request.teacherPawn == null) 
            {
                QWarn("Failed to generate Teacher.", Channel.Verse);
                CheeseLetter.AlertFail("!교육", "선생님 Pawn을 생성할수없습니다.");
                return;
            }
            if (!string.IsNullOrWhiteSpace(donorName))
                request.teacherPawn.Name = new NameSingle(TrimName(donorName));
            
            if (!TrySpawnTeacher(request, map, out IntVec3 rootCell))
            {
                QWarn("Failed to spawn Teacher.", Channel.Verse);
                CheeseLetter.AlertFail("!교육", "선생님이 맵에 진입할 수 있는 경로를 찾지 못했습니다.");
                return;
            }
            
            if (!TryMakeLord(request, map))
            {
                QWarn("Failed to make lord thrumbo.", Channel.Verse);
            }
            string letterLabel = "선생님 방문";

            string letterText =
                $"{request.teacherPawn.NameShortColored} 선생님이 방문했습니다." +
                "\n\n선생님에게 말을 걸어 교육을 받을 수 있습니다." +
                "\n\n수업을 무사히 끝마치면 기술을 습득할수있지만," +
                "\n수업 중 도주에 성공하면 더 큰 보상이 있을것같습니다..." +
                $"\n<color={ColorPositive}>(힌트: 수업이 시작하면 정착민을 소집할 수 있습니다.)</color>";


            CheeseLetter.SendCheeseLetter(
                CheeseCommand.Teacher,
                letterLabel,
                letterText,
                new LookTargets(request.teacherPawn),
                trace,
                map,
                LetterDefOf.PositiveEvent
            );
            QMsg($"Teacher successful. TeacherRequest: {request}", Channel.Debug);
        }

        public static bool TryMakeLord(TeacherRequest req, Map map)
        {
            if (map == null) return false;

            if (!req.IsValid || req.teacherPawn == null) return false;
            LordJob job = MakeTeacherLordJob(req, map, req.teacherPawn);
            
            LordMaker.MakeNewLord(req.teacherPawn.Faction, job, map, new List<Pawn> { req.teacherPawn });
            return true;
        }
        private static LordJob MakeTeacherLordJob(TeacherRequest req, Map map, Pawn teacher)
        {
            return new LordJob_Teacher(teacher, req);
        }
        
        public static TeacherRequest Generate(float quality, CheeseRollTrace trace)
        {
            CheeseSettings settings = CheeseProtocolMod.Settings;
            TeacherAdvancedSettings teacherAdvSetting = settings?.GetAdvSetting<TeacherAdvancedSettings>(CheeseCommand.Teacher);
            if (teacherAdvSetting == null) return null;
            TeacherRequest request = new TeacherRequest();
            ApplyTeacherCustomization(request, quality, settings.randomVar, teacherAdvSetting, trace);
            return request;
        }
        public static void ApplyTeacherCustomization(TeacherRequest request, float quality, float randomVar, TeacherAdvancedSettings adv, CheeseRollTrace trace)
        {
            ApplyStudentCount(request, quality, randomVar, adv.studentCountRange, trace);
            ApplyTeachSkill(request, quality, randomVar, adv.teachSkillRange, trace);
            if (adv.allowPassion)
                ApplyPassionProb(request, quality, randomVar, adv.passionProbRange, trace);
        }
        public static void ApplyStudentCount(TeacherRequest request, float quality, float randomVar, QualityRange range, CheeseRollTrace trace)
        {
            TraceStep traceStep = new TraceStep("학생 수");
            float studentCountF = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    range,
                    concentration01: 1f-randomVar,
                    traceStep
            );
            trace.steps.Add(traceStep);
            request.studentCount = Mathf.RoundToInt(Mathf.Clamp(studentCountF, GameplayConstants.StudentCountMin, GameplayConstants.StudentCountMax));
        }

        private static void ApplyTeachSkill(TeacherRequest request, float quality, float randomVar, QualityRange range, CheeseRollTrace trace)
        {
            TraceStep traceStep = new TraceStep("증가 스킬 레벨");
            float teachSkillF = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    range,
                    concentration01: 1f-randomVar,
                    traceStep
            );
            trace.steps.Add(traceStep);
            request.teachSkill = Mathf.RoundToInt(Mathf.Clamp(teachSkillF, GameplayConstants.TeachSkillMin, GameplayConstants.TeachSkillMax));
        }

        private static void ApplyPassionProb(TeacherRequest request, float quality, float randomVar, QualityRange range, CheeseRollTrace trace)
        {
            TraceStep traceStep = new TraceStep("열정 부여 확률");
            float passionProbF = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    range,
                    concentration01: 1f-randomVar,
                    traceStep
            );
            trace.steps.Add(traceStep);
            request.passionProb = Mathf.Clamp(passionProbF, 0, 1);
        }

        private static void GenerateTeacher(TeacherRequest request, Map map)
        {
            var faction = GetNonHostileFaction();
            var kind = GetSafeKind(faction);
            var pawn = GeneratePawn(faction, kind, map);
            if (faction == null || kind == null || pawn == null)
            {
                QWarn("Failed to generate teacher pawn.");
                return;
            }
            request.teacherPawn = pawn;
        }

        private static bool TrySpawnTeacher(TeacherRequest req, Map map, out IntVec3 rootCell)
        {
            rootCell = IntVec3.Invalid;
            if (!req.IsValid) return false; //additional safeguard
            bool spawned = false;
            var pawn = req.teacherPawn;

            if (!SpawnOne(pawn, map, ref rootCell)) 
            {
                QWarn("Failed to spawn teacher.", Channel.Verse);
                return spawned;
            }
            else
            {
                PawnVanishDebug.Track(pawn);
                spawned = true;
            }
            return spawned;
        }

        private static bool SpawnOne(Pawn pawn, Map map, ref IntVec3 rootCell)
        {
            if (pawn == null) return false;
            int radius = 4;          // starting radius
            int maxRadius = 300;      // max search
            int step = 10;
            IntVec3 cell = IntVec3.Invalid;
            bool found = false;

            while (radius <= maxRadius)
            {
                if (CellFinder.TryFindRandomCellNear(
                        rootCell,
                        map,
                        radius,
                        c => c.Standable(map) && c.Walkable(map) && !c.Fogged(map),
                        out cell))
                {
                    found = true;
                    break;
                }

                radius += step;
            }
            // fallback
            if (!found)
            {
                QWarn($"Failed near-root search up to maxRadius={maxRadius}, falling back.", Channel.Verse);

                if (!CellFinder.TryFindRandomEdgeCellWith(
                        c => c.Standable(map) && c.Walkable(map) && !c.Fogged(map),
                        map,
                        CellFinder.EdgeRoadChance_Animal,
                        out cell))
                {
                    if (!CellFinder.TryFindRandomCell(
                            map,
                            c => c.Standable(map) && c.Walkable(map),
                            out cell))
                        return false;
                }
            }
            if (rootCell == IntVec3.Invalid) rootCell = cell;
            //Anchor(pawn, cell);
            Thing spawnedPawn = GenSpawn.Spawn(pawn, cell, map);
            return true;
        }

        public static Faction GetNonHostileFaction()
        {
            var faction = Find.FactionManager.AllFactionsVisible
                .Where(f => f != null
                            && f != Faction.OfPlayer
                            && !f.IsPlayer
                            && f.def?.humanlikeFaction == true
                            && !f.def.hidden
                            && f.PlayerRelationKind != FactionRelationKind.Hostile)
                .InRandomOrder()
                .FirstOrDefault();
            if (faction == null) 
            {
                QWarn($"NonHostileFaction not found.");
                return null;
            }
            return faction;
        }

        public static PawnKindDef GetSafeKind(Faction faction)
        {
            if (faction == null) return null;

            var kind = PickSafeKindFromFactionGroupMakers(faction);
            if (kind == null)
            {
                QWarn($"No safe PawnKind found for faction={faction.def.defName}");
                return null;
            }
            return kind;
        }

        private static PawnKindDef PickSafeKindFromFactionGroupMakers(Faction faction)
        {
            var list = new List<PawnKindDef>();

            if (faction?.def?.pawnGroupMakers != null)
            {
                foreach (var gm in faction.def.pawnGroupMakers)
                {
                    if (gm?.options == null) continue;

                    foreach (var opt in gm.options)
                    {
                        var k = opt?.kind;
                        if (k?.RaceProps?.Humanlike != true) continue;
                        if (k.trader) continue;

                        string dn = k.defName ?? "";

                        if (dn.Contains("Bestower") || dn.Contains("Royal") || dn.Contains("Slave") || dn.Contains("Ceremony"))
                            continue;

                        if (dn.Contains("Child") || dn.Contains("_Child"))
                            continue;

                        list.Add(k);
                    }
                }
            }

            if (list.Count > 0)
                return list.RandomElement();

            return DefDatabase<PawnKindDef>.AllDefsListForReading
                .Where(k => k?.RaceProps?.Humanlike == true
                            && k.defaultFactionDef == faction.def
                            && !k.trader)
                .Where(k =>
                {
                    var dn = k.defName ?? "";
                    return !dn.Contains("Bestower")
                        && !dn.Contains("Royal")
                        && !dn.Contains("Slave")
                        && !dn.Contains("Ceremony")
                        && !(dn.Contains("Child") || dn.Contains("_Child"));
                })
                .InRandomOrder()
                .FirstOrDefault();
        }
        private static Pawn GeneratePawn(Faction faction, PawnKindDef kind, Map map)
        {
            var req = new PawnGenerationRequest(
                kind: kind,
                faction: faction,
                context: PawnGenerationContext.NonPlayer,
                tile: map.Tile,
                forceGenerateNewPawn: true,
                allowDead: false,
                allowDowned: false,
                canGeneratePawnRelations: false,
                mustBeCapableOfViolence: true
            );

            var pawn = PawnGenerator.GeneratePawn(req);
            TeacherTagger.Mark(pawn);
            cleanPawn(pawn);
            pawn.guest?.Recruitable  = false;

            SetSkill(pawn, SkillDefOf.Intellectual, 5, passionMajor: false);
            SetSkill(pawn, SkillDefOf.Social, 10, passionMajor: false);

            ApplyTraits(pawn);
            FillNeeds(pawn);
            GiveWoodenMace(pawn);
            ForceWearOnlySpecifiedApparel(pawn);

            return pawn;
        }
        private static void cleanPawn(Pawn pawn)
        {
            if (pawn == null) return;
            foreach (var skill in pawn.skills.skills)
            {
                skill.Level = 0;
                skill.passion = Passion.None;
            }
            pawn.story.traits.allTraits.Clear();
            HealthApplier.ClearAllHediffs(pawn);
            pawn.Notify_DisabledWorkTypesChanged();
        }
        private static void SetSkill(Pawn pawn, SkillDef def, int level, bool passionMajor)
        {
            var sk = pawn.skills?.GetSkill(def);
            if (sk == null) return;
            sk.Level = level;
            if (passionMajor) sk.passion = Passion.Major;
        }

        private static void ApplyTraits(Pawn pawn)
        {
            var traitList = CheeseProtocolMod.TraitCatalog;
            if (traitList == null || traitList.Count == 0) return;
            
            var traitFast = traitList.FirstOrDefault(t => t.key == "SpeedOffset(2)");
            var traitAnnoyingVoice = traitList.FirstOrDefault(t => t.key == "AnnoyingVoice(0)");
            var traitPychicNone = traitList.FirstOrDefault(t => t.key == "PsychicSensitivity(-2)");
            var traitPsycopath = traitList.FirstOrDefault(t => t.key == "Psychopath(0)");

            if (pawn.story?.traits != null)
            {
                if (traitFast.IsValid)
                    pawn.story.traits.GainTrait(new Trait(traitFast.def, traitFast.degree));
                if (traitAnnoyingVoice.IsValid)
                    pawn.story.traits.GainTrait(new Trait(traitAnnoyingVoice.def, traitAnnoyingVoice.degree));
                if (traitPychicNone.IsValid)
                    pawn.story.traits.GainTrait(new Trait(traitPychicNone.def, traitPychicNone.degree));
                if (traitPsycopath.IsValid)
                    pawn.story.traits.GainTrait(new Trait(traitPsycopath.def, traitPsycopath.degree));
            }
        }
        private static void GiveWoodenMace(Pawn pawn)
        {
            if (pawn == null) return;

            pawn.equipment?.DestroyAllEquipment();

            ThingDef maceDef = DefDatabase<ThingDef>.GetNamedSilentFail("MeleeWeapon_Mace");
            if (maceDef != null)
            {
                var club = (ThingWithComps) ThingMaker.MakeThing(
                    maceDef,
                    ThingDefOf.WoodLog
                );
                pawn.equipment?.AddEquipment(club);
            }
        }

        private static void ForceWearOnlySpecifiedApparel(Pawn pawn)
        {
            if (pawn?.apparel == null) return;

            var worn = pawn.apparel.WornApparel;
            for (int i = worn.Count - 1; i >= 0; i--)
            {
                var ap = worn[i];
                pawn.apparel.Remove(ap);
                ap.Destroy();
            }
            (string defName, ThingDef stuff)[] apparels =
            {
                ("Apparel_CollarShirt", ThingDefOf.Cloth),
                ("Apparel_Robe",        ThingDefOf.Cloth),
                ("Apparel_Pants",       ThingDefOf.Cloth),
            };

            foreach (var (defName, stuff) in apparels)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (def == null) continue;

                Apparel ap = (Apparel)ThingMaker.MakeThing(def, stuff);
                ap.HitPoints = ap.MaxHitPoints;

                pawn.apparel.Wear(ap, dropReplacedApparel: false);
            }
        }

        private static void FillNeeds(Pawn pawn)
        {
            if (pawn.needs == null) return;

            var food = pawn.needs.TryGetNeed<Need_Food>();
            if (food != null) food.CurLevelPercentage = 1f;

            var rest = pawn.needs.TryGetNeed<Need_Rest>();
            if (rest != null) rest.CurLevelPercentage = 1f;
        }

        private static string TrimName(string s)
        {
            s = s.Trim();
            if (s.Length > 24) s = s.Substring(0, 24);
            return s;
        }
    }
}