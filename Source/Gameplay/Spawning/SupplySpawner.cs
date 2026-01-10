using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using System.Linq;
using UnityEngine;
using static CheeseProtocol.CheeseLog;
using System.Net.Cache;

namespace CheeseProtocol
{
    internal static class SupplySpawner
    {
        public static void Spawn(string donorName, int amount, string message)
        {
            CheeseRollTrace trace = new CheeseRollTrace(donorName, CheeseCommand.Supply);
            float quality = QualityEvaluator.evaluateQuality(amount, CheeseCommand.Supply);
            SupplyRequest supply = Generate(quality, trace);
            if (supply == null) return;
            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;
            var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, map);

            IntVec3 rootCell;
            bool ok = SkyFaller.TrySpawnSupplyDropPod(
                map,
                near: DropCellFinder.TradeDropSpot(map),                   // 또는 edgeNear를 supply에서 넘겨도 됨
                supply,
                out rootCell
            );
            if (!ok)
            {
                QWarn($"Available cell search for Supply failed (center: {rootCell} | {supply})", Channel.Verse);
                CheeseLetter.AlertFail("!보급", "보급 운송포드 착지 가능한 Cell이 없습니다.");
            }
            else
            {
                string letterLabel = $"보급: {supply.label}";
                string letterText = $"보급이 도착했습니다. {supply.label}{(supply.count>1 ? $" {supply.count}개" : "")}.";
                CheeseLetter.SendCheeseLetter(
                    CheeseCommand.Supply,
                    letterLabel,
                    letterText,
                    new LookTargets(rootCell, map),
                    trace,
                    map,
                    LetterDefOf.PositiveEvent
                );
                QMsg($"Supply successful (center: {rootCell} | {supply})", Channel.Debug);
            }
        }

        public static SupplyRequest Generate(float quality, CheeseRollTrace trace)
        {
            CheeseSettings settings = CheeseProtocolMod.Settings;
            SupplyAdvancedSettings supplyAdvSetting = settings?.GetAdvSetting<SupplyAdvancedSettings>(CheeseCommand.Supply);
            if (supplyAdvSetting == null) return null;
            SupplyRequest request = new SupplyRequest();
            if(!TryApplySupplyCustomization(request, quality, settings.randomVar, supplyAdvSetting, trace))
                return null;
            return request;
        }
        public static bool TryApplySupplyCustomization(SupplyRequest supply, float quality, float randomVar, SupplyAdvancedSettings adv, CheeseRollTrace trace)
        {
            if (!TryApplyType(supply, adv))
            {
                QWarn("No supply is allowed");
                CheeseLetter.AlertFail("!보급", "허용된 보급 종류가 없습니다.");
                return false;
            }
            if (!TryApplyTier(supply, quality, randomVar, supply.type == SupplyType.Weapon ? adv.weaponTierRange : adv.supplyTierRange, trace))
            {
                QWarn("No available items among allowed supplies");
                CheeseLetter.AlertFail("!보급", "허용된 보급 종류 중 유효한 아이템이 없습니다.");
                return false;
            }
            if (!TryApplyValue(supply, quality, randomVar, adv.supplyValueRange, adv.weaponTechRange, trace))
            {
                QWarn("No supply meets the market Value");
                CheeseLetter.AlertFail("!보급", "설정된 보급 시장가치에 충족하는 아이템이 없습니다.");
                return false;
            }
            return true;
        }
        public static bool TryApplyType(SupplyRequest supply, SupplyAdvancedSettings adv)
        {
            return SupplyApplier.TryApplyTypeHelper(supply, adv);
        }
        public static bool TryApplyTier(SupplyRequest supply, float quality, float randomVar, QualityRange tierRange, CheeseRollTrace trace)
        {
            TraceStep traceStep = new TraceStep("보급 품질");
            float tier = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    tierRange,
                    concentration01: 1f-randomVar,
                    traceStep
            );
            trace.steps.Add(traceStep);
            return SupplyApplier.TryApplyTierHelper(supply, tier);
        }
        public static bool TryApplyValue(SupplyRequest supply, float quality, float randomVar, QualityRange supplyValueRange, QualityRange weaponTechRange, CheeseRollTrace trace)
        {
            //int techLevel = (int) TechLevel.Undefined;
            TraceStep traceStepTech = new TraceStep("무기 적용기술");
            TraceStep traceStepValue = new TraceStep("총 시장가치");
            int techLevel = Mathf.RoundToInt(QualityBetaSampler.SampleQualityWeightedBeta(
                quality,
                weaponTechRange,
                concentration01: 1f-randomVar,
                traceStepTech
            ));
            float supplyValue = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    supplyValueRange,
                    concentration01: 1f-randomVar,
                    traceStepValue
            );
            trace.steps.Add(traceStepTech);
            trace.steps.Add(traceStepValue);
            return SupplyApplier.TryApplyValueHelper(supply, supplyValue, (TechLevel)techLevel);
        }
    }
}