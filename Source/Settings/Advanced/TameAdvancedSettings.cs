using System;
using System.Collections.Generic;
using Verse;
using UnityEngine;
using System.Linq;

using RimWorld;

namespace CheeseProtocol
{
    public class TameAdvancedSettings : CommandAdvancedSettingsBase
    {
        public override CheeseCommand Command => CheeseCommand.Tame;
        public override string Label => "!??";
        private const float lineH = 26f;
        public QualityRange tameValueRange;
        public TameAdvancedSettings()
        {
            ResetToDefaults();
            InitializeAll();
        }
        public override void ExposeData()
        {
            LookRange(ref tameValueRange, "tameValueRange", CheeseDefaults.TameValueRange);
            //Scribe_Values.Look(ref allowWeaponSupply, "allowWeaponSupply", CheeseDefaults.AllowWeaponSupply);
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
            //allowFoodSupply = CheeseDefaults.AllowFoodSupply;
            ResetLeverRangesToDefaults();
        }
        private void ResetLeverRangesToDefaults()
        {
            //ageRange = CheeseDefaults.AgeRange;
            tameValueRange = CheeseDefaults.TameValueRange;
        }
        private void InitializeAll()
        {
            //ageRange = QualityRange.init(ageRange.qMin, ageRange.qMax);
            tameValueRange = QualityRange.init(tameValueRange.qMin, tameValueRange.qMax);
        }
        public override float Draw(Rect rect)
        {
            float curY = rect.y;
            float usedH = 0;
            float checkboxPaddingY = 6f;
            float rowH = lineH+checkboxPaddingY;
            string tameValueTip = "값이 높을수록 시장 가치가 높은 동물이 합류합니다.\n확장팩에 따라 최대 시장 가치의 동물이 달라질 수 있어 배율로 표시됩니다.";
            //UIUtil.RowWithHighlight(rect, ref curY, rowH, r =>{Widgets.CheckboxLabeled(r, "음식 보급 허용", ref allowFoodSupply);});
            UIUtil.RangeSliderWrapper(rect, ref curY, lineH, "합류 동물 시장가치", ref tameValueRange, isPercentile: true, tooltip:tameValueTip);
            usedH = curY - rect.y;
            return usedH;
        }
    }
}