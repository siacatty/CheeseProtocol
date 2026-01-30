using System.Collections.Generic;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CheeseProtocol
{
    /// <summary>
    /// Shout/Detect-escape intro job:
    /// - Face target pawn (TargetIndex.A)
    /// - Show speech bubble once
    /// - Wait for N ticks (TargetIndex.B as int via job.count)
    ///
    /// NOTE: TargetIndex.B cannot store an integer. Use job.count for delay ticks.
    ///       (So "B = delay number" is not possible in vanilla Job targets.)
    /// </summary>
    public class JobDriver_TeacherShout : JobDriver
    {
        private const TargetIndex FaceTargetInd = TargetIndex.A;
        private Effecter roarEff;
        private int roarEndTick;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn == null || pawn.Dead || pawn.Downed);

            var init = new Toil
            {
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            init.initAction = () =>
            {
                string detectEscape = LordChats.GetText(TeacherTextKey.DetectEscape);
                SpeechBubbleManager.Get(Map)?.AddNPCChat(detectEscape, pawn, fontSize: GameFont.Medium);

                Pawn faceTarget = job.GetTarget(FaceTargetInd).Pawn;
                if (faceTarget != null)
                {
                    pawn.rotationTracker.FaceTarget(faceTarget);
                    pawn.rotationTracker.UpdateRotation();
                }
                StartTerrorRoar(pawn, LordJob_Teacher.ShoutTeacherDelayTicks);
            };
            yield return init;
            int delayTicks = job.count > 0 ? job.count : LordJob_Teacher.ShoutTeacherDelayTicks;

            var wait = Toils_General.Wait(delayTicks);
            wait.handlingFacing = false;
            wait.socialMode = RandomSocialMode.Off;
            wait.tickAction = () =>
            {
                Pawn faceTarget = job.GetTarget(FaceTargetInd).Pawn;
                if (faceTarget != null)
                {
                    pawn.rotationTracker.FaceTarget(faceTarget);
                    pawn.rotationTracker.UpdateRotation();
                }
                TickTerrorRoar(pawn);
                
            };

            wait.AddFinishAction(delegate
            {
                StopTerrorRoar();
            });


            yield return wait;
        }

        private void StartTerrorRoar(Pawn caster, int durationTicks = 60)
        {
            if (caster?.Map == null || !caster.Spawned) return;

            roarEff?.Cleanup();
            roarEff = DefDatabase<EffecterDef>.GetNamedSilentFail("TerrorRoar")?.Spawn();
            roarEndTick = Find.TickManager.TicksGame + durationTicks;
            var a = caster;
            var b = TargetInfo.Invalid;

            roarEff.Trigger(a, b);

        }

        private void TickTerrorRoar(Pawn caster)
        {
            if (roarEff == null || caster?.Map == null || !caster.Spawned) return;

            int now = Find.TickManager.TicksGame;
            if (now >= roarEndTick)
            {
                roarEff.Cleanup();
                roarEff = null;
                return;
            }

            roarEff.EffectTick(caster, TargetInfo.Invalid);
        }

        private void StopTerrorRoar()
        {
            roarEff?.Cleanup();
            roarEff = null;
        }
    }
}