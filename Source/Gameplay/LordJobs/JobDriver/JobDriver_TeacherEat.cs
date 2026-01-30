using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CheeseProtocol
{
    public class JobDriver_TeacherEat : JobDriver
    {
        public const TargetIndex IngestibleSourceInd = TargetIndex.A;
        private const TargetIndex TableCellInd = TargetIndex.B;
        private bool reported;

        private Thing IngestibleSource => job.GetTarget(IngestibleSourceInd).Thing;

        private float ChewDurationMultiplier
        {
            get
            {
                Thing t = IngestibleSource;
                if (t?.def?.ingestible != null && !t.def.ingestible.useEatingSpeedStat)
                    return 1f;
                return 1f / pawn.GetStatValue(StatDefOf.EatingSpeed);
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Thing t = IngestibleSource;
            if (t == null) return false;
            int maxAmountToPickup = FoodUtility.GetMaxAmountToPickup(t, pawn, job.count);
            if (maxAmountToPickup == 0) return false;

            job.count = maxAmountToPickup;
            return pawn.Reserve(t, job, 10, maxAmountToPickup, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => reported); // 보고 후 깔끔 종료 보조(선택)

            Toil fail = ToilMaker.MakeToil("TeacherEat_Fail");
            fail.initAction = () =>
            {
                if (reported) return;
                reported = true;
                var lord = pawn.GetLord();
                if (lord != null)
                {
                    string eatFail = LordChats.GetText(TeacherTextKey.EatSnackFail);
                    SpeechBubbleManager.Get(Map)?.AddNPCChat(eatFail, pawn, speaker: SpeakerType.NonHostileNPC);
                    lord.ReceiveMemo(LordJob_Teacher.MemoEatFail);
                }
                pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
            };
            fail.defaultCompleteMode = ToilCompleteMode.Instant;

            Toil check = ToilMaker.MakeToil("TeacherEat_Check");
            check.initAction = () =>
            {
                Thing t = IngestibleSource;
                if (t == null || t.Destroyed || !t.Spawned || t.IsForbidden(pawn) || !t.IngestibleNow)
                    JumpToToil(fail);
            };
            check.defaultCompleteMode = ToilCompleteMode.Instant;

            yield return check;

            var gotoFood = Toils_Goto.GotoThing(IngestibleSourceInd, PathEndMode.Touch);
            yield return gotoFood;

            yield return check;

            yield return Toils_Ingest.PickupIngestible(IngestibleSourceInd, pawn);

            yield return Toils_Ingest.CarryIngestibleToChewSpot(pawn, IngestibleSourceInd);
            yield return Toils_Ingest.FindAdjacentEatSurface(TableCellInd, IngestibleSourceInd);

            Toil chewing = Toils_Ingest.ChewIngestible(pawn, ChewDurationMultiplier, IngestibleSourceInd, TableCellInd);
            yield return chewing;

            Toil finalize = Toils_Ingest.FinalizeIngest(pawn, IngestibleSourceInd);
            finalize.AddFinishAction(() =>
            {
                if (reported) return;
                reported = true;
                var lord = pawn.GetLord();
                if (lord != null)
                {
                    string eatSuccess = LordChats.GetText(TeacherTextKey.EatSnackSuccess);
                    SpeechBubbleManager.Get(Map)?.AddNPCChat(eatSuccess, pawn, speaker: SpeakerType.NonHostileNPC);
                    lord.ReceiveMemo(LordJob_Teacher.MemoEatSuccess);
                }
            });
            yield return finalize;
        }
    }
}