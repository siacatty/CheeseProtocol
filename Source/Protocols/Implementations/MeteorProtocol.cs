using RimWorld;
using Verse;
using static CheeseProtocol.CheeseLog;
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
            QMsg($"Executing protocol={Id} for {evt}", Channel.Debug);
            MeteorSpawner.Spawn(evt.username, evt.amount, evt.message);
        }
    }
}