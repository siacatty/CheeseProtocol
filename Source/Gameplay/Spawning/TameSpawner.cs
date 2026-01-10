using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using System.Linq;
using static CheeseProtocol.CheeseLog;

namespace CheeseProtocol
{
    internal static class TameSpawner
    {
        public static void Spawn(string donorName, int amount, string message)
        {
            CheeseRollTrace trace = new CheeseRollTrace(donorName, CheeseCommand.Tame);
            float quality = QualityEvaluator.evaluateQuality(amount, CheeseCommand.Tame);
            TameRequest tame = Generate(quality, trace);

            if (tame == null || !tame.IsValid) return;
            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;
            //var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, map);
            var req = new PawnGenerationRequest(
                tame.def,
                Faction.OfPlayer,
                PawnGenerationContext.NonPlayer,
                map.Tile,
                forceGenerateNewPawn: true,
                canGeneratePawnRelations: false
            );
            Pawn tameAnimal = PawnGenerator.GeneratePawn(req);
            if (tameAnimal == null)
            {
                QWarn("Failed to generate animal to be tamed", Channel.Verse);
                CheeseLetter.AlertFail("!조련", "현재 게임 설정에 조련할수있는 동물이 없습니다.");
                return;
            }
            IntVec3 rootCell;
            bool found = CellFinder.TryFindRandomEdgeCellWith(
                c => c.Standable(map) && c.Walkable(map) && !c.Fogged(map),
                map,
                CellFinder.EdgeRoadChance_Animal, // 동물/캐러밴용 확률
                out rootCell
            );
            if (!found)
            {
                QWarn("Failed to find a cell for animal to enter", Channel.Verse);
                CheeseLetter.AlertFail("!조련", "동물이 맵에 들어올 경로가 없습니다.");
                return;
            }
            GenSpawn.Spawn(tameAnimal, rootCell, map);
            string letterLabel = $"애완동물: {tame.label}";
            string letterText = $"새로운 애완동물이 합류합니다. {tame.label}(이)가 반갑게 인사를 합니다.";
            CheeseLetter.SendCheeseLetter(
                CheeseCommand.Tame,
                letterLabel,
                letterText,
                new LookTargets(tameAnimal),
                trace,
                map,
                LetterDefOf.PositiveEvent
            );
            QMsg($"Tame successful. TameRequest: {tame}", Channel.Debug);
        }

        public static TameRequest Generate(float quality, CheeseRollTrace trace)
        {
            CheeseSettings settings = CheeseProtocolMod.Settings;
            TameAdvancedSettings tameAdvSetting = settings?.GetAdvSetting<TameAdvancedSettings>(CheeseCommand.Tame);
            if (tameAdvSetting == null) return null;
            TameRequest request = new TameRequest();
            TryApplyTameCustomization(request, quality, settings.randomVar, tameAdvSetting, trace);
            return request;
        }
        public static bool TryApplyTameCustomization(TameRequest tame, float quality, float randomVar, TameAdvancedSettings adv, CheeseRollTrace trace)
        {
            if(!TryApplyTameValue(tame, quality, randomVar, adv.tameValueRange, trace))
            {
                QWarn("No animals available", Channel.Verse);
                CheeseLetter.AlertFail("!조련", "조련할수있는 동물 목록이 비어있습니다.");
                return false;
            }
            return true;
        }
        public static bool TryApplyTameValue(TameRequest tame, float quality, float randomVar, QualityRange tameValueRange, CheeseRollTrace trace)
        {
            TraceStep traceStep = new TraceStep("시장가치 배율");
            float tameValue01 = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    tameValueRange,
                    concentration01: 1f-randomVar,
                    traceStep
            );
            trace.steps.Add(traceStep);
            return TameApplier.TryApplyValueHelper(tame, tameValue01);
        }
    }
}