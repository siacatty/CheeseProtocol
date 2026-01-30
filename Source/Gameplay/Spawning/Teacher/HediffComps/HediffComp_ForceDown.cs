using Verse;
using static CheeseProtocol.CheeseLog;

namespace CheeseProtocol
{
    public class HediffCompProperties_ForceDown : HediffCompProperties
    {
        public int durationTicks = 1200;

        public HediffCompProperties_ForceDown()
        {
            compClass = typeof(HediffComp_ForceDown);
        }
    }

    public class HediffComp_ForceDown : HediffComp
    {
        private int ticksLeft;

        public HediffCompProperties_ForceDown Props =>
            (HediffCompProperties_ForceDown)props;

        public override void CompPostMake()
        {
            base.CompPostMake();
            ticksLeft = Props.durationTicks;
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (Pawn == null || Pawn.Dead)
                return;
            if (Pawn.IsHashIntervalTick(60)) Warn($"ForceDown tick on {Pawn} (downed={Pawn.Downed}");
            Pawn.health.forceDowned = true;
            if (!Pawn.Downed)
            {
                Pawn.jobs?.StopAll();
                Pawn.stances?.CancelBusyStanceSoft();
                Pawn.pather?.StopDead();
            }
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            if (Pawn == null || Pawn.Dead) return;
            Pawn.health.forceDowned = false;
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", 0);
        }
    }
}