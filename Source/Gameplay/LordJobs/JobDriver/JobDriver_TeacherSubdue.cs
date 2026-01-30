// CheeseProtocol - Teacher Subdue JobDriver
// - Goes to target pawn
// - On arrival: optional speech bubble / visual, applies hediff, stuns, forces undraft, downs (0 dmg style)
// - Supports "escape success" by failing if target leaves allowed range (d2) before contact
//
// Notes:
// - Keep long-term state (target UID, d2, spot, etc.) in LordJob_Teacher.
// - This driver reads parameters from the Job (targetA + spot cell in targetB) and/or from LordJob.
// - Customize the "down" behavior to your taste.

using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using static CheeseProtocol.CheeseLog;

namespace CheeseProtocol
{
    public class JobDriver_TeacherSubdue : JobDriver
    {
        // Targets:
        // A: student pawn
        // B: lesson spot (cell) OR you can ignore B and read from LordJob
        private const TargetIndex StudentInd = TargetIndex.A;
        private const TargetIndex SpotInd = TargetIndex.B;

        private const int HitPreDelay = 60;     // 2 seconds
        private const int StunTicks = 120;       // 2 seconds
        private const int HitPostDelay = 30;     // 0.5 second
        private const int TargetCheckInterval = 60;
        private int nextTargetCheckTick;
        

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Usually you don't need reservations for "discipline" style behavior.
            // If you want to reserve the pawn so others don't interact, you can.
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(StudentInd);
            this.FailOn(() =>
            {
                if (Find.TickManager.TicksGame < nextTargetCheckTick) return false;
                nextTargetCheckTick = Find.TickManager.TicksGame + TargetCheckInterval;
                var lj = pawn.GetLord().LordJob as LordJob_Teacher;
                if (lj == null) return true;

                Pawn student = (Pawn)job.GetTarget(TargetIndex.A).Thing;
                return !StillEscaping(lj.currentTargetUid);
            });

            var gotoToil =  Toils_Goto.GotoThing(StudentInd, PathEndMode.Touch);
            gotoToil.FailOn(() =>
            {
                return pawn.Downed || pawn.Dead;
            });
            gotoToil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            yield return gotoToil;
            
            var stun = new Toil();
            stun.initAction = () =>
            {
                var lj = pawn.GetLord().LordJob as LordJob_Teacher;
                Pawn student = (Pawn)job.GetTarget(StudentInd).Thing;
                if (student == null || lj == null) { EndJobWith(JobCondition.Incompletable); return; }

                pawn.rotationTracker?.FaceTarget(student);

                string subdueText =
                    student.GetUniqueLoadID() != lj.currentTargetUid
                        ? LordChats.GetText(TeacherTextKey.SubdueNonStudent, student.NameShortColored)
                        : LordChats.GetText(TeacherTextKey.SubdueStudent, student.NameShortColored);

                SpeechBubbleManager.Get(Map)?.AddNPCChat(subdueText, pawn);

                student.stances?.stunner?.StunFor(StunTicks, pawn);
                student.jobs?.EndCurrentJob(JobCondition.InterruptForced, true);
            };
            stun.defaultCompleteMode = ToilCompleteMode.Delay;
            stun.defaultDuration = HitPreDelay;
 
            stun = WithCustomProgressBarToilDelay(stun, TargetIndex.None, alwaysShow: true);

            yield return stun;

            // "Hit" / subdue action
            var hit = new Toil();
            hit.initAction = () =>
            {
                Pawn student = (Pawn)job.GetTarget(StudentInd).Thing;
                if (student == null) { EndJobWith(JobCondition.Incompletable); return; }
                pawn.rotationTracker?.FaceTarget(student);
                TryForceDownViaHediff(student);
            };
            hit.defaultCompleteMode = ToilCompleteMode.Delay;
            hit.defaultDuration = HitPostDelay;
            yield return hit;

            // Finish
            var done = new Toil();
            done.initAction = () =>
            {
                EndJobWith(JobCondition.Succeeded);
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
        private bool StillEscaping(string uid)
        {
            var lj = pawn.GetLord()?.LordJob as LordJob_Teacher;
            if (lj == null) return false;

            if (uid.NullOrEmpty()) return false;

            return lj.escapingStudentUIDs != null && lj.escapingStudentUIDs.Contains(uid);
        }

        private static bool TryAddHediff(Pawn p, HediffDef def)
        {
            if (p == null || def == null) return false;
            if (p.health?.hediffSet == null) return false;
            if (p.health.hediffSet.HasHediff(def)) return false;
            p.health.AddHediff(def);
            return true;
        }
        private void TryForceDownViaHediff(Pawn student)
        {
            HediffDef downDef = DefDatabase<HediffDef>.GetNamedSilentFail("CheeseProtocol_LessonDisciplineDown");
            if (downDef != null)
            {
                if(TryAddHediff(student, downDef))
                {
                    var lord = pawn.GetLord();
                    var lj = lord?.LordJob as LordJob_Teacher;
                    if (lj != null)
                    {
                        string uid = student.GetUniqueLoadID();
                        if (!uid.NullOrEmpty())
                        {
                            if (lj.escapingStudentUIDs.Contains(uid))
                            {
                                lj.lostStudentUIDs.Add(uid);
                                lj.lostCache[uid] = student;
                            }
                            if (lj.currentTargetUid == uid)
                                lj.currentTargetUid = null;
                        }
                    }
                }
            }
        }

        private static Toil WithCustomProgressBarToilDelay(Toil toil, TargetIndex ind, bool interpolateBetweenActorAndTarget = false, float offsetZ = -0.5f, bool alwaysShow = false)
        {
            return WithCustomProgressBar(toil, ind, () => 1f - (float)toil.actor.jobs.curDriver.ticksLeftThisToil / (float)toil.defaultDuration, interpolateBetweenActorAndTarget, offsetZ, alwaysShow);
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
