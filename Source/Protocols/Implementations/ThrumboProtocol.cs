using RimWorld;
using Verse;

namespace CheeseProtocol
{
    public class ThrumboProtocol : IProtocol
    {
        public string Id => "thrumbo";
        public string DisplayName => "Thrumbo protocol";

        public bool CanExecute(ProtocolContext ctx)
        {
            return ctx?.Map != null;
        }

        public void Execute(ProtocolContext ctx)
        {
            var evt = ctx.CheeseEvt;
            Log.Warning($"[CheeseProtocol] Executing protocol={Id} for {evt}");

            // Use your existing spawner
            ThrumboSpawner.Spawn(evt.username, evt.amount, evt.message);
        }
    }
}