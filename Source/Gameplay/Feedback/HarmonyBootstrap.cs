using HarmonyLib;
using Verse;
using static CheeseProtocol.CheeseLog;
namespace CheeseProtocol
{
    [StaticConstructorOnStartup]
    public static class HarmonyBootstrap
    {
        static HarmonyBootstrap()
        {
            Msg("HarmonyBootstrap start", Channel.Debug);
            var harmony = new Harmony("CheeseProtocol");
            harmony.PatchAll();
            Msg("HarmonyBootstrap done", Channel.Debug);
        }
    }
}