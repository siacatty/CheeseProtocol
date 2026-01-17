using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace CheeseProtocol
{
    // Travel(중앙 근처) -> DefendPoint(넓게 loiter) -> ExitMap(시간 후 이탈)
    public class LordJob_AnimalPass : LordJob
    {
        private IntVec3 travelDest;
        private int exitAtTick;
        private int stayTicks;

        // loiter 반경(바닐라보다 넓게)
        private float defendRadius;
        private float wanderRadius;

        public LordJob_AnimalPass() { } // Scribe용

        public LordJob_AnimalPass(
            Map map,
            int stayTicks,
            float defendRadius = 60f,
            float wanderRadius = 90f)
        {
            TryFindCentralTravelDest(map, out this.travelDest);
            this.stayTicks = stayTicks;
            this.defendRadius = defendRadius;
            this.wanderRadius = wanderRadius;

            exitAtTick = GenTicks.TicksGame + stayTicks;
        }

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();

            var travel = new LordToil_Travel(travelDest);

            var loiter = new LordToil_DefendPoint(travelDest, defendRadius: defendRadius, wanderRadius: wanderRadius);

            var exit = new LordToil_ExitMap();

            graph.StartingToil = travel;
            graph.AddToil(loiter);
            graph.AddToil(exit);

            // Travel 도착 -> Loiter
            var toLoiter = new Transition(travel, loiter);
            toLoiter.AddTrigger(new Trigger_Memo("TravelArrived"));
            toLoiter.AddPreAction(new TransitionAction_SetDefendLocalGroup());
            toLoiter.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(toLoiter);

            var toExit = new Transition(loiter, exit);
            toExit.AddTrigger(new Trigger_TickCondition(() => GenTicks.TicksGame >= exitAtTick));
            graph.AddTransition(toExit);

            return graph;
        }
        public static bool TryFindCentralTravelDest(Map map, out IntVec3 dest)
        {
            IntVec3 center = map.Center;

            // 점진적으로 반경 확장
            int radius = 0;
            int maxRadius = Mathf.Min(map.Size.x, map.Size.z) / 2;;
            int step = 16;
            while (radius <= maxRadius)
            {
                if (CellFinder.TryFindRandomCellNear(
                        center,
                        map,
                        radius,
                        c => c.Walkable(map),
                        out dest))
                {
                    return true;
                }
                radius += step;
            }
            // fallback
            if (!CellFinder.TryFindRandomCell(
                map,
                c => c.Walkable(map),
                out dest))
            {
                // 최후 fallback
                dest = center;
            }
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref travelDest, "travelDest");
            Scribe_Values.Look(ref exitAtTick, "exitAtTick", 0);
            Scribe_Values.Look(ref stayTicks, "stayTicks", 0);
            Scribe_Values.Look(ref defendRadius, "defendRadius", 60f);
            Scribe_Values.Look(ref wanderRadius, "wanderRadius", 90f);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // 혹시 exitAtTick이 비어있다면(구세이브/특이 케이스) 보정
                if (exitAtTick <= 0 && stayTicks > 0)
                    exitAtTick = GenTicks.TicksGame + stayTicks;

                // 안전값
                if (defendRadius <= 0f) defendRadius = 60f;
                if (wanderRadius <= 0f) wanderRadius = 90f;
            }
        }

        public int TicksUntilExit => exitAtTick - GenTicks.TicksGame;
    }
}