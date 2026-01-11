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
        private bool isResultDirty = true;
        private bool isSampledDirty = false;
        CheeseRollTrace trace = new CheeseRollTrace("", CheeseCommand.Tame);
        CheeseRollTrace sampleTrace = new CheeseRollTrace("", CheeseCommand.Tame);
        public TameAdvancedSettings()
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

            dh.AddRange(tameValueRange);

            return dh.Value;
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
                TameSpawner.Generate(settings.resultDonation01, sampleTrace);
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
                TameSpawner.Generate(settings.resultDonation01, trace);
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
            string tameValueTip = "값이 높을수록 시장 가치가 높은 동물이 합류합니다.\n확장팩에 따라 최대 시장 가치의 동물이 달라질 수 있어 배율로 표시됩니다.";
            //UIUtil.RowWithHighlight(rect, ref curY, rowH, r =>{Widgets.CheckboxLabeled(r, "음식 보급 허용", ref allowFoodSupply);});
            UIUtil.RangeSliderWrapper(rect, ref curY, lineH, "합류 동물 시장가치", ref tameValueRange, isPercentile: true, tooltip:tameValueTip);
            usedH = curY - rect.y;
            return usedH;
        }
    }
}