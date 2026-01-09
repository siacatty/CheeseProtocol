using RimWorld;
using Verse;

namespace CheeseProtocol
{
    public static class VanillaIncidentRunner
    {
        public static bool TryExecuteWithTrace(IncidentDef def, IncidentParms parms, CheeseRollTrace trace)
        {
            if (def?.Worker == null) return false;

            CheeseLetterContext.Push(trace);
            try
            {
                return def.Worker.TryExecute(parms);
            }
            catch (System.Exception e)
            {
                Log.Error($"[CheeseProtocol] Incident threw: {def?.defName}\n{e}");
                return false;
            }
            finally
            {
                CheeseLetterContext.Pop();
            }
        }
    }
}