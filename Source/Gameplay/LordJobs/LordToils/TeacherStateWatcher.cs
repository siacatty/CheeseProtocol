using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CheeseProtocol
{
    public static class TeacherStateWatcher
    {
        private struct Snap
        {
            public int tick;

            public bool spawned;

            public string jobDef;
            public bool jobNull;
            public bool playerForced;
            public string curDriverType;

            public bool patherExists;
            public bool moving;
            public IntVec3 pos;
            public bool posNearEdge8;

            public bool destValid;
            public IntVec3 dest;
            public bool destNearEdge8;

            public bool inMental;
            public string mentalDef;
            public string mentalStateObjType;

            public int exitMapAfterTick;

            public string dutyDef;
            public bool dutyFocusValid;
            public IntVec3 dutyFocus;

            public int lordId;
            public int ownedCount;
            public bool inExpectedLord;

            public bool downed;
            public float hpPct;

            public bool hostileToPlayer;

            public bool draftedKnown;
            public bool drafted;
            public bool isColonist;
            public bool isPrisoner;
        }

        private static readonly System.Collections.Generic.Dictionary<int, Snap> Last
            = new System.Collections.Generic.Dictionary<int, Snap>();

        public static void WatchAndDumpOnChange(Pawn teacher, Lord expectedLord, string tag = "TeacherWatch")
        {
            if (teacher == null || teacher.DestroyedOrNull()) return;

            int now = Find.TickManager?.TicksGame ?? -1;
            int key = teacher.thingIDNumber;

            Snap cur = Capture(now, teacher, expectedLord);

            if (!Last.TryGetValue(key, out Snap prev))
            {
                Last[key] = cur;
                return;
            }

            bool changed =
                cur.spawned != prev.spawned ||

                cur.jobDef != prev.jobDef ||
                cur.jobNull != prev.jobNull ||
                cur.playerForced != prev.playerForced ||
                cur.curDriverType != prev.curDriverType ||

                cur.patherExists != prev.patherExists ||
                cur.moving != prev.moving ||
                cur.destValid != prev.destValid ||
                cur.dest != prev.dest ||
                cur.pos != prev.pos ||
                cur.posNearEdge8 != prev.posNearEdge8 ||
                cur.destNearEdge8 != prev.destNearEdge8 ||

                cur.inMental != prev.inMental ||
                cur.mentalDef != prev.mentalDef ||
                cur.mentalStateObjType != prev.mentalStateObjType ||

                cur.exitMapAfterTick != prev.exitMapAfterTick ||

                cur.dutyDef != prev.dutyDef ||
                cur.dutyFocusValid != prev.dutyFocusValid ||
                cur.dutyFocus != prev.dutyFocus ||

                cur.lordId != prev.lordId ||
                cur.ownedCount != prev.ownedCount ||
                cur.inExpectedLord != prev.inExpectedLord ||

                cur.downed != prev.downed ||
                cur.hpPct != prev.hpPct ||

                cur.hostileToPlayer != prev.hostileToPlayer ||

                cur.draftedKnown != prev.draftedKnown ||
                (cur.draftedKnown && cur.drafted != prev.drafted) ||
                cur.isColonist != prev.isColonist ||
                cur.isPrisoner != prev.isPrisoner;

            if (!changed) return;

            var sb = new StringBuilder(1600);
            sb.AppendLine($"[CheeseProtocol][{tag}] Teacher state CHANGED");
            sb.AppendLine($"  tick: {prev.tick} -> {cur.tick}");
            sb.AppendLine($"  pawn: {teacher.LabelShortCap} id={teacher.thingIDNumber} uid={teacher.GetUniqueLoadID()} spawned={cur.spawned}");
            sb.AppendLine();

            sb.AppendLine($"  job: {prev.jobDef} -> {cur.jobDef} (null {prev.jobNull}->{cur.jobNull}) playerForced {prev.playerForced}->{cur.playerForced}");
            sb.AppendLine($"  driver: {prev.curDriverType} -> {cur.curDriverType}");
            sb.AppendLine($"  patherExists: {prev.patherExists} -> {cur.patherExists}");
            sb.AppendLine($"  moving: {prev.moving} -> {cur.moving}");
            sb.AppendLine($"  pos: {prev.pos} -> {cur.pos} (nearEdge8 {prev.posNearEdge8}->{cur.posNearEdge8})");
            sb.AppendLine($"  dest: {(prev.destValid ? prev.dest.ToString() : "Invalid")} -> {(cur.destValid ? cur.dest.ToString() : "Invalid")} (nearEdge8 {prev.destNearEdge8}->{cur.destNearEdge8})");
            sb.AppendLine();

            sb.AppendLine($"  mental: {prev.inMental}/{prev.mentalDef}/{prev.mentalStateObjType} -> {cur.inMental}/{cur.mentalDef}/{cur.mentalStateObjType}");
            sb.AppendLine($"  exitMapAfterTick: {prev.exitMapAfterTick} -> {cur.exitMapAfterTick}");
            sb.AppendLine($"  duty: {prev.dutyDef} -> {cur.dutyDef} focus {(prev.dutyFocusValid ? prev.dutyFocus.ToString() : "none")} -> {(cur.dutyFocusValid ? cur.dutyFocus.ToString() : "none")}");
            sb.AppendLine();

            sb.AppendLine($"  lordId: {prev.lordId} -> {cur.lordId} expected={(expectedLord != null ? expectedLord.loadID.ToString() : "null")} inExpected {prev.inExpectedLord}->{cur.inExpectedLord}");
            sb.AppendLine($"  ownedCount: {prev.ownedCount} -> {cur.ownedCount}");
            sb.AppendLine();

            sb.AppendLine($"  downed: {prev.downed}->{cur.downed} hpPct: {prev.hpPct:0.###}->{cur.hpPct:0.###}");
            sb.AppendLine($"  hostileToPlayer: {prev.hostileToPlayer}->{cur.hostileToPlayer}");
            sb.AppendLine($"  drafted: {(prev.draftedKnown ? prev.drafted.ToString() : "n/a")} -> {(cur.draftedKnown ? cur.drafted.ToString() : "n/a")}");
            sb.AppendLine($"  isColonist: {prev.isColonist}->{cur.isColonist} isPrisoner: {prev.isPrisoner}->{cur.isPrisoner}");

            Log.Warning(sb.ToString());

            Last[key] = cur;
        }

        private static Snap Capture(int now, Pawn p, Lord expectedLord)
        {
            var s = new Snap();
            s.tick = now;

            s.spawned = p.Spawned;
            s.pos = p.Position;

            // Job
            Job curJob = p.jobs?.curJob;
            s.jobNull = (curJob == null || curJob.def == null);
            s.jobDef = s.jobNull ? "null" : curJob.def.defName;
            s.playerForced = curJob?.playerForced ?? false;
            s.curDriverType = p.jobs?.curDriver != null ? p.jobs.curDriver.GetType().Name : "null";

            // Pather snapshot -> store as booleans / dest only (NO Snap.pather)
            s.patherExists = (p.pather != null);
            s.moving = p.pather?.MovingNow ?? false;

            s.destValid = (p.pather != null) && p.pather.Destination.Cell.IsValid;
            s.dest = s.destValid ? p.pather.Destination.Cell : IntVec3.Invalid;

            if (p.Map != null && p.Position.IsValid && p.Position.InBounds(p.Map))
                s.posNearEdge8 = p.Position.CloseToEdge(p.Map, 8);

            if (p.Map != null && s.destValid && s.dest.InBounds(p.Map))
                s.destNearEdge8 = s.dest.CloseToEdge(p.Map, 8);

            // Mental
            s.inMental = p.InMentalState;
            var mh = p.mindState?.mentalStateHandler;
            s.mentalDef = mh?.CurStateDef != null ? mh.CurStateDef.defName : "null";
            s.mentalStateObjType = mh?.CurState != null ? mh.CurState.GetType().Name : "null";

            // Exit map
            s.exitMapAfterTick = p.mindState?.exitMapAfterTick ?? -999;

            // Duty
            var duty = p.mindState?.duty;
            s.dutyDef = duty?.def != null ? duty.def.defName : "null";
            s.dutyFocusValid = duty != null && duty.focus.IsValid;
            s.dutyFocus = (IntVec3)(s.dutyFocusValid ? duty.focus : IntVec3.Invalid);

            // Lord
            Lord curLord = p.GetLord();
            s.lordId = curLord != null ? curLord.loadID : -1;
            s.ownedCount = curLord?.ownedPawns?.Count ?? -1;
            s.inExpectedLord = (expectedLord != null && curLord == expectedLord);

            // Health
            s.downed = p.Downed;
            s.hpPct = p.health?.summaryHealth?.SummaryHealthPercent ?? -1f;

            // Faction
            s.hostileToPlayer = p.HostileTo(Faction.OfPlayer);

            // Draft / colonist / prisoner
            s.draftedKnown = (p.drafter != null);
            s.drafted = p.drafter?.Drafted ?? false;
            s.isColonist = p.IsColonist;
            s.isPrisoner = p.IsPrisoner;

            return s;
        }
    }
}
