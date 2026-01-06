using Verse;
using RimWorld;

namespace CheeseProtocol.Protocols
{
    public class RaidProtocol : IProtocol
    {
        public string Id => "raid";
        public string DisplayName => "Raid protocol";

        public bool CanExecute(ProtocolContext ctx)
        {
            return ctx?.Map != null;
        }

        public void Execute(ProtocolContext ctx)
        {
            var evt = ctx.CheeseEvt;
            Log.Warning($"[CheeseProtocol] Executing protocol={Id} for {evt}");

            // Use your existing spawner
            RaidSpawner.Spawn(evt.username, evt.amount, evt.message);
            // Vanilla incident defName: "RaidEnemy"
        }
    }
}