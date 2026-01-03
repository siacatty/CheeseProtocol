using System;
using System.Collections.Generic;
using Verse;
using UnityEngine;
using RimWorld;

namespace CheeseProtocol
{
    public class JoinAdvancedSettings : IExposable
    {
        public QualityRange passionRange;
        public QualityRange traitsRange;
        public QualityRange skillRange;
        public QualityRange workDisableRange;
        public QualityRange healthRange;
        public QualityRange apparelRange;
        public QualityRange weaponRange;
        public List<TraitDef> preferredTraits;
        public float preferredTraitWeight;
        public bool forcePlayerIdeo;
        public bool forceHuman;
        public JoinAdvancedSettings()
        {
            ResetToDefaults();
            NormalizeAll();
        }
        public void ExposeData()
        {
            // Scribe_Values.Look(...)들
            LookRange(ref passionRange, "passionRange", CheeseDefaults.PassionRange);
            LookRange(ref traitsRange, "traitsRange", CheeseDefaults.TraitsRange);
            LookRange(ref skillRange, "skillLevelRange", CheeseDefaults.SkillRange);
            LookRange(ref workDisableRange, "workDisablesRange", CheeseDefaults.WorkDisableRange);
            LookRange(ref healthRange, "healthRange", CheeseDefaults.HealthRange);
            LookRange(ref apparelRange, "apparelQualityRange", CheeseDefaults.ApparelRange);
            LookRange(ref weaponRange, "weaponQualityRange", CheeseDefaults.WeaponRange);
            
            Scribe_Collections.Look(ref preferredTraits, "preferredTraits", LookMode.Def);
            Scribe_Values.Look(ref preferredTraitWeight, "preferredTraitWeight", CheeseDefaults.PreferredTraitWeight);

            Scribe_Values.Look(ref forcePlayerIdeo, "forcePlayerIdeo", true);
            Scribe_Values.Look(ref forceHuman, "forceHuman", true);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                preferredTraits ??= CheeseDefaults.CreatePreferredTraitsDefault();
                SanitizePreferredTraits();
                NormalizeAll();
            }
        }

        private void LookRange(ref QualityRange range, string baseKey, QualityRange defaultRange)
        {
            float min = range.qMin;
            float max = range.qMax;
            Scribe_Values.Look(ref min, baseKey + "_min", defaultRange.qMin);
            Scribe_Values.Look(ref max, baseKey + "_max", defaultRange.qMax);
        }
        private void ResetToDefaults()
        {
            preferredTraits = CheeseDefaults.CreatePreferredTraitsDefault();
            preferredTraitWeight = CheeseDefaults.PreferredTraitWeight;
            forcePlayerIdeo = CheeseDefaults.ForcePlayerIdeo;
            forceHuman = CheeseDefaults.ForceHuman;
            ResetLeverRangesToDefaults();
        }
        private void ResetLeverRangesToDefaults()
        {
            passionRange = CheeseDefaults.PassionRange;
            traitsRange = CheeseDefaults.TraitsRange;
            skillRange = CheeseDefaults.SkillRange;
            workDisableRange = CheeseDefaults.WorkDisableRange;
            healthRange = CheeseDefaults.HealthRange;
            apparelRange = CheeseDefaults.ApparelRange;
            weaponRange = CheeseDefaults.WeaponRange;
        }

        private void NormalizeAll()
        {
            preferredTraitWeight = Mathf.Clamp01(preferredTraitWeight);
            passionRange = QualityRange.Normalized(passionRange.qMin, passionRange.qMax);
            traitsRange = QualityRange.Normalized(traitsRange.qMin, traitsRange.qMax);
            skillRange = QualityRange.Normalized(skillRange.qMin, skillRange.qMax);
            workDisableRange = QualityRange.Normalized(workDisableRange.qMin, workDisableRange.qMax);
            healthRange = QualityRange.Normalized(healthRange.qMin, healthRange.qMax);
            apparelRange = QualityRange.Normalized(apparelRange.qMin, apparelRange.qMax);
            weaponRange = QualityRange.Normalized(weaponRange.qMin, weaponRange.qMax);
        }
        private void SanitizePreferredTraits()
        {
            if (preferredTraits == null) return;

            var seen = new HashSet<TraitDef>();
            for (int i = preferredTraits.Count - 1; i >= 0; i--)
            {
                TraitDef t = preferredTraits[i];
                if (t == null || !seen.Add(t))
                    preferredTraits.RemoveAt(i);
            }
        }
    }
}