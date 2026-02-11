using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;
using static CheeseProtocol.CheeseLog;

namespace CheeseProtocol
{
    public static class LessonPosUtility
    {

        public static Thing FindWallToBreakNearTarget(Pawn teacher, Map map, IntVec3 dest, int radius = 12, bool breakWallOnly = true)
        {
            Thing best = null;
            int bestDist = int.MaxValue;

            foreach (IntVec3 c in GenRadial.RadialCellsAround(dest, radius, useCenter: true))
            {
                if (!c.InBounds(map)) continue;
                Thing ed = c.GetEdifice(map);
                if (breakWallOnly && !IsArtificialWall(ed)) continue;
                if (!teacher.CanReach(ed, PathEndMode.Touch, Danger.Some)) continue;
                
                int d = c.DistanceToSquared(dest);
                if (d < bestDist) { bestDist = d; best = ed; }
            }
            return best;
        }

        private static bool IsArtificialWall(Thing t)
        {
            if (t == null) return false;
            if (t is Building_Door) return false;
            if (t is Building b)
            {
                if (b.def.passability != Traversability.Impassable) return false;
                if (b.def.building != null && b.def.building.isNaturalRock) return false;
                // Filters out rock walls; keeps constructed walls.
                return b.def.IsBuildingArtificial;
            }
            return false;
        }

        public static bool InGatheringArea(IntVec3 cell, IntVec3 partySpot, Map map, float dist=15f, int maxRoomCell=140)
        {
            if (!cell.InHorDistOf(partySpot, dist))
                return false;
            
            if (UseWholeRoomAsGatheringArea(partySpot, map, maxRoomCell))
            {
                return cell.GetRoom(map) == partySpot.GetRoom(map);
            }
            if (cell.InHorDistOf(partySpot, dist))
            {
                Building edifice = cell.GetEdifice(map);
                TraverseParms traverseParams = TraverseParms.For(TraverseMode.NoPassClosedDoors, Danger.None);
                if (edifice != null)
                {
                    if (map.reachability.CanReach(partySpot, edifice, PathEndMode.ClosestTouch, traverseParams))
                    {
                        return !NeedsToPassDoor();
                    }

                    return false;
                }

                if (map.reachability.CanReach(partySpot, cell, PathEndMode.ClosestTouch, traverseParams))
                {
                    return !NeedsToPassDoor();
                }

                return false;
            }

            return false;
            bool NeedsToPassDoor()
            {
                return cell.GetRoom(map) != partySpot.GetRoom(map);
            }
        }

        public static bool UseWholeRoomAsGatheringArea(IntVec3 partySpot, Map map, int maxRoomCell)
        {
            Room room = partySpot.GetRoom(map);
            if (room != null && !room.IsHuge && room.CellCount <= maxRoomCell && !room.PsychologicallyOutdoors)
            {
                return true;
            }

            return false;
        }
    }
}