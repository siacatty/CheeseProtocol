using Verse;

namespace CheeseProtocol.Protocols
{
    public class RaidProtocol : IProtocol
    {
        public string Id => "raid";
        public string DisplayName => "Raid (stub)";

        public bool CanExecute(ProtocolContext ctx) => ctx?.Map != null;

        public void Execute(ProtocolContext ctx)
        {
            Log.Warning($"[CheeseProtocol] RaidProtocol triggered (stub). Donation: {ctx.Donation}");
            //Messages.Message("[CheeseProtocol] !습격 received (stub).", MessageTypeDefOf.ThreatBig, false);
        }
    }
}