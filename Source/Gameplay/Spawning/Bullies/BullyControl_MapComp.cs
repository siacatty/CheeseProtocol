using System.Collections.Generic;
using RimWorld;
using UnityEngine.Pool;
using Verse;
using Verse.AI;

namespace CheeseProtocol
{
    public sealed class BullyControl_MapComp : MapComponent
    {
        private struct PendingStun
        {
            public int dueTick;
            public Pawn pawn;         // carrier
            public Thing instigator;  // victim 등
            public int stunTicks;
            public bool addBattleLog;
            public bool showMote;
            public bool disableRotation;
        }

        private readonly List<PendingStun> pendingStuns = new();
        private readonly Dictionary<Pawn, int> blockJobSearchUntil = new();

        public BullyControl_MapComp(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;

            // --- execute stuns ---
            for (int i = pendingStuns.Count - 1; i >= 0; i--)
            {
                var ps = pendingStuns[i];
                if (now < ps.dueTick) continue;

                pendingStuns.RemoveAt(i);

                var p = ps.pawn;
                if (p == null || p.DestroyedOrNull() || p.Dead) continue;
                if (!p.Spawned || p.Map != map) continue;

                p.stances?.stunner?.StunFor(ps.stunTicks, ps.instigator, ps.addBattleLog, ps.showMote, ps.disableRotation);
            }

            if (now % 250 == 0 && blockJobSearchUntil.Count > 0)
            {
                var tmp = ListPool<Pawn>.Get();
                foreach (var kv in blockJobSearchUntil)
                {
                    if (kv.Key == null || kv.Key.DestroyedOrNull() || kv.Key.Map != map || now >= kv.Value)
                        tmp.Add(kv.Key);
                }
                for (int i = 0; i < tmp.Count; i++) blockJobSearchUntil.Remove(tmp[i]);
                ListPool<Pawn>.Release(tmp);
            }
        }

        public void EnqueueStun(Pawn pawn, int delayTicks, int stunTicks, Thing instigator,
            bool addBattleLog = true, bool showMote = true, bool disableRotation = false)
        {
            if (pawn == null || pawn.Dead) return;

            pendingStuns.Add(new PendingStun
            {
                dueTick = Find.TickManager.TicksGame + delayTicks,
                pawn = pawn,
                instigator = instigator,
                stunTicks = stunTicks,
                addBattleLog = addBattleLog,
                showMote = showMote,
                disableRotation = disableRotation
            });
        }

        public void BlockJobSearch(Pawn pawn, int ticks)
        {
            if (pawn == null) return;
            int until = Find.TickManager.TicksGame + ticks;
            if (blockJobSearchUntil.TryGetValue(pawn, out int cur))
                blockJobSearchUntil[pawn] = (until > cur) ? until : cur;
            else
                blockJobSearchUntil[pawn] = until;
        }

        public bool IsJobSearchBlocked(Pawn pawn)
        {
            if (pawn == null) return false;
            int now = Find.TickManager.TicksGame;
            return blockJobSearchUntil.TryGetValue(pawn, out int until) && now < until;
        }

        public static BullyControl_MapComp Get(Map map) => map?.GetComponent<BullyControl_MapComp>();
    }
}