using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using static CheeseProtocol.CheeseLog;

namespace CheeseProtocol
{
    [HarmonyPatch(typeof(Pawn_CarryTracker), "TryStartCarry", new[] { typeof(Thing), typeof(int), typeof(bool) })]
    internal static class Patch_CarryTracker_TryStartCarry3Args_Log
    {
        private static readonly AccessTools.FieldRef<Pawn_CarryTracker, Pawn> pawnRef
            = AccessTools.FieldRefAccess<Pawn_CarryTracker, Pawn>("pawn");

        static bool Prefix(Pawn_CarryTracker __instance, Thing item, int count, bool reserve, ref int __result)
        {   
            if (item is not Pawn victim) return true;
            if (!BullyTagger.IsBully(victim)) return true;

            Pawn carrier = pawnRef(__instance);
            if (carrier?.Faction != Faction.OfPlayer) return true;

            var job = carrier.CurJob;
            if (job == null) return true;

            // "이 victim을 운반하려는 job"일 때만 차단
            if (job.targetA.Thing != victim && job.targetB.Thing != victim && job.targetC.Thing != victim)
                return true;

            __result = 0;
            //carrier.jobs?.EndCurrentJob(JobCondition.Incompletable);
            string captureResistChat = LordChats.GetText(BullyTextKey.ResistCapture, carrier?.Name?.ToStringShort ?? " ");
            SpeechBubbleManager.Get(victim.Map)?.AddNPCChat(captureResistChat, victim);
            BullyStunQueue.QueueStun(carrier, 300, victim);
            //carrier.stances?.stunner?.StunFor(300, victim, addBattleLog: false); // 5seconds
            return false;
        }
    }
}