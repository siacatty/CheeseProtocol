using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace CheeseProtocol
{
    public class JobDriver_TeacherTeach : JobDriver
    {
        // Target A: the cell to face (teacherSeat + teacherFaceDir)
        private const TargetIndex FaceDirInd = TargetIndex.A;

        // (Reserved) Target B: spot / venue
        private const TargetIndex SpotInd = TargetIndex.B;
        private const TargetIndex TeacherSeatInd = TargetIndex.C;
        private const int FaceDirInterval = 60;
        private const int ChatInterval = 480;
        private const int QuizEveryNChat = 4;
        private int chatCount;
        private int nextFaceDirTick;
        private int nextChatTick;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() =>
            {
                var t = job.GetTarget(FaceDirInd);
                return !t.IsValid || !t.Cell.IsValid;
            });
            this.FailOn(() => pawn == null || pawn.Dead || pawn.Downed);

            var teach = new Toil
            {
                defaultCompleteMode = ToilCompleteMode.Never,
                handlingFacing = false,
                socialMode = RandomSocialMode.Off
            };

            teach.initAction = () =>
            {
                var lj = pawn.GetLord()?.LordJob as LordJob_Teacher;
                if (lj != null)
                    lj.lastLessonStartTick = Find.TickManager.TicksGame;

                var cell = job.GetTarget(FaceDirInd).Cell;
                pawn.rotationTracker.FaceCell(cell);
                pawn.rotationTracker.UpdateRotation();
                int now = Find.TickManager.TicksGame;
                nextFaceDirTick = now + FaceDirInterval;
                nextChatTick = now + ChatInterval;
                chatCount = 0;
            };

            teach.tickAction = () =>
            {
                int now = Find.TickManager.TicksGame;

                if (job.targetC.IsValid)
                {
                    IntVec3 seat = job.GetTarget(TeacherSeatInd).Cell;

                    if (pawn.Position != seat)
                    {
                        var lj0 = pawn.GetLord()?.LordJob as LordJob_Teacher;
                        if (lj0 != null)
                            lj0.lastLessonStartTick = now;

                        if (!pawn.pather.Moving)
                            pawn.pather.StartPath(seat, PathEndMode.OnCell);

                        return;
                    }
                }

                var lord = pawn.GetLord();
                var lj = lord?.LordJob as LordJob_Teacher;
                if (lj != null)
                {
                    if (lj.lastLessonStartTick < 0)
                        lj.lastLessonStartTick = now;

                    int delta = now - lj.lastLessonStartTick;
                    if (delta < 0) delta = 0;

                    if (delta != 0)
                    {
                        lj.lessonProgressTicks += delta;
                        lj.lastLessonStartTick = now;

                        if (lj.lessonProgressTicks >= LordJob_Teacher.LessonTotalTicks)
                        {
                            lj.lessonProgressTicks = LordJob_Teacher.LessonTotalTicks;
                            string lessonComplete = LordChats.GetText(TeacherTextKey.EndLessonSuccess);
                            SpeechBubbleManager.Get(Map)?.AddNPCChat(lessonComplete, pawn, speaker:SpeakerType.NonHostileNPC);
                            lord?.ReceiveMemo(LordJob_Teacher.MemoLessonComplete);
                            ReadyForNextToil();
                            return;
                        }
                    }
                }
                if (now >= nextFaceDirTick)
                {
                    nextFaceDirTick = now + FaceDirInterval;

                    var faceCell = job.GetTarget(FaceDirInd).Cell;
                    pawn.rotationTracker.FaceCell(faceCell);
                    pawn.rotationTracker.UpdateRotation();
                }
                if (now >= nextChatTick)
                {
                    nextChatTick = now + ChatInterval;
                    chatCount ++;

                    bool doQuiz = chatCount % QuizEveryNChat == 0;
                    string text ="";

                    var students = lord?.ownedPawns.Where(p => p != pawn).ToList();

                    if (students.NullOrEmpty()) return;

                    if (doQuiz)
                    {
                        Pawn randomStudent = students.RandomElement();
                        text = LordChats.GetText(TeacherTextKey.TeachLessonQuiz, randomStudent.NameShortColored);
                    }
                    else
                    {
                        students.Shuffle();
                        Pawn offSeatStudent = null;
                        Pawn randomStudent = null;
                        if (students != null)
                        {
                            foreach (var p in students)
                            {
                                randomStudent = p;
                                if (lj.studentSeats.TryGetValue(p.GetUniqueLoadID(), out var seat))
                                {
                                    if (p.Position != seat)
                                        offSeatStudent = p;
                                }
                            }
                        }
                        if (offSeatStudent != null)
                        {
                            text = LordChats.GetText(TeacherTextKey.TeachLessonDrafted, offSeatStudent.NameShortColored);
                        }
                        else
                        {
                            text = LordChats.GetText(TeacherTextKey.TeachLesson, randomStudent?.NameShortColored ?? "");
                        }
                    }
                    SpeechBubbleManager.Get(Map)?.AddNPCChat(text, pawn, speaker:SpeakerType.NonHostileNPC);
                }
            };

            teach = WithCustomProgressBar(
                teach,
                SpotInd,
                () =>
                {
                    var lj = pawn.GetLord()?.LordJob as LordJob_Teacher;
                    if (lj == null) return 0f;
                    return (float)lj.lessonProgressTicks / LordJob_Teacher.LessonTotalTicks;
                },
                interpolateBetweenActorAndTarget: false,
                offsetZ: -0.5f,
                alwaysShow: true
            );

            yield return teach;

            yield return Toils_General.Do(delegate
            {
                var lord = pawn.GetLord();
                var lj = lord?.LordJob as LordJob_Teacher;
                if (lj == null) return;

                lj.FinishLesson(pawn);
                FleckMaker.Static((pawn.TrueCenter() + pawn.TrueCenter()) / 2f, pawn.Map, FleckDefOf.PsycastAreaEffect, 2f);
                //FleckMaker.ThrowMicroSparks(pawn.TrueCenter(), pawn.Map);

                SoundDefOf.Bestowing_Finished.PlayOneShot(pawn);
                EndJobWith(JobCondition.Succeeded);
            });
        }

        private static Toil WithCustomProgressBar(Toil toil, TargetIndex ind, Func<float> progressGetter,
            bool interpolateBetweenActorAndTarget = false, float offsetZ = -0.5f, bool alwaysShow = false)
        {
            Effecter effecter = null;

            toil.AddPreTickAction(delegate
            {

                if (effecter == null)
                    effecter = EffecterDefOf.ProgressBar.Spawn();

                LocalTargetInfo lti = (ind == TargetIndex.None)
                    ? (LocalTargetInfo)toil.actor.Position
                    : toil.actor.CurJob.GetTarget(ind);

                if (!lti.IsValid || (lti.HasThing && !lti.Thing.Spawned))
                {
                    // fallback: 최소한 actor 위치에라도 보이게 해서 디버깅 쉽게
                    lti = (LocalTargetInfo)toil.actor.Position;
                }

                if (interpolateBetweenActorAndTarget)
                    effecter.EffectTick(lti.ToTargetInfo(toil.actor.Map), toil.actor);
                else
                    effecter.EffectTick(lti.ToTargetInfo(toil.actor.Map), TargetInfo.Invalid);

                var sub = effecter.children[0] as SubEffecter_ProgressBar;
                var mote = sub?.mote;
                if (mote != null)
                {
                    mote.progress = Mathf.Clamp01(progressGetter());
                    mote.offsetZ = offsetZ;
                    mote.alwaysShow = alwaysShow;
                }
            });

            toil.AddFinishAction(delegate
            {
                effecter?.Cleanup();
                effecter = null;
            });

            return toil;
        }
    }
}