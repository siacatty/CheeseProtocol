using System;
using System.Collections.Generic;
using Verse;
using UnityEngine;
using System.Linq;

using RimWorld;

namespace CheeseProtocol
{
    public class ThrumboAdvancedSettings : CommandAdvancedSettingsBase
    {
        public override CheeseCommand Command => CheeseCommand.Thrumbo;
        public override string Label => "!??";
        private const float lineH = 26f;
        public QualityRange alphaProbRange;
        public QualityRange thrumboCountRange;
        public bool allowAlpha;
        public ThrumboAdvancedSettings()
        {
            ResetToDefaults();
            InitializeAll();
        }
        public override void ExposeData()
        {
            LookRange(ref alphaProbRange, "alphaProbRange", CheeseDefaults.AlphaProbRange);
            LookRange(ref thrumboCountRange, "thrumboCountRange", CheeseDefaults.ThrumboCountRange);
            Scribe_Values.Look(ref allowAlpha, "allowAlpha", CheeseDefaults.AllowAlpha);
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
            allowAlpha = CheeseDefaults.AllowAlpha;
            ResetLeverRangesToDefaults();
        }
        private void ResetLeverRangesToDefaults()
        {
            alphaProbRange = CheeseDefaults.AlphaProbRange;
            thrumboCountRange = CheeseDefaults.ThrumboCountRange;
        }
        private void InitializeAll()
        {
            alphaProbRange = QualityRange.init(alphaProbRange.qMin, alphaProbRange.qMax);
            thrumboCountRange = QualityRange.init(thrumboCountRange.qMin, thrumboCountRange.qMax);
        }
        public override float DrawResults(Rect rect)
        {
            float curY = rect.y;
            float usedH = 0;
            


            usedH = curY - rect.y;
            return usedH;
        }
        public override float Draw(Rect rect)
        {
            float curY = rect.y;
            float usedH = 0;
            float checkboxPaddingY = 6f;
            float rowH = lineH+checkboxPaddingY;
            string thrumboCountTip = "설정된 트럼보 개체 수에는 알파 트럼보의 개체 수도 포함됩니다.";
            string alphaProbTip = "알파 트럼보가 등장할 확률입니다.";
            UIUtil.RowWithHighlight(rect, ref curY, rowH, r =>{Widgets.CheckboxLabeled(r, "알파 트럼보 허용 (최대 한마리)", ref allowAlpha);});
            UIUtil.RangeSliderWrapper(rect, ref curY, lineH, "트럼보 마리 수", ref thrumboCountRange, baseMin: GameplayConstants.ThrumboMin, baseMax: GameplayConstants.ThrumboMax, roundTo: 1f, tooltip:thrumboCountTip);
            UIUtil.RangeSliderWrapper(rect, ref curY, lineH, "알파 트럼보 등장 확률", ref alphaProbRange, isPercentile: true, tooltip:alphaProbTip);
            usedH = curY - rect.y;
            return usedH;
        }
    }
}