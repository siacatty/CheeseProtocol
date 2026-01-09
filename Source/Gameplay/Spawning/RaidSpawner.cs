using System;
using System.Reflection;
using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;
using System.Linq;
using System.Collections;
using static CheeseProtocol.CheeseLog;

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
            CheeseRollTrace trace = new CheeseRollTrace(donorName, CheeseCommand.Raid);
            ApplyRaidCustomization(parms, quality, trace);
            IncidentDef def = DefDatabase<IncidentDef>.GetNamed("RaidEnemy", false);
            if (def == null)
            {
                QWarn("RaidEnemy def not found.", Channel.Verse);
                return;
            }
            string reason;
            bool ok = TryExecuteRaidWithFallback(IncidentDefOf.RaidEnemy, parms, out reason, trace);
            QMsg($"RaidSpawn ok={ok} points={parms.points:0} reason={reason ?? "n/a"}", Channel.Debug);
            if (!ok)
            {
                QWarn("RaidEnemy failed to execute.", Channel.Verse);
                CheeseLetter.AlertFail("!습격", "실행 실패: 로그 확인 필요.");
            }
        }

        private static bool TryExecuteRaidWithFallback(
            IncidentDef def,
            IncidentParms parms,
            out string reason,
            CheeseRollTrace trace)
        {
            reason = null;

            if (VanillaIncidentRunner.TryExecuteWithTrace(def, parms, trace))
                return true;

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

        private static void ApplyRaidCustomization(IncidentParms parms, float quality, CheeseRollTrace trace)
        {
            var settings = CheeseProtocolMod.Settings;
            RaidAdvancedSettings raidAdvSetting = settings?.GetAdvSetting<RaidAdvancedSettings>(CheeseCommand.Raid);
            float randomVar = settings.randomVar;

            ApplyRaidScale(parms, quality, randomVar, raidAdvSetting.raidScaleRange, trace);
        }
        private static void ApplyRaidScale(IncidentParms parms, float quality, float randomVar, QualityRange minMaxRange, CheeseRollTrace trace)
        {
            float raidScale = QualityBetaSampler.SampleQualityWeightedBeta(
                quality,
                minMaxRange,
                concentration01: 1f-randomVar,
                out float score
            );
            trace.steps.Add(new TraceStep("습격 강도 배율", score, minMaxRange.Expected(quality), raidScale));
            float baseRaidPoints = parms.points;
            float finalRaidPoints = baseRaidPoints * raidScale;
            parms.points = Mathf.Max(0f, finalRaidPoints);
            QMsg($"Raid base={baseRaidPoints:0} final={finalRaidPoints:0} scale={raidScale:0.00}", Channel.Debug);
        }
        
    }
}