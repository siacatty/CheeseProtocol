using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;

namespace CheeseProtocol
{
    internal static class ColonistSpawner
    {
        public static void Spawn(string donorName, int amount, string message)
        {
            Map map = Find.CurrentMap;
            if (map == null) return;

            // Generate a player colonist
            var req = new PawnGenerationRequest(
                PawnKindDefOf.Colonist,
                Faction.OfPlayer,
                PawnGenerationContext.PlayerStarter,
                map.Tile,
                forceGenerateNewPawn: true
            );

            Pawn pawn = PawnGenerator.GeneratePawn(req);
            float quality = evaluatePawnQuality(amount);
            if (Prefs.DevMode)
            {
                Log.Message($"[CheeseProtocol] Spawn quality={quality:0.00}");
            }

            ApplyQualityToPawn(pawn, quality);

            // Set name to donor
            if (!string.IsNullOrWhiteSpace(donorName))
                pawn.Name = new NameSingle(TrimName(donorName));

            // Spawn via drop pod or place nearby
            if (CheeseProtocolMod.Settings?.useDropPod ?? true)
            {
                IntVec3 dropSpot = DropCellFinder.TradeDropSpot(map);
                DropPodUtility.DropThingsNear(dropSpot, map, new Thing[] { pawn }, 110, canInstaDropDuringInit: false, leaveSlag: false);
            }
            else
            {
                IntVec3 spot = CellFinderLoose.RandomCellWith(
                    c => c.Standable(map) && !c.Fogged(map),
                    map, 200);

                GenSpawn.Spawn(pawn, spot, map);
            }

            Messages.Message(
                $"[CheeseProtocol] New colonist joined: {pawn.LabelShort} (₩{amount})",
                MessageTypeDefOf.PositiveEvent,
                false
            );
        }

        private static float evaluatePawnQuality(int amount)
        {
            var settings = CheeseProtocolMod.Settings;

            if (!settings.TryGetCommandConfig(CheeseCommand.Join, out var cfg))
                return 0f;

            return QualityEvaluator.Evaluate(
                amount,
                cfg.minDonation,
                cfg.maxDonation,
                cfg.curve
            );

        }
        private static void ApplyQualityToPawn(Pawn pawn, float quality)
        {
            var settings = CheeseProtocolMod.Settings;
            var joinSettings = settings.joinAdvanced;
            float randomVar = settings.randomVar; //higher values --> bigger noise (lucky/unlucky)
            float lower_tail = 0.1f; //higher values --> less likely for high amount donation to get unlucky
            ApplySkills(pawn, quality, randomVar, lower_tail, joinSettings.skillRange);
            //ApplyTraits(pawn, quality);
            //ApplyWorkDisables(pawn, quality);
            //ApplyHealth(pawn, quality);
            //ApplyApparel(pawn, quality);
            //ApplyWeapon(pawn, quality);
        }

        private static void ApplySkills(Pawn pawn, float quality, float randomVar, float lower_tail, QualityRange weightRange)
        {
            int baseMin = GameplayConstants.SkillLevelMin;
            int baseMax = GameplayConstants.SkillLevelMax;
            foreach (var skill in pawn.skills.skills)
            {
                float levelF = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    weightRange,
                    concentration01: 1f-randomVar,
                    baseMin: baseMin,
                    baseMax: baseMax,
                    true
                );
                skill.Level = Mathf.Clamp(Mathf.RoundToInt(levelF), baseMin, baseMax);
            }
        }

        private static string TrimName(string s)
        {
            s = s.Trim();
            if (s.Length > 24) s = s.Substring(0, 24);
            return s;
        }
    }
}
