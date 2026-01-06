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
    internal static class RaidSpawner
    {
        public static void Spawn(string donorName, int amount, string message)
        {
            RaidAdvancedSettings raidAdvSetting = CheeseProtocolMod.Settings?.GetAdvSetting<RaidAdvancedSettings>(CheeseCommand.Raid);
            if (raidAdvSetting == null) return;
            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;
            float quality = QualityEvaluator.evaluateQuality(amount, CheeseCommand.Raid);
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(
                IncidentCategoryDefOf.ThreatBig,
                map
            );
            ApplyRaidCustomization(parms, quality);
            IncidentDef def = DefDatabase<IncidentDef>.GetNamed("RaidEnemy", false);
            if (def == null)
            {
                Log.Warning("[CheeseProtocol] RaidEnemy def not found.");
                return;
            }
            string reason;
            bool ok = TryExecuteRaidWithFallback(IncidentDefOf.RaidEnemy, parms, out reason);
            if (Prefs.DevMode)
            {
                Log.Message($"[CheeseProtocol] ok={ok} points={parms.points:0} reason={reason ?? "n/a"}");
                if (!ok)
                    Log.Warning("[CheeseProtocol] RaidEnemy failed to execute.");
            }
        }

        private static bool TryExecuteRaidWithFallback(
            IncidentDef def,
            IncidentParms parms,
            out string reason)
        {
            reason = null;

            // 1차 시도 (바닐라 자동)
            if (def.Worker.TryExecute(parms))
                return true;

            // 2차: 안전 fallback
            var prevArrival = parms.raidArrivalMode;
            var prevStrategy = parms.raidStrategy;

            parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
            parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;

            if (def.Worker.TryExecute(parms))
            {
                reason =
                    $"fallback used: arrival {prevArrival?.defName ?? "auto"} -> EdgeWalkIn, " +
                    $"strategy {prevStrategy?.defName ?? "auto"} -> ImmediateAttack";
                return true;
            }

            reason = "raid execute failed even after fallback";
            return false;
        }

        private static void ApplyRaidCustomization(IncidentParms parms, float quality)
        {
            var settings = CheeseProtocolMod.Settings;
            RaidAdvancedSettings raidAdvSetting = settings?.GetAdvSetting<RaidAdvancedSettings>(CheeseCommand.Raid);
            float randomVar = settings.randomVar;

            ApplyRaidScale(parms, quality, randomVar, raidAdvSetting.raidScaleRange);
        }
        private static void ApplyRaidScale(IncidentParms parms, float quality, float randomVar, QualityRange minMaxRange)
        {
            float raidScale = QualityBetaSampler.SampleQualityWeightedBeta(
                quality,
                minMaxRange,
                concentration01: 1f-randomVar
            );
            float baseRaidPoints = parms.points;
            float finalRaidPoints = baseRaidPoints * raidScale;
            parms.points = Mathf.Max(0f, finalRaidPoints);
            Log.Message($"[CheeseProtocol][Raid] base={baseRaidPoints:0} final={finalRaidPoints:0} scale={raidScale:0.00}");
        }
        
    }
}