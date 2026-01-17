// using System.Diagnostics;
// using HarmonyLib;
// using Verse;
// using Verse.AI.Group;

// namespace CheeseProtocol
// {
//     [HarmonyPatch(typeof(Thing), nameof(Thing.DeSpawn))]
//     internal static class Patch_Thing_DeSpawn_LogTracked
//     {
//         static void Prefix(Thing __instance)
//         {
//             if (!(__instance is Pawn p)) return;
//             if (!PawnVanishDebug.IsTracked(p)) return;
//             var j = p.CurJob;
//             if (j != null)
//                 Log.Warning($"[Bully] CurJob={j.def?.defName} exitMapOnArrival={j.exitMapOnArrival}");
//             Log.Warning($"[CheeseProtocol][DESPAWN] Pawn despawning. id={p.thingIDNumber}, kind={p.kindDef?.defName}, faction={p.Faction?.def?.defName}");
//             Log.Warning($"[CheeseProtocol][DESPAWN] {p.LabelShort} job={j?.def?.defName} exitOnArrival={j?.exitMapOnArrival} " +
//                 $"exitAfterTick={p.mindState?.exitMapAfterTick} " +
//                 $"lordJob={p.GetLord()?.LordJob?.GetType().Name}");
//             Log.Warning($"[CheeseProtocol][DESPAWN] jobDef={(j?.def?.defName ?? "null")}, " +
//                         $"targetA={(j?.targetA.ToString() ?? "null")}, " +
//                         $"targetB={(j?.targetB.ToString() ?? "null")}, " +
//                         $"exitOnArrival={j?.exitMapOnArrival.ToString() ?? "null"}, " +
//                         $"locomotion={j?.locomotionUrgency.ToString() ?? "null"}");
//             Log.Warning("[CheeseProtocol][DESPAWN] StackTrace:\n" + new StackTrace(true));
//         }
//     }
// }