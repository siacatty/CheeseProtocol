using RimWorld;
using Verse;
using static CheeseProtocol.CheeseLog;
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
            QMsg($"Executing protocol={Id} for {evt}", Channel.Debug);
            TameSpawner.Spawn(evt.username, evt.amount, evt.message);
        }
    }
}