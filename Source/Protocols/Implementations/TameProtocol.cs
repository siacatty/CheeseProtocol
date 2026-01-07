using RimWorld;
using Verse;

namespace CheeseProtocol
{
    public class TameProtocol : IProtocol
    {
        public string Id => "tame";
        public string DisplayName => "Tame protocol";

        public bool CanExecute(ProtocolContext ctx)
        {
            return ctx?.Map != null;
        }

        public void Execute(ProtocolContext ctx)
        {
            var evt = ctx.CheeseEvt;
            Log.Warning($"[CheeseProtocol] Executing protocol={Id} for {evt}");

            // Use your existing spawner
            TameSpawner.Spawn(evt.username, evt.amount, evt.message);
        }
    }
}