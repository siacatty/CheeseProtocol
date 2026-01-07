using System;
using Verse;

namespace CheeseProtocol
{
    public readonly struct MeteorKey : IEquatable<MeteorKey>
    {
        public readonly string defName;

        public MeteorKey(string defName)
        {
            this.defName = defName;
        }

        public override string ToString() => $"{defName}";

        public static bool TryParse(string s, out MeteorKey key)
        {
            key = default;

            if (string.IsNullOrWhiteSpace(s))
                return false;

            string defName = s.Trim();

            if (string.IsNullOrEmpty(defName))
                return false;

            key = new MeteorKey(defName);
            return true;
        }

        public bool Equals(MeteorKey other) => defName == other.defName;
        public override bool Equals(object obj) => obj is MeteorKey other && Equals(other);
        public override int GetHashCode() => defName.GetHashCode();
    }
}