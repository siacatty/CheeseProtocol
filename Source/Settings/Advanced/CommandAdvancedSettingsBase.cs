using Verse;
using UnityEngine;

namespace CheeseProtocol
{
    public abstract class CommandAdvancedSettingsBase : IExposable
    {
        public abstract CheeseCommand Command { get; }
        public abstract string Label { get; }
        public abstract void ExposeData();
        public abstract float Draw(Rect inRect);
        public abstract float DrawResults(Rect inRect);
        public abstract void ResetToDefaults();
    }
}