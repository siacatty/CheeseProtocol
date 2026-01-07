using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using System.Linq;

namespace CheeseProtocol
{
    internal static class SupplySpawner
    {
        public static void Spawn(string donorName, int amount, string message)
        {
            //CaravanAdvancedSettings caravanAdvSetting = CheeseProtocolMod.Settings?.GetAdvSetting<CaravanAdvancedSettings>(CheeseCommand.Caravan);
            //if (caravanAdvSetting == null) return;
            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;
            //float quality = QualityEvaluator.evaluateQuality(amount, CheeseCommand.Caravan);
        }
    }
}