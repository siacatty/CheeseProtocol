using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CheeseProtocol
{
    public class MapComponent_TeacherWatch : MapComponent
    {
        public MapComponent_TeacherWatch(Map map) : base(map) { }

        // UID로 추적 (너 스타일대로)
        private string teacherUid;
        private int expectedLordId = -1;

        // spam 방지
        private int lastDumpTick = -999999;

        public void Arm(string teacherUniqueLoadId, int lordId)
        {
            teacherUid = teacherUniqueLoadId;
            expectedLordId = lordId;
            lastDumpTick = -999999;
        }

        public void Disarm()
        {
            teacherUid = null;
            expectedLordId = -1;
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (teacherUid.NullOrEmpty()) return;

            // 너무 자주 돌 필요 없음
            if (Find.TickManager.TicksGame % 15 != 0) return;

            Pawn teacher = FindTeacherByUid(map, teacherUid);
            if (teacher == null || teacher.DestroyedOrNull() || !teacher.Spawned) return;

            bool lordMissingOrChanged = false;
            Lord curLord = teacher.GetLord();
            int curId = curLord != null ? curLord.loadID : -1;
            if (expectedLordId >= 0 && curId != expectedLordId)
                lordMissingOrChanged = true;

            bool inMental = teacher.InMentalState;

            JobDef jd = teacher.jobs?.curJob?.def;
            string defName = jd?.defName ?? "null";
            bool exitLike =
                jd == JobDefOf.Flee ||
                jd == JobDefOf.Goto ||
                defName.Contains("Exit") || defName.Contains("Flee") || defName.Contains("Panic") || defName.Contains("Leave");

            if (!(lordMissingOrChanged || inMental || exitLike))
                return;

            int now = Find.TickManager.TicksGame;
            if (now - lastDumpTick < 60) return; // cooldown
            lastDumpTick = now;

            Log.Warning(BuildDump(teacher, curLord, curId, lordMissingOrChanged, inMental, exitLike));
            DumpLordIdentity(curLord);
            TeacherGraphSurgery.Dump(curLord?.Graph, "AtPanicMoment");
        }

        private static void DumpLordIdentity(Lord lord)
        {
            if (lord == null)
            {
                Log.Warning("[CheeseProtocol][Teacher] lord=null");
                return;
            }

            var lj = lord.LordJob;
            var curToil = lord.CurLordToil;

            Log.Warning(
                $"[CheeseProtocol][Teacher] Lord identity:\n" +
                $"  lordID={lord.loadID}\n" +
                $"  lordJobType={(lj != null ? lj.GetType().FullName : "null")}\n" +
                $"  curToilType={(curToil != null ? curToil.GetType().FullName : "null")}\n" +
                $"  faction={(lord.faction != null ? lord.faction.def.defName : "null")}"
            );
        }
        private static Pawn FindTeacherByUid(Map map, string uid)
        {
            // 가장 안정적: spawned pawn 리스트에서 UID 매칭
            var pawns = map?.mapPawns?.AllPawnsSpawned;
            if (pawns == null) return null;

            for (int i = 0; i < pawns.Count; i++)
            {
                var p = pawns[i];
                if (p == null) continue;
                if (p.GetUniqueLoadID() == uid)
                    return p;
            }
            return null;
        }

        private static string BuildDump(Pawn t, Lord curLord, int curLordId, bool lordChanged, bool inMental, bool exitLike)
        {
            var sb = new StringBuilder(1400);
            int now = Find.TickManager.TicksGame;

            sb.AppendLine("[CheeseProtocol][TeacherMapWatch] IMPORTANT teacher state");
            sb.AppendLine($"  tick={now} pawn={t.LabelShortCap} id={t.thingIDNumber} uid={t.GetUniqueLoadID()}");
            sb.AppendLine($"  trigger: lordChanged={lordChanged} inMental={inMental} exitLike={exitLike}");
            sb.AppendLine();

            sb.AppendLine($"  lord.cur={(curLord != null ? curLordId.ToString() : "null")} ownedCount={(curLord?.ownedPawns?.Count ?? -1)}");
            sb.AppendLine($"  job.def={(t.jobs?.curJob?.def != null ? t.jobs.curJob.def.defName : "null")} playerForced={(t.jobs?.curJob?.playerForced ?? false)} driver={(t.jobs?.curDriver != null ? t.jobs.curDriver.GetType().Name : "null")}");
            sb.AppendLine($"  moving={(t.pather?.MovingNow ?? false)} pos={t.Position} dest={(t.pather != null ? t.pather.Destination.Cell.ToString() : "n/a")}");

            var mh = t.mindState?.mentalStateHandler;
            sb.AppendLine($"  mental.in={t.InMentalState} def={(mh?.CurStateDef != null ? mh.CurStateDef.defName : "null")} stateObj={(mh?.CurState != null ? mh.CurState.GetType().Name : "null")}");
            sb.AppendLine($"  exitMapAfterTick={(t.mindState?.exitMapAfterTick ?? -999)} duty={(t.mindState?.duty?.def != null ? t.mindState.duty.def.defName : "null")}");
            sb.AppendLine($"  faction={(t.Faction != null ? t.Faction.def.defName : "null")} hostileToPlayer={t.HostileTo(Faction.OfPlayer)} downed={t.Downed}");

            return sb.ToString();
        }
    }
}
