using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using System.Linq;

namespace CheeseProtocol
{
    internal static class TameSpawner
    {
        public static void Spawn(string donorName, int amount, string message)
        {
            CheeseSettings settings = CheeseProtocolMod.Settings;
            TameAdvancedSettings tameAdvSetting = settings?.GetAdvSetting<TameAdvancedSettings>(CheeseCommand.Tame);
            if (tameAdvSetting == null) return;
            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;
            float quality = QualityEvaluator.evaluateQuality(amount, CheeseCommand.Tame);
            var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, map);
            TameRequest tame = new TameRequest(parms);
            TryApplyTameCustomization(tame, quality, settings.randomVar, tameAdvSetting);
            if (!tame.IsValid) return;
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
                Log.Warning("[CheeseProtocol] Failed to generate animal to be tamed");
                Messages.Message(
                    "!조련 실행 실패: 현재 스토리에 조련할수있는 동물이 없습니다.",
                    MessageTypeDefOf.RejectInput
                );
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
                Log.Warning("[CheeseProtocol] Failed to find a cell for animal to enter");
                Messages.Message(
                    "!조련 실행 실패: 동물이 맵에 들어올 경로가 없습니다.",
                    MessageTypeDefOf.RejectInput
                );
                return;
            }
            GenSpawn.Spawn(tameAnimal, rootCell, map);
            CheeseLetter.SendTameSuccessLetter(map, rootCell, tame);
            Log.Message($"[CheeseProtocol] Tame successful. TameRequest: {tame}");
        }
        public static bool TryApplyTameCustomization(TameRequest tame, float quality, float randomVar, TameAdvancedSettings adv)
        {
            if(!TryApplyTameValue(tame, quality, randomVar, adv.tameValueRange))
            {
                Log.Warning("[CheeseProtocol] No animals available");
                Messages.Message(
                    "!조련 실행 실패: 조련할수있는 동물 목록이 비어있습니다.",
                    MessageTypeDefOf.RejectInput
                );
                return false;
            }
            return true;
        }
        public static bool TryApplyTameValue(TameRequest tame, float quality, float randomVar, QualityRange tameValueRange)
        {
            float tameValue01 = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    tameValueRange,
                    concentration01: 1f-randomVar
            );
            return TameApplier.TryApplyValueHelper(tame, tameValue01);
        }
    }
}