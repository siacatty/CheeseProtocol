using System.Collections.Generic;
using Verse;

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
            Log.Message("[CheeseProtocol] Cooldown state reset (new game).");
        }

        public override void LoadedGame()
        {
            Reset();
            Log.Message("[CheeseProtocol] Cooldown state reset (loaded game).");
        }

        private void Reset()
        {
            lastTickByCmd.Clear();
        }

        // --- API ---
        public int GetLastTick(CheeseCommand cmd)
        {
            if (lastTickByCmd.TryGetValue(cmd, out var t)) return t;
            return -1; // never executed => always ready
        }

        public void MarkExecuted(CheeseCommand cmd, int nowTick)
        {
            lastTickByCmd[cmd] = nowTick;
        }

        public bool IsReady(CheeseCommand cmd, int cooldownHours, int nowTick)
        {
            if (cooldownHours <= 0) return true;

            int last = GetLastTick(cmd);
            int cdTicks = cooldownHours * 2500;
            //Log.Warning($"[CheeseProtocol] [CDState] command config cooldown: {cdTicks} | last executed: {last} | now:  {nowTick}");
            //Log.Warning($"[CheeseProtocol] [CDState] is cooldown complete?: {nowTick - last >= cdTicks}, nowTick - last =  {nowTick - last}, cdTicks = {cdTicks}");
            if (last < 0) //always run on first
                return true;
            return nowTick - last >= cdTicks;
        }

        public int RemainingSeconds(CheeseCommand cmd, int cooldownHours, int nowTick)
        {
            if (cooldownHours <= 0) return 0;

            int last = GetLastTick(cmd);
            int cdTicks = cooldownHours * 2500;
            int remainTicks = cdTicks - (nowTick - last);
            if (remainTicks <= 0) return 0;

            return (remainTicks + 59) / 60; // ceil
        }

        // helper accessor
        public static CheeseCooldownState Current
            => Verse.Current.Game?.GetComponent<CheeseCooldownState>();
    }
}