using Verse;

namespace CheeseProtocol
{
    internal static class BullyStunQueue
    {
        public static void QueueStun(Pawn pawn, int stunTicks, Thing instigator = null, int delayTicks = 2,
            bool addBattleLog = true, bool showMote = true, bool disableRotation = false)
        {
            if (pawn?.Map == null) return;

            var comp = pawn.Map.GetComponent<BullyControl_MapComp>();
            comp?.EnqueueStun(pawn, delayTicks, stunTicks, instigator, addBattleLog, showMote, disableRotation);
        }
    }
}