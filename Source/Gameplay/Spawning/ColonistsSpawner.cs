using System;
using RimWorld;
using Verse;

namespace CheeseProtocol
{
    internal static class ColonistSpawner
    {
        public static void Spawn(string donorName, int amount, string message)
        {
            Map map = Find.CurrentMap;
            if (map == null) return;

            // Generate a player colonist
            var req = new PawnGenerationRequest(
                PawnKindDefOf.Colonist,
                Faction.OfPlayer,
                PawnGenerationContext.PlayerStarter,
                map.Tile,
                forceGenerateNewPawn: true
            );

            Pawn pawn = PawnGenerator.GeneratePawn(req);

            // Set name to donor
            if (!string.IsNullOrWhiteSpace(donorName))
                pawn.Name = new NameSingle(TrimName(donorName));

            // Spawn via drop pod or place nearby
            if (CheeseProtocolMod.Settings?.useDropPod ?? true)
            {
                IntVec3 dropSpot = DropCellFinder.TradeDropSpot(map);
                DropPodUtility.DropThingsNear(dropSpot, map, new Thing[] { pawn }, 110, canInstaDropDuringInit: false, leaveSlag: false);
            }
            else
            {
                IntVec3 spot = CellFinderLoose.RandomCellWith(
                    c => c.Standable(map) && !c.Fogged(map),
                    map, 200);

                GenSpawn.Spawn(pawn, spot, map);
            }

            Messages.Message(
                $"[CheeseProtocol] New colonist joined: {pawn.LabelShort} (₩{amount})",
                MessageTypeDefOf.PositiveEvent,
                false
            );
        }

        private static string TrimName(string s)
        {
            s = s.Trim();
            if (s.Length > 24) s = s.Substring(0, 24);
            return s;
        }
    }
}
