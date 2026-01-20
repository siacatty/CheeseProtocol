using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using static CheeseProtocol.CheeseLog;

namespace CheeseProtocol
{
    /// <summary>
    /// Bully lord job (AnimalPass-style):
    /// Travel (gather near colony) -> Harass (tick-driven jobs) -> Steal (tick-driven jobs) -> ExitMap
    ///
    /// Key idea:
    /// - Do NOT rely on pawn think trees for harass/steal.
    /// - LordToilTick issues jobs directly so pawn won't "auto-leave" due to faction/guest/trader logic.
    /// </summary>
    public class LordJob_Bully : LordJob
    {
        private const string MemoTravelArrived = "TravelArrived";
        private const string MemoGoSteal = "Bully.GoSteal";
        private const string MemoExit = "Bully.Exit";

        private IntVec3 travelDest = IntVec3.Invalid;
        private IntVec3 exitCell = IntVec3.Invalid;
        private readonly HashSet<string> claimedStealUids = new HashSet<string>();

        // Tunables (can be moved to settings later)
        private const int TickInterval = 60;          // 1 sec
        private const float HarassApproachStopDist = 2.9f;
        private const float WanderApproachStopDist = 1.0f;

        public LordJob_Bully() { } // Scribe

        public LordJob_Bully(Pawn lord)
        {
            if (!TryFindWanderCellNearColony(lord, out travelDest))
                travelDest = lord.Map?.Center ?? IntVec3.Invalid;
        }

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            if (!travelDest.IsValid)
                return graph;

            // 1) Gather near colony
            var travel = new LordToil_Travel(travelDest);

            // 2) Harass phase (tick-driven)
            var harass = new LordToil_BullyHarass(travelDest);

            // 3) Steal phase (tick-driven)
            var steal = new LordToil_BullySteal(travelDest);

            // 4) Exit
            var exit = new LordToil_ExitMap();

            graph.StartingToil = travel;
            graph.AddToil(harass);
            graph.AddToil(steal);
            graph.AddToil(exit);

            // Travel arrived -> Harass
            var toHarass = new Transition(travel, harass);
            toHarass.AddTrigger(new Trigger_Memo(MemoTravelArrived));
            //toHarass.AddPreAction(new TransitionAction_SetDefendLocalGroup());
            toHarass.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(toHarass);

            // Harass -> Steal (memo OR condition)
            var toSteal = new Transition(harass, steal);
            toSteal.AddTrigger(new Trigger_Memo(MemoGoSteal));
            toSteal.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(toSteal);

            // Harass -> Exit (condition)
            var toExitFromHarass = new Transition(harass, exit);
            toExitFromHarass.AddTrigger(new Trigger_Memo(MemoExit));
            toExitFromHarass.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(toExitFromHarass);

            // Steal -> Exit
            var toExitFromSteal = new Transition(steal, exit);
            toExitFromSteal.AddTrigger(new Trigger_Memo(MemoExit));
            toExitFromSteal.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(toExitFromSteal);

            return graph;
        }
        public bool TryClaimStealTarget(string uid)
        {
            if (uid.NullOrEmpty()) return false;
            return claimedStealUids.Add(uid);
        }

        public void ReleaseStealTarget(string uid)
        {
            if (uid.NullOrEmpty()) return;
            claimedStealUids.Remove(uid);
        }

        public void ClearStealTargets()
        {
            claimedStealUids.Clear();
        }

        public bool IsStealTargetClaimed(string uid)
        {
            if (uid.NullOrEmpty()) return false;
            return claimedStealUids.Contains(uid);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref travelDest, "travelDest", IntVec3.Invalid);
            Scribe_Values.Look(ref exitCell, "exitCell", IntVec3.Invalid);
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

        // ----------------------------
        // Toils
        // ----------------------------

        private abstract class LordToil_BullyBase : LordToil
        {
            protected const int WanderRefreshInterval = 600; //10 seconds
            protected const int WanderRetargetInterval = 1200; //20 seconds
            protected const int ResetMoveDistSq = 650;
            protected readonly IntVec3 anchor;
            private int nextTick;
            protected List<IntVec3> storageAnchors;
            protected LordJob_Bully BullyJob
            {
                get => lord?.LordJob as LordJob_Bully;
            }
            protected LordToil_BullyBase(IntVec3 anchor)
            {
                this.anchor = anchor;
            }

            public override void Init()
            {
                base.Init();
                nextTick = 0;
                storageAnchors = StealAnchorClusterUtil.BuildClusterAnchors(lord?.Map);
                StealAnchorClusterUtil.AddPassengerShuttleAnchors(lord?.Map, storageAnchors);
            }

            public override void UpdateAllDuties()
            {
                // IMPORTANT:
                // Give *some* duty so ThinkNode_DutyConstant won't complain.
                // But we won't rely on duty for behavior; we directly issue jobs.
                var pawns = lord?.ownedPawns;
                if (pawns == null) return;

                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn p = pawns[i];
                    if (p == null || p.Dead) continue;

                    // Keep them generally "around" the anchor; low aggression semantics.
                    p.mindState.duty = new PawnDuty(DutyDefOf.Defend, anchor);
                }
            }

            public override void LordToilTick()
            {
                base.LordToilTick();
                var pawns = lord?.ownedPawns;
                if (pawns != null)
                {
                    for (int i = 0; i < pawns.Count; i++)
                    {
                        var p = pawns[i];
                        if (p == null || p.Dead || !p.Spawned) continue;
                        p.mindState.exitMapAfterTick = -1;
                    }
                }


                int now = Find.TickManager.TicksGame;
                if (now < nextTick) return;
                nextTick = now + TickInterval;

                TickIntervalLogic(now);
            }

            protected abstract void TickIntervalLogic(int now);

            protected BullyRegistry_MapComponent GetRegistry()
            {
                Map map = lord?.Map;
                if (map == null) return null;
                return map.GetComponent<BullyRegistry_MapComponent>();
            }

            protected static bool IsBusy(Pawn p)
            {
                // If in melee, downed, dead, etc., don't force jobs.
                if (p == null || p.Dead || p.Downed) return true;
                if (p.stances?.FullBodyBusy ?? false) return true;
                return false;
            }

            protected static bool IsExitGotoJob(Job j)
            {
                // Typical exit jobs are JobDefOf.Goto with exitMapOnArrival=true.
                return j != null && j.def == JobDefOf.Goto && j.exitMapOnArrival;
            }

            protected static Job MakeGotoPawnJob(Pawn pawn, Pawn target)
            {
                if (target == null) return null;

                Job job = JobMaker.MakeJob(JobDefOf.Goto, target);
                job.expiryInterval = 300;                 // re-evaluate often
                job.checkOverrideOnExpire = true;         // allow replacing
                job.locomotionUrgency = LocomotionUrgency.Jog;
                return job;
            }

            protected static Job MakeGotoCellJob(IntVec3 cell)
            {
                Job job = JobMaker.MakeJob(JobDefOf.Goto, cell);
                job.expiryInterval = 300;
                job.checkOverrideOnExpire = true;
                job.locomotionUrgency = LocomotionUrgency.Jog;
                return job;
            }

            protected Job MakeWanderNearAnchor(Pawn pawn, IntVec3 anchor)
            {
                Map map = pawn?.Map;
                if (map == null) return null;

                if (!CellFinder.TryFindRandomCellNear(anchor, map, 10,
                        c => c.Standable(map) && !c.Fogged(map) && !c.OnEdge(map),
                        out var c2))
                {
                    c2 = anchor;
                }

                return MakeGotoCellJob(c2);
            }

            protected static void ForceJob(Pawn p, Job job)
            {
                if (p == null || job == null) return;
                // Stronger than CheckForJobOverride; this is what makes “AnimalPass pattern” stable.
                p.jobs.TryTakeOrderedJob(job);
            }

            protected IntVec3 GetWanderWhileStealAnchor(Pawn bully, BullyState st, IntVec3 fallbackAnchor)
            {
                Map map = bully.Map;
                if (map == null)
                    return fallbackAnchor;

                int now = Find.TickManager.TicksGame;

                if (st.wanderAnchor.IsValid && now - st.wanderAnchorTick < WanderRefreshInterval)
                    return st.wanderAnchor;

                IntVec3 chosen = bully.Position;
                IntVec3 target = IntVec3.Invalid;
                Pawn targetPawn = null;
                var reg = GetRegistry();
                bool hasPawnTarget =
                    reg != null &&
                    reg.TryResolveWanderTargetPawn(st, out targetPawn) &&
                    IsValidTarget(bully, targetPawn);

                // Goto Storage
                if (!st.targetStorage.IsValid && !hasPawnTarget)
                {
                    if (storageAnchors != null && storageAnchors.Count > 0)
                    {
                        st.targetStorage = storageAnchors.RandomElement();
                        st.wanderRetargetTick = now; 
                    }
                }
                if (st.targetStorage.IsValid && !bully.CanReach(st.targetStorage, PathEndMode.Touch, Danger.Some))
                {
                    st.targetStorage = IntVec3.Invalid;
                }
                target = st.targetStorage;
                // Follow Pawn
                if (!target.IsValid)
                {
                    if (!hasPawnTarget)
                    {
                        Pawn newTarget = TryPickTargetRandom(bully);
                        if (newTarget != null)
                        {
                            st.wanderTargetUid = newTarget.GetUniqueLoadID();
                            st.wanderRetargetTick = now;
                            targetPawn = newTarget;
                        }
                    }
                    if (targetPawn != null)
                        target = targetPawn.Position;
                }

                if (target.IsValid)
                {
                    if (st.wanderRetargetTick <= 0)
                        st.wanderRetargetTick = now;

                    bool invalidPos =
                        !target.InBounds(map) ||
                        target.OnEdge(map);

                    if (invalidPos)
                    {
                        st.ResetWanderTarget();
                    }
                    else
                    {
                        bool arrived = bully.Position.DistanceTo(target) <= WanderApproachStopDist;
                        bool timeout = now - st.wanderRetargetTick >= WanderRetargetInterval;

                        if (arrived || timeout)
                        {
                            st.ResetWanderTarget();
                            st.wanderAnchorTick = 0;
                        }
                        else
                        {
                            chosen = target;
                        }
                    }
                }

                if (!chosen.IsValid || !chosen.InBounds(map) || chosen.OnEdge(map))
                    chosen = fallbackAnchor.IsValid ? fallbackAnchor : map.Center;
                
                st.wanderAnchor = chosen;
                st.wanderAnchorTick = now;

                if (!st.lastScanPos.IsValid)
                {
                    st.lastScanPos = st.wanderAnchor;
                }
                else if (st.lastScanPos.DistanceToSquared(st.wanderAnchor) > ResetMoveDistSq)
                {
                    QMsg($"Scan Reset: lastScanPos={st.lastScanPos}, updated={st.wanderAnchor}, Distance={st.lastScanPos.DistanceToSquared(st.wanderAnchor)}", Channel.Debug);
                    st.ResetScan();
                    st.lastScanPos = st.wanderAnchor;
                }

                return chosen;
            }

            protected static bool IsValidTarget(Pawn bully, Pawn t)
            {
                if (t == null) return false;
                if (!t.Spawned) return false;
                if (t.Dead) return false;
                if (t.Map != bully.Map) return false;
                if (!t.IsColonist) return false;
                if (!bully.CanReach(t, PathEndMode.Touch, Danger.Some)) return false;
                return true;
            }
            protected static Pawn TryPickTargetClosest(Pawn bully)
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
            protected static Pawn TryPickTargetFurthest(Pawn bully)
            {
                var list = bully.Map?.mapPawns?.FreeColonistsSpawned;
                if (list == null || list.Count == 0) return null;

                Pawn best = null;
                float bestDist = float.MinValue;

                for (int i = 0; i < list.Count; i++)
                {
                    Pawn c = list[i];
                    if (!IsValidTarget(bully, c)) continue;

                    float d = bully.Position.DistanceTo(c.Position);
                    if (d > bestDist)
                    {
                        bestDist = d;
                        best = c;
                    }
                }
                return best;
            }
            protected static Pawn TryPickTargetRandom(Pawn bully)
            {
                var list = bully.Map?.mapPawns?.FreeColonistsSpawned;
                if (list == null || list.Count == 0) return null;
                var shuffled = list.ToList();
                shuffled.Shuffle();
                Pawn random = null;

                for (int i = 0; i < shuffled.Count; i++)
                {
                    Pawn c = list[i];
                    if (IsValidTarget(bully, c))
                    {
                        random = c;
                        break;
                    }
                }
                return random;
            }
            protected void EnsureSharedExitCellOnce(LordJob_Bully job, List<Pawn> pawns)
            {
                if (job.exitCell.IsValid) return;

                Pawn any = pawns.FirstOrDefault(p => p != null && p.Spawned && !p.Dead && p.Map != null);
                if (any == null) return;

                job.exitCell = EnsureSharedExitCell(any, job.exitCell);
                if (!job.exitCell.InBounds(any.Map))
                    job.exitCell = CellFinder.RandomEdgeCell(any.Map);
            }
            protected static IntVec3 EnsureSharedExitCell(Pawn pawn, IntVec3 cur)
            {
                var map = pawn.Map;
                if (map == null) return IntVec3.Invalid;

                bool ok =
                    cur.InBounds(map) &&
                    cur.Standable(map) &&
                    pawn.CanReach(cur, PathEndMode.OnCell, Danger.Some);

                if (ok) return cur;

                if (CellFinder.TryFindRandomEdgeCellWith(
                        c => c.Standable(map) && pawn.CanReach(c, PathEndMode.OnCell, Danger.Some),
                        map, 0f, out var found))
                    return found;

                return CellFinder.RandomEdgeCell(map);
            }
            protected static bool IsEatingOrAboutToEat(Pawn p)
            {
                var j = p.CurJob;
                if (j == null) return false;

                // vanilla eating jobs
                if (j.def == JobDefOf.Ingest) 
                {
                    QMsg($"Harass: Eating food -> don't interrupt.", Channel.Debug);
                    return true;
                }
                if (j.def.defName != null && j.def.defName.IndexOf("Ingest", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                return false;
            }
        }

        /// <summary>
        /// Harass phase: follow target; if in range and cooldown ready, apply stun; if no target, wander near anchor.
        /// Switches to Steal when any bully reaches stealTick.
        /// Exits when any bully hits exitAtTick (or you can change to "all bullies" policy later).
        /// </summary>
        private class LordToil_BullyHarass : LordToil_BullyBase
        {
            private bool didAnnounceStealStart=false;
            public LordToil_BullyHarass(IntVec3 anchor) : base(anchor) { }
            private Pawn leader;
            private const int StunCooldown = 900; //every 15 seconds.
            protected override void TickIntervalLogic(int now)
            {
                var reg = GetRegistry();
                if (reg == null) return;

                var pawns = lord?.ownedPawns;
                if (pawns == null || pawns.Count == 0) return;

                bool anyStealDue = false;

                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn bully = pawns[i];
                    if (!reg.TryGetBully(bully, out var st) || st == null)
                    {
                        continue;
                    }

                    var carried = bully.carryTracker?.CarriedThing;
                    if (carried != null)
                    {
                        if (carried.def?.ingestible != null)
                        {
                            QMsg($"Harass: Carrying food ({carried.LabelCap}) -> don't interrupt.", Channel.Debug);
                            continue;
                        }
                    }
                    if (IsExitGotoJob(bully.CurJob))
                    {
                        ForceJob(bully, MakeWanderNearAnchor(bully, anchor));
                        continue;
                    }
                    if (IsBusy(bully)) 
                    {
                        continue;
                    }

                    if (leader == null && st.isLeader) leader = bully;
                    // Phase transitions (group-level policy)
                    if (st.exitAtTick > 0 && now >= st.exitAtTick)
                    {
                        lord.ReceiveMemo(MemoExit);
                        return;
                    }

                    if (st.stealTick > 0 && now >= st.stealTick)
                        anyStealDue = true;

                    // If the pawn somehow already started leaving, yank it back.

                    bool hasTarget = reg.TryResolveTargetPawn(st, out Pawn target) && IsValidTarget(bully, target);
                    if (!hasTarget)
                    {
                        Pawn newTarget = TryPickTargetClosest(bully);
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
                    {
                        ForceJob(bully, MakeWanderNearAnchor(bully, anchor));
                        continue;
                    }

                    // Cooldown gate: st.nextStunTick (rename harassTick if you already did)
                    if (now < st.nextStunTick)
                    {
                        ForceJob(bully, MakeGotoPawnJob(bully, target));
                        continue;
                    }

                    // Close enough => stun action, then set cooldown
                    if (bully.Position.DistanceTo(target.Position) <= HarassApproachStopDist)
                    {
                        // Minimal vanilla stun example (you can swap to "stun carrier" later)
                        // target.stances.stunner.StunFor(60, bully);
                        BullyActions.ApplyStun(bully, target, pawns.Count); // <- implement or redirect to your existing method
                        st.nextStunTick = now + StunCooldown;
                        continue;
                    }
                    // Otherwise approach
                    ForceJob(bully, MakeGotoPawnJob(bully, target));
                }

                if (anyStealDue)
                {
                    if (!didAnnounceStealStart)
                    {
                        string startStealChat = LordChats.GetText(BullyTextKey.StartSteal);
                        SpeechBubbleManager.Get(leader.Map)?.AddNPCChat(startStealChat, leader);
                        didAnnounceStealStart = true;
                    }
                    lord.ReceiveMemo(MemoGoSteal);
                }
            }
        }

        /// <summary>
        /// Steal phase: you said steal is right before exit.
        /// Here we just call into your steal action/job logic per pawn, and once any didSteal (or time runs out), exit.
        /// </summary>
        private class LordToil_BullySteal : LordToil_BullyBase
        {
            private bool didAnnounceLeave = false;
            public LordToil_BullySteal(IntVec3 anchor) : base(anchor) { }

            protected override void TickIntervalLogic(int now)
            {
                var job = BullyJob;
                if (job == null) 
                {
                    return;
                }
                var reg = GetRegistry();
                if (reg == null) 
                {
                    return;
                }

                var pawns = lord?.ownedPawns;
                if (pawns == null || pawns.Count == 0)
                {
                    return;
                }
                EnsureSharedExitCellOnce(job, pawns); 
                if (!job.exitCell.IsValid)
                {
                    return;
                }
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn bully = pawns[i];

                    if (!reg.TryGetBully(bully, out var st) || st == null)
                    {
                        continue;
                    }
                    if (st.exitAtTick > 0 && now >= st.exitAtTick)
                    {
                        if (!didAnnounceLeave)
                        {
                            if (st.isLeader)
                            {
                                string leaveChat = LordChats.GetText(BullyTextKey.ExitNow);
                                SpeechBubbleManager.Get(bully.Map)?.AddNPCChat(leaveChat, bully);
                                didAnnounceLeave = true;
                            }
                        }
                        job?.ClearStealTargets();
                        lord.ReceiveMemo(MemoExit);
                        return;
                    }
                    if (st.finalStealAtTick > 0 && now >= st.finalStealAtTick)
                    {
                        st.targetValue = 50f;
                    }
                    if (!st.shouldExit && IsExitGotoJob(bully.CurJob))
                    {
                        ForceJob(bully, MakeWanderNearAnchor(bully, anchor));
                        continue;
                    }
                    if (st.shouldExit)
                    {
                        if (!IsExitGotoJob(bully.CurJob))
                            ForceJob(bully, BullyActions.MakeExitJob(job.exitCell));
                        continue;
                    }
                    if (IsBusy(bully)) 
                    {
                        continue;
                    }

                    if (st.didSteal || st.giveupSteal)
                    {
                        if (!st.stealTargetUid.NullOrEmpty())
                        {
                            job.ReleaseStealTarget(st.stealTargetUid);
                            st.stealTargetUid = null;
                        }
                        ForceJob(bully, BullyActions.MakeExitJob(job.exitCell));
                        st.shouldExit = true;
                        continue;
                    }

                    // Let your existing steal logic pick target based on value and issue a job
                    // If you already have JobGiver_BullySteal logic, you can expose a method and call it here.
                    // Example placeholder:
                    Job stealJob = BullyActions.TryMakeStealJob(bully, st, anchor, job, out bool keepCurrentJob); // <- you implement
                    if (keepCurrentJob) continue;
                    if (stealJob != null)
                    {
                        ForceJob(bully, stealJob);
                    }
                    else
                    {
                        
                        ForceJob(bully, MakeWanderNearAnchor(bully, GetWanderWhileStealAnchor(bully, st, anchor)));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Small indirection so LordJob can call your existing stun/steal code without circular deps.
    /// Fill these using your current implementations.
    /// </summary>
    internal static class BullyActions
    {
        private const int MaxStealAttempts = 5;
        private const int StunDuration = 180;
        private const int RadiusStep = 20;
        private const int MaxRadius = 75;
        private const float LoRatioStep = 0.05f;
        private const float MinLoRatio = 0f;
        public static void ApplyStun(Pawn bully, Pawn target, int pawnCount)
        {
            if (bully == null || target == null) return;
            // TEMP: classic stun on target (you said later you want "carrier" stunned instead)
            if (pawnCount > 0 && Rand.Chance(0.8f/pawnCount))
            {
                string stunChat = LordChats.GetText(BullyTextKey.StunColonist, target.Name.ToStringShort);
                SpeechBubbleManager.Get(bully.Map)?.AddNPCChat(stunChat, bully);
            }
            target.stances?.stunner?.StunFor(StunDuration, bully);
            if (target.jobs?.curJob != null)
            {
                target.jobs.EndCurrentJob(JobCondition.InterruptForced, true);
            }
        }

        public static Job TryMakeStealJob(Pawn bully, BullyState st, IntVec3 anchor, LordJob_Bully jobBully, out bool keepCurrentJob)
        {
            keepCurrentJob = false;
            if (bully == null || bully.Dead || bully.Downed) 
            {
                return null;
            }
            Map map = bully.Map;
            if (map == null) 
            {
                return null;
            }
            var carried = bully.carryTracker?.CarriedThing;
            if (carried != null)
            {
                if (!st.stealTargetUid.NullOrEmpty() && carried.GetUniqueLoadID() == st.stealTargetUid)
                {
                    jobBully?.ReleaseStealTarget(st.stealTargetUid);
                    st.stealTargetUid = null;

                    st.didSteal = true;
                    st.stolenThing = carried;
                    QMsg("Steal: Carrying our steal target -> success.", Channel.Debug);
                    return null;
                }
                if (carried.def?.ingestible != null)
                {
                    QMsg($"Steal: Carrying food ({carried.LabelCap}) -> don't interrupt.", Channel.Debug);
                    keepCurrentJob = true;
                    return null;
                }
                QMsg($"Steal: Carrying non-target ({carried.LabelCap}) -> ignore.", Channel.Debug);
            }

            if (st.stealAttempts >= MaxStealAttempts)
            {
                jobBully?.ReleaseStealTarget(st.stealTargetUid);
                st.stealTargetUid = null;
                st.giveupSteal = true;
                QWarn($"{bully.Name?.ToStringShort ?? "Unknown"}: Failed to steal after {MaxStealAttempts}. Giving up.");
                return null;
            }

            Thing stealTarget = null;
            int now = Find.TickManager.TicksGame;

            if (!st.stealTargetUid.NullOrEmpty())
            {
                stealTarget = FindThingByUid(map, st.stealTargetUid, st.stealContainerUid);
            }
            if (stealTarget != null && IsValidStealTarget(stealTarget, bully))
            {
                if (st.stealTargetPickTick > 0 && now - st.stealTargetPickTick > 1800)
                {
                    jobBully?.ReleaseStealTarget(st.stealTargetUid);
                    st.stealTargetUid = null;
                    st.stealTargetPickTick = 0;
                    st.stealAttempts++;
                    QWarn($"Steal: Tried to steal target {stealTarget.LabelCap} for 1800 ticks. Search for new target.");
                    return null;
                }
            }
            else
            {
                jobBully?.ReleaseStealTarget(st.stealTargetUid);
                st.stealTargetUid = null;
                st.stealTargetPickTick = 0;
                stealTarget = null;
            }
            if (stealTarget == null && now - st.stealTargetSearchTick > 300) //search every 300 ticks (5 seconds)
            {
                st.stealTargetSearchTick = now;
                st.didSearch = true;
                IntVec3 center = bully.Position;

                
                if (st.scanRadius < MaxRadius)
                {
                    st.scanRadius = Mathf.Min(st.scanRadius + RadiusStep, MaxRadius);
                }
                else if (st.loRatio > MinLoRatio)
                {
                    st.loRatio = Mathf.Max(st.loRatio - LoRatioStep, MinLoRatio);
                }
                var things = ScanCellsAround(map, center, st.scanRadius);
                //QMsg($"{bully.Name.ToStringShort}: Searching... scanRadius={st.scanRadius}, loRatio={st.loRatio}", Channel.Debug);
                var top = PickClosestBelowValueTopN(bully, things, n: 20, targetValue: st.targetValue, minValue: 30f, job: jobBully, tooSmallRatio: st.loRatio);
                Thing picked = null;
                for (int i = 0; i < top.Count; i++)
                {
                    string uid = top[i].GetUniqueLoadID();
                    if (jobBully == null || jobBully.TryClaimStealTarget(uid))
                    {
                        picked = top[i];
                        QMsg($"{bully.Name.ToStringShort}: claimed={picked.LabelCap}, scanRadius={st.scanRadius}, loRatio={st.loRatio}", Channel.Debug);
                        break;
                    }
                }
                stealTarget = picked;
                if (stealTarget != null)
                {
                    st.stealTargetUid = stealTarget.GetUniqueLoadID();
                    var container = GetContainer(stealTarget);
                    st.stealContainerUid = container == null ? null : container.GetUniqueLoadID();
                    st.stealTargetPickTick = now;
                    st.ResetScan();
                }
            }

            if (stealTarget == null)
            {
                //Warn("stealTarget Reset to none.");
                st.stealTargetUid = null; 
                return null;
            }
            Thing gotoTarget = stealTarget?.SpawnedParentOrMe;
            if (gotoTarget == null) return null;
            if (bully.CanReach(gotoTarget, PathEndMode.ClosestTouch, Danger.Some) &&
                TouchPathEndModeUtility.IsAdjacentOrInsideAndAllowedToTouch(bully.Position, gotoTarget, bully.Map.pathing.Normal))
            {
                var holder = GetContainer(stealTarget);
                if (holder != null)
                {
                    if (!TryDropFromContainer(holder, stealTarget, out Thing dropped))
                        return null;
                    stealTarget = dropped;
                    st.stealTargetUid = stealTarget.GetUniqueLoadID();
                    var container = GetContainer(stealTarget);
                    st.stealContainerUid = container == null ? null : container.GetUniqueLoadID();
                }
                else
                {
                    int count = Mathf.Min(stealTarget.stackCount, 999);
                    int carriedCount = bully.carryTracker.TryStartCarry(stealTarget, count);
                    if (carriedCount > 0)
                    {
                        jobBully?.ReleaseStealTarget(st.stealTargetUid);
                        st.stealTargetUid = null;
                        st.didSteal = true;
                        st.stolenThing = bully.carryTracker.CarriedThing;
                        Messages.Message(
                            $"{bully?.Name?.ToStringShort ?? "Unknown"} 일진이 {st.stolenThing.LabelCap}을 훔쳐갑니다!",
                            new LookTargets(bully),
                            MessageTypeDefOf.NegativeEvent
                        );
                        string stealSuccessChat = LordChats.GetText(BullyTextKey.GrabbedItem, st.stolenThing?.LabelCap ?? "");
                        SpeechBubbleManager.Get(bully.Map)?.AddNPCChat(stealSuccessChat, bully);
                        return null;
                    }
                }
            }
            gotoTarget = stealTarget?.SpawnedParentOrMe;
            if (gotoTarget == null) return null;
            Job job = JobMaker.MakeJob(JobDefOf.Goto, gotoTarget);
            job.expiryInterval = 300;
            job.checkOverrideOnExpire = true;
            job.locomotionUrgency = LocomotionUrgency.Jog;
            return job;
        }
        private static bool TryDropFromContainer(Thing container, Thing itemInCargo, out Thing dropped)
        {
            dropped = null;

            if (container == null || itemInCargo == null)
                return false;

            if (!TryFindHoldingOwner(container, itemInCargo, out ThingOwner owner) || owner == null)
                return false;

            bool contains = owner.Contains(itemInCargo);
            if (!contains) return false;

            bool ok = owner.TryDrop(itemInCargo, container.Position, container.Map, ThingPlaceMode.Near, out dropped);
            QMsg($"[TryDrop] TryDrop ok={ok}, droppedNull={dropped==null}, droppedUid={dropped?.GetUniqueLoadID()}", Channel.Debug);

            return ok && dropped != null;
        }
        private static List<Thing> ScanCellsAround(Map map, IntVec3 center, float scanRadius)
        {
            var baseThings =
            GenRadial.RadialCellsAround(center, scanRadius, true)
                .Where(c => c.InBounds(map))
                .SelectMany(c => c.GetThingList(map))
                .Where(t => t?.def?.defName != null);

            var allThings = new List<Thing>(256);
            var seen = new HashSet<Thing>();
            foreach (var t in baseThings)
            {
                if (t == null) continue;
                if (!seen.Add(t)) continue;
                allThings.Add(t);

                string defname = t.def.defName;
                if (!AllowBullyExtractFromContainer(t)) continue;
                foreach (var it in EnumerateContainerContentsIfAny(t))
                {
                    if (it == null) continue;
                    if (!seen.Add(it)) continue;
                    allThings.Add(it);
                }
            }
            return allThings;
        }

        private static Thing GetContainer(Thing t)
        {
            if (t == null) return null;
            if (t.ParentHolder == null) return null;

            Thing root = t.SpawnedParentOrMe;
            if (root == null || root == t) return null;

            return root;
        }

        private static IEnumerable<Thing> EnumerateContainerContentsIfAny(Thing container)
        {
            if (container == null) yield break;

            if (container is IThingHolder th)
            {
                foreach (var t in EnumerateThingOwner(th.GetDirectlyHeldThings()))
                    yield return t;
            }

            if (container is ThingWithComps twc && twc.AllComps != null)
            {
                var comps = twc.AllComps;
                for (int i = 0; i < comps.Count; i++)
                {
                    if (comps[i] is IThingHolder ch)
                    {
                        foreach (var t in EnumerateThingOwner(ch.GetDirectlyHeldThings()))
                            yield return t;
                    }
                }
            }
        }

        private static IEnumerable<Thing> EnumerateThingOwner(ThingOwner owner)
        {
            if (owner == null) yield break;

            for (int i = 0; i < owner.Count; i++)
            {
                Thing t = owner[i];
                if (t != null) yield return t;
            }
        }
        private static bool HasQuestTags(Thing t) => t?.questTags != null && t.questTags.Count > 0;

        private static bool AllowBullyExtractFromContainer(Thing container)
        {
            if (container == null) return false;
            if (!container.Spawned) return false;

            if (HasQuestTags(container)) return false;
            if (!container.Faction.IsPlayerSafe()) return false;
            if (!HasOwner(container)) return false;

            // if (!container.def.BuildableByPlayer) return false;
            return true;
        }

        private static bool HasOwner(Thing container)
        {
            if (container == null) return false;
            // Thing itself as holder (rare)
            if (container is IThingHolder th)
            {
                return th.GetDirectlyHeldThings() != null;
            }

            // Comp-based holders (common)
            if (container is ThingWithComps twc && twc.AllComps != null)
            {
                for (int i = 0; i < twc.AllComps.Count; i++)
                {
                    if (twc.AllComps[i] is IThingHolder holder)
                    {
                        return holder.GetDirectlyHeldThings() != null;
                    }
                }
            }

            return false;
        }

        private static bool TryFindHoldingOwner(Thing container, Thing itemInCargo, out ThingOwner owner)
        {
            owner = null;
            if (container == null || itemInCargo == null) return false;
            if (container is IThingHolder th)
            {
                var o = th.GetDirectlyHeldThings();
                if (o != null && o.Contains(itemInCargo))
                {
                    owner = o;
                    return true;
                }
            }
            if (container is ThingWithComps twc && twc.AllComps != null)
            {
                var comps = twc.AllComps;
                for (int i = 0; i < comps.Count; i++)
                {
                    if (comps[i] is IThingHolder holder)
                    {
                        var o = holder.GetDirectlyHeldThings();
                        if (o != null && o.Contains(itemInCargo))
                        {
                            owner = o;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static List<Thing> PickClosestBelowValueTopN(
            Pawn bully,
            IEnumerable<Thing> things,
            int n,
            float targetValue,
            float minValue,
            LordJob_Bully job,
            float tooSmallRatio = 0.5f
        )
        {
            if (n <= 0) return new List<Thing>();
            tooSmallRatio = Mathf.Clamp(tooSmallRatio, 0.05f, 0.99f);
            float postMaxVal = Mathf.Max(targetValue, minValue / tooSmallRatio);
            float postMinVal = Math.Max(postMaxVal * tooSmallRatio, minValue);
            return things
                .Where(t => IsValidStealTarget(t, bully))
                .Select(t => new { t, value = GetStealValue(t), uid = t.GetUniqueLoadID() })
                .Where(x => x.value >= postMinVal && x.value <= postMaxVal)
                .Where(x => job == null || !job.IsStealTargetClaimed(x.uid))
                .OrderByDescending(x => x.value)
                .Take(n)
                .Select(x => x.t)
                .ToList();
        }
        private static Thing FindSpawnedThingByUid(Map map, string uid)
        {
            if (map == null || uid.NullOrEmpty()) return null;

            foreach (Thing t in map.listerThings.AllThings)
            {
                if (t != null && t.Spawned && t.GetUniqueLoadID() == uid)
                    return t;
            }
            return null;
        }
        private static Thing FindThingByUid(Map map, string targetUid, string containerUid)
        {
            if (map == null || targetUid.NullOrEmpty()) return null;
            Thing t = FindSpawnedThingByUid(map, targetUid);
            if (t != null) return t;
            if (containerUid.NullOrEmpty()) return null;
            Thing container = FindSpawnedThingByUid(map, containerUid);
            if (container == null) return null;
            foreach (var it in EnumerateContainerContentsIfAny(container))
            {
                if (it != null && it.GetUniqueLoadID() == targetUid)
                    return it;
            }
            return null;
        }
        private static bool IsValidStealTarget(Thing t, Pawn bully)
        {
            if (t == null || bully == null) return false;
            if (t.Destroyed) return false;
            //Thing parentOrMe = t.SpawnedParentOrMe;
            Thing container = GetContainer(t);
            if (HasQuestTags(t)) return false;
            if (t.def?.category != ThingCategory.Item) return false;
            if (IsForbiddenStealTarget(t)) return false;
            if (t.IsBurning()) return false;
            if (t.def.EverHaulable == false) return false;
            if (t.GetStatValue(StatDefOf.Mass) > bully.GetStatValue(StatDefOf.CarryingCapacity)) return false;

            if (container == null)
            {
                if (!t.Spawned) return false;
                if (!bully.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some)) return false;
            }
            else
            {
                if (!container.Spawned) return false;
                if (!bully.CanReserveAndReach(container, PathEndMode.ClosestTouch, Danger.Some)) return false;
            }
            return true;
        }

        private static bool IsForbiddenStealTarget(Thing t)
        {
            if (t.def == ThingDefOf.Genepack)
                return true;
            return false;
        }

        private static float GetStealValue(Thing t)
        {
            return t.MarketValue * t.stackCount;
        }
        public static Job MakeExitJob(IntVec3 exitCell)
        {
            var job = JobMaker.MakeJob(JobDefOf.Goto, exitCell);
            job.exitMapOnArrival = true;
            job.expiryInterval = 600;
            job.checkOverrideOnExpire = true;
            job.locomotionUrgency = LocomotionUrgency.Sprint;
            return job;
        }
    }
}