using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using System.Linq;
using Verse.AI.Group;
using UnityEngine;

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
            CheeseRollTrace trace = new CheeseRollTrace(donorName, CheeseCommand.Thrumbo);
            if (!TryApplyThrumboCustomization(thrumboRequest, quality, settings.randomVar, thrumboAdvSetting, trace))
            {
                FallbackVanilla(map);
                return;
            }
            if (!TrySpawnThrumboList(thrumboRequest, map, out IntVec3 rootCell))
            {
                Log.Warning("[CheeseProtocol] Failed to spawn thrumbos.");
                FallbackVanilla(map);
                return;
            }
            
            if (!TryMakeLord(thrumboRequest, map, out Lord lord))
            {
                Log.Warning("[CheeseProtocol] Failed to make lord thrumbo.");
            }
            string letterLabel = (thrumboRequest.alphaCount > 0 ? "알파 " : "희귀 ") + "트럼보";

            int count = thrumboRequest.alphaCount + thrumboRequest.thrumboCount;
            string size =
                count < 4 ? "작은 " :
                count < 8 ? "큰 " :
                            "굉장히 큰 ";
            string letterText =
                $"{size}무리의 트럼보들이 다가옵니다.\n\n" +
                $"총 {count}마리의 트럼보가 관측됩니다." +
                (thrumboRequest.alphaCount > 0
                    ? "\n\n이 무리는 알파 트럼보가 이끌고 있습니다. 각별한 주의가 필요합니다."
                    : "") +
                "\n\n트럼보는 희귀한 동물로, 천성은 순하나 맞설 경우 매우 위험합니다. " +
                "트럼보의 뿔과 가죽은 상인들 사이에서 아주 귀중한 재료로 여겨집니다." +
                "\n\n트럼보들은 며칠 머무른 뒤 이곳을 떠날 것입니다.";

            CheeseLetter.SendCheeseLetter(
                CheeseCommand.Thrumbo,
                letterLabel,
                letterText,
                new LookTargets(thrumboRequest.thrumboList),
                trace,
                map,
                LetterDefOf.PositiveEvent
            );
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

            req.leader = req.alphaThrumbo ?? req.thrumboList[0];
            LordJob job = MakeThrumboLordJob(req, map, req.leader);

            lord = LordMaker.MakeNewLord(req.parms.faction, job, map, req.thrumboList);

            return true;
        }
        private static LordJob MakeThrumboLordJob(ThrumboRequest req, Map map, Pawn leader)
        {
            int ticksToStay = Rand.RangeInclusive(100000, 200000);
            if (req.alphaCount > 0) ticksToStay = Mathf.RoundToInt(ticksToStay*1.2f);
            return new LordJob_AnimalPass(map, ticksToStay);
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
        public static bool TryApplyThrumboCustomization(ThrumboRequest request, float quality, float randomVar, ThrumboAdvancedSettings adv, CheeseRollTrace trace)
        {
            if (adv.allowAlpha)
            {
                if(!TryApplyAlphaProb(request, quality, randomVar, adv.alphaProbRange, trace))
                {
                    Log.Warning("[CheeseProtocol] Alpha Thrumbo not available.");
                    Messages.Message(
                        "!트럼보 실행 중: 알파 트럼보 정보를 찾을 수 없습니다. 일반 트럼보로 대체합니다.",
                        MessageTypeDefOf.RejectInput
                    );
                }
            }
            if(!TryApplyThrumboCount(request, quality, randomVar, adv.thrumboCountRange, trace))
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
        public static bool TryApplyAlphaProb(ThrumboRequest request, float quality, float randomVar, QualityRange alphaProbRange, CheeseRollTrace trace)
        {
            float alphaProb01 = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    alphaProbRange,
                    concentration01: 1f-randomVar,
                    out float score
            );
            trace.steps.Add(new TraceStep("알파트럼보 확률", score, alphaProbRange.Expected(quality), alphaProb01));
            return ThrumboApplier.TryApplyAlphaProbHelper(request, alphaProb01);
        }

        public static bool TryApplyThrumboCount(ThrumboRequest request, float quality, float randomVar, QualityRange thrumboCountRange, CheeseRollTrace trace)
        {
            float thrumboCountF = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    thrumboCountRange,
                    concentration01: 1f-randomVar,
                    out float score
            );
            trace.steps.Add(new TraceStep("트럼보 수", score, thrumboCountRange.Expected(quality), thrumboCountF));
            return ThrumboApplier.TryApplyThrumboCountHelper(request, thrumboCountF);
        }
    }
}