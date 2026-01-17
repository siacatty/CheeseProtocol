// using System;
// using System.Diagnostics;
// using HarmonyLib;
// using Verse;
// using RimWorld;

// namespace CheeseProtocol
// {
//     [HarmonyPatch(typeof(Thing), nameof(Thing.Destroy))]
//     internal static class Patch_Thing_Destroy_LogVanish
//     {
//         static void Prefix(Thing __instance, DestroyMode mode)
//         {
//             if (mode != DestroyMode.Vanish) return;
//             if (!(__instance is Pawn p)) return;
//             if (!PawnVanishDebug.IsTracked(p)) return;

//             try
//             {
//                 Log.Warning(
//                     $"[CheeseProtocol][VANISH] Pawn is vanishing! " +
//                     $"id={p.thingIDNumber}, kind={p.kindDef?.defName}, faction={p.Faction?.def?.defName}, " +
//                     $"spawned={p.Spawned}, map={(p.Map != null ? p.Map.Index.ToString() : "null")}, " +
//                     $"pos={(p.Spawned ? p.Position.ToString() : "NA")}, " +
//                     $"lord={(p.lord != null ? p.lord.GetType().Name : "null")}, " +
//                     $"questTag={(p.questTags != null && p.questTags.Count > 0 ? string.Join(",", p.questTags) : "none")}"
//                 );

//                 // Full call stack (this is what we need)
//                 Log.Warning("[CheeseProtocol][VANISH] StackTrace:\n" + new StackTrace(true));
//             }
//             catch (Exception e)
//             {
//                 Log.Error("[CheeseProtocol][VANISH] Failed to log vanish stack: " + e);
//             }
//             finally
//             {
//                 // optional: keep tracking if it might reappear; I prefer untracking to prevent repeats
//                 PawnVanishDebug.Untrack(p);
//             }
//         }
//     }
// }