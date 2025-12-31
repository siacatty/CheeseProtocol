using Verse;

namespace CheeseProtocol
{
    public class CheeseGameComponent : GameComponent
    {
        private bool autoConnectTried = false;

        public CheeseGameComponent(Game game) { }

        public override void GameComponentTick()
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            if (!autoConnectTried)
            {
                autoConnectTried = true;

                if (!string.IsNullOrWhiteSpace(CheeseProtocolMod.Settings.chzzkStudioUrl))
                {
                    Log.Message("[CheeseProtocol] Auto-connect on game start");
                    CheeseProtocolMod.ChzzkChat.RequestConnect("auto");
                }
            }
            CheeseProtocolMod.ChzzkChat.Tick();
        }
    }
}
