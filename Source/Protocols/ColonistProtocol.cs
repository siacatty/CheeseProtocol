using Verse;

namespace CheeseProtocol.Protocols
{
    public class ColonistProtocol : IProtocol
    {
        public string Id => "colonist";
        public string DisplayName => "Colonist Join";

        public bool CanExecute(ProtocolContext ctx)
        {
            return ctx?.Map != null && ctx?.Donation != null;
        }

        public void Execute(ProtocolContext ctx)
        {
            var d = ctx.Donation;
            Log.Warning($"[CheeseProtocol] Executing protocol={Id} for {d}");

            // Use your existing spawner
            ColonistSpawner.Spawn(d.donor, d.amount, d.message);
        }
    }
}