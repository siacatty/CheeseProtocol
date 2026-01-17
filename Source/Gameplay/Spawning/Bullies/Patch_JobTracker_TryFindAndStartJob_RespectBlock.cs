using HarmonyLib;
using Verse;
using Verse.AI;

namespace CheeseProtocol
{
    [HarmonyPatch(typeof(Pawn_JobTracker), "TryFindAndStartJob")]
    internal static class Patch_JobTracker_TryFindAndStartJob_RespectBlock
    {
        private static readonly AccessTools.FieldRef<Pawn_JobTracker, Pawn> pawnRef = AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");
        static bool Prefix(Pawn_JobTracker __instance)
        {
            Pawn pawn = pawnRef(__instance);
            if (pawn?.Map == null) return true;

            if (BullyControl_MapComp.Get(pawn.Map)?.IsJobSearchBlocked(pawn) == true)
                return false; // 이번 틱에는 새 job 시작 안 함

            return true;
        }
    }
}