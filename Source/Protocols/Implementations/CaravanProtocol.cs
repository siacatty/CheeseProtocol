using Verse;
using RimWorld;
using static CheeseProtocol.CheeseLog;
namespace CheeseProtocol.Protocols
{
    public class CaravanProtocol : IProtocol
    {
        public string Id => "caravan";
        public string DisplayName => "Caravan protocol";

        public bool CanExecute(ProtocolContext ctx)
        {
            return ctx?.Map != null;
        }

        public void Execute(ProtocolContext ctx)
        {
            var evt = ctx.CheeseEvt;
            QMsg($"Executing protocol={Id} for {evt}", Channel.Debug);

            CaravanSpawner.Spawn(evt.username, evt.amount, evt.message);
        }
    }
}