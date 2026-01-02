using Verse;

namespace CheeseProtocol.Protocols
{
    public class ColonistProtocol : IProtocol
    {
        public string Id => "colonist";
        public string DisplayName => "Colonist Join protocol";

        public bool CanExecute(ProtocolContext ctx)
        {
            return ctx?.Map != null && ctx?.CheeseEvt != null;
        }

        public void Execute(ProtocolContext ctx)
        {
            var evt = ctx.CheeseEvt;
            Log.Warning($"[CheeseProtocol] Executing protocol={Id} for {evt}");

            // Use your existing spawner
            ColonistSpawner.Spawn(evt.username, evt.amount, evt.message);
        }
    }
}