using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace CheeseProtocol
{
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentJob))]
    internal static class Patch_JobTracker_EndCurrentJob_BlockSearchAfterBullyCaptureFail
    {
        private static readonly AccessTools.FieldRef<Pawn_JobTracker, Pawn> pawnRef = AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");
        static void Prefix(Pawn_JobTracker __instance, JobCondition condition)
        {
            Pawn pawn = pawnRef(__instance);
            var job = pawn?.jobs?.curJob;
            if (pawn == null || job == null) return;

            // 플레이어만
            if (pawn.Faction != Faction.OfPlayer) return;

            // Capture/Arrest만
            if (job.def != JobDefOf.Capture && job.def != JobDefOf.Arrest) return;

            // targetA가 bully인지
            var victim = job.targetA.Thing as Pawn;
            if (victim == null || !BullyTagger.IsBully(victim)) return;

            // 실패/중단 류로 끝날 때만 block
            // (Succeeded까지 막을 필요는 보통 없음)
            if (condition == JobCondition.Incompletable || condition == JobCondition.InterruptForced || condition == JobCondition.Errored)
            {
                BullyControl_MapComp.Get(pawn.Map)?.BlockJobSearch(pawn, ticks: 2);
            }
        }
    }
}