using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using System.Linq;
using UnityEngine;

namespace CheeseProtocol
{
    internal static class SupplySpawner
    {
        public static void Spawn(string donorName, int amount, string message)
        {
            CheeseSettings settings = CheeseProtocolMod.Settings;
            SupplyAdvancedSettings supplyAdvSetting = settings?.GetAdvSetting<SupplyAdvancedSettings>(CheeseCommand.Supply);
            if (supplyAdvSetting == null) return;
            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;
            var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, map);
            SupplyRequest supply = new SupplyRequest(parms);
            float quality = QualityEvaluator.evaluateQuality(amount, CheeseCommand.Supply);
            if(!TryApplySupplyCustomization(supply, quality, settings.randomVar, supplyAdvSetting))
                return;
            Log.Message(supply);
            IntVec3 rootCell;
            bool ok = SkyFaller.TrySpawnSupplyDropPod(
                map,
                near: DropCellFinder.TradeDropSpot(map),                   // 또는 edgeNear를 supply에서 넘겨도 됨
                supply,
                out rootCell
            );
            if (!ok)
            {
                Log.Warning($"[CheeseProtocol] Supply failed (center: {rootCell} | {supply})");
                Messages.Message(
                    "!보급 실행 실패: 보급 운송포드 착지 가능한 Cell이 없습니다.",
                    MessageTypeDefOf.RejectInput
                );
            }
            else
            {
                CheeseLetter.SendSupplySuccessLetter(map, rootCell, supply);
                Log.Message($"[CheeseProtocol] Supply successful (center: {rootCell} | {supply})");
            }
        }
        public static bool TryApplySupplyCustomization(SupplyRequest supply, float quality, float randomVar, SupplyAdvancedSettings adv)
        {
            if (!TryApplyType(supply, adv))
            {
                Log.Warning("[CheeseProtocol] No supply is allowed");
                Messages.Message(
                    "!보급 실행 실패: 허용된 보급 종류가 없습니다.",
                    MessageTypeDefOf.RejectInput
                );
                return false;
            }
            if (!TryApplyTier(supply, quality, randomVar, supply.type == SupplyType.Weapon ? adv.weaponTierRange : adv.supplyTierRange))
            {
                Log.Warning("[CheeseProtocol] No available items among allowed supplies");
                Messages.Message(
                    "!보급 실행 실패: 허용된 보급 종류 중 유효한 아이템이 없습니다.",
                    MessageTypeDefOf.RejectInput
                );
                return false;
            }
            if (!TryApplyValue(supply, quality, randomVar, adv.supplyValueRange, adv.weaponTechRange))
            {
                Log.Warning("[CheeseProtocol] No supply meets the market Value");
                Messages.Message(
                    "!보급 실행 실패: 설정된 보급 시장가치에 충족하는 아이템이 없습니다.",
                    MessageTypeDefOf.RejectInput
                );
                return false;
            }
            return true;
        }
        public static bool TryApplyType(SupplyRequest supply, SupplyAdvancedSettings adv)
        {
            return SupplyApplier.TryApplyTypeHelper(supply, adv);
        }
        public static bool TryApplyTier(SupplyRequest supply, float quality, float randomVar, QualityRange tierRange)
        {
            float tier = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    tierRange,
                    concentration01: 1f-randomVar
            );
            return SupplyApplier.TryApplyTierHelper(supply, tier);
        }
        public static bool TryApplyValue(SupplyRequest supply, float quality, float randomVar, QualityRange supplyValueRange, QualityRange weaponTechRange)
        {
            int techLevel = (int) TechLevel.Undefined;
            if (supply.type == SupplyType.Weapon)
            {
                techLevel = Mathf.RoundToInt(QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    weaponTechRange,
                    concentration01: 1f-randomVar
                ));
            }
            float supplyValue = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    supplyValueRange,
                    concentration01: 1f-randomVar
            );
            return SupplyApplier.TryApplyValueHelper(supply, supplyValue, (TechLevel)techLevel);
        }
    }
}