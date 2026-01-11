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
        private bool isResultDirty = true;
        private bool isSampledDirty = false;
        CheeseRollTrace trace = new CheeseRollTrace("", CheeseCommand.Thrumbo);
        CheeseRollTrace sampleTrace = new CheeseRollTrace("", CheeseCommand.Thrumbo);
        public ThrumboAdvancedSettings()
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

            dh.AddRange(alphaProbRange);
            dh.AddRange(thrumboCountRange);

            dh.Add(allowAlpha);

            return dh.Value;
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
                ThrumboSpawner.Generate(settings.resultDonation01, sampleTrace);
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
                ThrumboSpawner.Generate(settings.resultDonation01, trace);
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