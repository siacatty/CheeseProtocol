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
            MeteorRequest meteor = new MeteorRequest(parms);
            CheeseRollTrace trace = new CheeseRollTrace(donorName, CheeseCommand.Meteor);
            ApplyMeteorCustomization(meteor, quality, trace);

            ThingDef skyfallerDef = ResolveMeteoriteIncomingDef();
            if (skyfallerDef == null)
            {
                QWarn("MeteoriteIncoming not found. Fallback to vanilla.", Channel.Verse);
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
                QWarn($"Custom meteor failed (center: {rootCell} | size: {meteor.size}). Fallback.", Channel.Verse);
                FallbackVanillaMeteor(meteor);
            }
            else
            {
                string letterText = $"큰 운석이 이 지역에 충돌했습니다. {meteor.label} 더미를 남겼습니다.";
                string letterLabel = $"운석: {meteor.label}";
                CheeseLetter.SendCheeseLetter(
                    CheeseCommand.Meteor,
                    letterLabel,
                    letterText,
                    new LookTargets(rootCell, map),
                    trace,
                    map,
                    meteor.type.ToLowerInvariant().Contains("mineable") ? LetterDefOf.PositiveEvent : LetterDefOf.NeutralEvent
                );
                QMsg($"Custom meteor success: {meteor.type} x {meteor.size}", Channel.Debug);
            }
        }
        
        private static void FallbackVanillaMeteor(MeteorRequest meteor)
        {
            IncidentDef def = DefDatabase<IncidentDef>.GetNamed("MeteoriteImpact", false);
            if (def == null)
            {
                QWarn("MeteoriteImpact def not found.", Channel.Verse);
                return;
            }
            bool ok = def.Worker.TryExecute(meteor.parms);
            if (!ok)
            {
                QWarn("Vanilla MeteorSpawn failed to execute.", Channel.Verse);
                CheeseLetter.AlertFail("!운석", "실행 실패: 로그 확인 필요.");
            }
        }
        private static void ApplyMeteorCustomization(MeteorRequest meteor, float quality, CheeseRollTrace trace)
        {
            var settings = CheeseProtocolMod.Settings;
            MeteorAdvancedSettings meteorAdvSettings = settings.GetAdvSetting<MeteorAdvancedSettings>(CheeseCommand.Meteor);
            float randomVar = settings.randomVar;
            ApplyMeteorType(meteor, quality, randomVar, meteorAdvSettings.meteorTypeRange, meteorAdvSettings.allowedMeteorCandidates, trace);
            ApplyMeteorSize(meteor, quality, randomVar, meteorAdvSettings.meteorSizeRange, trace);
            QMsg($"Meteor pick chosen={meteor.type} score={meteor.score:0.###}, size={meteor.size}", Channel.Debug);
        }

        private static void ApplyMeteorType(MeteorRequest meteor, float quality, float randomVar, QualityRange minMaxRange, List<MeteorCandidate> candidates, CheeseRollTrace trace)
        {
            float meteorTypeQuality = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    minMaxRange,
                    concentration01: 1f-randomVar,
                    out float score,
                    debugLog: false
            );
            trace.steps.Add(new TraceStep("운석 종류", score, minMaxRange.Expected(quality), meteorTypeQuality));
            MeteorApplier.ApplyMeteorTypeHelper(meteor, meteorTypeQuality, candidates);
        }
        private static void ApplyMeteorSize(MeteorRequest meteor, float quality, float randomVar, QualityRange minMaxRange, CheeseRollTrace trace)
        {
            int baseMinSize = GameplayConstants.MeteorSizeMin;
            int baseMaxSize = GameplayConstants.MeteorSizeMax;
            float sizeF = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    minMaxRange,
                    concentration01: 1f-randomVar,
                    out float score
                );
            QMsg($"Meteor baseMinSize={baseMinSize}, baseMaxSize={baseMaxSize}, rangeMin={minMaxRange.qMin}, rangeMax={minMaxRange.qMax}, sizeF={sizeF}", Channel.Debug);
            int meteorBaseSize = Mathf.RoundToInt(Mathf.Clamp(sizeF, baseMinSize, baseMaxSize));
            trace.steps.Add(new TraceStep("운석 크기", score, minMaxRange.Expected(quality), meteorBaseSize));
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