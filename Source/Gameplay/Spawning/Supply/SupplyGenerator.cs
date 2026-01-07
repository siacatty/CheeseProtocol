using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace CheeseProtocol
{
    public static class SupplyGenerator
    {
        public static List<SupplyCandidate> BuildCatalogSupplyCandidates()
        {
            var list = new List<SupplyCandidate>(512);

            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def == null) continue;
                var b = def.building;
                if (b == null) continue;
            }
            return list;
        }
    }
}