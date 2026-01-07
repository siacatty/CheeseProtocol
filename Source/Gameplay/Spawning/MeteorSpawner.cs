using System;
using System.Reflection;
using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;
using System.Linq;
using System.Collections;

namespace CheeseProtocol
{
    internal static class MeteorSpawner
    {
        public static void Spawn(string donorName, int amount, string message)
        {
            MeteorAdvancedSettings meteorAdvSetting = CheeseProtocolMod.Settings?.GetAdvSetting<MeteorAdvancedSettings>(CheeseCommand.Meteor);
            if (meteorAdvSetting == null) return;
            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;
            IntVec3 near = DropCellFinder.TradeDropSpot(map);
            float quality = QualityEvaluator.evaluateQuality(amount, CheeseCommand.Meteor);
            var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, map);
            MeteorObject meteor = new MeteorObject(parms);
            ApplyMeteorCustomization(meteor, quality);
            SpawnThrumbo(map);
            ThingDef skyfallerDef = ResolveMeteoriteIncomingDef();
            if (skyfallerDef == null)
            {
                Log.Warning("[CheeseProtocol] MeteoriteIncoming not found. Fallback to vanilla.");
                FallbackVanillaMeteor(meteor);
                return;
            }
            int maxRadius = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(meteor.size) * 0.9f), 1, 10);
            bool ok = SkyFaller.TrySpawnCustom(
                map: map,
                near: near,
                innerDef: meteor.def,
                skyfallerDef: skyfallerDef,
                pieces: meteor.size,
                out IntVec3 rootCell
            );

            if (!ok)
            {
                Log.Warning($"[CheeseProtocol] Custom meteor failed (center: {rootCell} | size: {meteor.size}). Fallback.");
                FallbackVanillaMeteor(meteor);
            }
            else
            {
                CheeseLetter.SendMeteorSuccessLetter(map, rootCell, meteor);
                Log.Message(
                    $"[CheeseProtocol] Custom meteor success: {meteor.type} x{meteor.size}"
                );
            }
        }

        private static void SpawnThrumbo(Map map)
        {
            PawnKindDef thrumboKind =
            DefDatabase<PawnKindDef>.GetNamedSilentFail("AlphaThrumbo");

            if (thrumboKind == null)
            {
                Log.Warning("[CheeseProtocol] AlphaThrumbo not found. Falling back to Thrumbo.");
                thrumboKind = PawnKindDefOf.Thrumbo;
            }

            // 2) 스폰 위치 찾기
            if (!CellFinder.TryFindRandomCellNear(
                    map.Center,
                    map,
                    20,
                    c => c.Standable(map) && !c.Fogged(map),
                    out IntVec3 cell))
            {
                Log.Warning("[CheeseProtocol] Failed to find spawn cell.");
                return;
        }

        // 3) Pawn 생성 (야생)
        Pawn pawn = PawnGenerator.GeneratePawn(thrumboKind, faction: null);
        GenSpawn.Spawn(pawn, cell, map);
        }

        private static void FallbackVanillaMeteor(MeteorObject meteor)
        {
            IncidentDef def = DefDatabase<IncidentDef>.GetNamed("MeteoriteImpact", false);
            if (def == null)
            {
                Log.Warning("[CheeseProtocol] MeteoriteImpact def not found.");
                return;
            }
            bool ok = def.Worker.TryExecute(meteor.parms);
            if (Prefs.DevMode)
            {
                if (!ok)
                    Log.Warning("[CheeseProtocol] Vanilla MeteorSpawn failed to execute.");
            }
        }
        private static void ApplyMeteorCustomization(MeteorObject meteor, float quality)
        {
            var settings = CheeseProtocolMod.Settings;
            MeteorAdvancedSettings meteorAdvSettings = settings.GetAdvSetting<MeteorAdvancedSettings>(CheeseCommand.Meteor);
            float randomVar = settings.randomVar;
            ApplyMeteorType(meteor, quality, randomVar, meteorAdvSettings.meteorTypeRange, meteorAdvSettings.allowedMeteorCandidates);
            ApplyMeteorSize(meteor, quality, randomVar, meteorAdvSettings.meteorSizeRange);
            Log.Message($"[CheeseProtocol] Meteor pick chosen={meteor.type} score={meteor.score:0.###}, size={meteor.size}");
            //DebugDumpMineables();
        }

        private static void ApplyMeteorType(MeteorObject meteor, float quality, float randomVar, QualityRange minMaxRange, List<MeteorCandidate> candidates)
        {
            float meteorTypeQuality = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    minMaxRange,
                    concentration01: 1f-randomVar,
                    debugLog: false
            );
            MeteorApplier.ApplyMeteorTypeHelper(meteor, meteorTypeQuality, candidates); 
        }
        private static void ApplyMeteorSize(MeteorObject meteor, float quality, float randomVar, QualityRange minMaxRange)
        {
            int baseMinSize = GameplayConstants.MeteorSizeMin;
            int baseMaxSize = GameplayConstants.MeteorSizeMax;
            float sizeF = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    minMaxRange,
                    concentration01: 1f-randomVar
                );
            Log.Warning($"[CheeseProtocol] baseMinSize={baseMinSize}, baseMaxSize={baseMaxSize}, rangeMin={minMaxRange.qMin}, rangeMax={minMaxRange.qMax}, sizeF={sizeF}");
            int meteorBaseSize = Mathf.RoundToInt(Mathf.Clamp(sizeF, baseMinSize, baseMaxSize));
            MeteorApplier.ApplyMeteorSizeHelper(meteor, meteorBaseSize);
        }
        private static ThingDef ResolveMeteoriteIncomingDef()
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("MeteoriteIncoming");
            if (def != null) return def;
            def = DefDatabase<ThingDef>.GetNamedSilentFail("MeteoriteIncomingLarge");
            if (def != null) return def;

            return null;
        }

    }
}