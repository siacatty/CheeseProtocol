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
    public class LordToil_Teacher_TakeSeats : LordToil
    {
        public Pawn teacher;

        public List<Pawn> students;
        public int arrived;
        private LessonVenue venue;
        private IntVec3 teacherSeat;
        private Dictionary<string, IntVec3> studentSeats;
        private IntVec3 teacherFaceDir;
        private int nextRotateTick = 0;
        private const int RotateInterval = 60;
        private int seated = 0;

        public LordToilData_Gathering Data => (LordToilData_Gathering)data;

        public LordToil_Teacher_TakeSeats(Pawn teacher, List<Pawn> students, LessonVenue venue)
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
            var job = lord.LordJob as LordJob_Teacher;
            teacher = job.teacher;
            students = job.students;
            venue = job.currentVenue;
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
            if (Find.TickManager.TicksGame < nextRotateTick)
                return;

            nextRotateTick = Find.TickManager.TicksGame + RotateInterval;

            if (teacher == null) return;
            var pawns = lord?.ownedPawns;
            if (pawns == null) return;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.Dead) continue;
                
                if (pawn == teacher)
                {
                    if (pawn.Position == teacherSeat)
                    {
                        pawn.rotationTracker.FaceCell(
                            teacherSeat + teacherFaceDir
                        );
                        pawn.rotationTracker.UpdateRotation();
                        if (!Data.presentForTicks.ContainsKey(pawn))
                        {
                            Data.presentForTicks.Add(pawn, 0);
                            seated ++;
                        }
                        Data.presentForTicks[pawn]++;
                    }
                }
                else
                {
                    if (studentSeats.TryGetValue(pawn.GetUniqueLoadID(), out var seat)
                        && pawn.Position == seat)
                    {
                        if (!Data.presentForTicks.ContainsKey(pawn))
                        {
                            Data.presentForTicks.Add(pawn, 0);
                            seated ++;
                        }
                        Data.presentForTicks[pawn]++;
                    }
                }
            }
            if (seated >= pawns.Count)
            {
                string text = LordChats.GetText(TeacherTextKey.LessonStart);
                SpeechBubbleManager.Get(Map)?.AddNPCChat(text, teacher, speaker:SpeakerType.NonHostileNPC);
                lord?.ReceiveMemo(LordJob_Teacher.MemoLessonStarted);
            }
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