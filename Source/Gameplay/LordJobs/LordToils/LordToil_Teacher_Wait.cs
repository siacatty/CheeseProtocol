using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimWorld;

namespace CheeseProtocol
{
    // Minimal "wait + begin dialog" toil:
    // - FloatMenu only
    // - DecoratePawnDuty removed
    // - Dialog_BeginRitual with ritual = null
    // - Students come from assignments.Participants (excluding teacher)
    public class LordToil_Teacher_Wait : LordToil_Wait
    {
        private Pawn teacher;
        private int maxStudents;
        private int nextRotateTick = 0;
        private const int RotateInterval = 900;

        public LordToil_Teacher_Wait(Pawn teacher, int maxStudents)
        {
            this.teacher = teacher;
            this.maxStudents = maxStudents;
        }

        public override void Init()
        {
            base.Init();
            var job = lord.LordJob as LordJob_Teacher;
            teacher = job.teacher;
            maxStudents = job.maxStudents;
            nextRotateTick = Find.TickManager.TicksGame + RotateInterval;
        }

        public override void LordToilTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now < nextRotateTick)
                return;
            nextRotateTick = Find.TickManager.TicksGame + RotateInterval;
            if (teacher == null) return;
            string text = LordChats.GetText(TeacherTextKey.Wait);
            SpeechBubbleManager.Get(Map)?.AddNPCChat(text, teacher, durationSec: 3f, speaker:SpeakerType.NonHostileNPC);
        }

        public override void DrawPawnGUIOverlay(Pawn pawn)
        {
            pawn.Map.overlayDrawer.DrawOverlay(pawn, OverlayTypes.QuestionMark);
        }



        public override IEnumerable<FloatMenuOption> ExtraFloatMenuOptions(Pawn target, Pawn forPawn)
        {
            if (target != teacher) yield break;

            var job = lord?.LordJob as LordJob_Teacher;
            if (job == null) yield break;

            yield return new FloatMenuOption("수업 시작하기", delegate
            {
                // "TargetInfo" here is the lesson 장소(spot). Pawn targets are chosen via Participants.
                TargetInfo lessonSpot = job.currentVenue?.anchorInfo.IsValid ?? false
                    ? job.currentVenue.anchorInfo.ToTargetInfo(teacher.Map)
                    : new TargetInfo(teacher.Position, teacher.Map);

                Find.WindowStack.Add(new Dialog_BeginCustom(
                    "참여 학생",
                    ritual: null,
                    target: lessonSpot,
                    map: teacher.Map,
                    action: (RitualRoleAssignments assignments) =>
                    {
                        var students = assignments?.Participants?
                            .Where(p => p != null && p != teacher)
                            .ToList() ?? new List<Pawn>();

                        StartLesson(job, students);
                        return true;
                    },
                    organizer: teacher,
                    obligation: null,
                    filter: delegate (Pawn pawn, bool voluntary, bool allowOtherIdeos)
                    {
                        if (pawn == null) return false;
                        if (pawn.GetLord()?.LordJob is LordJob_Ritual)
                        {
                            return false;
                        }
                        if (pawn.Faction != Faction.OfPlayer) return false;

                        if (pawn.IsSubhuman)
                        {
                            return false;
                        }

                        return !pawn.IsPrisonerOfColony && !pawn.RaceProps.Animal;
                    },
                    okButtonText: "Begin".Translate(),
                    requiredPawns: new List<Pawn> {},
                    forcedForRole: null,
                    outcome: null,
                    extraInfoText: null,
                    selectedPawn: null,
                    maxParticipants: maxStudents
                ));
            });
        }

        private void StartLesson(LordJob_Teacher job, List<Pawn> students)
        {
            if (teacher == null || students == null || students.Count == 0) return;

            // Detach from other voluntarily-joinable lords (vanilla pattern)
            for (int i = 0; i < students.Count; i++)
            {
                var p = students[i];
                if (p?.GetLord()?.LordJob is LordJob_VoluntarilyJoinable joinableLordJob)
                {
                    p.GetLord().Notify_PawnLost(p, PawnLostCondition.LeftVoluntarily);
                }
            }

            // Add to this lord
            lord.AddPawns(students);
            // Persist to LordJob (your implementation)
            job.AddStudents(students);
            // Advance state
            string text = LordChats.GetText(TeacherTextKey.GatherStudents);
            SpeechBubbleManager.Get(Map)?.AddNPCChat(text, teacher, speaker:SpeakerType.NonHostileNPC);
            lord.ReceiveMemo(LordJob_Teacher.MemoGatherStudents);
            // Clean-up: undraft + wake
            for (int i = 0; i < students.Count; i++)
            {
                var p = students[i];
                if (p == null) continue;

                if (p.drafter != null) p.drafter.Drafted = false;
                if (!p.Awake()) RestUtility.WakeUp(p);
            }
        }
    }
}