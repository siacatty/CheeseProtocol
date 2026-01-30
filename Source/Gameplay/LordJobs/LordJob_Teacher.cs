using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using static CheeseProtocol.CheeseLog;

namespace CheeseProtocol
{
    public class LordJob_Teacher : LordJob
    {
        // ========= Runtime =========
        public Pawn teacher;
        public List<Pawn> students = new();
        private bool needsSeatReassign = true;
        private float passionUpProb;
        private LordToil_Teacher_GatherStudents gather;
        private LordToil_Teacher_TakeSeats seat;
        public HashSet<string> escapingStudentUIDs;
        public HashSet<string> stunnedStudentUIDs;
        public HashSet<string> lostStudentUIDs;
        public Dictionary<string, Pawn> lostCache;

        // ========= Persisted =========
        private string teacherUid;
        private List<string> studentUids = new();
        //private LordToil exitToil;
        public string currentTargetUid;
        public LessonVenueCatalog catalog;
        public LessonVenue currentVenue;
        public int maxStudents;
        public IntVec3 teacherSeat;
        public Dictionary<string, IntVec3> studentSeats;
        public IntVec3 teacherFaceDir;
        public List<string> escapingStudentUIDList;
        public List<string> stunnedStudentUIDList;
        public List<string> lostStudentUIDList;
        public int lessonProgressTicks;
        public int lastLessonStartTick = -1;

        public const int LessonTotalTicks = 6000;
        public const string MemoGatherStudents = "GatherStudents";
        public const string MemoLessonStarted = "LessonStarted";
        public const string MemoTakeSeats = "LessonTakeSeats";
        public const string MemoArrived = "TeacherArrived";
        public const string MemoTightSpace = "TightSpace";
        public const string MemoEscapeDetected = "EscapeDetected";
        public const string MemoSubdueFinished = "SubdueFinished";
        public const string MemoCarryFinished = "CarryFinished";
        public const string MemoResumeLesson = "ResumeLesson";
        public const string MemoLessonComplete = "LessonComplete";
        public const string MemoLessonFailed = "LessonFailed";
        public const string MemoEatSuccess = "EatSuccess";
        public const string MemoEatFail = "EatFail";
        public const string ColorPositive = "#2e8032ff";
        public const string ColorNegative = "#aa4040ff";

        public int lastHasEscapeTick;
        public int nextShoutTick;
        public int shoutWindowUntilTick;
        public const int ShoutTeacherDelayTicks = 90;
        public const int ResumeLessonTicks = 180;
        public const int ShoutWindowTicks = 90;
        public const int ShoutCooldown = 300;
        public const int StunDuration = 300;
        public const float CatchDistance = 25f;

        public TeacherOutcome outcome;

        public LordJob_Teacher() { }

        public LordJob_Teacher(Pawn teacher, TeacherRequest req)
        {
            maxStudents = req.studentCount;
            outcome = new TeacherOutcome(req.teachSkill, req.passionProb);
            passionUpProb = req.passionProb;
            this.teacher = teacher;
            teacherUid = teacher?.GetUniqueLoadID();
            students = new List<Pawn>();
            escapingStudentUIDs = new();
            lostStudentUIDs = new();
            stunnedStudentUIDs = new();
            lostCache = new();

            catalog = LessonCatalogUtility.BuildLessonVenueCatalog(teacher, minRoomCells: 6);
            currentVenue = catalog.PickInitial();
            if (currentVenue == null)
                currentVenue = MakeOutdoorVenue();
        }

        public override void ExposeData()
        {
            // --- save ---
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                teacherUid = teacher?.GetUniqueLoadID();
                studentUids = students
                    .Where(p => p != null)
                    .Select(p => p.GetUniqueLoadID())
                    .ToList();
                escapingStudentUIDList = escapingStudentUIDs?.ToList();
                stunnedStudentUIDList = stunnedStudentUIDs?.ToList();
                lostStudentUIDList = lostStudentUIDs?.ToList();
            }

            Scribe_Values.Look(ref teacherUid, "teacherUid");
            Scribe_Values.Look(ref currentTargetUid, "currentTargetUid");
            Scribe_Values.Look(ref maxStudents, "maxStudents");
            Scribe_Collections.Look(ref studentUids, "studentUids", LookMode.Value);

            Scribe_Values.Look(ref teacherSeat, "teacherSeat");
            Scribe_Values.Look(ref teacherFaceDir, "teacherFaceDir");

            Scribe_Collections.Look(ref studentSeats, "studentSeats",
                LookMode.Value,   // key: string UID
                LookMode.Value    // value: IntVec3
            );
            Scribe_Values.Look(ref lessonProgressTicks, "lessonProgressTicks", 0);
            Scribe_Values.Look(ref lastLessonStartTick, "lastLessonStartTick", -1);

            Scribe_Collections.Look(ref escapingStudentUIDList, "escapingStudentUIDList", LookMode.Value);
            Scribe_Collections.Look(ref stunnedStudentUIDList, "stunnedStudentUIDList", LookMode.Value);
            Scribe_Collections.Look(ref lostStudentUIDList, "lostStudentUIDList", LookMode.Value);

            Scribe_Deep.Look(ref outcome, "outcome");
            Scribe_Deep.Look(ref currentVenue, "currentVenue");

            // --- load ---
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                studentSeats ??= new Dictionary<string, IntVec3>();

                ResolvePawns();
                if (lord?.Map == null || teacher == null || !teacher.Spawned || teacher.Map != lord.Map)
                {
                    needsSeatReassign = true;
                    return;
                }
                if (studentSeats.Count < studentUids.Count)
                {
                    ReassignSeats();
                }
                ResolveLists();

                if (outcome == null)
                    outcome = new TeacherOutcome(skillUp: 0, passionUpProb: 0f);

                if (currentVenue == null)
                {
                    currentVenue = catalog?.PickInitial();
                    if (currentVenue == null)
                        currentVenue = MakeOutdoorVenue();
                }
            }
        }

        public override bool BlocksSocialInteraction(Pawn pawn)
        {
            return true;
        }

        private void EnsureVenueCatalogInitialized()
        {
            if (catalog != null && currentVenue != null) return;

            Map map = lord?.Map;
            if (map == null) return;

            if (teacher == null || !teacher.Spawned || teacher.Map != map) return;

            catalog = LessonCatalogUtility.BuildLessonVenueCatalog(teacher, minRoomCells: 6);
            currentVenue = catalog?.PickInitial();

            if (currentVenue == null)
                currentVenue = MakeOutdoorVenue();
        }

        public override StateGraph CreateGraph()
        {
            ResolvePawns();
            EnsureVenueCatalogInitialized();

            if (needsSeatReassign)
            {
                EnsureVenueReady();
                ReassignSeats();
                needsSeatReassign = false;
            }

            var graph = new StateGraph();

            var move = new LordToil_Teacher_MoveInPlace(teacher, currentVenue);
            var wait = new LordToil_Teacher_Wait(teacher, maxStudents);
            gather = new LordToil_Teacher_GatherStudents(teacher, students);
            seat = new LordToil_Teacher_TakeSeats(teacher, students, currentVenue);
            var teach = new LordToil_Teacher_TeachLesson(teacher, students, currentVenue);
            var catchStudents = new LordToil_Teacher_CatchStudents(teacher, students, currentVenue);
            var snack = new LordToil_Teacher_Snacks(teacher);
            var exit = new LordToil_ExitMapRandom();

            var mentalStateWait = new LordToil_Wait();

            graph.AddToil(move);
            graph.AddToil(wait);
            graph.AddToil(gather);
            graph.AddToil(seat);
            graph.AddToil(teach);
            graph.AddToil(catchStudents);
            graph.AddToil(snack);
            graph.AddToil(exit);
            graph.AddToil(mentalStateWait);

            // -------------------------
            // Shared safe helpers
            // -------------------------
            bool TickSignal(TriggerSignal s) => s.type == TriggerSignalType.Tick;

            bool TeacherValidForRead()
                => teacher != null && !teacher.DestroyedOrNull() && !teacher.Dead;

            // -------------------------
            // Normal flow
            // -------------------------
            var arrived = new Transition(move, wait);
            arrived.AddTrigger(new Trigger_Memo(MemoArrived));
            graph.AddTransition(arrived);

            var gatherStudents = new Transition(wait, gather);
            gatherStudents.AddTrigger(new Trigger_Memo(MemoGatherStudents));
            graph.AddTransition(gatherStudents);

            var takeSeat = new Transition(gather, seat);
            takeSeat.AddTrigger(new Trigger_Memo(MemoTakeSeats));
            graph.AddTransition(takeSeat);

            var tightSpace = new Transition(seat, gather);
            tightSpace.AddTrigger(new Trigger_Memo(MemoTightSpace));
            graph.AddTransition(tightSpace);

            var startLesson = new Transition(seat, teach);
            startLesson.AddTrigger(new Trigger_Memo(MemoLessonStarted));
            graph.AddTransition(startLesson);

            var escapeDetected = new Transition(teach, catchStudents);
            escapeDetected.AddTrigger(new Trigger_Memo(MemoEscapeDetected));
            graph.AddTransition(escapeDetected);

            var resumeLesson = new Transition(catchStudents, teach);
            resumeLesson.AddTrigger(new Trigger_Memo(MemoResumeLesson));
            graph.AddTransition(resumeLesson);

            var eatSnack = new Transition(teach, snack);
            eatSnack.AddTrigger(new Trigger_Memo(MemoLessonComplete));
            eatSnack.AddTrigger(new Trigger_Memo(MemoLessonFailed));
            graph.AddTransition(eatSnack);

            var toExit = new Transition(snack, exit);
            toExit.AddTrigger(new Trigger_Memo(MemoEatFail));
            toExit.AddTrigger(new Trigger_Memo(MemoEatSuccess));
            graph.AddTransition(toExit);

            // -------------------------
            // Timeout (wait -> exit)
            // -------------------------
            Transition waitTimeOut = new Transition(wait, exit);
            waitTimeOut.AddTrigger(new Trigger_TicksPassed(30000));
            waitTimeOut.AddPostAction(new TransitionAction_Custom((Action)delegate
            {
                // teacher can be null during load / edge cases
                if (TeacherValidForRead())
                {
                    string text = LordChats.GetText(TeacherTextKey.TimeOut);
                    SpeechBubbleManager.Get(Map)?.AddNPCChat(text, teacher, speaker: SpeakerType.NonHostileNPC);
                    Messages.Message("선생님이 기다리다 지쳐 떠납니다.", teacher, MessageTypeDefOf.NegativeEvent);
                }
                else
                {
                    Messages.Message("선생님이 기다리다 지쳐 떠납니다.", MessageTypeDefOf.NegativeEvent);
                }
            }));
            graph.AddTransition(waitTimeOut);

            // -------------------------
            // Shared cleanup
            // -------------------------
            TransitionAction_Custom cleanStudents = new TransitionAction_Custom((Action)delegate
            {
                // teacher can be null right after load; don't touch jobs if null
                if (teacher?.CurJob != null)
                    teacher.jobs?.EndCurrentJob(JobCondition.InterruptForced);

                RemoveStudents();
            });

            Transition waitTimeOut2 = new Transition(catchStudents, exit);
            waitTimeOut2.AddTrigger(new Trigger_TicksPassed(10000));
            waitTimeOut2.AddPreAction(cleanStudents);
            waitTimeOut2.AddPostAction(new TransitionAction_Custom((Action)delegate
            {
                if (TeacherValidForRead())
                {
                    string text = LordChats.GetText(TeacherTextKey.TimeOut);
                    SpeechBubbleManager.Get(Map)?.AddNPCChat(text, teacher, speaker: SpeakerType.NonHostileNPC);
                    Messages.Message("선생님이 지쳐 떠납니다.", teacher, MessageTypeDefOf.NegativeEvent);
                }
                else
                {
                    Messages.Message("선생님이 지쳐 떠납니다.", MessageTypeDefOf.NegativeEvent);
                }
            }));
            graph.AddTransition(waitTimeOut2);

            // -------------------------
            // Teacher gone (null / down / dead / despawn) -> exit
            // NOTE: put this BEFORE other tick triggers to minimize chance of other checks running first.
            // -------------------------
            Transition teacherGone = new Transition(gather, exit);
            teacherGone.AddSource(seat);
            teacherGone.AddSource(teach);
            teacherGone.AddSource(catchStudents);
            teacherGone.AddSource(wait);
            teacherGone.AddSource(move);
            teacherGone.AddSource(snack);
            teacherGone.AddSource(mentalStateWait);

            teacherGone.AddTrigger(new Trigger_Custom(s =>
                TickSignal(s) &&
                (teacher == null
                || teacher.DestroyedOrNull()
                || teacher.Dead
                || teacher.Downed
                || !teacher.Spawned)
            ));
            teacherGone.AddPreAction(cleanStudents);
            teacherGone.AddPostAction(new TransitionAction_Custom((Action)delegate
            {
                Messages.Message("수업이 중단되었습니다.", MessageTypeDefOf.NegativeEvent);
            }));
            graph.AddTransition(teacherGone);

            // -------------------------
            // Mental state -> mentalStateWait
            // -------------------------
            Transition mentalState = new Transition(gather, mentalStateWait);
            mentalState.AddSource(seat);
            mentalState.AddSource(teach);
            mentalState.AddSource(catchStudents);

            mentalState.AddTrigger(new Trigger_Custom(s =>
                TickSignal(s) && TeacherValidForRead() && teacher.InMentalState
            ));

            mentalState.AddPreAction(cleanStudents);
            mentalState.AddPreAction(new TransitionAction_Custom((System.Action)delegate
            {
                if (TeacherValidForRead())
                {
                    string text = LordChats.GetText(TeacherTextKey.InMental);
                    SpeechBubbleManager.Get(Map)?.AddNPCChat(text, teacher, speaker: SpeakerType.NonHostileNPC);
                    Messages.Message("수업이 방해받아 중단되었습니다.", teacher, MessageTypeDefOf.NegativeEvent);
                }
                else
                {
                    Messages.Message("수업이 방해받아 중단되었습니다.", MessageTypeDefOf.NegativeEvent);
                }
            }));
            mentalState.postActions.Add(new TransitionAction_Custom((System.Action)delegate
            {
                ResetLesson();
            }));

            graph.AddTransition(mentalState);

            // -------------------------
            // Recover mental -> wait
            // -------------------------
            Transition recoverMental = new Transition(mentalStateWait, wait);
            recoverMental.AddTrigger(new Trigger_Custom((TriggerSignal s) =>
                TickSignal(s) && TeacherValidForRead() && !teacher.InMentalState
            ));
            graph.AddTransition(recoverMental);

            // -------------------------
            // Dangerous temperature -> exit
            // -------------------------
            Transition dangerTemperature = new Transition(move, exit);
            dangerTemperature.AddSource(wait);
            dangerTemperature.AddSource(gather);
            dangerTemperature.AddSource(seat);
            dangerTemperature.AddSource(teach);
            dangerTemperature.AddSource(catchStudents);
            dangerTemperature.AddSource(snack);
            dangerTemperature.AddSource(mentalStateWait);

            dangerTemperature.AddTrigger(new Trigger_Custom(delegate (TriggerSignal signal)
            {
                if (!TickSignal(signal)) return false;
                if (!TeacherValidForRead()) return false;

                var hs = teacher.health?.hediffSet;
                if (hs == null) return false;

                Hediff hypo = hs.GetFirstHediffOfDef(HediffDefOf.Hypothermia);
                if (hypo != null && hypo.Severity >= 0.35f) return true;

                Hediff heat = hs.GetFirstHediffOfDef(HediffDefOf.Heatstroke);
                if (heat != null && heat.Severity >= 0.35f) return true;

                return false;
            }));
            dangerTemperature.AddPreAction(cleanStudents);
            dangerTemperature.AddPostAction(new TransitionAction_Custom((Action)delegate
            {
                teacher?.Faction?.TryAffectGoodwillWith(Faction.OfPlayer, -50);

                if (TeacherValidForRead())
                {
                    string text = LordChats.GetText(TeacherTextKey.Temperature);
                    SpeechBubbleManager.Get(Map)?.AddNPCChat(text, teacher, speaker: SpeakerType.NonHostileNPC);
                    Messages.Message("선생님이 위험한 온도를 피해 수업을 중단합니다.", teacher, MessageTypeDefOf.NegativeEvent);
                }
                else
                {
                    Messages.Message("선생님이 위험한 온도를 피해 수업을 중단합니다.", MessageTypeDefOf.NegativeEvent);
                }
            }));
            graph.AddTransition(dangerTemperature);

            // -------------------------
            // Harmed -> exit
            // -------------------------
            Transition harmed = new Transition(move, exit);
            harmed.AddSource(wait);
            harmed.AddSource(gather);
            harmed.AddSource(seat);
            harmed.AddSource(teach);
            harmed.AddSource(catchStudents);
            harmed.AddSource(snack);
            harmed.AddSource(mentalStateWait);

            // base.Map might be null during edge cases; guard it.
            // If base.Map is null, this trigger will never fire (safe).
            harmed.AddTrigger(new Trigger_Custom(signal =>
            {
                if (teacher == null) return false;
                if (signal.Pawn != teacher) return false;
                if (signal.type == TriggerSignalType.PawnDamaged) return signal.dinfo.Def.ExternalViolenceFor(signal.Pawn);
                if (signal.type == TriggerSignalType.PawnLost)
                {
                    if (signal.condition != PawnLostCondition.MadePrisoner && signal.condition != PawnLostCondition.Incapped)
                    {
                        return signal.condition == PawnLostCondition.Killed;
                    }
                    return true;
                }
                if (signal.type == TriggerSignalType.PawnArrestAttempted)
                {
                    return true;
                }

                return false;
            }));

            harmed.AddPreAction(cleanStudents);
            harmed.AddPostAction(new TransitionAction_Custom((Action)delegate
            {
                if (TeacherValidForRead())
                {
                    string text = LordChats.GetText(TeacherTextKey.Harmed);
                    SpeechBubbleManager.Get(Map)?.AddNPCChat(text, teacher, speaker: SpeakerType.NonHostileNPC);
                }

                teacher?.Faction?.TryAffectGoodwillWith(Faction.OfPlayer, -50);

                if (TeacherValidForRead())
                    Messages.Message("선생님이 도망칩니다.", teacher, MessageTypeDefOf.NegativeEvent);
                else
                    Messages.Message("선생님이 도망칩니다.", MessageTypeDefOf.NegativeEvent);
            }));
            graph.AddTransition(harmed);

            // -------------------------
            // Gas exposed -> exit
            // -------------------------
            Transition gasExposed = new Transition(move, exit);
            gasExposed.AddSource(wait);
            gasExposed.AddSource(gather);
            gasExposed.AddSource(seat);
            gasExposed.AddSource(teach);
            gasExposed.AddSource(catchStudents);
            gasExposed.AddSource(snack);
            gasExposed.AddSource(mentalStateWait);

            gasExposed.AddTrigger(new Trigger_Custom(delegate (TriggerSignal signal)
            {
                if (!TickSignal(signal)) return false;
                if (!TeacherValidForRead()) return false;

                var hs = teacher.health?.hediffSet;
                if (hs == null) return false;

                if (ModsConfig.BiotechActive)
                {
                    Hediff tox = hs.GetFirstHediffOfDef(HediffDefOf.ToxGasExposure);
                    if (tox != null && tox.Severity >= 0.9f) return true;
                }

                Hediff lung = hs.GetFirstHediffOfDef(HediffDefOf.LungRotExposure);
                if (lung != null && lung.Severity >= 0.9f) return true;

                return false;
            }));
            gasExposed.AddPreAction(cleanStudents);
            gasExposed.AddPostAction(new TransitionAction_Custom((Action)delegate
            {
                if (TeacherValidForRead())
                {
                    string text = LordChats.GetText(TeacherTextKey.Gas);
                    SpeechBubbleManager.Get(Map)?.AddNPCChat(text, teacher, speaker: SpeakerType.NonHostileNPC);
                }

                teacher?.Faction?.TryAffectGoodwillWith(Faction.OfPlayer, -50);

                if (TeacherValidForRead())
                    Messages.Message("선생님이 도망칩니다.", teacher, MessageTypeDefOf.NegativeEvent);
                else
                    Messages.Message("선생님이 도망칩니다.", MessageTypeDefOf.NegativeEvent);
            }));
            graph.AddTransition(gasExposed);

            return graph;
        }

        public void ResetLesson()
        {
            lessonProgressTicks = 0;
            lastLessonStartTick = -1;
        }

        public override AcceptanceReport AllowsDrafting(Pawn pawn)
        {
            if (lord.CurLordToil == gather || lord.CurLordToil == seat)
            {
                return new AcceptanceReport("수업 장소로 이동 중 입니다. 수업이 시작되면 다시 소집할 수 있습니다.");
            }
            return true;
        }

        public void NotifyStudentEscape(Pawn student)
        {
            outcome.AddEscapeStudent(student);
            RemoveTeacherHediffs(student);
            RewardEscape(student);
        }

        public void AddTeacherBuff()
        {
            if (teacher == null)
                return;
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail("CheeseProtocol_TeacherRageBuff");
            TryAddHediff(teacher, def);
        }

        private static bool TryAddHediff(Pawn p, HediffDef def)
        {
            if (p == null || def == null) return false;
            if (p.health?.hediffSet == null) return false;
            if (p.health.hediffSet.HasHediff(def)) return false;
            p.health.AddHediff(def);
            return true;
        }

        public void RemoveTeacherBuff()
        {
            if (teacher == null)
                return;
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail("CheeseProtocol_TeacherRageBuff");
            if (def != null)
            {
                var h = teacher.health?.hediffSet?.GetFirstHediffOfDef(def);
                if (h != null) teacher.health.RemoveHediff(h);
            }
        }

        private void RewardEscape(Pawn student)
        {
            if (student == null) return;
            string passionResult = ApplyPassion(student, passionUpProb);
            ApplyThrillingThought(student);
            ApplyInspiration(student);

            var sb = new StringBuilder();
            sb.AppendLine($"{student.NameShortColored} 학생이 도주에 성공했습니다.");
            sb.AppendLine("짜릿한 일탈로 영감이 떠올랐고, 숨겨진 열정이 깨어났을지도 모릅니다.");
            sb.AppendLine();
            sb.AppendLine(passionResult);
            sb.AppendLine($"(열정 부여 성공 확률: {(passionUpProb*100):0.##}%)");
            CheeseLetter.SendCheeseLetter(CheeseCommand.Teacher, "도주 성공", sb.ToString(), student, null, student.Map, LetterDefOf.PositiveEvent);
        }
        private static bool ApplyInspiration(Pawn pawn)
        {
            var ih = pawn?.mindState?.inspirationHandler;
            if (ih == null) return false;

            if (ih.Inspired)
            {
                var cur = ih.CurState;
                if (cur != null)
                    ih.EndInspiration(cur);
            }
            var creative = InspirationDefOf.Inspired_Creativity;
            if (ih.TryStartInspiration(creative))
                return true;

            var randomDef = ih.GetRandomAvailableInspirationDef();
            if (randomDef != null && ih.TryStartInspiration(randomDef))
                return true;

            return false;
        }
        private static void ApplyThrillingThought(Pawn pawn)
        {
            if (pawn == null) return;
            ThoughtDef def = DefDatabase<ThoughtDef>.GetNamedSilentFail("CheeseProtocol_ThrillingEscape");
            if (def == null) return;
            pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(def);
        }
        private static string ApplyPassion(Pawn pawn, float passionUpProb)
        {
            string failPassion = $"<color={ColorNegative}>열정 부여: 실패</color>";
            if (pawn?.skills == null) 
                return failPassion;
            if (!Rand.Chance(passionUpProb))
                return failPassion;
            List<SkillRecord> list = new List<SkillRecord>(pawn.skills.skills);
            list.Shuffle();
            list.Sort((a, b) => b.Level.CompareTo(a.Level));
            foreach (var sr in list)
            {
                if (sr == null) continue;

                if (sr.passion == Passion.Major)
                    continue;

                sr.passion = sr.passion == Passion.None
                    ? Passion.Minor
                    : Passion.Major;

                string label = sr.def?.label ?? sr.def?.defName ?? "스킬";
                return $"<color={ColorPositive}>열정 부여: 성공 => {label}</color>";
            }
            return failPassion;
        }

        public void FinishLesson(Pawn teacher)
        {
            var pawns = lord?.ownedPawns;
            if (pawns == null || pawns.Count == 0) return;
            outcome?.Apply(teacher, pawns);
            RemoveStudents();
        }

        public void RemoveStudents()
        {
            var pawns = lord?.ownedPawns;
            if (pawns == null || pawns.Count == 0) return;
            var toRemove = new List<Pawn>(pawns.Count);
            for (int i = 0; i < pawns.Count; i++)
            {
                var p = pawns[i];
                if (p == null) continue;
                if (p == teacher) continue;
                toRemove.Add(p);
            }
            for (int i = 0; i < toRemove.Count; i++)
            {
                var p = toRemove[i];
                RemoveTeacherHediffs(p);
                try
                {
                    lord.Notify_PawnLost(p, PawnLostCondition.Undefined);
                }
                catch (System.Exception e)
                {
                    Warn($"FinishLesson Notify_PawnLost failed: {e}");
                }
            }
        }
        public void RemoveTeacherHediffs(Pawn pawn)
        {
            HediffDef downDef = DefDatabase<HediffDef>.GetNamedSilentFail("CheeseProtocol_LessonDisciplineDown");
            HediffDef debufDef = DefDatabase<HediffDef>.GetNamedSilentFail("CheeseProtocol_LessonDisciplineDebuff");
            if (downDef != null)
            {
                var h = pawn.health?.hediffSet?.GetFirstHediffOfDef(downDef);
                if (h != null) pawn.health.RemoveHediff(h);
            }
            if (debufDef != null)
            {
                var h = pawn.health?.hediffSet?.GetFirstHediffOfDef(debufDef);
                if (h != null) pawn.health.RemoveHediff(h);
            }
        }

        public void NotifyStudentSubdued(Pawn student)
        {
            
        }

        public override void Notify_InMentalState(Pawn pawn, MentalStateDef stateDef)
        {
            base.Notify_InMentalState(pawn, stateDef);
        }

        public void ReassignSeats()
        {
            if (lord?.Map == null || teacher == null || students == null || students.Count == 0 || currentVenue == null) return;
            if(SeatUtility.TryAssignSeats(lord?.Map, teacher, students, currentVenue, out teacherSeat, out studentSeats, out teacherFaceDir))
            {
                currentVenue.spotCell = teacherSeat;
            }
            else
            {
                currentVenue = UpdateVenueAfterFail(students.Count);
                lord.ReceiveMemo(MemoTightSpace);    
            }
        }

        public LessonVenue UpdateVenueAfterFail(int studentCount)
        {
            int totalCount = studentCount + 1;
            if (currentVenue == null)
            {
                currentVenue = MakeOutdoorVenue();
                return currentVenue;
            }
            LessonVenue found = null;
            switch (currentVenue.kind)
            {
                case LessonRoomKind.Blackboard:
                    found = LessonCatalogUtility.FindFirstFittable(catalog.tables, totalCount) ??
                            LessonCatalogUtility.FindFirstFittable(catalog.plains, totalCount);
                    break;
                case LessonRoomKind.Table:
                    found = LessonCatalogUtility.FindFirstFittable(catalog.plains, totalCount);
                    break;
            }
            if (found == null)
            {
                currentVenue = MakeOutdoorVenue();
                return currentVenue;
            }
            currentVenue = found;
            return found;
        }

        public LessonVenue UpdateVenue(int studentCount)
        {
            int totalCount = studentCount + 1;
            Warn("UpdateVenue called");
            if (catalog == null) return null;
            if (studentCount <= 0) return null;

            if (currentVenue != null && LessonCatalogUtility.CanFitStudents(currentVenue, totalCount))
            {
                Warn($"currentVenue not null and can fit students : type={currentVenue.kind}");
                return currentVenue;
            }
            Warn("currentVenue is null");

            LessonVenue found =
                LessonCatalogUtility.FindFirstFittable(catalog.blackboards, totalCount) ??
                LessonCatalogUtility.FindFirstFittable(catalog.tables, totalCount) ??
                LessonCatalogUtility.FindFirstFittable(catalog.plains, totalCount);

            if (found != null)
            {
                currentVenue = found;
                return found;
            }
            var outdoor = MakeOutdoorVenue();
            currentVenue = outdoor;
            return outdoor;
        }

        // ========= Resolve =========
        public void ResolveLists()
        {
            escapingStudentUIDs = escapingStudentUIDList != null
            ? new HashSet<string>(escapingStudentUIDList)
            : new HashSet<string>();
            stunnedStudentUIDs = stunnedStudentUIDList != null
            ? new HashSet<string>(stunnedStudentUIDList)
            : new HashSet<string>();
            lostStudentUIDs = lostStudentUIDList != null
            ? new HashSet<string>(lostStudentUIDList)
            : new HashSet<string>();
        }

        public void EnsureVenueReady()
        {
            if (currentVenue != null) return;
            currentVenue = catalog?.PickInitial();
            if (currentVenue == null)
                currentVenue = MakeOutdoorVenue();
        }

        public void ResolvePawns()
        {
            Map map = lord?.Map;
            if (map == null) return;

            if (teacher == null && !teacherUid.NullOrEmpty())
            {
                teacher = FindPawnByUid(map, teacherUid);
            }
            if (students.NullOrEmpty())
            {
                students = new List<Pawn>();
                if (!studentUids.NullOrEmpty())
                {
                    foreach (string uid in studentUids)
                    {
                        Pawn p = FindPawnByUid(map, uid);
                        if (p != null)
                            students.Add(p);
                    }
                }
            }
        }

        private static Pawn FindPawnByUid(Map map, string uid)
        {
            if (uid.NullOrEmpty() || map == null) return null;
            
            var spawned = map.mapPawns?.AllPawnsSpawned;    
            if (spawned != null)
            {
                for (int i = 0; i < spawned.Count; i++)
                {
                    Pawn p = spawned[i];
                    if (p != null && p.GetUniqueLoadID() == uid)
                        return p;
                }
            }

            var all = map.mapPawns?.AllPawns;
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    Pawn p = all[i];
                    if (p != null && p.GetUniqueLoadID() == uid)
                        return p;
                }
            }
            Warn($"AllPawnsSpawned={spawned?.Count ?? -1}, AllPawns={all?.Count ?? -1}, targetUid={uid}");


            return null;
        }

        public IntVec3 GetSpot()
        {
            IntVec3 result = IntVec3.Invalid;
            if (currentVenue != null && currentVenue.anchorInfo.Thing != null)
            {
                IntVec3 interactionCell = currentVenue.anchorInfo.Thing.InteractionCell;
                IntVec3 intVec = currentVenue.spotCell;
                foreach (IntVec3 item in GenSight.PointsOnLineOfSight(interactionCell, intVec))
                {
                    if (!(item == interactionCell) && !(item == intVec) && CanUseSpot(teacher, item))
                    {
                        result = item;
                        break;
                    }
                }
            }
            if (result.IsValid)
            {
                return result;
            }
            return TryGetUsableSpotAdjacentToTeacher(teacher);
        }

        private static bool CanUseSpot(Pawn bestower, IntVec3 spot)
        {
            if (!spot.InBounds(bestower.Map))
            {
                return false;
            }
            if (!spot.Standable(bestower.Map))
            {
                return false;
            }
            if (!GenSight.LineOfSight(spot, bestower.Position, bestower.Map))
            {
                return false;
            }
            if (!bestower.CanReach(spot, PathEndMode.OnCell, Danger.Deadly))
            {
                return false;
            }
            return true;
        }
        
        public static IntVec3 TryGetUsableSpotAdjacentToTeacher(Pawn teacher)
        {
            foreach (int item in Enumerable.Range(1, 4).InRandomOrder())
            {
                IntVec3 result = teacher.Position + GenRadial.ManualRadialPattern[item];
                if (CanUseSpot(teacher, result))
                {
                    return result;
                }
            }
            return IntVec3.Invalid;
        }

        public void AddStudents(IEnumerable<Pawn> newStudents)
        {
            if (newStudents == null) return;

            var uidSet = new HashSet<string>();
            if (!studentUids.NullOrEmpty())
            {
                for (int i = 0; i < studentUids.Count; i++)
                {
                    var uid = studentUids[i];
                    if (!uid.NullOrEmpty()) uidSet.Add(uid);
                }
            }

            if (students == null) students = new List<Pawn>();

            foreach (var p in newStudents)
            {
                if (p == null) continue;
                if (p == teacher) continue;

                string uid = p.GetUniqueLoadID();
                if (uid.NullOrEmpty()) continue;

                if (uidSet.Add(uid))
                    students.Add(p);
            }

            studentUids = uidSet.ToList();
            outcome.SetStudents(new HashSet<string>(uidSet));
        }
        

        public static bool TryFindCentralTravelDest(Map map, out IntVec3 cell)
        {
            IntVec3 center = map.Center;

            int radius = 0;
            int maxRadius = Mathf.Min(map.Size.x, map.Size.z) / 2;
            int step = 16;

            while (radius <= maxRadius)
            {
                if (CellFinder.TryFindRandomCellNear(center, map, radius, c => c.Walkable(map), out cell))
                    return true;
                radius += step;
            }
            return CellFinder.TryFindRandomCell(map, c => c.Walkable(map), out cell);
        }
        private static bool TryFindWanderCellNearColony(Pawn pawn, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            Map map = pawn.Map;
            if (map == null) return false;

            Area home = map.areaManager?.Home;
            if (home != null)
            {
                IntVec3 homeCenter = home.ActiveCells.Any()
                    ? home.ActiveCells.OrderBy(c => c.DistanceToSquared(pawn.Position)).First()
                    : IntVec3.Invalid;

                if (homeCenter.IsValid)
                {
                    return CellFinder.TryFindRandomReachableNearbyCell(
                        homeCenter, map, 18,
                        TraverseParms.For(pawn),
                        c => c.Standable(map) && !c.OnEdge(map),
                        null,
                        out cell);
                }
            }
            Building b = map.listerBuildings?.allBuildingsColonist?.FirstOrDefault();
            if (b != null && b.Spawned)
            {
                return CellFinder.TryFindRandomReachableNearbyCell(
                    b.Position, map, 18,
                    TraverseParms.For(pawn),
                    c => c.Standable(map),
                    null,
                    out cell);
            }

            return TryFindCentralTravelDest(map, out cell);
        }

        private LessonVenue MakeOutdoorVenue()
        {
            IntVec3 spot;
            if (!TryFindWanderCellNearColony(teacher, out spot))
                spot = lord.Map?.Center ?? IntVec3.Invalid;

            return new LessonVenue
            {
                kind = LessonRoomKind.Outdoor, // 또는 별도 Outdoor enum을 추가
                roomId = -1,
                roomKeyCell = IntVec3.Invalid,
                spotCell = spot,
                anchorInfo = null,
                roomCellCount = 0,
                capacity = 999999 // 의미 없는 값. seat assign에서 실제로 뽑게 될 것
            };
        }

        
    }
}