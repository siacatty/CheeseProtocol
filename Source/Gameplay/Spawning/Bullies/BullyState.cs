using Verse;

namespace CheeseProtocol
{
    /// <summary>
    /// Per-bully pawn state (tick-driven). Stored in BullyRegistry_MapComponent.
    /// Pawn identity is tracked via GetUniqueLoadID().
    /// </summary>
    public class BullyState : IExposable
    {
        // Identity (Pawn.GetUniqueLoadID())
        public string bullyPawnUid;

        // Target (colonist) tracking
        public string targetPawnUid = "";
        public string stealTargetUid = "";
        public string wanderTargetUid = "";

        // Timeline (all tick-based)
        public int harassTick = 0;   // initial harassTick
        public int stealTick = 0;    // when to attempt steal (usually near exit)
        public int exitAtTick = 0;       // when to stop harassing and leave
        public int finalStealAtTick = 0;
        public int nextStunTick = 0;  // when to attempt to stun
        public int lastRetargetTick = 0;
        public int stealTargetPickTick = 0;
        public int stealTargetSearchTick = 0;
        public int wanderAnchorTick = 0;
        public int wanderRetargetTick = 0;

        // One-shot flags
        public bool didSteal = false;
        public bool didSearch = false;
        public bool shouldExit = false;
        public bool giveupSteal = false;
        public bool isLeader = false;

        public int stealAttempts = 0;
        public int scanRadius = 15;
        public float targetValue = 0f;
        public float loRatio = 0.5f;
        public IntVec3 wanderAnchor = IntVec3.Invalid;
        public IntVec3 lastScanPos = IntVec3.Invalid;
        
        [Unsaved]
        public Thing stolenThing = null;

        public BullyState() { }

        public BullyState(Pawn bully)
        {
            bullyPawnUid = bully?.GetUniqueLoadID();
        }

        public void SetInitialTarget(Pawn colonist)
        {
            targetPawnUid = colonist?.GetUniqueLoadID();
            lastRetargetTick = 0;
        }

        public void ResetTarget()
        {
            targetPawnUid = null;
            lastRetargetTick = 0;
        }

        public void ResetWanderTarget()
        {
            wanderTargetUid = null;
            wanderRetargetTick = 0;
        }

        public void ResetScan()
        {
            scanRadius = 15;
            loRatio = 0.5f;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref bullyPawnUid, "bullyPawnUid");
            Scribe_Values.Look(ref targetPawnUid, "targetPawnUid");
            Scribe_Values.Look(ref stealTargetUid, "stealTargetUid");
            Scribe_Values.Look(ref wanderTargetUid, "wanderTargetUid");

            Scribe_Values.Look(ref harassTick, "harassTick", 0);
            Scribe_Values.Look(ref stealTick, "stealTick", 0);
            Scribe_Values.Look(ref exitAtTick, "exitAtTick", 0);
            Scribe_Values.Look(ref finalStealAtTick, "finalStealAtTick", 0);
            Scribe_Values.Look(ref nextStunTick, "nextStunTick", 0);
            Scribe_Values.Look(ref lastRetargetTick, "lastRetargetTick", 0);
            Scribe_Values.Look(ref stealTargetPickTick, "stealTargetPickTick", 0);
            Scribe_Values.Look(ref stealTargetSearchTick, "stealTargetSearchTick", 0);
            Scribe_Values.Look(ref wanderAnchorTick, "wanderAnchorTick", 0);
            Scribe_Values.Look(ref wanderRetargetTick, "wanderRetargetTick", 0);

            Scribe_Values.Look(ref didSteal, "didSteal", false);
            Scribe_Values.Look(ref didSteal, "didSearch", false);
            Scribe_Values.Look(ref shouldExit, "shouldExit", false);
            Scribe_Values.Look(ref giveupSteal, "giveupSteal", false);
            Scribe_Values.Look(ref isLeader, "isLeader", false);

            Scribe_Values.Look(ref stealAttempts, "stealAttempts", 0);
            Scribe_Values.Look(ref scanRadius, "scanRadius", 15);
            Scribe_Values.Look(ref targetValue, "targetValue", 15);
            Scribe_Values.Look(ref loRatio, "loRatio", 0.5f);
            Scribe_Values.Look(ref wanderAnchor, "wanderAnchor", IntVec3.Invalid);
            Scribe_Values.Look(ref lastScanPos, "lastScanPos", IntVec3.Invalid);


        }

        public override string ToString()
        {
            return $"BullyState(bullyUid={bullyPawnUid}, targetUid={targetPawnUid}, " +
                   $"nextHarass={harassTick}, nextSteal={stealTick}, exitAt={exitAtTick}, didSteal={didSteal})";
        }
    }
}