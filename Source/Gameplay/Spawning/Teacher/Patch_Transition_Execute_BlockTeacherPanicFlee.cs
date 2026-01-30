using HarmonyLib;
using Verse;
using Verse.AI.Group;

namespace CheeseProtocol
{
    [HarmonyPatch(typeof(Transition), "Execute")]
    internal static class Patch_Transition_Execute_BlockTeacherPanicFlee
    {
        private static readonly AccessTools.FieldRef<Transition, LordToil> targetRef
            = AccessTools.FieldRefAccess<Transition, LordToil>("target");

        static bool Prefix(Transition __instance, Lord lord)
        {
            if (lord?.LordJob is not LordJob_Teacher) return true;
            if (__instance == null) return true;

            LordToil target = null;
            try { target = targetRef(__instance); }
            catch { return true; } // if field name differs in some version, just don't interfere

            if (target != null && target.GetType().FullName == "RimWorld.LordToil_PanicFlee")
            {
                return false;
            }

            return true;
        }
    }
}