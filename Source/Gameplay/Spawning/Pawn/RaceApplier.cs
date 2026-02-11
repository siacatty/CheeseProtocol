using System;
using System.Linq;
using System.Collections.Generic;
using Verse;
using UnityEngine;
using RimWorld;
using System.Reflection;
using static CheeseProtocol.CheeseLog;
using System.Text;

namespace CheeseProtocol
{
    public static class RaceApplier
    {
        public static PawnKindDef GetRace()
        {
            PawnKindDef race = PawnKindDefOf.Colonist;
            var allowed = CheeseProtocolMod.Settings?.GetAdvSetting<JoinAdvancedSettings>(CheeseCommand.Join)?.allowedRaces;
            if (allowed != null && allowed.Count > 0)
            {
                race = allowed.RandomElement();
            }
            return race;
        }
        private static bool IsOfficial(ModContentPack mcp)
        {
            if (mcp == null) return true;

            var id = mcp.PackageId ?? "";
            return id.StartsWith("ludeon.", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMod(Def def) => def?.modContentPack != null && !IsOfficial(def.modContentPack);

        private static bool IsPlayerFaction(FactionDef f) => f != null && f.isPlayer;

        public static List<PawnKindDef> BuildRaceCatalog()
        {
            var all = DefDatabase<PawnKindDef>.AllDefsListForReading;

            // Vanilla: only Colonist
            var vanillaColonistOnly = all
                .Where(k => k == PawnKindDefOf.Colonist && IsOfficial(k.modContentPack))
                .ToList();

            // Mods: any humanlike + defaultFactionType.isPlayer
            var modKinds = all
                .Where(k => k?.race?.race != null && k.race.race.Humanlike)
                .Where(k => IsMod(k))
                .Where(k => IsPlayerFaction(k.defaultFactionDef))
                .OrderBy(k => k.label ?? k.defName)
                .ToList();

            var result = new List<PawnKindDef>(vanillaColonistOnly);
            foreach (var k in modKinds)
                if (!result.Contains(k)) result.Add(k);

            return result;
        }

        public static void BuildPools(
            List<PawnKindDef> catalog,
            List<string> allowedRaceKeys,
            out List<PawnKindDef> allowedRace,
            out List<PawnKindDef> disallowedRace
        )
        {
            allowedRace = new List<PawnKindDef>();
            disallowedRace = new List<PawnKindDef>();

            var allowedKeySet = (allowedRaceKeys != null && allowedRaceKeys.Count > 0)
                ? new HashSet<string>(allowedRaceKeys)
                : null;

            foreach (var c in catalog)
            {
                bool isAllowed = allowedKeySet != null && allowedKeySet.Contains(c.defName);

                if (isAllowed) allowedRace.Add(c);
                else disallowedRace.Add(c);
            }
        }

        public static void LogDump()
        {
            var sb = new StringBuilder(64 * 1024);

            // --- PawnKindDef (Humanlike only) ---
            var pawnKinds = DefDatabase<PawnKindDef>.AllDefsListForReading
                //.Where(true)
                .OrderBy(k => k.defName, StringComparer.Ordinal)
                .ToList();

            sb.AppendLine($"[DefDump] Humanlike PawnKindDefs: {pawnKinds.Count}");
            foreach (var k in pawnKinds)
            {
                string mod = k.modContentPack?.Name ?? "UnknownMod";
                string race = k.race?.defName ?? "null";
                string faction = k.defaultFactionDef?.defName ?? "null";
                sb.AppendLine($"  - {k.defName} | label={k.label ?? "null"} | race={race} | defaultFaction={faction} | mod={mod}");
            }

            // --- XenotypeDef (Biotech) ---
            if (ModsConfig.BiotechActive)
            {
                var xenos = DefDatabase<XenotypeDef>.AllDefsListForReading
                    //.Where(IsUsableXenotype)
                    .OrderBy(x => x.defName, StringComparer.Ordinal)
                    .ToList();

                sb.AppendLine();
                sb.AppendLine($"[DefDump] XenotypeDefs: {xenos.Count} (BiotechActive={ModsConfig.BiotechActive})");
                foreach (var x in xenos)
                {
                    string mod = x.modContentPack?.Name ?? "UnknownMod";
                    sb.AppendLine($"  - {x.defName} | label={x.label ?? "null"} | icon={x.iconPath ?? "null"} | inheritable={x.inheritable} | mod={mod}");
                }
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("[DefDump] Biotech inactive -> XenotypeDefs skipped.");
            }

            Warn(sb.ToString());
        }
    }
}