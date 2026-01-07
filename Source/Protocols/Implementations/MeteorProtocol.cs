using RimWorld;
using Verse;

namespace CheeseProtocol
{
    public class MeteorProtocol : IProtocol
    {
        public string Id => "meteor";
        public string DisplayName => "Meteorite protocol";

        public bool CanExecute(ProtocolContext ctx)
        {
            return ctx?.Map != null;
        }

        public void Execute(ProtocolContext ctx)
        {
            var evt = ctx.CheeseEvt;
            Log.Warning($"[CheeseProtocol] Executing protocol={Id} for {evt}");

            // Use your existing spawner
            MeteorSpawner.Spawn(evt.username, evt.amount, evt.message);
        }
    }
}