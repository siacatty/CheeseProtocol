using System;
using Verse;
using static CheeseProtocol.CheeseLog;

namespace CheeseProtocol
{
    public class CheeseGameComponent : GameComponent
    {
        private bool autoConnectTried = false;

        private readonly object uiLock = new object();
        public CheeseHudWindow hudWindow;
        private CheeseUiStatusSnapshot uiStatus = new CheeseUiStatusSnapshot();

        public CheeseGameComponent(Game game) { }

        public CheeseUiStatusSnapshot GetUiStatusSnapshot()
        {
            lock (uiLock)
            {
                return uiStatus.Clone();
            }
        }

        public void UpdateUiStatus(Action<CheeseUiStatusSnapshot> mutate)
        {
            lock (uiLock)
            {
                mutate(uiStatus);
            }
        }

        public override void GameComponentOnGUI()
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            if (Find.WindowStack == null) return;
            var settings = CheeseProtocolMod.Settings;
            bool hudEnabled = settings == null ? true : settings.showHud;
            if (hudEnabled)
            {
                if (hudWindow == null)
                {
                    hudWindow = new CheeseHudWindow();
                    Find.WindowStack.Add(hudWindow);
                }
            }
            else
            {
                if (hudWindow != null)
                {
                    Find.WindowStack.TryRemove(hudWindow);
                    hudWindow = null;
                }
            }
            if (settings.drainQueue)
                CheeseProtocolMod.ChzzkChat.ProcessEventQueues();
        }

        public static CheeseGameComponent Instance =>
            Current.Game?.GetComponent<CheeseGameComponent>();

        public override void GameComponentTick()
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            if (!autoConnectTried)
            {
                autoConnectTried = true;

                if (!string.IsNullOrWhiteSpace(CheeseProtocolMod.Settings.chzzkStudioUrl))
                {
                    Msg("Auto-connect on game start");
                    CheeseProtocolMod.ChzzkChat.RequestConnect("auto");
                }
            }
            Flush(maxPerFlush: 200);
            CheeseProtocolMod.ChzzkChat.Tick();
        }

        public override void FinalizeInit()
        {
            CheeseLog.Clear();

            CheeseLog.MinLevel = Prefs.DevMode
                ? CheeseLog.Level.Trace
                : CheeseLog.Level.Message;
            CheeseLog.SetChannel(CheeseLog.Channel.Debug, Prefs.DevMode);
        }
    }
}
