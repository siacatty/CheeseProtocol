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
    internal static class BullySpawner
    {
        public static void Spawn(string donorName, int amount, string message)
        {
            CheeseRollTrace trace = new CheeseRollTrace(donorName, CheeseCommand.Bully);
            float quality = QualityEvaluator.evaluateQuality(amount, CheeseCommand.Bully);
            BullyRequest bully = Generate(quality, trace);

            if (bully == null || !bully.IsValid) return;
            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;
            GenerateBullies(bully, map);
            if (bully.bullyList == null || bully.bullyList.Count == 0) 
            {
                QWarn("Failed to generate Bullies.", Channel.Verse);
                CheeseLetter.AlertFail("!일진", "일진이 Pawn을 생성할수 없습니다.");
                return;
            }
            bully.leader = bully.bullyList[0];
            if (!string.IsNullOrWhiteSpace(donorName))
                bully.leader.Name = new NameSingle(TrimName(donorName));
            
            if (!TrySpawnBullies(bully, map, out IntVec3 rootCell))
            {
                QWarn("Failed to spawn Bullies.", Channel.Verse);
                CheeseLetter.AlertFail("!일진", "일진이 맵에 진입할 수 있는 경로를 찾지 못했습니다.");
                //FallbackVanilla(map, true);
                return;
            }
            
            if (!TryMakeLord(bully, map))
            {
                QWarn("Failed to make lord thrumbo.", Channel.Verse);
            }
            string letterLabel = "일진 등장!";

            string letterText =
                $"<color=#d09b61>{donorName}</color> 일진이 나타났습니다!\n\n" +
                $"총 {bully.bullyCount}명의 무리를 이끌고 정착치를 눈여겨봅니다." +
                "\n\n일진은 정착민을 공격하지는 않지만 괴롭히고 훔쳐갈 물건을 찾습니다." +
                "일진들은 10~16시간정도 머무른 뒤 이곳을 떠날 것입니다.";

            CheeseLetter.SendCheeseLetter(
                CheeseCommand.Bully,
                letterLabel,
                letterText,
                new LookTargets(bully.bullyList),
                trace,
                map,
                LetterDefOf.ThreatSmall
            );
            QMsg($"Bully successful. BullyRequest: {bully}", Channel.Debug);
        }

        public static bool TryMakeLord(BullyRequest req, Map map)
        {
            req.initialTargets = MatchBulliesToColonists(map, req.bullyList);
            var reg = map.GetComponent<BullyRegistry_MapComponent>();
            reg.AddBullies(req, Rand.RangeInclusive(10*2500, 16*2500), req.stealValue * (map?.wealthWatcher?.WealthItems ?? 0f));
            //reg.AddBullies(req, Rand.RangeInclusive(1*2500, 1*2500));

            if (!req.IsValid || req.leader == null || req.bullyList.Count < 1) return false; //additional safeguard
            LordJob job = MakeBullyLordJob(req, map, req.leader);
            
            LordMaker.MakeNewLord(req.leader.Faction, job, map, req.bullyList);
            string enterColony = LordChats.GetText(BullyTextKey.ArrivedColonyEdge);
            SpeechBubbleManager.Get(req.leader.Map)?.AddNPCChat(enterColony, req.leader);
            return true;
        }
        private static LordJob MakeBullyLordJob(BullyRequest req, Map map, Pawn leader)
        {
            return new LordJob_Bully(leader);
        }
        
        public static Dictionary<Pawn, Pawn> MatchBulliesToColonists(Map map, List<Pawn> bullies)
        {
            var colonists = map.mapPawns.FreeColonistsSpawned;
            var result = new Dictionary<Pawn, Pawn>();
            if (bullies == null || colonists == null || bullies.Count == 0 || colonists.Count == 0)
                return result;

            // down 제외 colonist 우선
            var activeColonists = colonists.Where(p => !p.Downed).ToList();
            if (activeColonists.Count == 0)
                activeColonists = colonists.ToList();

            // colonist만 shuffle
            var shuffledColonists = activeColonists.InRandomOrder().ToList();

            if (shuffledColonists.Count >= bullies.Count)
            {
                // bully <= colonist : 1:1 (중복 없음)
                for (int i = 0; i < bullies.Count; i++)
                {
                    result[bullies[i]] = shuffledColonists[i];
                }
            }
            else
            {
                // bully > colonist : colonist 중복 허용
                int idx = 0;
                for (int i = 0; i < bullies.Count; i++)
                {
                    result[bullies[i]] = shuffledColonists[idx];
                    idx = (idx + 1) % shuffledColonists.Count;
                }
            }

            return result;
        }
        public static BullyRequest Generate(float quality, CheeseRollTrace trace)
        {
            CheeseSettings settings = CheeseProtocolMod.Settings;
            BullyAdvancedSettings bullyAdvSetting = settings?.GetAdvSetting<BullyAdvancedSettings>(CheeseCommand.Bully);
            if (bullyAdvSetting == null) return null;
            BullyRequest request = new BullyRequest();
            ApplyBullyCustomization(request, quality, settings.randomVar, bullyAdvSetting, trace);
            return request;
        }
        public static void ApplyBullyCustomization(BullyRequest request, float quality, float randomVar, BullyAdvancedSettings adv, CheeseRollTrace trace)
        {
            ApplyBullyCount(request, quality, randomVar, adv.bullyCountRange, trace);
            ApplyStealValue(request, quality, randomVar, adv.stealValueRange, trace);
        }
        public static void ApplyBullyCount(BullyRequest request, float quality, float randomVar, QualityRange bullyCountRange, CheeseRollTrace trace)
        {
            TraceStep traceStep = new TraceStep("등장 일진 수");
            float bullyCount = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    bullyCountRange,
                    concentration01: 1f-randomVar,
                    traceStep
            );
            trace.steps.Add(traceStep);
            request.bullyCount = Mathf.RoundToInt(Mathf.Clamp(bullyCount, GameplayConstants.BullyCountMin, GameplayConstants.BullyCountMax));
        }

        private static void ApplyStealValue(BullyRequest request, float quality, float randomVar, QualityRange stealValueRange, CheeseRollTrace trace)
        {
            TraceStep traceStep = new TraceStep("도난물품 가치");
            float stealValue01 = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    stealValueRange,
                    concentration01: 1f-randomVar,
                    traceStep
            );
            trace.steps.Add(traceStep);
            request.stealValue = Mathf.Clamp(stealValue01, 0, 1);
        }

        private static void GenerateBullies(BullyRequest request, Map map)
        {
            var faction = GetNonHostileFaction();
            var kind = GetSafeKind(faction);
            for (int i=0; i < request.bullyCount; i++)
            {
                var pawn = GeneratePawn(faction, kind, map);
                if (faction == null || kind == null || pawn == null)
                {
                    QWarn("Failed to generate bully pawn.");
                    continue;
                }
                request.bullyList.Add(pawn);
            }
        }

        private static bool TrySpawnBullies(BullyRequest req, Map map, out IntVec3 rootCell)
        {
            rootCell = IntVec3.Invalid;
            if (!req.IsValid) return false; //additional safeguard
            bool spawned = false;
            foreach (var bully in req.bullyList)
            {
                if (!SpawnOne(bully, map, ref rootCell)) 
                {
                    QWarn("Failed to spawn bully. Skipping.", Channel.Verse);
                    continue;
                }
                else
                {
                    PawnVanishDebug.Track(bully);
                    spawned = true;
                }
            };
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
            BullyTagger.Mark(pawn);
            cleanPawn(pawn, true);
            pawn.guest?.Recruitable  = false;
            
            if (ModsConfig.BiotechActive && pawn.genes != null)
            {
                var hussar = DefDatabase<XenotypeDef>.GetNamedSilentFail("Hussar");
                if (hussar != null)
                    pawn.genes.SetXenotype(hussar);
                else
                    QWarn("Cannot find Hussar Xenotype.");
            }
            else
            {
                QWarn("Biotech is inactive or pawn.genes == null.");
            }

            // 3) 스킬: 사격 20, 격투 20
            SetSkill(pawn, SkillDefOf.Shooting, 20, passionMajor: false);
            SetSkill(pawn, SkillDefOf.Melee, 20, passionMajor: false);

            // 4) 특성: (신속/강인함/난사광) OR (신속/강인함/싸움꾼) 랜덤 1개
            //    - 신속 = Nimble 로 처리
            bool isMelee = Rand.Bool; // 여기서 랜덤 선택
            ApplyTraits(pawn, isMelee);

            // 5) 장비: 해병대 갑옷(전설), 해병대 헬멧(전설), 무기(전설) 랜덤
            //    + 전부 생체인증
            GiveLegendaryLoadout(pawn, isMelee);

            // 6) Death Acidifier 이식
            AddDeathAcidifier(pawn);

            // 7) 배고픔/피곤함 풀
            FillNeeds(pawn);

            return pawn;
        }
        private static void cleanPawn(Pawn pawn, bool allowWorkDisable)
        {
            if (pawn == null) return;
            foreach (var skill in pawn.skills.skills)
            {
                skill.Level = 0;
                skill.passion = Passion.None;
            }
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
            var all = genes.GenesListForReading.ToList();

            foreach (var g in all)
            {
                var cat = g.def.endogeneCategory;
                if (cat == EndogeneCategory.Melanin ||
                    cat == EndogeneCategory.HairColor)
                    continue;

                genes.RemoveGene(g);
            }
        }
        private static void SetSkill(Pawn pawn, SkillDef def, int level, bool passionMajor)
        {
            var sk = pawn.skills?.GetSkill(def);
            if (sk == null) return;
            sk.Level = level;
            if (passionMajor) sk.passion = Passion.Major;
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

        private static void ApplyTraits(Pawn pawn, bool isMelee)
        {
            var traitList = CheeseProtocolMod.TraitCatalog;
            if (traitList == null || traitList.Count == 0) return;
            
            var traitTough = traitList.FirstOrDefault(t => t.key == "Tough(0)");
            var traitFast = traitList.FirstOrDefault(t => t.key == "SpeedOffset(2)");
            var traitTriggerHappy = traitList.FirstOrDefault(t => t.key == "ShootingAccuracy(-1)");
            var traitBrawler = traitList.FirstOrDefault(t => t.key == "Brawler(0)");
            if (pawn.story?.traits != null)
            {
                if (traitTough.IsValid)
                    pawn.story.traits.GainTrait(new Trait(traitTough.def, traitTough.degree));
                if (traitFast.IsValid)
                    pawn.story.traits.GainTrait(new Trait(traitFast.def, traitFast.degree));
                if (isMelee && traitBrawler.IsValid)
                    pawn.story.traits.GainTrait(new Trait(traitBrawler.def, traitBrawler.degree));
                if (!isMelee && traitTriggerHappy.IsValid)
                    pawn.story.traits.GainTrait(new Trait(traitTriggerHappy.def, traitTriggerHappy.degree));
                    
            }
        }

        private static void GiveLegendaryLoadout(Pawn pawn, bool isMelee)
        {
            // Apparel: MarineArmor, MarineHelmet
            // defName은 보통 Apparel_MarineArmor / Apparel_MarineHelmet
            var marineArmor = MakeLegendaryThing<ThingDef>("Apparel_PowerArmor", pawn);
            var marineHelmet = MakeLegendaryThing<ThingDef>("Apparel_PowerArmorHelmet", pawn);

            if (marineArmor is Apparel aArmor && pawn.apparel != null)
                pawn.apparel.Wear(aArmor, dropReplacedApparel: true);

            if (marineHelmet is Apparel aHelmet && pawn.apparel != null)
                pawn.apparel.Wear(aHelmet, dropReplacedApparel: true);

            // Weapon: ranged or melee random one
            Thing weapon = isMelee ? MakeLegendaryMeleeWeapon(pawn) : MakeLegendaryRangedWeapon(pawn);

            if (weapon != null && pawn.equipment != null)
            {
                // 기존 무기 제거 후 장착
                pawn.equipment.DestroyAllEquipment();
                pawn.equipment.AddEquipment((ThingWithComps)weapon);
            }
        }

        private static Thing MakeLegendaryRangedWeapon(Pawn pawn)
        {
            // 저격소총 / 전투산탄총 / 돌격소총
            // - SniperRifle: Gun_SniperRifle
            // - AssaultRifle: Gun_AssaultRifle
            // - Combat shotgun: 버전에 따라 PumpShotgun/ChainShotgun일 수 있어 후보 2개
            var candidates = new List<string>
            {
                "Gun_SniperRifle",
                "Gun_AssaultRifle",
                "Gun_ChainShotgun"
            };

            var existing = new List<string>();
            foreach (var c in candidates)
                if (DefDatabase<ThingDef>.GetNamedSilentFail(c) != null)
                    existing.Add(c);

            if (existing.Count == 0)
            {
                QWarn("Cannot find Ranged weapon defs");
                return null;
            }

            string pick = existing.RandomElement();
            return MakeLegendaryThing<ThingDef>(pick, pawn);
        }

        private static Thing MakeLegendaryMeleeWeapon(Pawn pawn)
        {
            // 단분자검 / 제우스 망치
            // - Monosword: MeleeWeapon_Monosword
            // - Zeus hammer: MeleeWeapon_ZeusHammer
            var candidates = new List<string>
            {
                "MeleeWeapon_MonoSword",
                "MeleeWeapon_Zeushammer"
            };

            var existing = new List<string>();
            foreach (var c in candidates)
                if (DefDatabase<ThingDef>.GetNamedSilentFail(c) != null)
                    existing.Add(c);

            if (existing.Count == 0)
            {
                QWarn("Cannot find Melee weapon defs");
                return null;
            }

            string pick = existing.RandomElement();
            return MakeLegendaryThing<ThingDef>(pick, pawn);
        }

        private static Thing MakeLegendaryThing<TDef>(string defName, Pawn owner) where TDef : Def
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                QWarn($"Cannot find : ThingDef '{defName}'");
                return null;
            }

            Thing thing = ThingMaker.MakeThing(def);

            var compQ = thing.TryGetComp<CompQuality>();
            if (compQ != null)
                compQ.SetQuality(QualityCategory.Legendary, ArtGenerationContext.Colony);

            BiocodeIfPossible(thing, owner);

            return thing;
        }

        private static void BiocodeIfPossible(Thing thing, Pawn owner)
        {
            if (thing is ThingWithComps twc)
            {
                var bio = twc.TryGetComp<CompBiocodable>();
                if (bio != null && !bio.Biocoded)
                    bio.CodeFor(owner);
            }
        }

        public static void AddDeathAcidifier(Pawn pawn)
        {
            if (pawn?.health == null) return;

            var def = DefDatabase<HediffDef>.GetNamedSilentFail("DeathAcidifier");
            if (def == null)
            {
                QWarn("HediffDef 'DeathAcidifier' not found.");
                return;
            }

            // Avoid duplicates
            if (pawn.health.hediffSet.HasHediff(def)) return;

            // DeathAcidifier is an implant -> needs a body part
            BodyPartRecord part =
                pawn.RaceProps?.body?.corePart // usually Torso
                ?? pawn.health.hediffSet.GetNotMissingParts()?.FirstOrDefault(); // fallback

            if (part == null)
            {
                QWarn("Could not find a valid body part to install DeathAcidifier.");
                return;
            }

            // Some mods/races can have weird bodies; ensure part is not missing
            if (pawn.health.hediffSet.PartIsMissing(part))
            {
                part = pawn.health.hediffSet.GetNotMissingParts()?.FirstOrDefault();
                if (part == null)
                {
                    QWarn("All parts missing? Cannot install DeathAcidifier.");
                    return;
                }
            }

            pawn.health.AddHediff(def, part);
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