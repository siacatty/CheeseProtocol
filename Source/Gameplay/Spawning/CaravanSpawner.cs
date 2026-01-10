using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using System.Linq;
using static CheeseProtocol.CheeseLog;
using System.Security.Permissions;
using RimWorld.Planet;
using System.Configuration;

namespace CheeseProtocol
{
    internal static class CaravanSpawner
    {
        public static void Spawn(string donorName, int amount, string message)
        {
            CaravanAdvancedSettings caravanAdvSetting = CheeseProtocolMod.Settings?.GetAdvSetting<CaravanAdvancedSettings>(CheeseCommand.Caravan);
            if (caravanAdvSetting == null) 
            {
                CheeseLetter.AlertFail("!상단", "설정이 로드되지 않았습니다.");
                return;
            }
            float quality = QualityEvaluator.evaluateQuality(amount, CheeseCommand.Caravan);
            CheeseRollTrace trace = new CheeseRollTrace(donorName, CheeseCommand.Caravan);
            CaravanRequest request = Generate(quality, trace);

            Map map = Find.AnyPlayerHomeMap;
            if (map == null) 
            {
                CheeseLetter.AlertFail("!상단");
                return;
            }
            if (!isValidCaravan(request, map))
                return;
            if (map.mapPawns.FreeColonistsSpawnedCount == 0)
            {
                CheeseLetter.AlertFail("!상단", "본진에 정착민이 없어 상단이 도착하지 않습니다.");
                return;
            }
            //QMsg($"IncidentDef is null : {request.incidentDef == null} | Parms is null : {request.parms == null}", Channel.Debug);
            if (!VanillaIncidentRunner.TryExecuteWithTrace(request.incidentDef, request.parms, trace))
            {
                CheeseLetter.AlertFail("!상단", "실행 실패: 로그 확인 필요.");
                QWarn("TraderCaravanArrival failed to execute.");
            }
            QMsg($"Caravan called ==> TraderKind={request.parms.traderKind.defName}", Channel.Debug);
        }
        public static CaravanRequest Generate(float quality, CheeseRollTrace trace)
        {
            CaravanRequest request = new CaravanRequest();
            ApplyTradeCustomization(request, quality, trace);
            return request;
        }
        public static bool isValidCaravan(CaravanRequest request, Map map)
        {
            return ValidateTradeCustomization(request, map);
        }
        private static void ApplyTradeCustomization(CaravanRequest request, float quality, CheeseRollTrace trace)
        {
            var setting = CheeseProtocolMod.Settings;
            CaravanAdvancedSettings adv = setting?.GetAdvSetting<CaravanAdvancedSettings>(CheeseCommand.Caravan);
            if (adv == null) return;
            ApplyPOrbital(request, quality, setting.randomVar, adv.orbitalRange, trace);

            request.traderPool = BuildAllowedTraderKindPool();
            List<TraderKindDef> filtered = request.traderPool.Where(tk => tk != null && tk.orbital == request.isOrbital).ToList();
            if (filtered.Count > 0)
            {
                TraderKindDef traderKind = filtered.RandomElement();
                request.traderDef = traderKind;
            }
            return;
        }
        private static bool ValidateTradeCustomization(CaravanRequest request, Map map)
        {
            request.incidentDef = ValidateIncidentDef(request, map);
            request.traderDef = ValidateTraderDef(request, map);
            if (request.traderDef == null)
            {
                QWarn("No trader is allowed");
                CheeseLetter.AlertFail("!상단", "!상단 실행 실패: 허용된 상단 종류 중 현재 도착 가능한 상단이 없습니다.");
                return false;
            }
            request.parms.traderKind = request.traderDef;
            if (request.incidentDef.Worker.CanFireNow(request.parms))
                return true;
            else
            {
                QWarn($"{request.traderDef.defName} cannot be called.");
                CheeseLetter.AlertFail("!상단", "!상단 실행 실패: 허용된 상단 종류 중 현재 도착 가능한 상단이 없습니다.");
            }
            return false;
        }

        private static List<TraderKindDef> BuildAllowedTraderKindPool()
        {
            CaravanAdvancedSettings adv = CheeseProtocolMod.Settings.GetAdvSetting<CaravanAdvancedSettings>(CheeseCommand.Caravan);
            //bool wantOrbital = (incidentDef == IncidentDefOf.OrbitalTraderArrival);
            IEnumerable<TraderKindDef> all = DefDatabase<TraderKindDef>.AllDefs;
            //var basePool = all.Where(tk => tk != null && tk.orbital == wantOrbital);
            var basePool = DefDatabase<TraderKindDef>.AllDefs;
            //filter trade types (e.g. Bulk/exotic)
            var filtered = new List<TraderKindDef>();

            foreach (var tk in basePool)
            {
                if (!IsAllowedCaravan(tk, adv)) continue;
                filtered.Add(tk);
            }

            return filtered;
        }

        public static void ApplyPOrbital(CaravanRequest request, float quality, float randomVar, QualityRange orbitalRange, CheeseRollTrace trace)
        {
            TraceStep traceStep = new TraceStep("궤도상선 확률");
            float pOrbital = QualityBetaSampler.SampleQualityWeightedBeta(
                    quality,
                    orbitalRange,
                    concentration01: 1f-randomVar,
                    traceStep,
                    debugLog: false
            );
            trace.steps.Add(traceStep);
            if (Rand.Chance(pOrbital))
                request.isOrbital = true;
            return;
        }

        public static IncidentDef ValidateIncidentDef(CaravanRequest request, Map map)
        {
            IncidentDef ground = IncidentDefOf.TraderCaravanArrival;
            IncidentDef orbital = IncidentDefOf.OrbitalTraderArrival;
            request.parms = StorytellerUtility.DefaultParmsNow(ground.category, map);
            request.parms.forced = true;

            if (request.isOrbital)
            {
                var op = StorytellerUtility.DefaultParmsNow(orbital.category, map);
                op.forced = true;

                if (orbital.Worker.CanFireNow(op) && HasPoweredCommsConsole(map))
                {
                    request.parms = op;
                    return orbital;
                }
                else
                {
                    request.requireRepick = true;
                    request.isOrbital = false;
                    QWarn("Unable to fire Orbital Trade incident", Channel.Verse);
                }
            }

            if (!ground.Worker.CanFireNow(request.parms))
            {
                QWarn("Unable to fire Ground Trade incident", Channel.Verse);
                return null;
            }

            return ground;
        }

        public static TraderKindDef ValidateTraderDef(CaravanRequest request, Map map)
        {
            List<TraderKindDef> pool = request.traderPool.Where(tk => tk != null && tk.orbital == request.isOrbital).ToList();
            if (pool.Count == 0) return null;
            if (request.requireRepick)
            {
                request.traderDef = pool.RandomElement();
                request.requireRepick = false;
            }
            if (request.traderDef == null) return null;
            if (request.traderDef.faction != null && EqualsIgnoreCase(request.traderDef.faction.defName, "Empire"))
            {
                if (request.traderDef.permitRequiredForTrading != null)
                {
                    if (AnyColonistHasPermit(map, request.traderDef.permitRequiredForTrading, Find.FactionManager.FirstFactionOfDef(request.traderDef.faction)))
                        return request.traderDef;
                    else
                    {
                        QWarn("No colonist has permit for Imperial Trading", Channel.Debug);
                        var filtered = pool.Where(tk => tk != null && tk.permitRequiredForTrading == null).ToList();
                        return filtered.Count > 0 ? filtered.RandomElement() : null;
                    }
                }
            }
            return request.traderDef;
        }

        public static bool IsAllowedCaravan(
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
                    return adv.allowImperialCaravan;
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
            QMsg($"TraderKind fell through filters: {tk.defName} (label={tk.label}, category={tk.category ?? "(none)"}, orbital={tk.orbital})", Channel.Debug);
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