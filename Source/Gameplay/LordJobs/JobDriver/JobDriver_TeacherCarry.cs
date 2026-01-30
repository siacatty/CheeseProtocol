using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using static CheeseProtocol.CheeseLog;

namespace CheeseProtocol
{
    // Job targets:
    //   TargetIndex.A = downed student pawn to carry
    //   TargetIndex.B = destination cell (classroom spot or seat cell)
    public class JobDriver_TeacherCarry : JobDriver
    {
        private const TargetIndex StudentInd = TargetIndex.A;
        private const TargetIndex DestInd = TargetIndex.B;

        private Pawn Student => job.GetTarget(StudentInd).Pawn;
        private IntVec3 DestCell => job.GetTarget(DestInd).Cell;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Reserve the student (thing reservation).
            // For dest cell, reservation is optional; usually unnecessary unless many carriers.
            return pawn.Reserve(Student, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            job.count = 1;

            // --- Global fail conditions (safe during entire job) ---
            this.FailOn(() => pawn == null || pawn.Dead);
            this.FailOn(() => pawn.Downed);
            this.FailOn(() => pawn.health?.capacities == null || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation));
            this.FailOn(() => !DestCell.IsValid || !DestCell.InBounds(Map));

            // Student basic validity (DO NOT require Spawned here because it becomes unspawned while carried)
            this.FailOn(() => Student == null || Student.DestroyedOrNull() || Student.Dead);


            // 1) Go to student (requires student to be spawned at this moment)
            yield return Toils_Goto.GotoThing(StudentInd, PathEndMode.Touch)
                .FailOnDespawnedNullOrForbidden(StudentInd)
                .FailOn(() =>
                {
                    var s = Student;
                    return s == null || s.Dead || !s.Downed; // pickup 전에만 down 조건 확인
                });

            // 2) Pick up student
            yield return Toils_Haul.StartCarryThing(StudentInd, putRemainderInQueue: false, subtractNumTakenFromJobCount: false)
                .FailOn(() =>
                {
                    var s = Student;
                    if (s == null)
                    {
                        Warn("Carry failed: student is null");
                        return true;
                    }
                    if (!s.Downed)
                    {
                        Warn($"Carry failed: student not downed ({s})");
                        return true;
                    }
                    if (s.ParentHolder is Pawn_CarryTracker ct && ct.pawn != pawn)
                    {
                        Warn($"Carry failed: target already carried by {ct.pawn} (target={s})");
                        return true;
                    }
                    return false;
                });

            // 3) Carry to destination (now the student is inside carryTracker; do NOT check s.Spawned)
            yield return Toils_Goto.GotoCell(DestInd, PathEndMode.Touch)
                .FailOn(() =>
                {
                    // If we are no longer carrying the student, stop.
                    var carried = pawn.carryTracker?.CarriedThing;
                    if (carried == null) return true;
                    return carried != Student; // student reference stays valid while carried
                });

            // 4) Drop -> cleanup -> rejoin
            var dropAndFinalize = new Toil
            {
                initAction = () =>
                {
                    var carried = pawn.carryTracker?.CarriedThing;
                    if (carried == null) return;

                    Pawn student = Student;
                    if (student == null || student.Dead) return;

                    Thing droppedThing;
                    bool droppedOk = pawn.carryTracker.TryDropCarriedThing(DestCell, ThingPlaceMode.Near, out droppedThing);
                    if (!droppedOk) return;

                    Pawn droppedPawn = droppedThing as Pawn;
                    if (droppedPawn == null || droppedPawn != student) return;
                    if (!droppedPawn.Spawned) return;

                    // Remove Down hediff (if your design wants instant wake-up)
                    HediffDef downDef = DefDatabase<HediffDef>.GetNamedSilentFail("CheeseProtocol_LessonDisciplineDown");
                    if (downDef != null)
                    {
                        var h = droppedPawn.health?.hediffSet?.GetFirstHediffOfDef(downDef);
                        if (h != null) droppedPawn.health.RemoveHediff(h);
                    }

                    var lord = pawn.GetLord();
                    var lj = lord?.LordJob as LordJob_Teacher;

                    if (lj != null)
                    {
                        string uid = droppedPawn.GetUniqueLoadID();
                        if (!uid.NullOrEmpty())
                        {
                            lj.escapingStudentUIDs?.Remove(uid);
                            lj.stunnedStudentUIDs?.Remove(uid);
                            lj.lostStudentUIDs?.Remove(uid);
                            lj.lostCache.Remove(uid);
                            if (lj.currentTargetUid == uid)
                                lj.currentTargetUid = null;
                        }
                    }

                    // Rejoin AFTER drop, and only if not lost
                    if (lord != null && droppedPawn.GetLord() != lord && !droppedPawn.Downed)
                    {
                        lord.AddPawn(droppedPawn);
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            yield return dropAndFinalize;
        }
    }
}