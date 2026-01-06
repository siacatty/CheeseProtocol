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
            bool ok = def.Worker.TryExecute(parms);
            if (!ok)
                Log.Warning("[CheeseProtocol] RaidEnemy failed to execute.");
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
        private static float GetThreatScale()
        {
            float threatScale = 1f;

            if (Find.Storyteller == null)
                return threatScale;

            var diff = Find.Storyteller.difficulty;
            if (diff == null) return threatScale;

            threatScale = diff.threatScale;
            return threatScale;
        }
        private static bool TryGetWealth(Map map, out float total, out float items, out float buildings, out float pawns)
        {
            if (map == null || map.wealthWatcher == null)
            {
                total = items = buildings = pawns = 0f;
                return false;
            }

            var w = map.wealthWatcher;
            total = w.WealthTotal;
            items = w.WealthItems;
            buildings = w.WealthBuildings;
            pawns = w.WealthPawns;
            return true;
        }

        //Raid Points = (Wealth Points + Pawn Points) * (Difficulty) * (Starting Factor) * (Adaption Factor)
        //"Storyteller Wealth" = (Colony Wealth Items + Colony Wealth Creatures + (Colony Wealth Buildings * 0.5))
        private static float GetCurrentThreatPoints(Map map)
        {
            if (map == null) return 0f;
            return StorytellerUtility.DefaultThreatPointsNow(map);
        }
    }
}