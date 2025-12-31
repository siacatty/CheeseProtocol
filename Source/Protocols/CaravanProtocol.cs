using Verse;

namespace CheeseProtocol.Protocols
{
    public class CaravanProtocol : IProtocol
    {
        public string Id => "caravan";
        public string DisplayName => "Caravan (stub)";

        public bool CanExecute(ProtocolContext ctx) => ctx?.Map != null;

        public void Execute(ProtocolContext ctx)
        {
            Log.Warning($"[CheeseProtocol] CaravanProtocol triggered (stub). Donation: {ctx.Donation}");
            //Messages.Message("[CheeseProtocol] !상단 received (stub).", MessageTypeDefOf.NeutralEvent, false);
        }
    }
}