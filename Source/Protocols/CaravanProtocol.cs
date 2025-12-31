using Verse;
using RimWorld;

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
            var map = ctx.Map;

            var parms = StorytellerUtility.DefaultParmsNow(
                IncidentCategoryDefOf.Misc,
                map
            );

            // Vanilla incident defName: "TraderCaravanArrival"
            IncidentDef def = DefDatabase<IncidentDef>.GetNamed("TraderCaravanArrival", false);
            if (def == null)
            {
                Log.Warning("[CheeseProtocol] TraderCaravanArrival def not found.");
                return;
            }

            if (!def.Worker.TryExecute(parms))
                Log.Warning("[CheeseProtocol] TraderCaravanArrival failed to execute.");
        }
    }
}