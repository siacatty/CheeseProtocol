using Verse;
using UnityEngine;
namespace CheeseProtocol
{
    public struct MeteorCandidate
    {
        public ThingDef def;
        public string defName;
        public string key;
        public string label;
        public string yieldName;
        public string yieldLabel;
        public float marketValue;
        public bool preventMeteor;
        public float dropChance;
        public float commonality;
        public IntRange lumpSizeRange;
        public float score;
        public int scoreKey;
        public bool IsValid => !string.IsNullOrWhiteSpace(defName) || !string.IsNullOrWhiteSpace(key);

        public MeteorCandidate(
            ThingDef def,
            string defName,
            string label,
            string yieldName,
            string yieldLabel,
            float marketValue,
            bool preventMeteor,
            float dropChance,
            float commonality,
            IntRange lumpSizeRange
            )
        {
            this.def = def;
            this.defName = defName;
            this.key = MakeKey(def);
            this.label = label;
            this.yieldName = yieldName;
            this.yieldLabel = yieldLabel;
            this.marketValue = marketValue;
            this.preventMeteor = preventMeteor;
            this.dropChance = dropChance;
            this.commonality = commonality;
            this.lumpSizeRange = lumpSizeRange;
            float v = Mathf.Log(1f + Mathf.Max(0f, marketValue));
            float rarity = (commonality > 0f) ? -Mathf.Log(commonality) : 0f;
            float avgLump = (lumpSizeRange.min + lumpSizeRange.max) * 0.5f;
            float l = Mathf.Log(1f + Mathf.Max(0f, avgLump));

            this.score = 1.00f * v + 1.20f * rarity + 0.30f * l;
            this.scoreKey = Mathf.RoundToInt(score * 1000f);
        }
        public override string ToString()
        {
            return
                "MeteorCandidate " +
                $"def={defName}, " +
                $"key={key}, " +
                $"label={label}, " +
                $"yield={yieldName}({yieldLabel}), " +
                $"marketValue = {marketValue}," +
                $"preventMeteor={preventMeteor}, " +
                $"dropChance={dropChance:0.###}, " +
                $"commonality={commonality:0.###}, " +
                $"lumpSizeRange={lumpSizeRange.min}~{lumpSizeRange.max}";
        }
        private static string MakeKey(ThingDef def) => $"{def.defName}";
    }
}