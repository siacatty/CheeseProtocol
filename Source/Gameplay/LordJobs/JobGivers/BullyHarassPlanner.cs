using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace CheeseProtocol
{
    /// <summary>
    /// Harass planner: returns a job to run NOW, and may mutate BullyState (target/cooldown).
    /// Designed to be called from LordToilTick (TryTakeOrderedJob) or ThinkTree (TryGiveJob).
    /// </summary>
    public static class BullyHarassPlanner
    {
        private const int RetargetIntervalTicks = 120; // 2s
        private const float StunRange = 1.5f;

        private const int StunTicksMin = 60;
        private const int StunTicksMax = 120;

        private const int CooldownMin = 120;
        private const int CooldownMax = 240;

        public static Job TryBuildJob(Pawn bully)
        {
            Map map = bully?.Map;
            if (map == null) return null;

            var reg = map.GetComponent<BullyRegistry_MapComponent>();
            if (reg == null) return null;

            if (!reg.TryGetBully(bully, out var st) || st == null)
                return null;

            int now = Find.TickManager.TicksGame;

            // Phase gates: only active during harass window
            if (now < st.harassTick) return null;
            if (st.stealTick > 0 && now >= st.stealTick) return null;
            if (st.exitAtTick > 0 && now >= st.exitAtTick) return null;
            if (st.didSteal) return null;

            // Resolve/retarget
            Pawn target = null;
            bool hasTarget =
                reg.TryResolveTargetPawn(st, out target, preferThisMap: true) &&
                IsValidTarget(bully, target);

            bool canRetargetNow = now - st.lastRetargetTick >= RetargetIntervalTicks;

            if (!hasTarget || (canRetargetNow && ShouldRetarget(bully, target)))
            {
                Pawn newTarget = TryPickTarget(bully);
                if (newTarget != null)
                {
                    st.targetPawnUid = newTarget.GetUniqueLoadID();
                    st.lastRetargetTick = now;
                    target = newTarget;
                }
                else
                {
                    target = null;
                }
            }

            // If no targets exist at all => wander near colony
            if (target == null)
                return MakeWanderNearColony(bully);

            // Cooldown: follow only
            if (now < st.nextStunTick)
                return MakeGotoJob(target);

            // Close enough: stun + set cooldown + keep them busy with a short follow job
            if (bully.Position.DistanceTo(target.Position) <= StunRange)
            {
                ApplyStun(bully, target);
                st.nextStunTick = now + Rand.RangeInclusive(CooldownMin, CooldownMax);

                // Important: return a small "follow/hover" job instead of null
                // so the pawn doesn't fall back to leave/idle logic immediately.
                return MakeGotoJob(target, expiry: 60);
            }

            // Otherwise approach
            return MakeGotoJob(target);
        }

        private static Job MakeGotoJob(Pawn target, int expiry = 120)
        {
            var job = JobMaker.MakeJob(JobDefOf.Goto, target);
            job.expiryInterval = expiry;
            job.checkOverrideOnExpire = true;
            job.locomotionUrgency = LocomotionUrgency.Jog;
            return job;
        }

        private static Job MakeWanderNearColony(Pawn pawn)
        {
            if (TryFindWanderCellNearColony(pawn, out var wanderCell))
            {
                var job = JobMaker.MakeJob(JobDefOf.Goto, wanderCell);
                job.expiryInterval = 180;
                job.checkOverrideOnExpire = true;
                job.locomotionUrgency = LocomotionUrgency.Jog;
                return job;
            }
            return null;
        }

        private static bool IsValidTarget(Pawn bully, Pawn t)
        {
            if (t == null) return false;
            if (!t.Spawned) return false;
            if (t.Dead) return false;
            if (t.Map != bully.Map) return false;
            if (!t.IsColonist) return false; // 너가 downed도 허용했다면 downed 체크는 빼면 됨
            return true;
        }

        private static bool ShouldRetarget(Pawn bully, Pawn currentTarget)
        {
            if (currentTarget == null) return true;
            return bully.Position.DistanceTo(currentTarget.Position) > 60f;
        }

        private static Pawn TryPickTarget(Pawn bully)
        {
            var list = bully.Map?.mapPawns?.FreeColonistsSpawned;
            if (list == null || list.Count == 0) return null;

            Pawn best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < list.Count; i++)
            {
                Pawn c = list[i];
                if (!IsValidTarget(bully, c)) continue;

                float d = bully.Position.DistanceTo(c.Position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = c;
                }
            }
            return best;
        }

        private static void ApplyStun(Pawn bully, Pawn victim)
        {
            var stunner = victim?.stances?.stunner;
            if (stunner == null) return;

            int stunTicks = Rand.RangeInclusive(StunTicksMin, StunTicksMax);
            stunner.StunFor(stunTicks, bully);
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
                        c => c.Standable(map),
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

            return CellFinder.TryFindRandomReachableNearbyCell(
                pawn.Position, map, 12,
                TraverseParms.For(pawn),
                c => c.Standable(map),
                null,
                out cell);
        }
    }
}