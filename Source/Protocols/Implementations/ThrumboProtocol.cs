using RimWorld;
using Verse;
using static CheeseProtocol.CheeseLog;
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
            QMsg($"Executing protocol={Id} for {evt}", Channel.Debug);
            ThrumboSpawner.Spawn(evt.username, evt.amount, evt.message);
        }
    }
}