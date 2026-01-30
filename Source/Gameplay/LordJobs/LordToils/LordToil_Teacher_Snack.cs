using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CheeseProtocol
{
    public class LordToil_Teacher_Snacks : LordToil
    {
        private const int CheckIntervalTicks = 30;
        private const int SearchMealDelayTicks = 400;
        private const float SearchRadius = 75f;
        private Pawn teacher;
        private int nextCheckTick;
        private int startSearchMealAtTick;

        public LordToil_Teacher_Snacks(Pawn teacher)
        {
            this.teacher = teacher;
            startSearchMealAtTick = Find.TickManager.TicksGame + SearchMealDelayTicks;
        }

        public override void Init()
        {
            base.Init();
            var job = lord.LordJob as LordJob_Teacher;
            if (teacher == null)
                teacher = job.teacher;
        }

        public override void LordToilTick()
        {
            base.LordToilTick();

            int now = Find.TickManager.TicksGame;
            if (now < startSearchMealAtTick || now < nextCheckTick) return;
            nextCheckTick = now + CheckIntervalTicks;

            if (teacher == null) return;
            var pawns = lord?.ownedPawns;
            if (pawns == null || pawns.Count == 0) return;
            var lj = lord?.LordJob as LordJob_Teacher;
            if (lj == null) return;

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (!ShouldEatNow(p))
                {
                    string eatFail = LordChats.GetText(TeacherTextKey.EatSnackFail);
                    SpeechBubbleManager.Get(Map)?.AddNPCChat(eatFail, p, speaker: SpeakerType.NonHostileNPC);
                    lord?.ReceiveMemo(LordJob_Teacher.MemoEatFail);
                    continue;
                }
                var cur = p.CurJob;
                bool isEating = cur != null && cur.def.defName == "CheeseProtocol_TeacherEat";
                if (isEating) continue;
                Thing meal = FindNearbyMeal(p, SearchRadius);
                if (meal == null)
                {
                    string eatFail = LordChats.GetText(TeacherTextKey.EatSnackFail);
                    SpeechBubbleManager.Get(Map)?.AddNPCChat(eatFail, p, speaker: SpeakerType.NonHostileNPC);
                    lord?.ReceiveMemo(LordJob_Teacher.MemoEatFail);
                    continue;
                }

                if (CanStartIngestJobNow(p, meal))
                    StartIngestJob(p, meal);
            }
        }
        private void StartIngestJob(Pawn teacher, Thing target)
        {
            if (teacher == null || target == null) return;
            //Warn($"StartSubdue Job => {target}");
            JobDef def = DefDatabase<JobDef>.GetNamedSilentFail("CheeseProtocol_TeacherEat");
            if (def == null) return;

            Job job = JobMaker.MakeJob(def, target);
            job.expiryInterval = 2400;
            job.checkOverrideOnExpire = true;
            job.playerForced = true;
            teacher.jobs.StartJob(job, JobCondition.InterruptForced, resumeCurJobAfterwards: false);
        }
        private bool CanStartIngestJobNow(Pawn teacher, Thing target)
        {
            if (teacher == null || target == null) return false;
            if (teacher.Downed || teacher.Dead) return false;

            var cur = teacher.CurJob;
            if (cur == null) return true;
            if (cur.def.defName == "CheeseProtocol_TeacherEat"
                && cur.targetA.Thing == target)
                return false;
            return true;
        }
        public override void UpdateAllDuties()
        {
            if (teacher == null || teacher.Dead) return;
            teacher.mindState.duty = new PawnDuty(DutyDefOf.Defend, teacher.Position);
        }

        private static bool ShouldEatNow(Pawn p)
        {
            if (p == null || p.Dead || p.Downed) return false;
            if (!p.Spawned || p.Map == null) return false;

            var foodNeed = p.needs?.food;
            if (foodNeed == null) return false;
            //if (foodNeed.CurCategory < HungerCategory.Hungry) return false;
            return true;
        }

        private static Thing FindNearbyMeal(Pawn pawn, float maxDist)
        {
            if (pawn?.Map == null) return null;

            Predicate<Thing> validator = t =>
                t != null
                && t.Spawned
                && t.def?.ingestible != null
                && (t.def.ingestible.foodType & FoodTypeFlags.Meal) != 0
                && t.IngestibleNow
                && pawn.CanReserve(t)
                && pawn.CanReach(t, PathEndMode.Touch, Danger.Some);

            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.FoodSourceNotPlantOrTree),
                PathEndMode.Touch,
                TraverseParms.For(pawn, Danger.Some),
                maxDist,
                validator
            );
        }
    }
}