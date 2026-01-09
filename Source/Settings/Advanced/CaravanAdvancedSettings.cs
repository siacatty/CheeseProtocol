using System;
using System.Collections.Generic;
using Verse;
using UnityEngine;
using System.Linq;

using RimWorld;

namespace CheeseProtocol
{
    public class CaravanAdvancedSettings : CommandAdvancedSettingsBase
    {
        public override CheeseCommand Command => CheeseCommand.Caravan;
        public override string Label => "!상단";
        private const float lineH = 26f;

        public QualityRange orbitalRange;
        public bool allowShamanCaravan;
        public bool allowBulkCaravan;
        public bool allowSlaverCaravan;
        public bool allowExoticCaravan;
        public bool allowCombatCaravan;
        public bool allowRoyalCaravan;
        public bool allowImperialCaravan;
        public CaravanAdvancedSettings()
        {
            ResetToDefaults();
            InitializeAll();
        }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref allowShamanCaravan, "allowShamanCaravan", CheeseDefaults.AllowShamanCaravan);
            Scribe_Values.Look(ref allowBulkCaravan, "allowBulkCaravan", CheeseDefaults.AllowBulkCaravan);
            Scribe_Values.Look(ref allowSlaverCaravan, "allowSlaverCaravan", CheeseDefaults.AllowSlaverCaravan);
            Scribe_Values.Look(ref allowExoticCaravan, "allowExoticCaravan", CheeseDefaults.AllowExoticCaravan);
            Scribe_Values.Look(ref allowCombatCaravan, "allowCombatCaravan", CheeseDefaults.AllowCombatCaravan);
            Scribe_Values.Look(ref allowRoyalCaravan, "allowRoyalCaravan", CheeseDefaults.AllowRoyalCaravan);
            Scribe_Values.Look(ref allowImperialCaravan, "allowImperialCaravan", CheeseDefaults.AllowImperialCaravan);
            LookRange(ref orbitalRange, "orbitalRange", CheeseDefaults.OrbitalRange);
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
            allowRoyalCaravan = CheeseDefaults.AllowRoyalCaravan;
            allowImperialCaravan = CheeseDefaults.AllowImperialCaravan;
            allowShamanCaravan = CheeseDefaults.AllowShamanCaravan;
            allowBulkCaravan = CheeseDefaults.AllowBulkCaravan;
            allowSlaverCaravan = CheeseDefaults.AllowSlaverCaravan;
            allowExoticCaravan = CheeseDefaults.AllowExoticCaravan;
            allowCombatCaravan = CheeseDefaults.AllowCombatCaravan;
            ResetLeverRangesToDefaults();
        }
        private void ResetLeverRangesToDefaults()
        {
            orbitalRange = CheeseDefaults.OrbitalRange;
        }
        private void InitializeAll()
        {
            orbitalRange = QualityRange.init(orbitalRange.qMin, orbitalRange.qMax);
        }
        public override float Draw(Rect rect)
        {
            float curY = rect.y;
            float usedH = 0;
            float checkboxPaddingY = 6f;
            float rowH = lineH+checkboxPaddingY;
            string allowTraderTooltip = "모든 상단 비허용 시 이벤트가 실행되지 않습니다.";
            string orbitTooltip = "현재 맵에 전력이 연결된 통신기가 없을 경우, 지상 상단으로 고정됩니다.";
            UIUtil.RowWithHighlight(rect, ref curY, rowH, r =>{Widgets.CheckboxLabeled(r, "황실 공물 징수인 허용", ref allowRoyalCaravan);}, tooltip:allowTraderTooltip);
            UIUtil.RowWithHighlight(rect, ref curY, rowH, r =>{Widgets.CheckboxLabeled(r, "제국 상인 허용", ref allowImperialCaravan);}, tooltip:allowTraderTooltip);
            UIUtil.RowWithHighlight(rect, ref curY, rowH, r =>{Widgets.CheckboxLabeled(r, "골동품 상인 허용", ref allowShamanCaravan);}, tooltip:allowTraderTooltip);
            UIUtil.RowWithHighlight(rect, ref curY, rowH, r =>{Widgets.CheckboxLabeled(r, "원자재 상인 허용", ref allowBulkCaravan);}, tooltip:allowTraderTooltip);
            UIUtil.RowWithHighlight(rect, ref curY, rowH, r =>{Widgets.CheckboxLabeled(r, "노예 상인 허용", ref allowSlaverCaravan);}, tooltip:allowTraderTooltip);
            UIUtil.RowWithHighlight(rect, ref curY, rowH, r =>{Widgets.CheckboxLabeled(r, "희귀품 상인 허용", ref allowExoticCaravan);}, tooltip:allowTraderTooltip);
            UIUtil.RowWithHighlight(rect, ref curY, rowH, r =>{Widgets.CheckboxLabeled(r, "무기 상인 허용", ref allowCombatCaravan);}, tooltip:allowTraderTooltip);
            UIUtil.RangeSliderWrapper(rect, ref curY, lineH, "궤도상선 확률 보정치", ref orbitalRange, isPercentile: true, tooltip: orbitTooltip);
            usedH = curY - rect.y;
            return usedH;
        }
    }
}