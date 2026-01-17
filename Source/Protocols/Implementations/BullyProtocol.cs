using RimWorld;
using Verse;
using static CheeseProtocol.CheeseLog;
namespace CheeseProtocol
{
    public class BullyProtocol : IProtocol
    {
        public string Id => "bully";
        public string DisplayName => "Bully protocol";

        public bool CanExecute(ProtocolContext ctx)
        {
            return ctx?.Map != null;
        }

        public void Execute(ProtocolContext ctx)
        {
            var evt = ctx.CheeseEvt;
            QMsg($"Executing protocol={Id} for {evt}", Channel.Debug);
            BullySpawner.Spawn(evt.username, evt.amount, evt.message);
        }
    }
}