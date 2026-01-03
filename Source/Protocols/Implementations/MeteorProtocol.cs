using RimWorld;
using Verse;

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
            Map map = ctx.Map;
            IncidentDef def = DefDatabase<IncidentDef>.GetNamed("MeteoriteImpact", false);
            if (def == null)
            {
                Log.Warning("[CheeseProtocol] MeteoriteImpact def not found.");
                return;
            }

            var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, map);
            // (선택) 운석 크기/위치 조절은 이 incident는 내부에서 랜덤 처리됨

            if (!def.Worker.TryExecute(parms))
                Log.Warning("[CheeseProtocol] MeteoriteImpact failed to execute.");
        }
    }
}