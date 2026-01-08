using Verse;
using RimWorld;
using UnityEngine;

namespace CheeseProtocol
{
    public struct TameCandidate
    {
        public PawnKindDef def;
        public string defName;
        public string key;
        public string label;
        public float marketValue;
        public float wildness;
        public float combatPower;
        public float bodySize;
        public bool isHerdable;
        public string train;

        // 후보 사용 금지 플래그
        public bool disabled;

        public bool IsValid =>
            def != null &&
            !string.IsNullOrWhiteSpace(defName);
        public TameCandidate(
            PawnKindDef def,
            string label,
            float marketValue,
            float wildness,
            float combatPower,
            float bodySize,
            bool isHerdable,
            string train,
            bool disabled = false)
        {
            this.def = def;
            defName = def?.defName?? "";
            this.label = label;
            this.marketValue = marketValue;
            key = MakeKey(def, marketValue);
            this.wildness = wildness;
            this.combatPower = combatPower;
            this.bodySize = bodySize;
            this.isHerdable = isHerdable;
            this.train = train;
            this.disabled = disabled;
        }

        public override string ToString()
        {
            return
                $"def={defName} " +
                $"label={label} " +
                $"mv={marketValue:0.##} " +
                $"wild={wildness:0.###} " +
                $"cp={combatPower:0.##} " +
                $"body={bodySize:0.###} " +
                $"herd={isHerdable} " +
                $"train={train} " +
                $"disabled={disabled}";
        }

        private static string MakeKey(PawnKindDef def, float marketValue)
        {
            string d = def != null ? def.defName : "null";
            return $"{d}:{marketValue}";
        }
    }
}