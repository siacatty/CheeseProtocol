using System;
using System.Collections.Generic;
using Verse;
using UnityEngine;
using System.Linq;

using RimWorld;

namespace CheeseProtocol
{
    public class RaidAdvancedSettings : CommandAdvancedSettingsBase
    {
        public override CheeseCommand Command => CheeseCommand.Raid;
        public override string Label => "!습격";
        private const float lineH = 26f;
        public QualityRange raidScaleRange;
        public RaidAdvancedSettings()
        {
            ResetToDefaults();
            InitializeAll();
        }
        public override void ExposeData()
        {
            LookRange(ref raidScaleRange, "raidScaleRange", CheeseDefaults.RaidScaleRange);
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
            ResetLeverRangesToDefaults();
        }
        private void ResetLeverRangesToDefaults()
        {
            //ageRange = CheeseDefaults.AgeRange;
            raidScaleRange = CheeseDefaults.RaidScaleRange;
        }
        private void InitializeAll()
        {
            //ageRange = QualityRange.init(ageRange.qMin, ageRange.qMax);
            raidScaleRange = QualityRange.init(raidScaleRange.qMin, raidScaleRange.qMax);
        }
        public override float Draw(Rect rect)
        {
            float curY = rect.y;
            float usedH = 0;
            float checkboxPaddingY = 6f;
            float rowH = lineH+checkboxPaddingY;
            //UIUtil.RowWithHighlight(rect, ref curY, rowH, r =>{Widgets.CheckboxLabeled(r, "결격사항 허용", ref allowWorkDisable);});
            Log.Warning($"[CheeseProtocol] raidScale min = {raidScaleRange.qMin}, raidScale max = {raidScaleRange.qMax}");
            UIUtil.RangeSliderWrapper(rect, ref curY, lineH, "습격 강도", ref raidScaleRange, baseMin: GameplayConstants.RaidScaleMin, baseMax: GameplayConstants.RaidScaleMax, isPercentile: true);
            usedH = curY - rect.y;
            return usedH;
        }
    }
}