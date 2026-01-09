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
        public bool allowCenterDrop;
        public bool allowBreacher;
        public bool allowSiege;
        public QualityRange raidScaleRange;
        public RaidAdvancedSettings()
        {
            ResetToDefaults();
            InitializeAll();
        }
        public override void ExposeData()
        {
            LookRange(ref raidScaleRange, "raidScaleRange", CheeseDefaults.RaidScaleRange);
            Scribe_Values.Look(ref allowCenterDrop, "allowCenterDrop", CheeseDefaults.AllowCenterDrop);
            Scribe_Values.Look(ref allowBreacher, "allowBreacher", CheeseDefaults.AllowBreacher);
            Scribe_Values.Look(ref allowSiege, "allowSiege", CheeseDefaults.AllowSiege);
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
            allowCenterDrop = CheeseDefaults.AllowCenterDrop;
            allowBreacher = CheeseDefaults.AllowBreacher;
            allowSiege = CheeseDefaults.AllowSiege;
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
            string raidScaleTip = "설정된 수치는 현재 정착지의 습격 포인트에 곱해집니다.\n주의) 값을 지나치게 높게 설정하면 난이도가 급격히 상승할 수 있습니다.";
            //UIUtil.RowWithHighlight(rect, ref curY, rowH, r =>{Widgets.CheckboxLabeled(r, "중앙드랍 허용", ref allowCenterDrop);});
            //UIUtil.RowWithHighlight(rect, ref curY, rowH, r =>{Widgets.CheckboxLabeled(r, "벽파괴 습격 (브리처+새퍼) 허용", ref allowBreacher);});
            //UIUtil.RowWithHighlight(rect, ref curY, rowH, r =>{Widgets.CheckboxLabeled(r, "공성(박격포) 허용", ref allowSiege);});
            UIUtil.RangeSliderWrapper(rect, ref curY, lineH, "습격 강도 배율", ref raidScaleRange, baseMin: GameplayConstants.RaidScaleMin, baseMax: GameplayConstants.RaidScaleMax, isPercentile: true, tooltip:raidScaleTip);
            usedH = curY - rect.y;
            return usedH;
        }
    }
}