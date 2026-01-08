using System;
using System.Collections.Generic;
using Verse;
using UnityEngine;
using System.Linq;

using RimWorld;

namespace CheeseProtocol
{
    public class SupplyAdvancedSettings : CommandAdvancedSettingsBase
    {

        public override CheeseCommand Command => CheeseCommand.Supply;
        public override string Label => "!??";
        private const float lineH = 26f;
        public bool allowFoodSupply;
        public bool allowMedSupply;
        public bool allowDrugSupply;
        public bool allowWeaponSupply;
        public QualityRange supplyValueRange;
        public QualityRange supplyTierRange;
        public QualityRange weaponTierRange;
        public QualityRange weaponTechRange;
        public SupplyAdvancedSettings()
        {
            Log.Warning("[CheeseProtocol] Supply Initialized");
            ResetToDefaults();
            InitializeAll();
        }
        public override void ExposeData()
        {
            LookRange(ref supplyTierRange, "supplyTierRange", CheeseDefaults.SupplyTierRange);
            LookRange(ref supplyValueRange, "supplyValueRange", CheeseDefaults.SupplyValueRange);
            LookRange(ref weaponTierRange, "weaponTierRange", CheeseDefaults.WeaponTierRange);
            LookRange(ref weaponTechRange, "weaponTechRange", CheeseDefaults.WeaponTechRange);
            Scribe_Values.Look(ref allowFoodSupply, "allowFoodSupply", CheeseDefaults.AllowFoodSupply);
            Scribe_Values.Look(ref allowMedSupply, "allowMedSupply", CheeseDefaults.AllowMedSupply);
            Scribe_Values.Look(ref allowDrugSupply, "allowDrugSupply", CheeseDefaults.AllowDrugSupply);
            Scribe_Values.Look(ref allowWeaponSupply, "allowWeaponSupply", CheeseDefaults.AllowWeaponSupply);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                InitializeAll();
            }
        }
        private void LookRange(ref QualityRange range, string baseKey, QualityRange defaultRange)
        {
            float min = range.qMin;
            float max = range.qMax;
            Scribe_Values.Look(ref min, baseKey + "_min", defaultRange.qMin);
            Scribe_Values.Look(ref max, baseKey + "_max", defaultRange.qMax);
            range = QualityRange.init(min, max);
        }
        public override void ResetToDefaults()
        {
            allowFoodSupply = CheeseDefaults.AllowFoodSupply;
            allowMedSupply = CheeseDefaults.AllowMedSupply;
            allowDrugSupply = CheeseDefaults.AllowDrugSupply;
            allowWeaponSupply = CheeseDefaults.AllowWeaponSupply;
            ResetLeverRangesToDefaults();
        }
        private void ResetLeverRangesToDefaults()
        {
            //ageRange = CheeseDefaults.AgeRange;
            supplyTierRange = CheeseDefaults.SupplyTierRange;
            supplyValueRange = CheeseDefaults.SupplyValueRange;
            weaponTierRange = CheeseDefaults.WeaponTierRange;
            weaponTechRange = CheeseDefaults.WeaponTechRange;
        }
        private void InitializeAll()
        {
            //ageRange = QualityRange.init(ageRange.qMin, ageRange.qMax);
            supplyTierRange = QualityRange.init(supplyTierRange.qMin, supplyTierRange.qMax);
            supplyValueRange = QualityRange.init(supplyValueRange.qMin, supplyValueRange.qMax);
            weaponTierRange = QualityRange.init(weaponTierRange.qMin, weaponTierRange.qMax);
            weaponTechRange = QualityRange.init(weaponTechRange.qMin, weaponTechRange.qMax);
        }
        public override float Draw(Rect rect)
        {
            float curY = rect.y;
            float usedH = 0;
            float checkboxPaddingY = 6f;
            float rowH = lineH+checkboxPaddingY;
            UIUtil.RowWithHighlight(rect, ref curY, rowH, r =>{Widgets.CheckboxLabeled(r, "음식 보급 허용", ref allowFoodSupply);});
            UIUtil.RowWithHighlight(rect, ref curY, rowH, r =>{Widgets.CheckboxLabeled(r, "의약품 보급 허용", ref allowMedSupply);});
            UIUtil.RowWithHighlight(rect, ref curY, rowH, r =>{Widgets.CheckboxLabeled(r, "약물 보급 허용", ref allowDrugSupply);});
            UIUtil.RowWithHighlight(rect, ref curY, rowH, r =>{Widgets.CheckboxLabeled(r, "무기 보급 허용", ref allowWeaponSupply);});

            UIUtil.RangeSliderWrapper(rect, ref curY, lineH, "보급품 총 시장가치", ref supplyValueRange, baseMin: GameplayConstants.SupplyValueMin, baseMax: GameplayConstants.SupplyValueMax, roundTo: 1f);
            UIUtil.RangeSliderWrapper(rect, ref curY, lineH, "보급품 (무기제외) 품질", ref supplyTierRange, isPercentile: true);
            UIUtil.RangeSliderWrapperThingTier(rect, ref curY, lineH, "무기 품질", ref weaponTierRange, baseMin: GameplayConstants.ThingTierMin, baseMax: GameplayConstants.ThingTierMax, roundTo: 1f);
            UIUtil.RangeSliderWrapperTechLevel(rect, ref curY, lineH, "무기 기술수준", ref weaponTechRange, baseMin: GameplayConstants.TechLevelMin, baseMax: GameplayConstants.TechLevelMax, roundTo: 1f);
            usedH = curY - rect.y;
            return usedH;
        }
    }
}