using HarmonyLib;
using Verse;

namespace CheeseProtocol
{
    [StaticConstructorOnStartup]
    public static class HarmonyBootstrap
    {
        static HarmonyBootstrap()
        {
            Log.Message("[CheeseProtocol] HarmonyBootstrap start");
            var harmony = new Harmony("CheeseProtocol");
            harmony.PatchAll();
            Log.Message("[CheeseProtocol] HarmonyBootstrap done");
        }
    }
}