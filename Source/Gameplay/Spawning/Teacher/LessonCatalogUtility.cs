using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace CheeseProtocol
{
    public enum LessonRoomKind
    {
        Blackboard,
        Table,
        Plain,
        Outdoor
    }

    public class LessonVenueCatalog
    {
        public List<LessonVenue> blackboards = new List<LessonVenue>();
        public List<LessonVenue> tables = new List<LessonVenue>();
        public List<LessonVenue> plains = new List<LessonVenue>();

        public LessonVenue PickInitial()
        {
            if (!blackboards.NullOrEmpty()) return blackboards[0];
            if (!tables.NullOrEmpty()) return tables[0];
            if (!plains.NullOrEmpty()) return plains[0];
            return null;
        }
    }

    public static class LessonCatalogUtility
    {
        public static LessonVenueCatalog BuildLessonVenueCatalog(Pawn teacher, int minRoomCells = 6)
        {
            var cat = new LessonVenueCatalog();
            Map map = teacher.Map;
            if (map?.regionGrid?.AllRooms == null) return cat;

            // 1) 방 후보 수집 (실내 + 최소 크기)
            var rooms = map.regionGrid.AllRooms
                .Where(r => r != null && !r.TouchesMapEdge && r.CellCount >= minRoomCells)
                .ToList();

            if (rooms.Count == 0) return cat;

            // 2) roomId 기준 빠른 조회
            var roomById = new Dictionary<int, Room>(rooms.Count);
            for (int i = 0; i < rooms.Count; i++)
            {
                var r = rooms[i];
                roomById[r.ID] = r;
            }

            // 3) 각 룸에 있는 anchor 후보(blackboard/table) 1개씩 기록
            //    - building을 돌면서 b.GetRoom()으로 매핑 (빠름)
            var bbAnchorByRoomId = new Dictionary<int, Building>();
            var tableAnchorByRoomId = new Dictionary<int, Building>();

            // colonist buildings만 볼지/전체를 볼지 선택:
            // - "수업 공간"은 보통 내 기지 내부라 colonist만으로도 충분하지만,
            // - 유적/맵 구조물까지 포함하고 싶으면 allBuildingsNonColonist도 합치면 됨.
            var buildings = map.listerBuildings?.allBuildingsColonist;
            if (buildings != null)
            {
                for (int i = 0; i < buildings.Count; i++)
                {
                    var b = buildings[i];
                    if (b == null || b.DestroyedOrNull() || !b.Spawned) continue;

                    var room = b.GetRoom();
                    if (room == null) continue;
                    if (!roomById.ContainsKey(room.ID)) continue; // 최소크기/실내 필터 통과한 방만

                    // Blackboard
                    if (b.def == ThingDefOf.Blackboard)
                    {
                        if (!bbAnchorByRoomId.ContainsKey(room.ID))
                            bbAnchorByRoomId[room.ID] = b;
                        continue;
                    }

                    // Table (식탁/책상 포함)
                    if (b.def != null && b.def.IsTable)
                    {
                        if (!tableAnchorByRoomId.ContainsKey(room.ID))
                            tableAnchorByRoomId[room.ID] = b;
                    }
                }
            }

            const PathEndMode PEM = PathEndMode.OnCell;
            const Danger DG = Danger.Some;

            // 4) 룸별로 venue 생성 (중복 없이 하나씩: Blackboard > Table > Plain)
            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                Building anchor = null;
                LocalTargetInfo anchorInfo = null;
                LessonRoomKind kind;

                if (bbAnchorByRoomId.TryGetValue(room.ID, out anchor))
                {
                    kind = LessonRoomKind.Blackboard;
                }
                else if (tableAnchorByRoomId.TryGetValue(room.ID, out anchor))
                {
                    kind = LessonRoomKind.Table;
                }
                else
                {
                    kind = LessonRoomKind.Plain;
                }

                IntVec3 spot = IntVec3.Invalid;

                if (anchor != null && anchor.Spawned)
                {
                    anchorInfo = new LocalTargetInfo(anchor);
                    if (!TryPickSpotNearAnchorInRoom(map, room, anchor, out spot))
                    {
                        TryPickAnyStandableInRoom(map, room, out spot);
                    }
                }
                else
                {
                    TryPickAnyStandableInRoom(map, room, out spot);
                }

                if (!spot.IsValid) continue;
                if (!teacher.CanReach(spot, PEM, DG)) continue;

                var venue = new LessonVenue
                {
                    kind = kind,
                    roomId = room.ID,
                    roomKeyCell = spot,
                    spotCell = spot,
                    anchorInfo = anchorInfo,
                    roomCellCount = room.CellCount,
                    capacity = CountStandableCellsInRoom(map, room)
                };

                switch (kind)
                {
                    case LessonRoomKind.Blackboard: cat.blackboards.Add(venue); break;
                    case LessonRoomKind.Table:      cat.tables.Add(venue); break;
                    default:                        cat.plains.Add(venue); break;
                }
            }

            // 5) 각 그룹: 큰 방부터 정렬
            cat.blackboards.Sort((a, b) => b.roomCellCount.CompareTo(a.roomCellCount));
            cat.tables.Sort((a, b) => b.roomCellCount.CompareTo(a.roomCellCount));
            cat.plains.Sort((a, b) => b.roomCellCount.CompareTo(a.roomCellCount));

            return cat;
        }

        private static int CountStandableCellsInRoom(Map map, Room room)
        {
            int count = 0;
            foreach (var c in room.Cells)
            {
                if (!c.InBounds(map)) continue;
                if (!c.Standable(map)) continue;
                if (c.GetRoom(map) != room) continue;
                count++;
            }
            return count;
        }

        private static bool TryPickSpotNearAnchorInRoom(Map map, Room room, Building anchor, out IntVec3 spot)
        {
            spot = IntVec3.Invalid;
            foreach (IntVec3 c in GenAdj.CellsAdjacent8Way(anchor))
            {
                if (!c.InBounds(map)) continue;
                if (!c.Standable(map)) continue;
                if (c.GetRoom(map) != room) continue;
                spot = c;
                return true;
            }
            return false;
        }

        private static bool TryPickAnyStandableInRoom(Map map, Room room, out IntVec3 spot)
        {
            spot = IntVec3.Invalid;
            foreach (var c in room.Cells)
            {
                if (!c.InBounds(map)) continue;
                if (!c.Standable(map)) continue;
                if (c.GetRoom(map) != room) continue;
                spot = c;
                return true;
            }
            return false;
        }

        public static bool CanFitStudents(LessonVenue v, int studentCount)
        {
            // 야외 venue면 room 제약이 없으니 그냥 true 처리(자리 뽑기에서 해결)
            if (v == null) return false;
            if (!v.spotCell.IsValid) return false;

            // 실내: venue.capacity는 "standable 상한"이니 1차 필터로 충분
            // (정밀 체크는 TeachLesson에서 seat list 만들 때 하게 해도 됨)
            return v.capacity >= studentCount;
        }

        public static LessonVenue FindFirstFittable(List<LessonVenue> list, int studentCount)
        {
            if (list.NullOrEmpty()) return null;
            for (int i = 0; i < list.Count; i++)
            {
                var v = list[i];
                if (CanFitStudents(v, studentCount))
                    return v;
            }
            return null;
        }

        private static bool DestroyedOrNull(this Thing t) => t == null || t.Destroyed;
    }
}