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
        private LessonVenue venue;
        private IntVec3 teacherSeat;
        private Dictionary<string, IntVec3> studentSeats;
        private IntVec3 teacherFaceDir;
        private int nextRotateTick = 0;
        private Pawn escapingStudent;
        private const int RotateInterval = 30;

        public LordToilData_Gathering Data => (LordToilData_Gathering)data;

        public LordToil_Teacher_TeachLesson(Pawn teacher, List<Pawn> students, LessonVenue venue)
        {
            this.teacher = teacher;
            this.students = students;
            this.venue = venue;
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
            venue = job.currentVenue;
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
            teacherSeat = job.teacherSeat;
            studentSeats = job.studentSeats;
            teacherFaceDir = job.teacherFaceDir;
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

            IntVec3 spot = venue.spotCell;
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
                        var faceCell = teacherSeat + teacherFaceDir;
                        if (CanStartTeachJobNow(teacher, faceCell, teacherSeat))
                            StartTeachJob(teacher, faceCell, teacher, teacherSeat);
                    }
                }
                else
                {
                    bool inMental = pawn.InMentalState;
                    bool inClass = LessonPosUtility.InGatheringArea(pawn.Position, spot, Map, dist: 18f, maxRoomCell: 170);
                    if (!inClass || inMental)
                    {
                        escapingStudent = pawn;
                        break;
                    }
                    if (!pawn.Drafted && studentSeats.TryGetValue(pawn.GetUniqueLoadID(), out var seat))
                    {
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
            IntVec3 spot = ((LordJob_Teacher)lord.LordJob).GetSpot();
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
                    if (pawn.Position != teacherSeat)
                        teacherDuty = new PawnDuty(DutyDefOf.Goto, teacherSeat);
                    else
                    {
                        teacherDuty = new PawnDuty(DutyDefOf.Idle);
                    }
                    pawn.mindState.duty = teacherDuty;
                }
                else
                {
                    if (studentSeats.TryGetValue(pawn.GetUniqueLoadID(), out IntVec3 seat))
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