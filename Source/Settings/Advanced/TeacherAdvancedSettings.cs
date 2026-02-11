using System;
using System.Collections.Generic;
using Verse;
using UnityEngine;
using System.Linq;

using RimWorld;

namespace CheeseProtocol
{
    public class TeacherAdvancedSettings : CommandAdvancedSettingsBase
    {
        public override CheeseCommand Command => CheeseCommand.Teacher;
        public override string Label => "!??";
        private const float lineH = 26f;
        public QualityRange passionProbRange;
        public QualityRange teachSkillRange;
        public QualityRange studentCountRange;
        public bool allowPassion;
        private bool isResultDirty = true;
        private bool isSampledDirty = false;
        CheeseRollTrace trace = new CheeseRollTrace("", CheeseCommand.Teacher);
        CheeseRollTrace sampleTrace = new CheeseRollTrace("", CheeseCommand.Teacher);
        public TeacherAdvancedSettings()
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

            dh.AddRange(passionProbRange);
            dh.AddRange(teachSkillRange);

            dh.Add(allowPassion);

            return dh.Value;
        }
        public override void ExposeData()
        {
            LookRange(ref passionProbRange, "passionProbRange", CheeseDefaults.PassionProbRange);
            LookRange(ref teachSkillRange, "teachSkillRange", CheeseDefaults.TeachSkillRange);
            LookRange(ref studentCountRange, "studentCountRange", CheeseDefaults.StudentCountRange);
            Scribe_Values.Look(ref allowPassion, "allowPassion", CheeseDefaults.AllowPassion);
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
            allowPassion = CheeseDefaults.AllowAlpha;
            ResetLeverRangesToDefaults();
        }
        private void ResetLeverRangesToDefaults()
        {
            passionProbRange = CheeseDefaults.PassionProbRange;
            teachSkillRange = CheeseDefaults.TeachSkillRange;
            studentCountRange = CheeseDefaults.StudentCountRange;
        }
        private void InitializeAll()
        {
            passionProbRange = QualityRange.init(passionProbRange.qMin, passionProbRange.qMax);
            teachSkillRange = QualityRange.init(teachSkillRange.qMin, teachSkillRange.qMax);
            studentCountRange = QualityRange.init(studentCountRange.qMin, studentCountRange.qMax);
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
                TeacherSpawner.Generate(settings.resultDonation01, sampleTrace);
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
                TeacherSpawner.Generate(settings.resultDonation01, trace);
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
            string studentCountTip = "선생님이 가르칠수있는 최대 학생 수";
            string teachSkillTip = "수업으로부터 얻는 스킬 경험치";
            string passionProbTip = "도주 성공 시 열정을 부여할 확률";
            UIUtil.RowWithHighlight(rect, ref curY, rowH, r =>{Widgets.CheckboxLabeled(r, "열정 부여 가능", ref allowPassion);});
            UIUtil.RangeSliderWrapper(rect, ref curY, lineH, "가르치는 학생 수", ref studentCountRange, baseMin: GameplayConstants.StudentCountMin, baseMax: GameplayConstants.StudentCountMax, roundTo: 1f, tooltip:studentCountTip);
            UIUtil.RangeSliderWrapper(rect, ref curY, lineH, "증가 스킬 경험치", ref teachSkillRange, baseMin: GameplayConstants.TeachSkillEXPMin, baseMax: GameplayConstants.TeachSkillEXPMax, roundTo: 1f, tooltip:teachSkillTip);
            UIUtil.RangeSliderWrapper(rect, ref curY, lineH, "도주 시 열정부여 확률", ref passionProbRange, isPercentile: true, tooltip:passionProbTip);
            usedH = curY - rect.y;
            return usedH;
        }
    }
}