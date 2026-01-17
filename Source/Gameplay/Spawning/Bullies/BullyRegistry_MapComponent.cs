using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Noise;
using static CheeseProtocol.CheeseLog;

namespace CheeseProtocol
{
    /// <summary>
    /// Map-scoped registry for bully pawns.
    /// Key: Pawn.GetUniqueLoadID()
    /// Value: BullyState
    /// </summary>
    public class BullyRegistry_MapComponent : MapComponent
    {
        private Dictionary<string, BullyState> bullies = new Dictionary<string, BullyState>();

        // Scribe helpers
        private List<string> tmpKeys;
        private List<BullyState> tmpValues;

        private int nextPruneTick = 0;

        public BullyRegistry_MapComponent(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            int now = Find.TickManager.TicksGame;
            if (now < nextPruneTick) return;
            nextPruneTick = now + 600; // ~10 seconds

            PruneInvalid(aggressive: false);
        }

        // ------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------

        public int BullyCount => bullies?.Count ?? 0;

        public void AddBullies(BullyRequest req, int durationTicks, float stealValueTotal)
        {
            var pawns = req.bullyList;
            int now = Find.TickManager.TicksGame;
            float[] stealValues = SplitFloatOrdered(stealValueTotal, pawns.Count);
            for(int i = 0; i < pawns.Count; i++)
            {
                var p = pawns[i];
                var st = EnsureBully(p);
                if (i == 0)
                    st.isLeader = true;
                st.didSteal = false;
                st.SetInitialTarget(req.initialTargets.TryGetValue(p));
                //st.ResetTarget();
                st.stealTick = now + durationTicks;
                st.finalStealAtTick = now + durationTicks + 27500; //try final steal with minimum value 
                st.exitAtTick = now + durationTicks + 32500; //exit if failed to steal after 12 hours
                st.harassTick = now; 
                st.targetValue = stealValues[i];
                QMsg($"{p.Name.ToStringShort}'s target steal value={st.targetValue}", Channel.Debug);
            }
        }

        public static float[] SplitFloatOrdered(float total, int n)
        {
            if (n <= 0) return Array.Empty<float>();
            if (n == 1) return new[] { total };

            float[] raw = new float[n];
            float sum = 0f;

            for (int i = 0; i < n; i++)
            {
                raw[i] = Rand.Value; // 0~1
                sum += raw[i];
            }

            float scale = total / sum;
            for (int i = 0; i < n; i++)
                raw[i] *= scale;

            Array.Sort(raw, (a, b) => b.CompareTo(a));
            return raw;
        }

        public bool IsBully(Pawn pawn)
        {
            string uid = pawn?.GetUniqueLoadID();
            return uid != null && bullies.ContainsKey(uid);
        }

        public bool TryGetBully(Pawn pawn, out BullyState state)
        {
            state = null;
            string uid = pawn?.GetUniqueLoadID();
            if (uid == null) return false;
            return bullies.TryGetValue(uid, out state);
        }

        public bool TryGetBully(string bullyUid, out BullyState state)
        {
            state = null;
            if (string.IsNullOrEmpty(bullyUid)) return false;
            return bullies.TryGetValue(bullyUid, out state);
        }

        public BullyState EnsureBully(Pawn bullyPawn)
        {
            string uid = bullyPawn?.GetUniqueLoadID();
            if (uid == null) return null;

            if (!bullies.TryGetValue(uid, out var state) || state == null)
            {
                state = new BullyState(bullyPawn);
                bullies[uid] = state;
            }
            else if (string.IsNullOrEmpty(state.bullyPawnUid))
            {
                state.bullyPawnUid = uid;
            }

            return state;
        }

        public bool RemoveBully(Pawn pawn)
        {
            return RemoveBullyByUid(pawn?.GetUniqueLoadID());
        }

        public bool RemoveBullyByUid(string bullyUid)
        {
            if (string.IsNullOrEmpty(bullyUid)) return false;
            return bullies.Remove(bullyUid);
        }

        // ------------------------------------------------------------
        // Pawn resolving helpers
        // ------------------------------------------------------------

        public bool TryResolveBullyPawn(BullyState state, out Pawn pawn, bool preferThisMap = true)
        {
            pawn = null;
            if (state == null || string.IsNullOrEmpty(state.bullyPawnUid)) return false;
            return TryResolvePawn(state.bullyPawnUid, out pawn, preferThisMap);
        }

        public bool TryResolveTargetPawn(BullyState state, out Pawn pawn, bool preferThisMap = true)
        {
            pawn = null;
            if (state == null || string.IsNullOrEmpty(state.targetPawnUid)) return false;
            return TryResolvePawn(state.targetPawnUid, out pawn, preferThisMap);
        }

        public bool TryResolveWanderTargetPawn(BullyState state, out Pawn pawn, bool preferThisMap = true)
        {
            pawn = null;
            if (state == null || string.IsNullOrEmpty(state.wanderTargetUid)) return false;
            return TryResolvePawn(state.wanderTargetUid, out pawn, preferThisMap);
        }

        public bool TryResolvePawn(string pawnUid, out Pawn pawn, bool preferThisMap = true)
        {
            pawn = null;
            if (string.IsNullOrEmpty(pawnUid)) return false;

            if (preferThisMap)
            {
                pawn = FindPawnOnThisMap(pawnUid);
                if (pawn != null) return true;
            }

            pawn = FindPawnGlobalAlive(pawnUid);
            return pawn != null;
        }

        // ------------------------------------------------------------
        // Pruning
        // ------------------------------------------------------------

        /// <summary>
        /// Removes invalid bully entries.
        /// aggressive=false : remove only if resolved pawn is dead/destroyed
        /// aggressive=true  : also remove if pawn cannot be resolved at all
        /// </summary>
        public int PruneInvalid(bool aggressive)
        {
            if (bullies == null || bullies.Count == 0) return 0;

            int removed = 0;

            tmpKeys ??= new List<string>();
            tmpKeys.Clear();
            tmpKeys.AddRange(bullies.Keys);

            for (int i = 0; i < tmpKeys.Count; i++)
            {
                string uid = tmpKeys[i];
                if (!bullies.TryGetValue(uid, out var state) || state == null)
                {
                    bullies.Remove(uid);
                    removed++;
                    continue;
                }

                bool resolved = TryResolvePawn(state.bullyPawnUid, out var pawn, preferThisMap: true);

                if (resolved)
                {
                    if (pawn.DestroyedOrNull() || pawn.Dead)
                    {
                        bullies.Remove(uid);
                        removed++;
                    }
                }
                else if (aggressive)
                {
                    bullies.Remove(uid);
                    removed++;
                }
            }

            return removed;
        }

        // ------------------------------------------------------------
        // Save / Load
        // ------------------------------------------------------------

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(
                ref bullies,
                "bullies",
                LookMode.Value,
                LookMode.Deep,
                ref tmpKeys,
                ref tmpValues
            );

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                bullies ??= new Dictionary<string, BullyState>();

                // Heal uid mismatch
                tmpKeys ??= new List<string>();
                tmpKeys.Clear();
                tmpKeys.AddRange(bullies.Keys);

                for (int i = 0; i < tmpKeys.Count; i++)
                {
                    string keyUid = tmpKeys[i];
                    if (!bullies.TryGetValue(keyUid, out var st) || st == null) continue;

                    if (string.IsNullOrEmpty(st.bullyPawnUid) || st.bullyPawnUid != keyUid)
                        st.bullyPawnUid = keyUid;
                }

                PruneInvalid(aggressive: false);
            }
        }

        // ------------------------------------------------------------
        // Internals
        // ------------------------------------------------------------

        private Pawn FindPawnOnThisMap(string uid)
        {
            if (map?.mapPawns == null) return null;

            var spawned = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < spawned.Count; i++)
            {
                Pawn p = spawned[i];
                if (p != null && p.GetUniqueLoadID() == uid)
                    return p;
            }

            var all = map.mapPawns.AllPawns;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn p = all[i];
                if (p != null && p.GetUniqueLoadID() == uid)
                    return p;
            }

            return null;
        }

        private static Pawn FindPawnGlobalAlive(string uid)
        {
            var all = PawnsFinder.AllMapsWorldAndTemporary_Alive;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn p = all[i];
                if (p != null && p.GetUniqueLoadID() == uid)
                    return p;
            }
            return null;
        }
    }
}