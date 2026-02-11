using System;
using System.Collections.Generic;
using Verse;
using UnityEngine;
using System.Linq;

using RimWorld;

namespace CheeseProtocol
{
    public class MeteorAdvancedSettings : CommandAdvancedSettingsBase
    {
        public List<string> disallowedMeteorKeys;
        public List<MeteorCandidate> allowedMeteorCandidates;
        public List<MeteorCandidate> disallowedMeteorCandidates;
        public override CheeseCommand Command => CheeseCommand.Meteor;
        public override string Label => "!운석";
        private const float lineH = 26f;
        private Vector2 disallowedMeteorScrollPos;
        private float disallowedMeteorListHeight = 400f;
        public QualityRange meteorTypeRange;
        public QualityRange meteorSizeRange;
        private readonly Color HeaderBg     = new Color(0.20f, 0.32f, 0.40f); // steel blue
        private readonly Color HeaderBorder = new Color(0.45f, 0.65f, 0.75f);

        private readonly Color BodyBg       = new Color(0.18f, 0.22f, 0.26f); // blue-charcoal
        private readonly Color BodyBorder   = new Color(0.35f, 0.48f, 0.55f);
        private bool isResultDirty = true;
        private bool isSampledDirty = false;
        CheeseRollTrace trace = new CheeseRollTrace("", CheeseCommand.Meteor);
        CheeseRollTrace sampleTrace = new CheeseRollTrace("", CheeseCommand.Meteor);
        public MeteorAdvancedSettings()
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

            dh.AddRange(meteorTypeRange);
            dh.AddRange(meteorSizeRange);

            // allowedMeteorKeys는 보통 "선택된 집합" 의미라 순서 무관 처리 추천
            dh.AddListUnordered(disallowedMeteorKeys);

            return dh.Value;
        }
        public override void ExposeData()
        {
            LookRange(ref meteorTypeRange, "meteorTypeRange", CheeseDefaults.MeteorTypeRange);
            LookRange(ref meteorSizeRange, "meteorSizeRange", CheeseDefaults.MeteorSizeRange);

            Scribe_Collections.Look(ref disallowedMeteorKeys, "disallowedMeteorKeys", LookMode.Value);
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (disallowedMeteorKeys == null)
                    disallowedMeteorKeys = new List<string>(CheeseDefaults.DisallowedMeteorKeys);
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
            disallowedMeteorScrollPos = Vector2.zero;
            
            disallowedMeteorKeys = new List<string>(CheeseDefaults.DisallowedMeteorKeys);
            if (CheeseProtocolMod.MeteorCatalog != null)
            {
                UpdateMeteorList();
            }
            else
            {
                LongEventHandler.ExecuteWhenFinished(UpdateMeteorList);
            }
            ResetLeverRangesToDefaults();
        }
        private void ResetLeverRangesToDefaults()
        {
            meteorTypeRange = CheeseDefaults.MeteorTypeRange;
            meteorSizeRange = CheeseDefaults.MeteorSizeRange;
        }
        private void InitializeAll()
        {
            meteorTypeRange = QualityRange.init(meteorTypeRange.qMin, meteorTypeRange.qMax);
            meteorSizeRange = QualityRange.init(meteorSizeRange.qMin, meteorSizeRange.qMax);

            //ageRange = QualityRange.init(ageRange.qMin, ageRange.qMax);
            disallowedMeteorKeys = disallowedMeteorKeys.Distinct().ToList();
            UpdateMeteorList();
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
                MeteorSpawner.Generate(settings.resultDonation01, sampleTrace);
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
                MeteorSpawner.Generate(settings.resultDonation01, trace);
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
            string typeTip = "값이 높을수록 희귀도와 시장 가치가 높은 운석이 선택될 확률이 증가합니다.";
            string sizeTip = "값이 높을수록 운석 크기가 커집니다. 운석 크기는 선택된 운석 종류에 따라 약간의 보정이 적용됩니다.";
            //UIUtil.RowWithHighlight(rect, ref curY, rowH, r =>{Widgets.CheckboxLabeled(r, "결격사항 허용", ref allowWorkDisable);});
            UIUtil.RangeSliderWrapper(rect, ref curY, lineH, "운석 종류 퀄리티", ref meteorTypeRange, isPercentile: true, tooltip: typeTip);
            UIUtil.RangeSliderWrapper(rect, ref curY, lineH, "운석 크기", ref meteorSizeRange, baseMin: GameplayConstants.MeteorSizeMin, baseMax: GameplayConstants.MeteorSizeMax, roundTo: 1f, tooltip:sizeTip);
            usedH = curY - rect.y;
            return usedH;
        }
        public float DrawEditableList(Rect rect, string title, float lineH, float paddingX, float paddingY)
        {
            float usedH = 0f;
            float windowH = 220f;
            float topBarH = 34f;
            float btnSize = 24f;
            Rect windowRect = new Rect(rect.x, rect.y, rect.width, windowH);
            UIUtil.SplitVerticallyByRatio(windowRect, out Rect disallowedMeteorRect, out Rect unused, 0.5f, paddingX);
            UIUtil.SplitHorizontallyByHeight(disallowedMeteorRect, out Rect disallowedTopRect, out Rect disallowedListRect, topBarH, 0f);
            UIUtil.SplitVerticallyByRatio(disallowedTopRect, out Rect disallowedTopLabel, out Rect disallowedAddBtn, 0.7f, 0f);

            Color oldColor = GUI.color;
            Widgets.DrawBoxSolid(disallowedTopRect, HeaderBg);
            GUI.color = HeaderBorder;
            Widgets.DrawBox(disallowedTopRect);
            Widgets.DrawBoxSolid(disallowedListRect, BodyBg);
            GUI.color = BodyBorder;
            Widgets.DrawBox(disallowedListRect);
            GUI.color= oldColor;

            disallowedTopLabel = UIUtil.ShrinkRect(disallowedTopLabel, 6f);
            UIUtil.DrawCenteredText(disallowedTopLabel, "비허용 운석 종류", align: TextAlignment.Left);

            disallowedAddBtn = UIUtil.ResizeRectAligned(disallowedAddBtn, btnSize, btnSize, TextAlignment.Right);
            Widgets.DrawHighlightIfMouseover(disallowedAddBtn);

            DrawAddMeteorDropdownButton(disallowedAddBtn, disallowedMeteorCandidates);
            UIUtil.AutoScrollView(
                disallowedListRect,
                ref disallowedMeteorScrollPos,
                ref disallowedMeteorListHeight,
                viewRect =>
                {
                    return DrawMeteorList(viewRect, disallowedMeteorCandidates);
                },
                true);
            usedH += windowH;
            return usedH;
        }
        private void DrawAddMeteorDropdownButton(
            Rect plusRect,
            List<MeteorCandidate> targetCandidates)
        {
            plusRect.x -= 4f; //additional padding for + button
            bool hasAny = allowedMeteorCandidates != null && allowedMeteorCandidates.Count > 0;
            using (new UIUtil.GUIStateScope(hasAny))
            {
                Widgets.Dropdown(
                    plusRect,
                    hasAny ? (object)allowedMeteorCandidates : null,
                    _ => 0, // dummy payload
                    _ => BuildMeteorDropdown(targetCandidates),
                    "");
            }
            Rect contentRect = plusRect.ContractedBy(4f);
            contentRect.y -= 1f;
            contentRect.x += 1f;

            UIUtil.DrawCenteredText(
                contentRect,
                "＋",
                TextAlignment.Center,
                font: GameFont.Medium
            );
        }

        private List<Widgets.DropdownMenuElement<int>> BuildMeteorDropdown(List<MeteorCandidate> targetCandidates)
        {
            var oldFont = Text.Font;
            Text.Font = GameFont.Small;
            var menu = new List<Widgets.DropdownMenuElement<int>>();

            if (allowedMeteorCandidates == null || allowedMeteorCandidates.Count == 0)
            {
                menu.Add(new Widgets.DropdownMenuElement<int>
                {
                    option = new FloatMenuOption("(none)", null),
                    payload = 0
                });
                return menu;
            }

            // Snapshot to avoid issues if we modify neutralCandidates after selection.
            // We store the key in a local var for the closure.
            foreach (var cand in allowedMeteorCandidates.ToList())
            {
                var captured = cand;
                string key = captured.key;
                string label = captured.label;

                menu.Add(new Widgets.DropdownMenuElement<int>
                {
                    option = new FloatMenuOption(label, () =>
                    {
                        TryAddMeteor(captured);
                    }),
                    payload = 0
                });
            }
            Text.Font = oldFont;

            return menu;
        }
        private float DrawMeteorList(Rect rect, List<MeteorCandidate> meteorCandidates)
        {
            if (meteorCandidates == null)
                return 0f;
            float margin = 8f;
            var listing = new Listing_Standard();
            float lineH = 26f;
            //float btnSize = 24f;

            listing.maxOneColumn = true;
            listing.Begin(rect.ContractedBy(margin));

            foreach (var cand in meteorCandidates.ToList()) //mutate list for safety
            {
                var captured = cand;
                Rect row = listing.GetRect(lineH);
                Widgets.DrawHighlightIfMouseover(row);
                UIUtil.SplitVerticallyByRatio(row, out Rect labelRect, out Rect deleteBtn, 0.5f, 0f);
                labelRect = UIUtil.ShrinkRect(labelRect, 4f);
                UIUtil.DrawCenteredText(labelRect, captured.label, TextAlignment.Left);
                Rect xRect = deleteBtn;
                xRect = UIUtil.ResizeRectAligned(xRect, lineH, lineH, TextAlignment.Right); // uses the aligned version we made
                xRect.x -= 4f; //additional padding for X button

                // Use an invisible button to render "×" ourselves (font/color control)
                if (Widgets.ButtonInvisible(xRect))
                {
                    TryRemoveMeteor(captured);
                    break;
                }
                Color xColor = Mouse.IsOver(xRect) ? Color.red : new Color(0.7f,0.7f,0.7f);
                UIUtil.DrawCenteredText(xRect, "×", TextAlignment.Center, font: GameFont.Medium, color: xColor);
            }
            listing.End();

            return listing.CurHeight + margin*2f;
        }

        public void UpdateMeteorList()
        {
            if (CheeseProtocolMod.MeteorCatalog != null)
            {
                MeteorApplier.BuildPools(
                    CheeseProtocolMod.MeteorCatalog,
                    disallowedMeteorKeys,
                    out List<MeteorCandidate> allowed,
                    out List<MeteorCandidate> disallowed
                );
                allowedMeteorCandidates = allowed;
                disallowedMeteorCandidates = disallowed;
            }
        }
        private bool TryAddMeteor(MeteorCandidate cand)
        {
            if (string.IsNullOrEmpty(cand.key)) return false;

            // already selected?
            if (disallowedMeteorKeys.Contains(cand.key))
                return false;

            RemoveByKey(allowedMeteorCandidates, cand.key);
            AddUniqueByKey(disallowedMeteorCandidates, cand);
            disallowedMeteorKeys.Add(cand.key);
            return true;
        }

        private bool TryRemoveMeteor(MeteorCandidate cand)
        {
            if (string.IsNullOrEmpty(cand.key)) return false;

            bool removed;
            removed = RemoveByKey(disallowedMeteorCandidates, cand.key);
            if (!removed) return false;
            disallowedMeteorKeys.Remove(cand.key);
            AddUniqueByKey(allowedMeteorCandidates, cand);
            return true;
        }
        private static bool RemoveByKey(List<MeteorCandidate> list, string key)
        {
            int idx = list.FindIndex(t => t.key == key);
            if (idx < 0) return false;
            list.RemoveAt(idx);
            return true;
        }

        private static void AddUniqueByKey(List<MeteorCandidate> list, MeteorCandidate cand)
        {
            if (list.Any(t => t.key == cand.key)) return;
            list.Add(cand);
        }
    }
}