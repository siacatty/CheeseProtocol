using System.Collections.Generic;
using Verse;
using static CheeseProtocol.CheeseLog;

namespace CheeseProtocol
{
    public class CheeseCooldownState : GameComponent
    {
        // cmd -> last executed tick
        private Dictionary<CheeseCommand, int> lastTickByCmd = new Dictionary<CheeseCommand, int>();

        public CheeseCooldownState(Game game) { }

        public override void StartedNewGame()
        {
            Reset();
            Msg("Cooldown state reset (new game).");
        }

        public override void LoadedGame()
        {
            Msg("Cooldown state loaded.");
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref lastTickByCmd, "lastTickByCmd", LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                lastTickByCmd ??= new Dictionary<CheeseCommand, int>();
            }
        }

        private void Reset() => lastTickByCmd.Clear();

        public void ResetAllCd() => Reset();

        public int GetLastTick(CheeseCommand cmd)
            => lastTickByCmd.TryGetValue(cmd, out var t) ? t : -1;

        public void MarkExecuted(CheeseCommand cmd, int nowTick)
            => lastTickByCmd[cmd] = nowTick;

        public bool IsReady(CheeseCommand cmd, int cooldownHours, int nowTick)
        {
            if (cooldownHours <= 0) return true;

            int last = GetLastTick(cmd);
            int cdTicks = cooldownHours * 2500;
            if (last < 0) return true; // first run is ready
            return nowTick - last >= cdTicks;
        }

        public int RemainingTicks(CheeseCommand cmd, int cooldownHours, int nowTick)
        {
            if (cooldownHours <= 0) return 0;

            int last = GetLastTick(cmd);
            int cdTicks = cooldownHours * 2500;

            if (last < 0) return 0; // never executed -> ready

            int remain = cdTicks - (nowTick - last);
            return remain <= 0 ? 0 : remain;
        }

        public static CheeseCooldownState Current
            => Verse.Current.Game?.GetComponent<CheeseCooldownState>();
    }
}