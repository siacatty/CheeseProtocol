using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace CheeseProtocol
{
    [HarmonyPatch(typeof(IncidentWorker_WandererJoin), "CanFireNowSub")]
    internal static class Patch_BlockStrangerInBlackJoin_WhileTeacher
    {
        static void Postfix(IncidentWorker_WandererJoin __instance, IncidentParms parms, ref bool __result)
        {
            if (!__result) return;

            if (__instance?.def?.defName != "StrangerInBlackJoin") return;
            Map map = parms.target as Map;
            if (IsTeacherCatchStudentsActive(map))
            {
                __result = false;
                //Warn("[CheeseProtocol] Blocked StrangerInBlackJoin because Teacher lord is active.");
            }
        }
        internal static bool IsTeacherCatchStudentsActive(Map map)
        {
            if (map?.lordManager?.lords == null) return false;

            var lords = map.lordManager.lords;
            for (int i = 0; i < lords.Count; i++)
            {
                Lord lord = lords[i];
                if (lord?.LordJob is not LordJob_Teacher) continue;

                var curToil = lord.CurLordToil;
                if (curToil is LordToil_Teacher_CatchStudents)
                    return true;
            }
            return false;
        }
    }
}