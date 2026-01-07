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
        public override CheeseCommand Command => CheeseCommand.None;
        public override string Label => "!??";
        private const float lineH = 26f;
        public ThrumboAdvancedSettings()
        {
            ResetToDefaults();
            InitializeAll();
        }
        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                InitializeAll();
            }
        }
        public override void ResetToDefaults()
        {
            ResetLeverRangesToDefaults();
        }
        private void ResetLeverRangesToDefaults()
        {
            //ageRange = CheeseDefaults.AgeRange;
        }
        private void InitializeAll()
        {
            //ageRange = QualityRange.init(ageRange.qMin, ageRange.qMax);
        }
        public override float Draw(Rect rect)
        {
            return 0f;
        }
    }
}