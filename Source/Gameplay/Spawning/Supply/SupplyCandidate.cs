using Verse;
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

        public float marketValue;

        // 후보 선택 가중치 (weighted random)
        public float weight;

        // 이 후보가 뽑히면 한 번에 생성될 스택 범위
        public IntRange stackSizeRange;

        // 후보 사용 금지 플래그
        public bool disabled;

        public bool IsValid =>
            def != null &&
            !string.IsNullOrWhiteSpace(defName) &&
            weight > 0f;

        public SupplyCandidate(
            SupplyType type,
            ThingDef def,
            float weight,
            IntRange stackSizeRange,
            bool disabled = false)
        {
            this.type = type;

            this.def = def;
            this.defName = def?.defName ?? "";
            this.key = MakeKey(type, def);
            this.label = def?.label ?? "";

            this.marketValue = def != null ? def.BaseMarketValue : 0f;

            this.weight = weight;
            this.stackSizeRange = stackSizeRange;
            this.disabled = disabled;
        }

        public override string ToString()
        {
            return
                "[CheeseProtocol] SupplyCandidate " +
                $"type={type}, " +
                $"def={defName}, " +
                $"key={key}, " +
                $"label={label}, " +
                $"marketValue={marketValue:0.###}, " +
                $"weight={weight:0.###}, " +
                $"stackSizeRange={stackSizeRange.min}~{stackSizeRange.max}, " +
                $"disabled={disabled}";
        }

        private static string MakeKey(SupplyType type, ThingDef def)
        {
            string d = def != null ? def.defName : "null";
            return $"{type}:{d}";
        }
    }
}