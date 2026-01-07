using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using System.Linq;

namespace CheeseProtocol
{
    internal static class CaravanSpawner
    {
        public static void Spawn(string donorName, int amount, string message)
        {
            CaravanAdvancedSettings caravanAdvSetting = CheeseProtocolMod.Settings?.GetAdvSetting<CaravanAdvancedSettings>(CheeseCommand.Caravan);
            if (caravanAdvSetting == null) return;
            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;
            float quality = QualityEvaluator.evaluateQuality(amount, CheeseCommand.Caravan);

            if (!TryApplyTradeCustomization(map, quality, out IncidentDef def, out IncidentParms parms))
                return;
            Log.Message($"[CheeseProtocol] Caravan called ==> TraderKind={parms.traderKind.defName}");

            if (!def.Worker.TryExecute(parms))
                Log.Warning("[CheeseProtocol] TraderCaravanArrival failed to execute.");
        }

        private static bool TryApplyTradeCustomization(Map map, float quality, out IncidentDef def, out IncidentParms parms)
        {
            List<TraderKindDef> pool = BuildAllowedTraderKindPool(map, quality, out IncidentDef incidentDef, out IncidentParms incidentParms);
            def = incidentDef;
            parms = incidentParms;
            if (def == null || parms == null) {
                Log.Warning("[CheeseProtocol] TraderDef || Trader Parms not found");
                return false;
            }
            if (pool.Count <= 0)
            {
                Log.Warning("[CheeseProtocol] No trader is allowed");
                Messages.Message(
                    "!상단 실행 실패: 허용된 상단 종류 중 현재 도착 가능한 상단이 없습니다.\n",
                    MessageTypeDefOf.RejectInput
                );
                return false;
            }
            while (pool.Count > 0)
            {
                TraderKindDef traderKind = pool.RandomElement();
                parms.traderKind = traderKind;
                if (incidentDef.Worker.CanFireNow(parms))
                {
                    return true;
                }
                else
                {
                    pool.Remove(traderKind);
                }
            }
            Log.Warning("[CheeseProtocol] No trader among allowed can be called");
            Messages.Message(
                "!상단 실행 실패: 허용된 상단 종류 중 현재 도착 가능한 상단이 없습니다.\n",
                MessageTypeDefOf.RejectInput
            );
            return false;
        }

        private static List<TraderKindDef> BuildAllowedTraderKindPool(Map map, float quality, out IncidentDef incidentDef, out IncidentParms incidentParms)
        {
            CaravanAdvancedSettings adv = CheeseProtocolMod.Settings.GetAdvSetting<CaravanAdvancedSettings>(CheeseCommand.Caravan);
            incidentDef = PickCaravanOrOrbital(map, quality, out IncidentParms parms);
            incidentParms = parms;
            if (incidentDef == null)
                return new List<TraderKindDef>();
            bool wantOrbital = (incidentDef == IncidentDefOf.OrbitalTraderArrival);
            IEnumerable<TraderKindDef> all = DefDatabase<TraderKindDef>.AllDefs;
            var basePool = all.Where(tk => tk != null && tk.orbital == wantOrbital);
            
            //filter trade types (e.g. Bulk/exotic)
            var filtered = new List<TraderKindDef>();

            foreach (var tk in basePool)
            {
                if (!IsAllowedCaravan(map, tk, adv)) continue;
                filtered.Add(tk);
            }

            return filtered;
        }

        public static IncidentDef PickCaravanOrOrbital(Map map, float quality, out IncidentParms parms)
        {
            var settings = CheeseProtocolMod.Settings;
            CaravanAdvancedSettings caravanAdvSettings = settings.GetAdvSetting<CaravanAdvancedSettings>(CheeseCommand.Caravan);
            QualityRange orbitalRange = caravanAdvSettings.orbitalRange;
            float randomVar = settings.randomVar;

            IncidentDef ground = IncidentDefOf.TraderCaravanArrival;
            IncidentDef orbital = IncidentDefOf.OrbitalTraderArrival;
            parms = StorytellerUtility.DefaultParmsNow(ground.category, map);
            parms.forced = true;
            
            //parms.traderKind = TraderKindDefOf

            float pOrbital = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    orbitalRange,
                    concentration01: 1f-randomVar,
                    debugLog: false
            ); 

            if (Rand.Chance(pOrbital))
            {
                var op = StorytellerUtility.DefaultParmsNow(orbital.category, map);
                op.forced = true;

                if (orbital.Worker.CanFireNow(op) && HasPoweredCommsConsole(map))
                {
                    parms = op;
                    return orbital;
                }
                else
                {
                    Log.Warning("[CheeseProtocol] Unable to fire Orbital Trade incident");
                }
            }

            if (!ground.Worker.CanFireNow(parms))
            {
                Log.Warning("[CheeseProtocol] Unable to fire Ground Trade incident");
                return null;
            }

            return ground;
        }

        public static bool IsAllowedCaravan(
            Map map,
            TraderKindDef tk,
            CaravanAdvancedSettings adv)
        {
            if (tk == null) return false;

            // 0) 호출 대상으로 부적절한 베이스/방문자 제거
            if (StartsWith(tk.defName, "Base_")) return false;
            if (StartsWith(tk.defName, "Visitor_")) return false;

            // 1) Empire / Royal
            // - permitRequiredForTrading이 있으면 제국 상인, 없으면 황실 공물 징수인.
            if (tk.faction != null && EqualsIgnoreCase(tk.faction.defName, "Empire"))
            {
                if (EqualsIgnoreCase(tk.category, "TributeCollector"))
                    return adv.allowRoyalCaravan;
                else if (tk.permitRequiredForTrading != null)
                {
                    if (adv.allowImperialCaravan)
                    {
                        if (AnyColonistHasPermit(map, tk.permitRequiredForTrading, Find.FactionManager.FirstFactionOfDef(tk.faction)))
                            return true;
                        else
                        {
                            //Log.Warning("[CheeseProtocol] No colonist has permit for Imperial Trading");
                            return false;
                        }
                    }
                }
            }
            // permit만 있는 케이스가 Empire 외에도 생길 수 있으니 (모드)
            if (tk.permitRequiredForTrading != null)
                return adv.allowImperialCaravan;

            // 2) Slaver
            if (!string.IsNullOrEmpty(tk.category) &&
                EqualsIgnoreCase(tk.category, "Slaver"))
            {
                return adv.allowSlaverCaravan;
            }
            // Shaman
            if (ContainsIgnoreCase(tk.defName, "Shaman"))
                return adv.allowShamanCaravan;
            // Bulk
            if (ContainsIgnoreCase(tk.defName, "BulkGoods"))
                return adv.allowBulkCaravan;

            // Exotic
            if (ContainsIgnoreCase(tk.defName, "Exotic"))
                return adv.allowExoticCaravan;

            // Combat
            if (ContainsIgnoreCase(tk.defName, "CombatSupplier") ||
                ContainsIgnoreCase(tk.defName, "WarMerchant"))
                return adv.allowCombatCaravan;
            Log.Message($"[CheeseProtocol] TraderKind fell through filters: {tk.defName} (label={tk.label}, category={tk.category ?? "(none)"}, orbital={tk.orbital})");
            return true;
        }

        private static bool HasPoweredCommsConsole(Map map)
        {
            // Building_CommsConsole 존재 + 전원 ON
            var consoles = map.listerBuildings.AllBuildingsColonistOfClass<Building_CommsConsole>();
            foreach (var c in consoles)
            {
                var p = c.GetComp<CompPowerTrader>();
                if (p == null || p.PowerOn) return true; // 전원 컴프 없으면 항상 ON 취급
            }
            return false;
        }

        private static bool AnyColonistHasPermit(Map map, RoyalTitlePermitDef permit, Faction faction)
        {
            if (!ModsConfig.RoyaltyActive) return false;
            if (permit == null) return true;

            var pawns = map.mapPawns.FreeColonistsSpawned;
            foreach (var p in pawns)
            {
                var r = p.royalty;
                if (r == null) continue;

                if (r.HasPermit(permit, faction))
                    return true;
            }
            return false;
        }

        private static bool StartsWith(string s, string prefix)
            => !string.IsNullOrEmpty(s) && s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

        private static bool ContainsIgnoreCase(string s, string token)
            => !string.IsNullOrEmpty(s) && s.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool EqualsIgnoreCase(string a, string b)
            => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}