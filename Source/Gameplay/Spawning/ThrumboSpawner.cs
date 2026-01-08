using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using System.Linq;
using Verse.AI.Group;

namespace CheeseProtocol
{
    internal static class ThrumboSpawner
    {
        public static void Spawn(string donorName, int amount, string message)
        {
            CheeseSettings settings = CheeseProtocolMod.Settings;
            ThrumboAdvancedSettings thrumboAdvSetting = settings?.GetAdvSetting<ThrumboAdvancedSettings>(CheeseCommand.Thrumbo);
            if (thrumboAdvSetting == null) return;
            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;
            
            float quality = QualityEvaluator.evaluateQuality(amount, CheeseCommand.Thrumbo);
            var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, map);
            ThrumboRequest thrumboRequest = new ThrumboRequest(parms);
            if (!TryApplyThrumboCustomization(thrumboRequest, quality, settings.randomVar, thrumboAdvSetting))
            {
                FallbackVanilla(map);
                return;
            }
            if (!TrySpawnThrumboList(thrumboRequest, map, out IntVec3 rootCell))
            {
                Log.Warning("[CheeseProtocol] Failed to spawn thrumbos.");
                Messages.Message(
                    "!트럼보 실패: 트럼보를 소환하지 못했습니다. 소환할 수 있는 경로를 못찾았습니다.",
                    MessageTypeDefOf.RejectInput
                );
                FallbackVanilla(map);
                return;
            }
            
            if (!TryMakeLord(thrumboRequest, map, out Lord lord))
            {
                Log.Warning("[CheeseProtocol] Failed to make lord thrumbo.");
            }
            
            CheeseLetter.SendThrumboSuccessLetter(map, rootCell, thrumboRequest);
            Log.Message($"[CheeseProtocol] Thrumbo request successful: {thrumboRequest}");
        }
        public static void FallbackVanilla(Map map)
        {
            IncidentDef def = IncidentDef.Named("ThrumboPasses");

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(
                def.category,
                map
            );

            def.Worker.TryExecute(parms);
        }
        public static bool TrySpawnThrumboList(ThrumboRequest req, Map map, out IntVec3 rootCell)
        {
            rootCell = IntVec3.Invalid;
            if (!req.IsValid) return false; //additional safeguard

            var thrumboList = req.thrumboList;
            if (!CellFinder.TryFindRandomEdgeCellWith(
                    c => c.Standable(map) && c.Walkable(map) && !c.Fogged(map),
                    map,
                    CellFinder.EdgeRoadChance_Animal,
                    out var cell))
            {
                if (!CellFinder.TryFindRandomCell(
                            map,
                            c => c.Standable(map) && c.Walkable(map),
                            out cell))
                        return false;
                rootCell = cell;
            }
            rootCell = cell;
            // 1) Spawn alpha first (if any)
            if (req.alphaCount == 1)
            {
                req.alphaThrumbo = SpawnOne(req.alphaDef, map, req.parms, rootCell);
                if (req.alphaThrumbo == null){
                    Log.Warning("[CheeseProtocol] Failed to spawn alpha thrumbo. Fallback: replace with normal thrumbo");
                    req.alphaThrumbo = null;
                    req.alphaCount = 0;
                    req.thrumboCount += 1;
                }
                else
                    thrumboList.Add(req.alphaThrumbo);
            }

            // 2) Spawn normal thrumbos
            for (int i = 0; i < req.thrumboCount; i++)
            {
                var thrumbo = SpawnOne(req.thrumboDef, map, req.parms, rootCell);
                if (thrumbo == null) 
                {
                    Log.Warning("[CheeseProtocol] Failed to spawn thrumbo. Skipping.");
                    continue;
                }
                thrumboList.Add(thrumbo);
            }
            if (thrumboList.Count == 0) return false;
            return true;
        }
        public static bool TryMakeLord(ThrumboRequest req, Map map, out Lord lord)
        {
            lord = null;
            if (!req.IsValid) return false; //additional safeguard

            // 3) Pick leader: alpha > first thrumbo
            req.leader = req.alphaThrumbo ?? req.thrumboList[0];

            // 4) Make Lord and add pawns
            // NOTE: req.parms.faction 이 null이면 Lord를 만들 수 없는 경우가 많아서
            //       너 incident 설계상 faction이 들어온다고 가정하고 진행.
            /*if (req.parms?.faction == null)
            {
                Log.Warning("[CheeseProtocol] No faction is set. MakeLord failed. Fallback");
                return false;
            }
            */
            // 원하는 LordJob으로 교체 가능:
            // - 습격/적대 이벤트 느낌: LordJob_AssaultColony(...)
            // - 맨헌터 느낌: LordJob_Manhunter(...)
            // 네 Thrumbo 커맨드 의도에 맞춰 선택.
            LordJob job = MakeThrumboLordJob(req, map, req.leader);

            lord = LordMaker.MakeNewLord(req.parms.faction, job, map, req.thrumboList);

            // 리더 강제 지정이 필요하면(상황에 따라):
            // lord.ownedPawns 를 구성한 뒤 leader를 먼저 Add하는 식으로 컨트롤할 수도 있음.
            // 하지만 보통은 job/AI가 자동으로 처리해서 크게 신경 안 써도 됨.
            return true;
        }
        private static LordJob MakeThrumboLordJob(ThrumboRequest req, Map map, Pawn leader)
        {
            // 예시 1) 습격/적대 이벤트처럼
            // return new LordJob_AssaultColony(req.parms.faction, canKidnap: false, canTimeoutOrFlee: true);

            // 예시 2) 맨헌터 이벤트처럼
            //return new LordJob_Manhunter(req.parms.faction, canTimeoutOrFlee: true);

            // 일단 임시로 "AssaultColony" 추천 (네 의도에 맞게 교체)
            //return new LordJob_AssaultColony(req.parms.faction, canKidnap: false, canTimeoutOrFlee: true);
            return new LordJob_AnimalPass(map, 60000);
        }
        private static Pawn SpawnOne(PawnKindDef kind, Map map, IncidentParms parms, IntVec3 rootCell)
        {
            if (kind == null) return null;

            // Generate (faction 포함 여부는 네 설계에 맞게)
            Pawn pawn = PawnGenerator.GeneratePawn(kind, parms?.faction);
            if (pawn == null) return null;
            int radius = 4;          // starting radius
            int maxRadius = 60;      // max search
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
                Log.Warning($"[ThrumboSpawn] Failed near-root search up to maxRadius={maxRadius}, falling back.");

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
                        return null;
                }
            }

            GenSpawn.Spawn(pawn, cell, map);
            return pawn;
        }
        public static bool TryApplyThrumboCustomization(ThrumboRequest request, float quality, float randomVar, ThrumboAdvancedSettings adv)
        {
            if (adv.allowAlpha)
            {
                if(!TryApplyAlphaProb(request, quality, randomVar, adv.alphaProbRange))
                {
                    Log.Warning("[CheeseProtocol] Alpha Thrumbo not available.");
                    Messages.Message(
                        "!트럼보 실행 중: 알파 트럼보 정보를 찾을 수 없습니다. 일반 트럼보로 대체합니다.",
                        MessageTypeDefOf.RejectInput
                    );
                }
            }
            if(!TryApplyThrumboCount(request, quality, randomVar, adv.thrumboCountRange))
            {
                Log.Warning("[CheeseProtocol] Alpha Thrumbo not available.");
                Messages.Message(
                    "!트럼보 실패: 트럼보 정보를 찾을 수 없습니다.",
                    MessageTypeDefOf.RejectInput
                );
                return false;
            }
            return true;
        }
        public static bool TryApplyAlphaProb(ThrumboRequest request, float quality, float randomVar, QualityRange alphaProbRange)
        {
            float alhphaProb01 = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    alphaProbRange,
                    concentration01: 1f-randomVar
            );
            return ThrumboApplier.TryApplyAlphaProbHelper(request, alhphaProb01);
        }

        public static bool TryApplyThrumboCount(ThrumboRequest request, float quality, float randomVar, QualityRange thrumboCountRange)
        {
            float thrumboCountF = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    thrumboCountRange,
                    concentration01: 1f-randomVar
            );
            Log.Warning($"[CheeseProtocol] ThrumboCountF = {thrumboCountF}");
            return ThrumboApplier.TryApplyThrumboCountHelper(request, thrumboCountF);
        }
    }
}