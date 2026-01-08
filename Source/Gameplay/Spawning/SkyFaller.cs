using System;
using System.Reflection;
using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;
using System.Linq;
using System.Collections;

namespace CheeseProtocol
{
    public static class SkyFaller
    {

        public static bool TrySpawnCustom(
            Map map,
            IntVec3 near,
            ThingDef innerDef,      // MineableSteel / ChunkGranite / Obsidian 등 "안에 들어갈 것"
            ThingDef skyfallerDef,  // MeteoriteIncoming 계열(컨테이너형 skyfaller)
            int pieces,
            out IntVec3 rootCell,
            int initialNearMaxDist = 40,
            int minDistToEdge = 10,
            bool allowRoofed = true,
            bool allowItems = false,
            bool allowBuildings = false,
            int radiusStep = 12,
            int maxNearMaxDist = 200
        )
        {
            rootCell = IntVec3.Invalid;

            if (map == null || innerDef == null || skyfallerDef == null)
            {
                Log.Warning("[CheeseProtocol] TrySpawnMeteorCluster_NoReflection: null args.");
                return false;
            }
            //skyfallerDef.size = pieces;

            pieces = Mathf.Clamp(pieces, GameplayConstants.MeteorSizeMin, GameplayConstants.MeteorSizeMax);

            if (!near.IsValid || !near.InBounds(map))
                near = map.Center;

            // 1) skyfaller root cell 찾기 (점진 확장)
            int cur = Mathf.Max(1, initialNearMaxDist);
            bool found = false;

            // Heavy affordance면 물/습지 같은 곳은 걸러질 수 있음
            // 원하면 null 대신 TerrainAffordanceDefOf.Light / Medium 등으로 바꿔서 완화 가능
            var affordance = TerrainAffordanceDefOf.Light;

            while (cur <= maxNearMaxDist)
            {
                if (CellFinderLoose.TryFindSkyfallerCell(
                        skyfallerDef,
                        map,
                        affordance,
                        out rootCell,
                        minDistToEdge: minDistToEdge,
                        nearLoc: near,
                        nearLocMaxDist: cur,
                        allowRoofedCells: allowRoofed,
                        allowCellsWithItems: allowItems,
                        allowCellsWithBuildings: allowBuildings,
                        colonyReachable: false,
                        avoidColonistsIfExplosive: false,
                        alwaysAvoidColonists: false,
                        extraValidator: null
                    ))
                {
                    found = true;
                    break;
                }

                cur += radiusStep;
            }

            if (!found)
            {
                Log.Warning($"[CheeseProtocol] No root cell for skyfaller within nearMaxDist <= {maxNearMaxDist}. Fallback.");
                return false;
            }
            Log.Message($"[CheeseProtocol] skyfaller size = {skyfallerDef.size.x}x{skyfallerDef.size.z}");
            // 2) skyfaller 1개 생성 (컨테이너형)
            Skyfaller skyfaller = SkyfallerMaker.MakeSkyfaller(skyfallerDef);
            if (skyfaller == null)
            {
                Log.Error("[CheeseProtocol] MakeSkyfaller returned null.");
                return false;
            }

            // 3) inner container 얻어서 pieces만큼 채우기
            ThingOwner inner = skyfaller.GetDirectlyHeldThings();
            if (inner == null)
            {
                Log.Error($"[CheeseProtocol] Skyfaller '{skyfallerDef.defName}' has no inner container. " +
                          $"(Not a container-type skyfallerDef?)");
                return false;
            }

            // innerDef를 pieces개 넣기
            for (int i = 0; i < pieces; i++)
            {
                Thing t = ThingMaker.MakeThing(innerDef);
                inner.TryAdd(t);
            }

            // 4) skyfaller 1개만 Spawn => "한 덩어리"로 떨어짐
            GenSpawn.Spawn(skyfaller, rootCell, map);
            
            Log.Message($"[CheeseProtocol] Spawned meteor CLUSTER: inner={innerDef.defName} pieces={pieces} " +
                        $"skyfaller={skyfallerDef.defName}, {skyfallerDef.thingClass} at {rootCell} nearMaxDistUsed={cur}");

            return true;
        }
        private static Thing MakeActiveDropPodWithContents(List<Thing> things)
        {
            ActiveTransporterInfo info = new ActiveTransporterInfo();
            var inner = info.GetDirectlyHeldThings();
            foreach (var t in things)
                inner.TryAdd(t);
            Thing pod = ThingMaker.MakeThing(ThingDefOf.ActiveDropPod);
            ActiveTransporter transporter = pod as ActiveTransporter;
            transporter.Contents = info;
            return pod;
        }

        private static List<Thing> BuildContentsFromSupplyRequest(SupplyRequest supply)
        {
            var list = new List<Thing>();

            if (supply == null || supply.def == null)
                return list;

            // 1) 아이템 생성 (stuff 포함)
            Thing t = MakeThingSafe(supply.def);

            // 2) 무기면 품질 적용 + 무조건 1개
            if (supply.type == SupplyType.Weapon)
            {
                var qc = t.TryGetComp<CompQuality>();
                if (qc != null && supply.isWeaponTierSet)
                    qc.SetQuality(supply.weaponTier, ArtGenerationContext.Outsider);

                t.stackCount = 1;
                list.Add(t);
                return list;
            }

            // 3) 그 외는 count/stackLimit로 쪼개서 넣기
            int remaining = Mathf.Max(1, supply.count);
            int perStack = supply.stackLimit > 0 ? supply.stackLimit : t.def.stackLimit;
            perStack = Mathf.Max(1, perStack);

            while (remaining > 0)
            {
                int take = Mathf.Min(perStack, remaining);

                Thing stackThing = (list.Count == 0) ? t : MakeThingSafe(supply.def);
                stackThing.stackCount = take;

                list.Add(stackThing);
                remaining -= take;
            }

            return list;
        }

        public static bool TrySpawnSupplyDropPod(
            Map map,
            IntVec3 near,
            SupplyRequest supply,
            out IntVec3 rootCell,
            int initialNearMaxDist = 40,
            int minDistToEdge = 10,
            bool allowRoofed = true,
            bool allowItems = false,
            bool allowBuildings = false,
            int radiusStep = 12,
            int maxNearMaxDist = 200
        )
        {
            rootCell = IntVec3.Invalid;

            if (map == null || supply == null || supply.def == null)
            {
                Log.Warning("[CheeseProtocol] TrySpawnSupplyDropPodIncoming: null args.");
                return false;
            }

            if (!near.IsValid || !near.InBounds(map))
                near = map.Center;

            // ✅ 0) contents를 함수 내부에서 만든다
            List<Thing> contents = BuildContentsFromSupplyRequest(supply);
            if (contents.Count == 0)
            {
                Log.Warning("[CheeseProtocol] TrySpawnSupplyDropPodIncoming: contents empty.");
                return false;
            }

            var skyfallerDef = ThingDefOf.DropPodIncoming;

            // ✅ 1) root cell 찾기
            int cur = Mathf.Max(1, initialNearMaxDist);
            bool found = false;

            var affordance = TerrainAffordanceDefOf.Light;

            while (cur <= maxNearMaxDist)
            {
                if (CellFinderLoose.TryFindSkyfallerCell(
                        skyfallerDef,
                        map,
                        affordance,
                        out rootCell,
                        minDistToEdge: minDistToEdge,
                        nearLoc: near,
                        nearLocMaxDist: cur,
                        allowRoofedCells: allowRoofed,
                        allowCellsWithItems: allowItems,
                        allowCellsWithBuildings: allowBuildings,
                        colonyReachable: false,
                        avoidColonistsIfExplosive: false,
                        alwaysAvoidColonists: false,
                        extraValidator: null
                    ))
                {
                    found = true;
                    break;
                }
                cur += radiusStep;
            }

            if (!found)
            {
                Log.Warning($"[CheeseProtocol] No root cell for DropPodIncoming within nearMaxDist <= {maxNearMaxDist}.");
                return false;
            }

            // ✅ 2) DropPodIncoming 내부는 ActiveDropPod 여야 함
            Thing pod = MakeActiveDropPodWithContents(contents);

            // ✅ 3) Skyfaller 생성 + Spawn
            Skyfaller skyfaller = SkyfallerMaker.MakeSkyfaller(skyfallerDef, pod);
            if (skyfaller == null)
            {
                Log.Error("[CheeseProtocol] MakeSkyfaller returned null.");
                return false;
            }

            GenSpawn.Spawn(skyfaller, rootCell, map);

            Log.Message(
                "[CheeseProtocol] Spawned Supply DropPodIncoming: " +
                $"type={supply.type}, label={supply.label}, count={supply.count}, " +
                $"tier={(supply.isWeaponTierSet ? supply.weaponTier.ToString() : "unset")}, " +
                $"tech={supply.techLevel}, at {rootCell}, nearMaxDistUsed={cur}"
            );

            return true;
        }

        private static Thing MakeThingSafe(ThingDef def)
        {
            ThingDef stuff = null;

            if (def.MadeFromStuff)
            {
                stuff = GenStuff.DefaultStuffFor(def);

                // 혹시 null이면 안전한 fallback
                if (stuff == null)
                    stuff = ThingDefOf.Steel; // 대부분 무기에 안전
            }

            return ThingMaker.MakeThing(def, stuff);
        }

        private static List<int> SplitIntoStacks(int total, int stackLimit)
        {
            var res = new List<int>();
            int remaining = Mathf.Max(0, total);
            int limit = Mathf.Max(1, stackLimit);

            while (remaining > 0)
            {
                int take = Mathf.Min(limit, remaining);
                res.Add(take);
                remaining -= take;
            }
            return res;
        }
    }
}
