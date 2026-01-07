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
    }
}
