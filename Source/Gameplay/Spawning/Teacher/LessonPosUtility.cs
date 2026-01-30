using Verse;
using Verse.AI;


namespace CheeseProtocol
{
    public static class LessonPosUtility
    {
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