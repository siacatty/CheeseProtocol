using Verse;
using static CheeseProtocol.CheeseLog;
namespace CheeseProtocol.Protocols
{
    public class ColonistProtocol : IProtocol
    {
        public string Id => "colonist";
        public string DisplayName => "Colonist Join protocol";

        public bool CanExecute(ProtocolContext ctx)
        {
            return ctx?.Map != null && ctx?.CheeseEvt != null;
        }

        public void Execute(ProtocolContext ctx)
        {
            var evt = ctx.CheeseEvt;
            QMsg($"Executing protocol={Id} for {evt}", Channel.Debug);
            ColonistSpawner.Spawn(evt.username, evt.amount, evt.message);
        }
    }
}