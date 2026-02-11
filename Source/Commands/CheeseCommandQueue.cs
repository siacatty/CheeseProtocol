using System.Collections.Generic;
using Verse;
using static CheeseProtocol.CheeseLog;

namespace CheeseProtocol
{
    public class CheeseCommandQueue : GameComponent
    {
        private List<CheeseEvent> q = new();
        private Dictionary<CheeseCommand, int> pendingCountByCmd = new Dictionary<CheeseCommand, int>();


        private const int CheckIntervalTicks = 60;
        private int nextCheckTick = -1;

        private const int MaxExecutePerPump = 10;

        public CheeseCommandQueue(Game game) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref q, "cheeseCommandQueue", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                q ??= new List<CheeseEvent>();
                nextCheckTick = -1;
                RebuildCounts();
            }
        }

        public void Enqueue(CheeseEvent evt)
        {
            if (evt == null) return;

            q.Add(evt);

            pendingCountByCmd.TryGetValue(evt.cmd, out var c);
            pendingCountByCmd[evt.cmd] = c + 1;

            QMsg($"[Queue] enqueued cmd={evt.cmd} size={q.Count}", Channel.Debug);
        }
        private void RemoveAt(int index)
        {
            var evt = q[index];
            q.RemoveAt(index);

            if (evt == null) return;

            if (pendingCountByCmd.TryGetValue(evt.cmd, out var c))
            {
                c--;
                if (c <= 0) pendingCountByCmd.Remove(evt.cmd);
                else pendingCountByCmd[evt.cmd] = c;
            }
        }
        private void RebuildCounts()
        {
            pendingCountByCmd.Clear();
            for (int i = 0; i < q.Count; i++)
            {
                var evt = q[i];
                if (evt == null) continue;
                pendingCountByCmd.TryGetValue(evt.cmd, out var c);
                pendingCountByCmd[evt.cmd] = c + 1;
            }
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (nextCheckTick < 0) nextCheckTick = now + CheckIntervalTicks;
            if (now < nextCheckTick) return;

            nextCheckTick = now + CheckIntervalTicks;
            Pump(now);
        }
        public int GetPendingCount(CheeseCommandConfig cfg)
        {
            if (cfg == null) return 0;
            return pendingCountByCmd.TryGetValue(cfg.cmd, out var c) ? c : 0;
        }
        private void Pump(int nowTick)
        {
            if (q.Count == 0) return;

            var settings = CheeseProtocolMod.Settings;
            var cdState = CheeseCooldownState.Current;

            if (settings == null || cdState == null) return;

            int executed = 0;

            for (int i = 0; i < q.Count && executed < MaxExecutePerPump; )
            {
                var evt = q[i];
                if (evt == null) { RemoveAt(i); continue; }

                if (!settings.TryGetCommandConfig(evt.cmd, out var cfg) || !cfg.enabled)
                {
                    RemoveAt(i);
                    continue;
                }

                if (!cdState.IsReady(cfg.cmd, cfg.cooldownHours, nowTick))
                {
                    i++;
                    continue;
                }

                if (!ProtocolRouter.CanExecuteProtocol(evt))
                {
                    i++;
                    continue;
                }

                bool ok = ProtocolRouter.TryExecuteFromQueue(evt);
                 // TODO: add policy for non executable event
                RemoveAt(i);
                executed++;
            }
        }

        public static CheeseCommandQueue Current => Verse.Current.Game?.GetComponent<CheeseCommandQueue>();
    }
}