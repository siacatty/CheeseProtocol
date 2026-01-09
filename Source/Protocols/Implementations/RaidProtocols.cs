using Verse;
using RimWorld;
using static CheeseProtocol.CheeseLog;
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
            QMsg($"Executing protocol={Id} for {evt}", Channel.Debug);
            RaidSpawner.Spawn(evt.username, evt.amount, evt.message);
        }
    }
}