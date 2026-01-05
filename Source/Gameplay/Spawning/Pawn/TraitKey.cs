using System;
using Verse;

namespace CheeseProtocol
{
    public readonly struct TraitKey : IEquatable<TraitKey>
    {
        public readonly string defName;
        public readonly int degree; // degree 없는 trait도 0으로 통일

        public TraitKey(string defName, int degree)
        {
            this.defName = defName;
            this.degree = degree;
        }

        public override string ToString() => $"{defName}({degree})";

        public static bool TryParse(string s, out TraitKey key)
        {
            key = default;

            // "DefName(1)" 형식
            int open = s.LastIndexOf('(');
            int close = s.LastIndexOf(')');
            if (open <= 0 || close != s.Length - 1) return false;

            string name = s.Substring(0, open);
            string degStr = s.Substring(open + 1, close - open - 1);

            if (string.IsNullOrWhiteSpace(name)) return false;
            if (!int.TryParse(degStr, out int deg)) return false;

            key = new TraitKey(name, deg);
            return true;
        }

        public bool Equals(TraitKey other) => degree == other.degree && defName == other.defName;
        public override bool Equals(object obj) => obj is TraitKey other && Equals(other);
        public override int GetHashCode() => Gen.HashCombineInt(defName.GetHashCode(), degree);
    }
}