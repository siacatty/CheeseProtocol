using Verse;
using RimWorld;

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
            var map = ctx.Map;

            var parms = StorytellerUtility.DefaultParmsNow(
                IncidentCategoryDefOf.ThreatBig,
                map
            );

            // Vanilla incident defName: "RaidEnemy"
            IncidentDef def = DefDatabase<IncidentDef>.GetNamed("RaidEnemy", false);
            if (def == null)
            {
                Log.Warning("[CheeseProtocol] RaidEnemy def not found.");
                return;
            }
            if (!def.Worker.TryExecute(parms))
                Log.Warning("[CheeseProtocol] RaidEnemy failed to execute.");
        }
    }
}