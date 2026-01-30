using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using static CheeseProtocol.CheeseLog;


namespace CheeseProtocol
{
    public class LordToil_Teacher_GatherStudents : LordToil
    {
        public Pawn teacher;

        public List<Pawn> students;
        public int arrived;
        private LessonVenue venue;

        public LordToilData_Gathering Data => (LordToilData_Gathering)data;

        public LordToil_Teacher_GatherStudents(Pawn teacher, List<Pawn> students)
        {
            this.teacher = teacher;
            this.students = students;
            arrived = 0;
            data = new LordToilData_Gathering();
        }

        public override void Init()
        {
            base.Init();
            var job = lord.LordJob as LordJob_Teacher;
            if (teacher == null)
                teacher = job.teacher;
            if (students == null)
                students = job.students;
            venue = job.UpdateVenue(lord.ownedPawns.Count);
            TeacherTextKey textKey = TeacherTextKey.GatherStudents;
            switch (venue.kind)
            {
                case LessonRoomKind.Outdoor:
                    textKey = TeacherTextKey.GatherStudentsOutdoor;
                    break;
                case LessonRoomKind.Plain:
                    textKey = TeacherTextKey.GatherStudentsPlain;
                    break;
                case LessonRoomKind.Table:
                    textKey = TeacherTextKey.GatherStudentsTable;
                    break;
                case LessonRoomKind.Blackboard:
                    textKey = TeacherTextKey.GatherStudentsBlackboard;
                    break;
                default:
                    break;
            }
            string text = LordChats.GetText(textKey, teacher.NameShortColored);
            SpeechBubbleManager.Get(Map)?.AddNPCChat(text, teacher, speaker:SpeakerType.NonHostileNPC);
            if (!teacher.Awake())
            {
                RestUtility.WakeUp(teacher);
            }
        }

        public override void LordToilTick()
        {
            if (teacher == null) return;
            List<Pawn> ownedPawns = lord.ownedPawns;
            for (int i = 0; i < ownedPawns.Count; i++)
            {
                if (GatheringsUtility.InGatheringArea(ownedPawns[i].Position, venue.spotCell, base.Map))
                {
                    if (!Data.presentForTicks.ContainsKey(ownedPawns[i]))
                    {
                        Data.presentForTicks.Add(ownedPawns[i], 0);
                        arrived ++;
                    }

                    Data.presentForTicks[ownedPawns[i]]++;
                }
            }
            if (arrived >= ownedPawns.Count)
            {
                string text = LordChats.GetText(TeacherTextKey.TakeSeats);
                SpeechBubbleManager.Get(Map)?.AddNPCChat(text, teacher, speaker:SpeakerType.NonHostileNPC);
                lord.ReceiveMemo(LordJob_Teacher.MemoTakeSeats);
            }
        }
        public override void UpdateAllDuties()
        {
            //IntVec3 spot = ((LordJob_Teacher)lord.LordJob).GetSpot();
            IntVec3 spot = venue.spotCell;
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pawn = lord.ownedPawns[i];
                if (!pawn.Awake())
                {
                    RestUtility.WakeUp(pawn);
                }

                if (pawn == teacher)
                {
                    PawnDuty pawnDuty = new PawnDuty(DutyDefOf.Goto, spot);
                    //pawnDuty.focus = students[0] == null ? spot : students[0];
                    pawn.mindState.duty = pawnDuty;
                }
                else
                {
                    PawnDuty pawnDuty2 = new PawnDuty(DutyDefOf.WanderClose, spot);
                    //pawnDuty2.spectateRect = CellRect.CenteredOn(spot, 0);
                    //pawnDuty2.spectateRectAllowedSides = SpectateRectSide.All;
                    //pawnDuty2.spectateDistance = new IntRange(2, 2);
                    pawn.mindState.duty.wanderRadius = 20;
                    pawn.mindState.duty = pawnDuty2;
                    
                }

                pawn.jobs?.CheckForJobOverride();
            }
        }
    }
}