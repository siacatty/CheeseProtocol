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
        private bool isResultDirty = true;
        private bool isSampledDirty = false;
        CheeseRollTrace trace = new CheeseRollTrace("", CheeseCommand.Raid);
        CheeseRollTrace sampleTrace = new CheeseRollTrace("", CheeseCommand.Raid);
        public RaidAdvancedSettings()
        {
            ResetToDefaults();
            InitializeAll();
        }
        public override void UpdateResults()
        {
            isResultDirty = true;
        }
        public override int GetPreviewDirtyHash()
        {
            var dh = new DirtyHash();

            dh.AddRange(raidScaleRange);

            dh.Add(allowCenterDrop);
            dh.Add(allowBreacher);
            dh.Add(allowSiege);

            return dh.Value;
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
        public override float DrawResults(Rect rect)
        {
            CheeseSettings settings = CheeseProtocolMod.Settings;
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 32f);
            float curLY = headerRect.yMax;
            float curRY = headerRect.yMax;
            UIUtil.SplitVerticallyByRatio(headerRect, out Rect expectedHeader, out Rect sampledHeader, 0.5f, 0f);
            UIUtil.DrawCenteredText(expectedHeader, "평균값");
            UIUtil.SplitVerticallyByRatio(new Rect (rect.x, headerRect.yMax, rect.width, 1f), out Rect expectRect, out Rect sampledRect, 0.5f, 4f);
            Rect sampledBtn = UIUtil.ResizeRectAligned(sampledHeader, sampledHeader.width* 0.7f, sampledHeader.height *0.8f);
            if (Widgets.ButtonText(sampledBtn, "미리 뽑아보기"))
            {
                sampleTrace.steps.Clear();
                RaidSpawner.Generate(settings.resultDonation01, sampleTrace);
                sampleTrace.CalculateScore();
                isSampledDirty = true;
            }
            float usedH = 0;
            //settings.resultDonation01;
            //settings.randomVar;
            if (isSampledDirty)
            {
                if (sampleTrace.IsValid())
                {
                    foreach(var t in sampleTrace.steps)
                    {
                        DrawSampledRow(sampledRect, t, ref curRY);
                    }
                    Rect row = new Rect(sampledRect.x, curRY, sampledRect.width, 24f);
                    curRY += 24f;
                    UIUtil.SplitVerticallyByRatio(row, out Rect summaryLabel, out Rect summaryContent, 0.4f, 8f);
                    UIUtil.DrawCenteredText(summaryLabel, "총평 : ", TextAlignment.Left);
                    if (sampleTrace.luckScore >= 0)
                        UIUtil.DrawCenteredText(summaryContent, $"+{sampleTrace.luckScore*100:#0.#}% ({sampleTrace.outcome})", TextAlignment.Left, color:Color.green);
                    else
                        UIUtil.DrawCenteredText(summaryContent, $"{sampleTrace.luckScore*100:#0.#}% ({sampleTrace.outcome})", TextAlignment.Left, color:Color.red);
                }
            }
            if (isResultDirty)
            {
                trace.steps.Clear();
                RaidSpawner.Generate(settings.resultDonation01, trace);
                trace.CalculateScore();
                isResultDirty = false;
            }
            if (trace.IsValid())
            {
                foreach(var t in trace.steps)
                {
                    DrawExpectedRow(expectRect, t, ref curLY);
                }
            }
            usedH = curRY > curLY ? curRY - rect.y : curLY - rect.y;
            return usedH;
        }
        private void DrawExpectedRow(Rect rect, TraceStep step, ref float curY)
        {
            Rect row = new Rect(rect.x, curY, rect.width, 24f);
            curY += 24f;
            UIUtil.SplitVerticallyByRatio(row, out Rect labelRect, out Rect expectedRect, 0.6f, 8f);
            UIUtil.DrawCenteredText(labelRect, step.title, TextAlignment.Left);
            UIUtil.DrawCenteredText(expectedRect, $"{step.expected:0.##}", TextAlignment.Left);
        }
        private void DrawSampledRow(Rect rect, TraceStep step, ref float curY)
        {
            Rect row = new Rect(rect.x, curY, rect.width, 24f);
            curY += 24f;
            UIUtil.SplitVerticallyByRatio(row, out Rect valueRect, out Rect scoreRect, 0.5f, 8f);
            UIUtil.DrawCenteredText(valueRect, $"{step.value:0.##}", TextAlignment.Left);
            if (step.score >= 0)
                UIUtil.DrawCenteredText(scoreRect, $"+{step.score*100:0.##}%", TextAlignment.Left, color: Color.green);
            else
                UIUtil.DrawCenteredText(scoreRect, $"{step.score*100:0.##}%", TextAlignment.Left, color: Color.red);
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