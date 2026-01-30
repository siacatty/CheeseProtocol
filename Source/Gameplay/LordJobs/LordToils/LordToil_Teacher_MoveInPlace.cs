using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using static CheeseProtocol.CheeseLog;

namespace CheeseProtocol
{
    public class LordToil_Teacher_MoveInPlace : LordToil
    {

        private Pawn teacher;
        private LessonVenue venue;
        private bool sentArrived;

        public LordToil_Teacher_MoveInPlace(Pawn teacher, LessonVenue venue)
        {
            this.teacher = teacher;
            this.venue = venue;
        }

        public override void Init()
        {
            base.Init();
            UpdateAllDuties();
            // teacher -> anchor
            var job = lord.LordJob as LordJob_Teacher;
            teacher = job.teacher;
            venue = job.currentVenue;
            if (teacher != null && teacher.Spawned && !teacher.Downed && !teacher.Dead && venue != null && venue.spotCell.IsValid)
            {
                var j = JobMaker.MakeJob(JobDefOf.Goto, venue.spotCell);
                j.locomotionUrgency = LocomotionUrgency.Walk;
                teacher.jobs?.TryTakeOrderedJob(j);
            }
        }

        public override void UpdateAllDuties()
        {
            if (teacher == null || teacher.Dead || venue == null || !venue.spotCell.IsValid) return;
            teacher.mindState.duty = new PawnDuty(DutyDefOf.Defend, venue.spotCell);
        }

        public override void LordToilTick()
        {
            base.LordToilTick();

            if (sentArrived) return;
            if (lord == null) return;
            if (teacher == null || teacher.Dead || !teacher.Spawned) return;
            if (venue == null) return;
            if (!venue.spotCell.IsValid) return;
            if (teacher.Position == venue.spotCell)
            {
                string text = LordChats.GetText(TeacherTextKey.Arrived, teacher.NameShortColored);
                SpeechBubbleManager.Get(Map)?.AddNPCChat(text, teacher, speaker:SpeakerType.NonHostileNPC);
                sentArrived = true;
                lord.ReceiveMemo(LordJob_Teacher.MemoArrived);
            }
        }
    }
}