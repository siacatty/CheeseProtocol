using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using static CheeseProtocol.CheeseLog;


namespace CheeseProtocol
{
    public class LordToil_Teacher_TeachLesson : LordToil
    {
        public Pawn teacher;

        public List<Pawn> students;
        public int arrived;
        private int nextRotateTick = 0;
        private Pawn escapingStudent;
        private const int RotateInterval = 30;

        public LordToilData_Gathering Data => (LordToilData_Gathering)data;

        public LordToil_Teacher_TeachLesson(Pawn teacher, List<Pawn> students, LessonVenue venue)
        {
            this.teacher = teacher;
            this.students = students;
            arrived = 0;
            data = new LordToilData_Gathering();
        }
        public override void Init()
        {
            base.Init();
            var job = (LordJob_Teacher)lord?.LordJob;
            if (job == null) return;
            teacher = job.teacher;
            students = job.students;
            if (job.lastLessonStartTick < 0)
            { 
            }
            else
            {
                escapingStudent = null;
            }
            job.RemoveTeacherBuff();
            if (!job.teacherSeat.IsValid || job.studentSeats == null || job.studentSeats.Count < students.Count)
                job.ReassignSeats();
            if (!teacher.Awake())
            {
                RestUtility.WakeUp(teacher);
            }
        }

        public override void LordToilTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now < nextRotateTick)
                return;
            if (teacher == null) return;
            var lj = lord.LordJob as LordJob_Teacher;
            if (lj == null) return;
            nextRotateTick = Find.TickManager.TicksGame + RotateInterval;

            IntVec3 spot = lj.currentVenue.spotCell;
            var pawns = lord?.ownedPawns;
            if (pawns == null) return;
            if (pawns.Count == 1 && pawns[0] == teacher)
            {
                string text = LordChats.GetText(TeacherTextKey.EndLessonFail);
                SpeechBubbleManager.Get(Map)?.AddNPCChat(text, teacher, speaker:SpeakerType.NonHostileNPC);
                lord?.ReceiveMemo(LordJob_Teacher.MemoLessonFailed);
                return;
            }
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.Dead) continue;

                if (pawn == teacher)
                {
                    if (escapingStudent != null)
                    {
                        pawn.rotationTracker.FaceTarget(escapingStudent);
                        pawn.rotationTracker.UpdateRotation();
                        lord?.ReceiveMemo(LordJob_Teacher.MemoEscapeDetected);
                        break;
                    }
                    else
                    {
                        if (!lj.teacherSeat.IsValid || !lj.teacherSeat.Walkable(Map))
                            lj.ReassignSeats();
                        var faceCell = lj.teacherSeat + lj.teacherFaceDir;
                        if (CanStartTeachJobNow(teacher, faceCell, lj.teacherSeat))
                            StartTeachJob(teacher, faceCell, teacher, lj.teacherSeat);
                    }
                }
                else
                {
                    bool canReach = true;//teacher.CanReach(pawn, PathEndMode.Touch, Danger.Some);
                    bool inMental = pawn.InMentalState;
                    bool inClass = LessonPosUtility.InGatheringArea(pawn.Position, spot, Map);
                    if (canReach && (!inClass || inMental))
                    {
                        escapingStudent = pawn;
                        break;
                    }
                    if (!pawn.Drafted && lj.studentSeats.TryGetValue(pawn.GetUniqueLoadID(), out var seat))
                    {
                        if (!seat.IsValid || !seat.Walkable(Map))
                            lj.ReassignSeats();
                        if (pawn.Position == seat)
                        {
                            if (teacher != null)
                            {
                                pawn.mindState.duty = new PawnDuty(DutyDefOf.Idle);
                                pawn.rotationTracker.FaceTarget(teacher);
                                pawn.rotationTracker.UpdateRotation();
                            }
                        }
                        else
                        {
                            pawn.mindState.duty = new PawnDuty(DutyDefOf.Goto, seat);
                        }
                    }
                }
            }
        }

        private void StartTeachJob(Pawn teacher, IntVec3 faceCell, Pawn progressBarPawn, IntVec3 teacherSeat)
        {
            if (teacher == null) return;
            JobDef def = DefDatabase<JobDef>.GetNamedSilentFail("CheeseProtocol_TeacherTeach");
            if (def == null) return;

            Job job = JobMaker.MakeJob(def, faceCell, progressBarPawn, teacherSeat);
            job.playerForced = true;

            teacher.jobs.StartJob(job, JobCondition.InterruptForced, resumeCurJobAfterwards: false);
        }
        private bool CanStartTeachJobNow(Pawn teacher, IntVec3 faceCell, IntVec3 teacherSeat)
        {
            if (teacher == null || !faceCell.IsValid) return false;
            if (teacher.Downed || teacher.Dead) return false;
            var lj = lord?.LordJob as LordJob_Teacher;
            if (lj == null) return false;
            if (lj.lessonProgressTicks >= LordJob_Teacher.LessonTotalTicks) return false;
            var cur = teacher.CurJob;
            if (cur == null) return true;

            if (cur.def.defName == "CheeseProtocol_TeacherTeach"&& cur.targetA.Cell == faceCell && cur.targetC.Cell == teacherSeat)
                return false;
            return true;
        }
        
        public override void UpdateAllDuties()
        {
            var lj = lord?.LordJob as LordJob_Teacher;
            if (lj == null) return;
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pawn = lord.ownedPawns[i];
                if (!pawn.Awake())
                {
                    RestUtility.WakeUp(pawn);
                }

                if (pawn == teacher)
                {
                    PawnDuty teacherDuty;
                    if (pawn.Position != lj.teacherSeat)
                        teacherDuty = new PawnDuty(DutyDefOf.Goto, lj.teacherSeat);
                    else
                    {
                        teacherDuty = new PawnDuty(DutyDefOf.Idle);
                    }
                    pawn.mindState.duty = teacherDuty;
                }
                else
                {
                    if (lj.studentSeats.TryGetValue(pawn.GetUniqueLoadID(), out IntVec3 seat))
                    {
                        PawnDuty studentDuty;
                        if (pawn.Position != seat)
                            studentDuty = new PawnDuty(DutyDefOf.Goto, seat);
                        else
                        {
                            studentDuty = new PawnDuty(DutyDefOf.Idle);
                        }
                        pawn.mindState.duty = studentDuty;
                    }
                }
            }
        }
    }
}