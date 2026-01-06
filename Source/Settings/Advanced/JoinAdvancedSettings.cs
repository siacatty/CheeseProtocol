using System;
using System.Collections.Generic;
using Verse;
using UnityEngine;
using System.Linq;

using RimWorld;

namespace CheeseProtocol
{
    public class JoinAdvancedSettings : CommandAdvancedSettingsBase
    {
        public QualityRange ageRange;
        public QualityRange passionRange;
        public QualityRange traitsRange;
        public QualityRange skillRange;
        public QualityRange healthRange;
        public QualityRange apparelRange;
        public QualityRange weaponRange;
        public bool forcePlayerIdeo;
        public bool forceHuman;
        public bool allowWorkDisable;
        public bool useDropPod;
        public List<string> negativeTraitKeys;
        public List<string> positiveTraitKeys;
        private Vector2 blackTraitScrollPos = Vector2.zero;
        private Vector2 whiteTraitScrollPos = Vector2.zero;
        private float blackListHeight = 400f;
        private float whiteListHeight = 400f;
        public List<TraitCandidate> positiveCandidates;
        public List<TraitCandidate> neutralCandidates;
        public List<TraitCandidate> negativeCandidates;
        public override CheeseCommand Command => CheeseCommand.Join;
        public override string Label => "!참여";
        private const float lineH = 26f;
        public JoinAdvancedSettings()
        {
            ResetToDefaults();
            InitializeAll();
        }
        public override void ExposeData()
        {
            // Scribe_Values.Look(...)들
            LookRange(ref ageRange, "ageRange", CheeseDefaults.AgeRange);
            LookRange(ref passionRange, "passionRange", CheeseDefaults.PassionRange);
            LookRange(ref traitsRange, "traitsRange", CheeseDefaults.TraitsRange);
            LookRange(ref skillRange, "skillLevelRange", CheeseDefaults.SkillRange);
            LookRange(ref healthRange, "healthRange", CheeseDefaults.HealthRange);
            LookRange(ref apparelRange, "apparelQualityRange", CheeseDefaults.ApparelRange);
            LookRange(ref weaponRange, "weaponQualityRange", CheeseDefaults.WeaponRange);
            
            Scribe_Values.Look(ref allowWorkDisable, "allowWorkDisable", CheeseDefaults.AllowWorkDisable);
            Scribe_Values.Look(ref forcePlayerIdeo, "forcePlayerIdeo", CheeseDefaults.ForcePlayerIdeo);
            Scribe_Values.Look(ref forceHuman, "forceHuman", CheeseDefaults.ForceHuman);
            Scribe_Values.Look(ref useDropPod, "useDropPod", CheeseDefaults.UseDropPod);
            Scribe_Collections.Look(ref negativeTraitKeys, "negativeTraitKeys", LookMode.Value);
            Scribe_Collections.Look(ref positiveTraitKeys, "positiveTraitKeys", LookMode.Value);

            if (negativeTraitKeys == null)
                negativeTraitKeys = new List<string>(CheeseDefaults.NegativeTraitKeys);
            if (positiveTraitKeys == null)
                positiveTraitKeys = new List<string>(CheeseDefaults.PositiveTraitKeys);

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
            forcePlayerIdeo = CheeseDefaults.ForcePlayerIdeo;
            forceHuman = CheeseDefaults.ForceHuman;
            allowWorkDisable = CheeseDefaults.AllowWorkDisable;
            useDropPod = CheeseDefaults.UseDropPod;
            negativeTraitKeys = new List<string>(CheeseDefaults.NegativeTraitKeys);
            positiveTraitKeys = new List<string>(CheeseDefaults.PositiveTraitKeys);
            if (CheeseProtocolMod.TraitCatalog != null)
            {
                UpdateTraitList();
            }
            else
            {
                LongEventHandler.ExecuteWhenFinished(UpdateTraitList);
            }
            ResetLeverRangesToDefaults();
        }

        public void UpdateTraitList()
        {
            if (CheeseProtocolMod.TraitCatalog != null)
            {
                TraitApplier.BuildPools(
                    CheeseProtocolMod.TraitCatalog,
                    positiveTraitKeys,
                    negativeTraitKeys,
                    out List<TraitCandidate> pos,
                    out List<TraitCandidate> neu,
                    out List<TraitCandidate> neg
                );
                positiveCandidates = pos;
                neutralCandidates = neu;
                negativeCandidates = neg;
            }
        }

        private void ResetLeverRangesToDefaults()
        {
            ageRange = CheeseDefaults.AgeRange;
            passionRange = CheeseDefaults.PassionRange;
            traitsRange = CheeseDefaults.TraitsRange;
            skillRange = CheeseDefaults.SkillRange;
            healthRange = CheeseDefaults.HealthRange;
            apparelRange = CheeseDefaults.ApparelRange;
            weaponRange = CheeseDefaults.WeaponRange;
        }

        private void InitializeAll()
        {
            ageRange = QualityRange.init(ageRange.qMin, ageRange.qMax);
            passionRange = QualityRange.init(passionRange.qMin, passionRange.qMax);
            traitsRange = QualityRange.init(traitsRange.qMin, traitsRange.qMax);
            skillRange = QualityRange.init(skillRange.qMin, skillRange.qMax);
            healthRange = QualityRange.init(healthRange.qMin, healthRange.qMax);
            apparelRange = QualityRange.init(apparelRange.qMin, apparelRange.qMax);
            weaponRange = QualityRange.init(weaponRange.qMin, weaponRange.qMax);
            negativeTraitKeys = negativeTraitKeys.Distinct().ToList();
            positiveTraitKeys = positiveTraitKeys.Distinct().ToList();
            UpdateTraitList();
        }
        public override float Draw(Rect rect)
        {
            float curY = rect.y;
            float usedH = 0;
            float checkboxPaddingY = 6f;
            float rowH = lineH+checkboxPaddingY;
            UIUtil.RowWithHighlight(rect, ref curY, rowH, r =>{Widgets.CheckboxLabeled(r, "결격사항 허용", ref allowWorkDisable);});
            UIUtil.RowWithHighlight(rect, ref curY, rowH, r =>{Widgets.CheckboxLabeled(r, "플레이어 이념 강제", ref forcePlayerIdeo);});
            UIUtil.RowWithHighlight(rect, ref curY, rowH, r =>{Widgets.CheckboxLabeled(r, "인간 종족만 허용", ref forceHuman);});
            UIUtil.RowWithHighlight(rect, ref curY, rowH, r =>{Widgets.CheckboxLabeled(r, "합류 시 수송포드 사용", ref useDropPod);});
            UIUtil.RangeSliderWrapper(rect, ref curY, lineH, "스킬 레벨", ref skillRange, baseMin: GameplayConstants.SkillLevelMin, baseMax: GameplayConstants.SkillLevelMax, roundTo: 1f);
            UIUtil.RangeSliderWrapper(rect, ref curY, lineH, "열정 개수", ref passionRange, baseMin: GameplayConstants.PassionMin, baseMax: GameplayConstants.PassionMax, roundTo: 1f);
            UIUtil.RangeSliderWrapper(rect, ref curY, lineH, "특성 퀄리티", ref traitsRange, isPercentile: true);
            UIUtil.RangeSliderWrapper(rect, ref curY, lineH, "나이", ref ageRange, baseMin: GameplayConstants.AgeMin, baseMax: GameplayConstants.AgeMax, roundTo: 1f);
            UIUtil.RangeSliderWrapper(rect, ref curY, lineH, "건강", ref healthRange, isPercentile: true);
            //TODO: add apparel and weapon range as well
            //RangeSlider(rect, ref curY, lineH, "의상 퀄리티", ref apparelRange, isPercentile: true);
            //RangeSlider(rect, ref curY, lineH, "무기 퀄리티", ref weaponRange, isPercentile: true);
            usedH = curY - rect.y;
            return usedH;
        }
        public float DrawTraitSetting(Rect rect, string title, float lineH, float paddingX, float paddingY)
        {
            float usedH = 0f;
            float traitWindowH = 220f;
            float topBarH = 34f;
            float btnSize = 24f;
            Rect traitRect = new Rect(rect.x, rect.y, rect.width, traitWindowH);
            UIUtil.SplitVerticallyByRatio(traitRect, out Rect blackTraitRect, out Rect whiteTraitRect, 0.5f, paddingX);
            UIUtil.SplitHorizontallyByHeight(blackTraitRect, out Rect blackTopRect, out Rect blackListRect, topBarH, 0f);
            UIUtil.SplitHorizontallyByHeight(whiteTraitRect, out Rect whiteTopRect, out Rect whiteListRect, topBarH, 0f);
            UIUtil.SplitVerticallyByRatio(blackTopRect, out Rect blackTopLabel, out Rect blackAddBtn, 0.7f, 0f);
            UIUtil.SplitVerticallyByRatio(whiteTopRect, out Rect whiteTopLabel, out Rect whiteAddBtn, 0.7f, 0f);
            blackTopLabel = UIUtil.ShrinkRect(blackTopLabel, 6f);
            whiteTopLabel = UIUtil.ShrinkRect(whiteTopLabel, 6f);
            UIUtil.DrawCenteredText(blackTopLabel, "비선호 특성", align: TextAlignment.Left);
            UIUtil.DrawCenteredText(whiteTopLabel, "선호 특성", align: TextAlignment.Left);
            blackAddBtn = UIUtil.ResizeRectAligned(blackAddBtn, btnSize, btnSize, TextAlignment.Right);
            whiteAddBtn = UIUtil.ResizeRectAligned(whiteAddBtn, btnSize, btnSize, TextAlignment.Right);
            Widgets.DrawHighlightIfMouseover(blackAddBtn);
            Widgets.DrawHighlightIfMouseover(whiteAddBtn);
            DrawAddTraitDropdownButton(blackAddBtn, negativeCandidates, TraitPolarity.Negative);
            DrawAddTraitDropdownButton(whiteAddBtn, positiveCandidates, TraitPolarity.Positive);
            UIUtil.AutoScrollView(
                blackListRect,
                ref blackTraitScrollPos,
                ref blackListHeight,
                viewRect =>
                {
                    return DrawTraitList(viewRect, negativeCandidates, TraitPolarity.Negative);
                },
                true);
            UIUtil.AutoScrollView(
                whiteListRect,
                ref whiteTraitScrollPos,
                ref whiteListHeight,
                viewRect =>
                {
                    return DrawTraitList(viewRect, positiveCandidates, TraitPolarity.Positive);
                },
                true);
            
            Widgets.DrawBox(blackTopRect);
            Widgets.DrawBox(whiteTopRect);
            Widgets.DrawBox(blackListRect);
            Widgets.DrawBox(whiteListRect);
            usedH += traitWindowH;
            return usedH;
        }

        private void DrawAddTraitDropdownButton(
            Rect plusRect,
            List<TraitCandidate> targetCandidates,
            TraitPolarity traitPolarity)
        {
            plusRect.x -= 4f; //additional padding for + button
            bool hasAny = neutralCandidates != null && neutralCandidates.Count > 0;
            using (new UIUtil.GUIStateScope(hasAny))
            {
                Widgets.Dropdown(
                    plusRect,
                    hasAny ? (object)neutralCandidates : null,
                    _ => 0, // dummy payload
                    _ => BuildTraitDropdown(targetCandidates, traitPolarity),
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
            //UIUtil.DrawCenteredText(plusRect, "＋", font: GameFont.Medium);
        }

        private List<Widgets.DropdownMenuElement<int>> BuildTraitDropdown(List<TraitCandidate> targetCandidates, TraitPolarity traitPolarity)
        {
            var oldFont = Text.Font;
            Text.Font = GameFont.Small;
            var menu = new List<Widgets.DropdownMenuElement<int>>();

            if (neutralCandidates == null || neutralCandidates.Count == 0)
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
            foreach (var cand in neutralCandidates.ToList())
            {
                var captured = cand;
                string key = captured.key;
                string label = captured.label;

                menu.Add(new Widgets.DropdownMenuElement<int>
                {
                    option = new FloatMenuOption(label, () =>
                    {
                        TryAddTrait(traitPolarity, captured);
                    }),
                    payload = 0
                });
            }
            Text.Font = oldFont;

            return menu;
        }

        private float DrawTraitList(Rect rect, List<TraitCandidate> traitCandidates, TraitPolarity traitPolarity)
        {
            if (traitCandidates == null)
                return 0f;
            float margin = 8f;
            var listing = new Listing_Standard();
            float lineH = 26f;
            //float btnSize = 24f;

            listing.maxOneColumn = true;
            listing.Begin(rect.ContractedBy(margin));

            foreach (var cand in traitCandidates.ToList()) //mutate list for safety
            {
                var captured = cand;
                Rect row = listing.GetRect(lineH);
                Widgets.DrawHighlightIfMouseover(row);
                UIUtil.SplitVerticallyByRatio(row, out Rect traitLabel, out Rect deleteBtn, 0.5f, 0f);
                traitLabel = UIUtil.ShrinkRect(traitLabel, 4f);
                UIUtil.DrawCenteredText(traitLabel, captured.label, TextAlignment.Left);
                Rect xRect = deleteBtn;
                xRect = UIUtil.ResizeRectAligned(xRect, lineH, lineH, TextAlignment.Right); // uses the aligned version we made
                xRect.x -= 4f; //additional padding for X button

                // Use an invisible button to render "×" ourselves (font/color control)
                if (Widgets.ButtonInvisible(xRect))
                {
                    TryRemoveTrait(traitPolarity, captured);
                    break;
                }
                Color xColor = Mouse.IsOver(xRect) ? Color.red : new Color(0.7f,0.7f,0.7f);
                UIUtil.DrawCenteredText(xRect, "×", TextAlignment.Center, font: GameFont.Medium, color: xColor);
            }
            listing.End();

            return listing.CurHeight + margin*2f;
        }

        public enum TraitPolarity
        {
            Positive,
            Negative
        }
        private bool TryAddTrait(TraitPolarity polarity, TraitCandidate cand)
        {
            if (string.IsNullOrEmpty(cand.key)) return false;

            // already selected?
            if (positiveTraitKeys.Contains(cand.key) || negativeTraitKeys.Contains(cand.key))
                return false;

            RemoveByKey(neutralCandidates, cand.key);

            if (polarity == TraitPolarity.Positive)
            {
                AddUniqueByKey(positiveCandidates, cand);
                positiveTraitKeys.Add(cand.key);
            }
            else // Negative
            {
                AddUniqueByKey(negativeCandidates, cand);
                negativeTraitKeys.Add(cand.key);
            }

            return true;
        }

        private bool TryRemoveTrait(TraitPolarity polarity, TraitCandidate cand)
        {
            if (string.IsNullOrEmpty(cand.key)) return false;

            bool removed;
            if (polarity == TraitPolarity.Positive)
            {
                removed = RemoveByKey(positiveCandidates, cand.key);
                if (!removed) return false;
                positiveTraitKeys.Remove(cand.key);
            }
            else
            {
                removed = RemoveByKey(negativeCandidates, cand.key);
                if (!removed) return false;
                negativeTraitKeys.Remove(cand.key);
            }

            AddUniqueByKey(neutralCandidates, cand);
            return true;
        }
        private static bool RemoveByKey(List<TraitCandidate> list, string key)
        {
            int idx = list.FindIndex(t => t.key == key);
            if (idx < 0) return false;
            list.RemoveAt(idx);
            return true;
        }

        private static void AddUniqueByKey(List<TraitCandidate> list, TraitCandidate cand)
        {
            if (list.Any(t => t.key == cand.key)) return;
            list.Add(cand);
        }
    }
}