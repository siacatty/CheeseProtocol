using RimWorld;
using Verse;
using static CheeseProtocol.CheeseLog;
namespace CheeseProtocol
{
    public class SupplyProtocol : IProtocol
    {
        public string Id => "supply";
        public string DisplayName => "Supply protocol";

        public bool CanExecute(ProtocolContext ctx)
        {
            return ctx?.Map != null;
        }

        public void Execute(ProtocolContext ctx)
        {
            var evt = ctx.CheeseEvt;
            QMsg($"Executing protocol={Id} for {evt}", Channel.Debug);
            SupplySpawner.Spawn(evt.username, evt.amount, evt.message);
        }
    }
}