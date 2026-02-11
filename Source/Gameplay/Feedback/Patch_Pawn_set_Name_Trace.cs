using HarmonyLib;
using RimWorld;
using Verse;
using System;
using System.Diagnostics;
using System.Text;

namespace CheeseProtocol
{
    [HarmonyPatch(typeof(Pawn), "set_Name")]
    public static class Patch_Pawn_set_Name_BlockHAROnly
    {
        public static bool Prefix(Pawn __instance, Name value)
        {
            if (__instance == null) return true;
            if (!CheeseParticipantRegistry.Get().IsParticipant(__instance)) return true;

            // HAR 렌더링 경로에서만 차단
            var st = new System.Diagnostics.StackTrace(skipFrames: 1, fNeedFileInfo: false);
            foreach (var f in st.GetFrames())
            {
                var m = f.GetMethod();
                var t = m?.DeclaringType;
                var tn = t?.FullName;
                if (tn == null) continue;

                if (tn.StartsWith("AlienRace.") &&
                    (tn.Contains("AlienRenderTreePatches") || tn.Contains("AlienRenderTree")))
                {
                    return false;
                }
            }
            return true;
        }
    }
}