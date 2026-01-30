using Verse;

namespace CheeseProtocol
{
    public class LessonVenue : IExposable
    {
        public LessonRoomKind kind;
        public int roomId;
        public IntVec3 roomKeyCell;   // room 재구성용
        public IntVec3 spotCell;      // 수업 중심 위치
        public LocalTargetInfo anchorInfo;
        public int roomCellCount;
        public int capacity;

        public LessonVenue()
        {
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref roomId, "roomId", 0);
            Scribe_Values.Look(ref roomCellCount, "roomCellCount", 0);
            Scribe_Values.Look(ref capacity, "capacity", 0);

            Scribe_Values.Look(ref roomKeyCell, "roomKeyCell");
            Scribe_Values.Look(ref spotCell, "spotCell");
            Scribe_TargetInfo.Look(ref anchorInfo, "anchorInfo");
        }

        public override string ToString()
            => $"{kind} roomId={roomId} size={roomCellCount} cap={capacity} spot={spotCell} anchor={anchorInfo.Thing?.LabelCap ?? "null"}";
    }
}