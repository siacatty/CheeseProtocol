using System;
using System.Collections.Generic;
using Verse;
using RimWorld;

namespace CheeseProtocol
{
    internal static class StealAnchorClusterUtil
    {
        public static List<IntVec3> BuildClusterAnchors(
            Map map,
            int binSize = 16,
            int maxStorages = 9999,
            int maxStockpileCellsPerZone = 300,
            int stockpileCellStride = 3,
            bool useMeanRepresentative = true)
        {
            var result = new List<IntVec3>();
            if (map == null) return result;
            if (binSize <= 0) binSize = 12;
            if (stockpileCellStride <= 0) stockpileCellStride = 1;

            // binKey -> accumulator
            var bins = new Dictionary<long, BinAcc>(256);

            // 1) Storage buildings -> positions
            int storagesSeen = 0;
            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
            {
                if (storagesSeen >= maxStorages) break;
                if (t is not Building_Storage bs) continue;
                if (!bs.Spawned) continue;

                storagesSeen++;
                AddPoint(map, bins, bs.Position, binSize);
            }

            // 2) Stockpile zones -> sampled cells
            var zones = map.zoneManager?.AllZones;
            if (zones != null)
            {
                for (int i = 0; i < zones.Count; i++)
                {
                    if (zones[i] is not Zone_Stockpile zp) continue;

                    var cells = zp.Cells;
                    int count = cells.Count;
                    if (count == 0) continue;

                    int visited = 0;
                    // stride sampling to avoid huge zones cost
                    for (int c = 0; c < count; c += stockpileCellStride)
                    {
                        if (visited >= maxStockpileCellsPerZone) break;
                        visited++;

                        IntVec3 cell = cells[c];
                        if (!cell.IsValid || !cell.InBounds(map)) continue;
                        if (cell.OnEdge(map)) continue;
                        if (HasCorpse(map, cell)) continue;

                        AddPoint(map, bins, cell, binSize);
                    }
                }
            }

            if (bins.Count == 0) return result;

            // 3) Convert each bin into one representative IntVec3
            result.Capacity = bins.Count;
            foreach (var kv in bins)
            {
                BinAcc acc = kv.Value;
                if (acc.count <= 4) continue;

                IntVec3 rep;
                if (useMeanRepresentative)
                {
                    // mean of points in this bin (rounded)
                    int x = (int)Math.Round(acc.sumX / (double)acc.count);
                    int z = (int)Math.Round(acc.sumZ / (double)acc.count);
                    rep = new IntVec3(x, 0, z);
                }
                else
                {
                    // center of the bin cell in map coordinates
                    int bx = acc.binX;
                    int bz = acc.binZ;
                    int x = bx * binSize + binSize / 2;
                    int z = bz * binSize + binSize / 2;
                    rep = new IntVec3(x, 0, z);
                }

                if (!rep.IsValid || !rep.InBounds(map) || rep.OnEdge(map))
                    continue;

                result.Add(rep);
            }

            return result;
        }

        private static void AddPoint(Map map, Dictionary<long, BinAcc> bins, IntVec3 p, int binSize)
        {
            if (!p.IsValid || !p.InBounds(map)) return;
            if (p.OnEdge(map)) return;

            int bx = p.x / binSize;
            int bz = p.z / binSize;

            long key = PackKey(bx, bz);

            if (!bins.TryGetValue(key, out BinAcc acc))
            {
                acc = new BinAcc { binX = bx, binZ = bz };
                bins[key] = acc;
            }

            acc.count++;
            acc.sumX += p.x;
            acc.sumZ += p.z;
        }

        private static bool HasCorpse(Map map, IntVec3 cell)
        {
            var things = map.thingGrid.ThingsListAt(cell);
            if (things == null) return false;

            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] is Corpse)
                    return true;
            }
            return false;
        }

        public static void AddPassengerShuttleAnchors(Map map, List<IntVec3> anchors)
        {
            if (map == null || anchors == null) return;

            var things = map.listerThings.AllThings;
            if (things == null) return;

            for (int i = 0; i < things.Count; i++)
            {
                Thing t = things[i];
                if (t == null || !t.Spawned) continue;

                if (t.def != null && t.def.defName == "PassengerShuttle")
                {
                    IntVec3 p = t.Position;
                    if (p.IsValid && p.InBounds(map) && !p.OnEdge(map))
                    {
                        anchors.Add(p);
                    }
                }
            }
        }

        private static long PackKey(int bx, int bz)
        {
            // pack two ints into one long (safe enough for map-sized bins)
            return ((long)bx << 32) ^ (uint)bz;
        }

        private sealed class BinAcc
        {
            public int binX;
            public int binZ;
            public int count;
            public long sumX;
            public long sumZ;
        }
    }
}