using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using static CheeseProtocol.CheeseLog;

namespace CheeseProtocol
{
    public static class SeatUtility
    {
        /// <summary>
        /// Assign teacherSeat + studentSeats. Tries to keep students at least minDist away from teacherSeat,
        /// but will relax to 2, then 1 if needed.
        /// </summary>
        public static bool TryAssignSeats(
            Map map,
            Pawn teacher,
            List<Pawn> students,
            LessonVenue currentVenue,
            out IntVec3 teacherSeat,
            out Dictionary<string, IntVec3> studentSeats,
            out IntVec3 teacherFaceDir,
            int minDist = 3,
            int maxDist = 8)
        {
            studentSeats = new Dictionary<string, IntVec3>();
            teacherSeat = IntVec3.Invalid;
            teacherFaceDir = IntVec3.South;

            if (map == null) return false;
            if (teacher == null || !teacher.Spawned || teacher.Dead) return false;
            if (currentVenue == null || !currentVenue.spotCell.IsValid) return false;

            var stu = (students ?? new List<Pawn>())
                .Where(p => p != null && p.Spawned && !p.Dead && p != teacher)
                .ToList();

            if (stu.Count == 0)
            {
                if (!DetermineTeacherSeatAndDir(map, currentVenue, teacher, out teacherSeat, out teacherFaceDir))
                {
                    teacherSeat = currentVenue.spotCell;
                    teacherFaceDir = IntVec3.North;
                }
                return false;
            }

            // room via key cell (may be null outdoors)
            Room room = currentVenue.roomKeyCell.GetRoom(map);

            // 1) teacherSeat + faceDir
            if (!DetermineTeacherSeatAndDir(map, currentVenue, teacher, out teacherSeat, out teacherFaceDir))
            {
                teacherSeat = currentVenue.spotCell;
                teacherFaceDir = IntVec3.North;
            }

            // 2) blocked cells (teacher + anchor + around anchor)
            var blocked = new HashSet<IntVec3> { teacherSeat };

            if (currentVenue.anchorInfo.IsValid && currentVenue.anchorInfo.HasThing)
            {
                var aThing = currentVenue.anchorInfo.Thing;
                if (aThing != null && aThing.Spawned)
                {
                    blocked.Add(aThing.Position);
                    foreach (var c in GenAdj.CellsAdjacent8Way(aThing))
                        if (c.InBounds(map)) blocked.Add(c);
                }
            }

            // 3) collect candidates once (tier1/tier2 already sorted)
            var tier1 = new List<IntVec3>(256);
            var tier2 = new List<IntVec3>(512);
            CollectSeatCandidates(map, room, teacherSeat, teacherFaceDir, blocked, tier1, tier2);

            // 4) student ordering (optional; keep deterministic)
            IntVec3 seatOrigin = teacherSeat;
            stu.Sort((a, b) =>
            {
                int da = a.Position.DistanceToSquared(seatOrigin);
                int db = b.Position.DistanceToSquared(seatOrigin);
                return da.CompareTo(db);
            });

            // 5) Try assign with distance relaxation: minDist -> 2 -> 1 (clamped)
            int wanted = Math.Max(1, minDist);
            int[] distStages = BuildDistStages(wanted);

            foreach (int stageDist in distStages)
            {
                // attempt a full assignment from scratch for best layout
                if (TryAssignAllStudentsAtMinDist(map, room, teacher, teacherFaceDir, stu, currentVenue, teacherSeat, blocked, tier1, tier2, stageDist, maxDist, out var seats))
                {
                    studentSeats = seats;
                    return true;
                }
            }
            // last resort: ignore minDist but keep all other checks (still avoids teacher/anchor blocks)
            if (TryAssignAllStudentsAtMinDist(map, room, teacher, teacherFaceDir, stu, currentVenue, teacherSeat, blocked, tier1, tier2, 0, maxDist, out var fallbackSeats))
            {
                studentSeats = fallbackSeats;
                return true;
            }

            return false;
        }

        private static int[] BuildDistStages(int wanted)
        {
            // wanted (>=1), then 2, then 1, without duplicates and in descending strictness
            if (wanted <= 1) return new[] { 1 };
            if (wanted == 2) return new[] { 2, 1 };
            return new[] { wanted, 2, 1 }; // e.g., 3 -> 3,2,1 or 4 -> 4,2,1
        }

        /// <summary>
        /// stageMinDist:
        /// - 3 means >= 3 cells away (Manhattan)
        /// - 2 means >= 2
        /// - 1 means >= 1
        /// - 0 means no min-distance constraint
        /// This function tries to assign ALL students; returns false if any student can't get a seat.
        /// </summary>
        private static bool TryAssignAllStudentsAtMinDist(
            Map map,
            Room room,
            Pawn teacher,
            IntVec3 faceDir,
            List<Pawn> students,
            LessonVenue venue,
            IntVec3 teacherSeat,
            HashSet<IntVec3> baseBlocked,
            List<IntVec3> tier1,
            List<IntVec3> tier2,
            int stageMinDist,
            int maxDist,
            out Dictionary<string, IntVec3> resultSeats)
        {
            resultSeats = new Dictionary<string, IntVec3>();

            // local blocked so we don't mutate the caller's set between stages
            var blocked = new HashSet<IntVec3>(baseBlocked);

            int idx1 = 0, idx2 = 0;

            for (int i = 0; i < students.Count; i++)
            {
                var p = students[i];
                if (p == null) continue;

                string uid = p.GetUniqueLoadID();
                if (uid.NullOrEmpty()) continue;

                IntVec3 seat = IntVec3.Invalid;

                // Tier1 first (front)
                while (idx1 < tier1.Count)
                {
                    var c = tier1[idx1++];
                    if (blocked.Contains(c)) continue;
                    if (maxDist > 0 && !MeetsMaxDepth(c, teacherSeat, faceDir, maxDist)) continue;
                    if (stageMinDist > 0 && !MeetsMinDepth(c, teacherSeat, faceDir, stageMinDist)) continue;
                    if (!IsUsableSeat(map, room, teacher, c)) continue;
                    seat = c;
                    break;
                }

                // Tier2 (rest)
                if (!seat.IsValid)
                {
                    while (idx2 < tier2.Count)
                    {
                        var c = tier2[idx2++];
                        if (blocked.Contains(c)) continue;
                        if (maxDist > 0 && !MeetsMaxDepth(c, teacherSeat, faceDir, maxDist)) continue;
                        if (stageMinDist > 0 && !MeetsMinDepth(c, teacherSeat, faceDir, stageMinDist)) continue;
                        if (!IsUsableSeat(map, room, teacher, c)) continue;
                        seat = c;
                        break;
                    }
                }

                // Fallback near spot (still enforces minDist if stageMinDist>0)
                if (!seat.IsValid)
                {
                    seat = FindFallbackSeatNear(map, teacher, faceDir, venue.spotCell, blocked, teacherSeat, stageMinDist, maxDist, room);
                }

                if (!seat.IsValid)
                {
                    // If any student can't be seated for this stage, fail the whole stage
                    resultSeats.Clear();
                    return false;
                }

                resultSeats[uid] = seat;
                blocked.Add(seat);
            }

            return true;
        }

        private static bool MeetsMinDepth(IntVec3 a, IntVec3 b, IntVec3 facedir, int minDist)
        {
            if (minDist <= 0) return true;

            // depth: how far in front (along facedir) 'a' is from 'b'
            ComputeDepthLateral(b, a, facedir, out int depth, out _);

            // must be at least minDist tiles in front
            return depth >= minDist;
        }

        private static bool MeetsMaxDepth(IntVec3 a, IntVec3 b, IntVec3 facedir, int maxDist)
        {
            if (maxDist <= 0) return true;

            ComputeDepthLateral(b, a, facedir, out int depth, out _);

            // must be within maxDist tiles in front
            return depth <= maxDist;
        }

        private static IntVec3 FindFallbackSeatNear(
            Map map,
            Pawn teacher,
            IntVec3 faceDir,
            IntVec3 center,
            HashSet<IntVec3> blocked,
            IntVec3 teacherSeat,
            int stageMinDist,
            int maxDist,
            Room room)
        {
            for (int r = 2; r <= 12; r++)
            {
                foreach (var c in GenRadial.RadialCellsAround(center, r, true))
                {
                    if (!c.InBounds(map)) continue;
                    if (blocked.Contains(c)) continue;
                    if (!c.Standable(map)) continue;

                    if (room != null && c.GetRoom(map) != room) continue;

                    if (c.GetEdifice(map) is Building_Door) continue;

                    if (maxDist > 0 && !MeetsMaxDepth(c, teacherSeat, faceDir, maxDist)) continue;
                    if (stageMinDist > 0 && !MeetsMinDepth(c, teacherSeat, faceDir, stageMinDist)) continue;
                    if (teacher != null && teacher.Spawned && !teacher.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                    return c;
                }
            }
            return IntVec3.Invalid;
        }

        // ===== your existing helpers below (unchanged) =====

        private static void ComputeDepthLateral(IntVec3 origin, IntVec3 c, IntVec3 frontDir, out int depth, out int lateral)
        {
            int dx = c.x - origin.x;
            int dz = c.z - origin.z;

            if (frontDir == IntVec3.North) { depth = dz; lateral = dx; return; }
            if (frontDir == IntVec3.South) { depth = -dz; lateral = dx; return; }
            if (frontDir == IntVec3.East)  { depth = dx; lateral = dz; return; }
            depth = -dx; lateral = dz; // West
        }

        private static bool DetermineTeacherSeatAndDir(Map map, LessonVenue venue, Pawn teacher, out IntVec3 teacherSeat, out IntVec3 frontDir)
        {
            teacherSeat = IntVec3.Invalid;
            frontDir = IntVec3.North;

            Room room = venue.spotCell.GetRoom(map);

            if (venue.anchorInfo.IsValid && venue.anchorInfo.HasThing)
            {
                Thing a = venue.anchorInfo.Thing;
                if (a != null && a.Spawned)
                {
                    var candidates = new List<IntVec3>(8);
                    foreach (var c in GenAdj.CellsAdjacent8Way(a))
                    {
                        if (!c.InBounds(map)) continue;
                        if (!c.Standable(map)) continue;
                        if (room != null && c.GetRoom(map) != room) continue;
                        if (!teacher.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                        candidates.Add(c);
                    }

                    if (candidates.Count > 0)
                    {
                        IntVec3 bestSeat = candidates[0];
                        IntVec3 bestDir = IntVec3.North;
                        int bestScore = int.MinValue;

                        for (int i = 0; i < candidates.Count; i++)
                        {
                            var seat = candidates[i];
                            PickBestFrontDir(map, room, seat, out var dir, out int score);
                            if (score > bestScore)
                            {
                                bestScore = score;
                                bestSeat = seat;
                                bestDir = dir;
                            }
                        }

                        teacherSeat = bestSeat;
                        frontDir = bestDir;
                        return true;
                    }
                }
            }
            // Prefer wall-center on the longer side (tie -> vertical), and pick the wall that yields wider "front" area.
            if (venue.kind != LessonRoomKind.Outdoor && room != null && room.CellCount > 0)
            {
                // 1) bounds from room.Cells (robust even for irregular rooms)
                int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue;
                foreach (var c in room.Cells)
                {
                    if (c.x < minX) minX = c.x;
                    if (c.x > maxX) maxX = c.x;
                    if (c.z < minZ) minZ = c.z;
                    if (c.z > maxZ) maxZ = c.z;
                }

                int width = maxX - minX + 1;
                int height = maxZ - minZ + 1;

                // 2) choose orientation: longer side; tie -> vertical (north/south walls)
                bool useNorthSouthWalls = (height >= width);

                // helper: find standable reachable cell near wall center, 1 tile inward
                bool TryPickWallCenterSeat(bool northOrEast, out IntVec3 picked)
                {
                    picked = IntVec3.Invalid;

                    if (useNorthSouthWalls)
                    {
                        // North (maxZ) or South (minZ)
                        int z = northOrEast ? (maxZ - 1) : (minZ + 1);
                        if (z < minZ || z > maxZ) z = northOrEast ? maxZ : minZ;

                        int midX = (minX + maxX) / 2;
                        for (int step = 0; step <= (maxX - minX); step++)
                        {
                            // center, +1, -1, +2, -2...
                            int x = midX + ((step % 2 == 0) ? (step / 2) : -((step + 1) / 2));
                            if (x < minX || x > maxX) continue;

                            var c = new IntVec3(x, 0, z);
                            if (!c.InBounds(map)) continue;
                            if (!c.Standable(map)) continue;
                            if (c.GetRoom(map) != room) continue;
                            if (!teacher.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;

                            picked = c;
                            return true;
                        }
                    }
                    else
                    {
                        // East (maxX) or West (minX)
                        int x = northOrEast ? (maxX - 1) : (minX + 1);
                        if (x < minX || x > maxX) x = northOrEast ? maxX : minX;

                        int midZ = (minZ + maxZ) / 2;
                        for (int step = 0; step <= (maxZ - minZ); step++)
                        {
                            int z = midZ + ((step % 2 == 0) ? (step / 2) : -((step + 1) / 2));
                            if (z < minZ || z > maxZ) continue;

                            var c = new IntVec3(x, 0, z);
                            if (!c.InBounds(map)) continue;
                            if (!c.Standable(map)) continue;
                            if (c.GetRoom(map) != room) continue;
                            if (!teacher.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;

                            picked = c;
                            return true;
                        }
                    }

                    return false;
                }

                // 3) evaluate both candidate walls and pick the better one by "front cell count"
                IntVec3 bestSeat = IntVec3.Invalid;
                IntVec3 bestDir = IntVec3.North;
                int bestScore = int.MinValue;

                // northOrEast: true=North or East, false=South or West
                for (int i = 0; i < 2; i++)
                {
                    bool northOrEast = (i == 0);
                    if (!TryPickWallCenterSeat(northOrEast, out var seat)) continue;

                    PickBestFrontDir(map, room, seat, out var dir, out int score);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestSeat = seat;
                        bestDir = dir;
                    }
                }

                if (bestSeat.IsValid)
                {
                    teacherSeat = bestSeat;
                    frontDir = bestDir;
                    return true;
                }

                // fallback inside-room: first reachable standable (should be rare)
                foreach (var c in room.Cells)
                {
                    if (!c.InBounds(map)) continue;
                    if (!c.Standable(map)) continue;
                    if (!teacher.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                    teacherSeat = c;
                    PickBestFrontDir(map, room, teacherSeat, out frontDir, out _);
                    return true;
                }
            }

            // No room (outdoor) or failed: use spotCell if possible, else nearest reachable standable around it
            IntVec3 seed = venue.spotCell;
            if (!seed.InBounds(map) || !seed.Standable(map) || !teacher.CanReach(seed, PathEndMode.OnCell, Danger.Some))
            {
                seed = IntVec3.Invalid;
                foreach (var c in GenRadial.RadialCellsAround(venue.spotCell, 12f, true))
                {
                    if (!c.InBounds(map)) continue;
                    if (!c.Standable(map)) continue;
                    if (!teacher.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                    seed = c;
                    break;
                }
            }

            if (!seed.IsValid) return false;

            PickBestFrontDir(map, room: null, seed, out var dir2, out _);
            teacherSeat = seed;
            frontDir = dir2;
            return true;
        }


        private static void PickBestFrontDir(Map map, Room room, IntVec3 origin, out IntVec3 bestDir, out int bestScore)
        {
            IntVec3[] dirs = new[] { IntVec3.South, IntVec3.North, IntVec3.East, IntVec3.West };

            bestDir = IntVec3.South;
            bestScore = int.MinValue;

            for (int i = 0; i < dirs.Length; i++)
            {
                var dir = dirs[i];
                int score = CountFrontCells(map, room, origin, dir);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestDir = dir;
                }
            }
        }

        private static int CountFrontCells(Map map, Room room, IntVec3 origin, IntVec3 dir)
        {
            int count = 0;
            IEnumerable<IntVec3> cells = (room != null) ? room.Cells : GenRadial.RadialCellsAround(origin, 12f, true);

            foreach (var c in cells)
            {
                if (!c.InBounds(map)) continue;
                if (!c.Standable(map)) continue;
                if (room != null && c.GetRoom(map) != room) continue;
                if (IsInFront(origin, c, dir)) count++;
            }
            return count;
        }

        private static bool IsInFront(IntVec3 origin, IntVec3 c, IntVec3 dir)
        {
            if (dir == IntVec3.North) return c.z > origin.z;
            if (dir == IntVec3.South) return c.z < origin.z;
            if (dir == IntVec3.East)  return c.x > origin.x;
            if (dir == IntVec3.West)  return c.x < origin.x;
            return false;
        }

        private static void CollectSeatCandidates(
            Map map,
            Room room,
            IntVec3 teacherSeat,
            IntVec3 frontDir,
            HashSet<IntVec3> blocked,
            List<IntVec3> tier1,
            List<IntVec3> tier2)
        {
            // ====== TUNING ======
            const int MaxCol = 5; // 각 depth(줄)에서 가운데부터 우선으로 뽑을 칸 수

            IEnumerable<IntVec3> cells = (room != null)
                ? room.Cells
                : GenRadial.RadialCellsAround(teacherSeat, 18f, true);

            var t1All = new List<IntVec3>(512);
            var t2 = new List<IntVec3>(512);

            // frontMap: (depth, lat) -> best representative cell (closest to teacherSeat)
            var frontMap = new Dictionary<(int depth, int lat), IntVec3>(512);

            int maxAbsLatSeen = 0;
            int maxDepthSeen = 0;

            foreach (var c in cells)
            {
                if (!c.InBounds(map)) continue;
                if (!c.Standable(map)) continue;
                if (room != null && c.GetRoom(map) != room) continue;
                if (blocked.Contains(c)) continue;

                // keep "not adjacent" baseline; stageMinDist can further restrict
                if (c.DistanceToSquared(teacherSeat) <= 1) continue;

                if (IsInFront(teacherSeat, c, frontDir))
                {
                    ComputeDepthLateral(teacherSeat, c, frontDir, out int depth, out int lat);
                    if (depth <= 0) continue;

                    t1All.Add(c);

                    var key = (depth, lat);
                    if (!frontMap.TryGetValue(key, out var prev) ||
                        c.DistanceToSquared(teacherSeat) < prev.DistanceToSquared(teacherSeat))
                    {
                        frontMap[key] = c;
                    }

                    int absLat = Math.Abs(lat);
                    if (absLat > maxAbsLatSeen) maxAbsLatSeen = absLat;
                    if (depth > maxDepthSeen) maxDepthSeen = depth;
                }
                else
                {
                    t2.Add(c);
                }
            }

            // 0, +1, -1, +2, -2...
            static List<int> CenterOutLaterals(int count)
            {
                var list = new List<int>(Math.Max(1, count));
                list.Add(0);
                int k = 1;
                while (list.Count < count)
                {
                    list.Add(+k);
                    if (list.Count >= count) break;
                    list.Add(-k);
                    k++;
                }
                return list;
            }

            var centerLaterals = CenterOutLaterals(MaxCol);
            int half = MaxCol / 2;     // 5 => 2
            int startSide = half + 1;  // next side starts at 3 when MaxCol=5

            var preferred = new List<IntVec3>(512);
            var used = new HashSet<IntVec3>();

            // ===== Phase A: for every depth, take center MaxCol first (row-major across depth) =====
            for (int depth = 1; depth <= maxDepthSeen; depth++)
            {
                for (int i = 0; i < centerLaterals.Count; i++)
                {
                    int lat = centerLaterals[i];
                    if (frontMap.TryGetValue((depth, lat), out var cell))
                    {
                        if (used.Add(cell)) preferred.Add(cell);
                    }
                }
            }

            // ===== Phase B: then expand sides round-robin by depth, for increasing |lat| =====
            for (int side = startSide; side <= maxAbsLatSeen; side++)
            {
                for (int depth = 1; depth <= maxDepthSeen; depth++)
                {
                    if (frontMap.TryGetValue((depth, +side), out var cPos))
                        if (used.Add(cPos)) preferred.Add(cPos);

                    if (frontMap.TryGetValue((depth, -side), out var cNeg))
                        if (used.Add(cNeg)) preferred.Add(cNeg);
                }
            }

            // ===== Phase C: append all remaining front cells with old natural order as safety net =====
            t1All.Sort((a, b) =>
            {
                ComputeDepthLateral(teacherSeat, a, frontDir, out int da, out int la);
                ComputeDepthLateral(teacherSeat, b, frontDir, out int db, out int lb);

                int cmp = da.CompareTo(db);
                if (cmp != 0) return cmp;

                cmp = Math.Abs(la).CompareTo(Math.Abs(lb));
                if (cmp != 0) return cmp;

                return a.DistanceToSquared(teacherSeat).CompareTo(b.DistanceToSquared(teacherSeat));
            });

            foreach (var c in preferred) tier1.Add(c);
            foreach (var c in t1All)
                if (used.Add(c)) tier1.Add(c);

            // ===== tier2 unchanged =====
            t2.Sort((a, b) =>
            {
                ComputeDepthLateral(teacherSeat, a, frontDir, out int da, out int la);
                ComputeDepthLateral(teacherSeat, b, frontDir, out int db, out int lb);

                int cmp = da.CompareTo(db);
                if (cmp != 0) return cmp;

                cmp = Math.Abs(la).CompareTo(Math.Abs(lb));
                if (cmp != 0) return cmp;

                return a.DistanceToSquared(teacherSeat).CompareTo(b.DistanceToSquared(teacherSeat));
            });

            tier2.AddRange(t2);
        }

        private static bool IsUsableSeat(Map map, Room room, Pawn teacher, IntVec3 c)
        {
            if (!c.InBounds(map)) return false;
            if (!c.Standable(map)) return false;
            if (room != null && c.GetRoom(map) != room) return false;

            if (teacher != null && teacher.Spawned)
            {
                if (!teacher.CanReach(c, PathEndMode.OnCell, Danger.Some)) return false;
            }

            return true;
        }
    }
}