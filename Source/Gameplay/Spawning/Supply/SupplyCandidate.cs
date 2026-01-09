using Verse;
using RimWorld;
using UnityEngine;

namespace CheeseProtocol
{
    public struct SupplyCandidate
    {
        public SupplyType type;

        public ThingDef def;
        public string defName;
        public string key;
        public string label;
        public int stackLimit;
        public TechLevel techLevel;
        public float marketValue;

        // 후보 선택 가중치 (weighted random)
        //public float weight;

        // 후보 사용 금지 플래그
        public bool disabled;

        public bool IsValid =>
            def != null &&
            !string.IsNullOrWhiteSpace(defName);
        public SupplyCandidate(
            SupplyType type,
            ThingDef def,
            bool disabled = false)
        {
            this.type = type;
            this.def = def;
            defName = def?.defName?? "";
            key = MakeKey(type, def);
            label = def?.label?? "";
            stackLimit = def?.stackLimit?? 0;
            techLevel = def?.techLevel?? RimWorld.TechLevel.Undefined;
            marketValue = def?.BaseMarketValue?? 0f;
            this.disabled = disabled;
        }

        public override string ToString()
        {
            return
                "SupplyCandidate " +
                $"type={type}, " +
                $"def={defName}, " +
                $"key={key}, " +
                $"label={label}, " +
                $"stackLimit={stackLimit}, " +
                $"tech={techLevel}, " +
                $"marketValue={marketValue:0.##}, " +
                $"disabled={disabled}";
        }

        private static string MakeKey(SupplyType type, ThingDef def)
        {
            string d = def != null ? def.defName : "null";
            return $"{type}:{d}";
        }
    }
}