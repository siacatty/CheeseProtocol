using System.Collections.Generic;
using Verse;

namespace CheeseProtocol
{
    internal static class PawnVanishDebug
    {
        // Track only our generated pawns to avoid log spam
        private static readonly HashSet<int> tracked = new HashSet<int>();

        public static void Track(Pawn p)
        {
            if (p == null) return;
            tracked.Add(p.thingIDNumber);
        }

        public static bool IsTracked(Pawn p)
        {
            if (p == null) return false;
            return tracked.Contains(p.thingIDNumber);
        }

        public static void Untrack(Pawn p)
        {
            if (p == null) return;
            tracked.Remove(p.thingIDNumber);
        }
    }
}