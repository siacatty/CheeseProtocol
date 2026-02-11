using System;
using System.Collections.Generic;
using System.ComponentModel;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace CheeseProtocol
{
    public class JobDriver_TeacherBreakWall : JobDriver
    {
        private const TargetIndex WallInd = TargetIndex.A;
        private Thing Wall => job.GetTarget(WallInd).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(WallInd);
            this.FailOn(() => Wall == null || !Wall.Spawned);

            var gotoToil = Toils_Goto.GotoThing(WallInd, PathEndMode.Touch);
            gotoToil.AddPreInitAction(() =>
            {
                Log.Warning("[CheeseProtocol] gotoAddPreInitAction");
                string text = LordChats.GetText(TeacherTextKey.BreakWall);
                SpeechBubbleManager.Get(pawn.Map)?.AddNPCChat(text, pawn, fontSize: GameFont.Medium);
            });
            yield return gotoToil;

            Toil waitToil = Toils_General.Wait(60);
            waitToil = WithCustomProgressBarToilDelay(waitToil, TargetIndex.None, alwaysShow: true);
            yield return waitToil;

            yield return Toils_General.Do(delegate
            {
                if (Wall == null || Wall.Destroyed)
                    return;

                Map map = Wall.Map;
                IntVec3 c = Wall.Position;

                FleckMaker.ThrowDustPuff(c.ToVector3Shifted(), map, 1.5f);
                SoundDefOf.Building_Deconstructed.PlayOneShot(new TargetInfo(c, map));

                var dinfo = new DamageInfo(
                    DamageDefOf.Bomb,
                    99999f,
                    instigator: pawn
                );

                Wall.TakeDamage(dinfo);
            });
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