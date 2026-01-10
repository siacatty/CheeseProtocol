using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace CheeseProtocol
{
    public class SupplyRequest
    {
        public SupplyType type;
        public string label;
        public int count;
        public float marketValue;
        public float totalValue;
        public int scatterRadius;
        public ThingDef def;
        public List<SupplyType> allowedTypes;
        public SupplyCandidate chosen;
        public QualityCategory weaponTier;
        public TechLevel techLevel;
        public bool isWeaponTierSet;
        public int stackLimit;
        public bool IsValid =>
            def != null &&
            !string.IsNullOrWhiteSpace(label) &&
            type != SupplyType.Undefined &&
            count > 0;
        public SupplyRequest()
        {
            type = SupplyType.Undefined;
            label = "";
            count = 0;
            marketValue = 0f;
            totalValue = 0f;
            def = null;
            allowedTypes = new List<SupplyType>();
            isWeaponTierSet = false;
            stackLimit = 0;
            techLevel = TechLevel.Undefined;
        }
        public void setChosen(SupplyCandidate chosen)
        {
            this.chosen = chosen;
            def = chosen.def;
            label = chosen.label;
            marketValue = chosen.marketValue;
            stackLimit = chosen.stackLimit;
        }
        public override string ToString()
        {
            return
                "[SupplyRequest: " +
                $"type={type}, " +
                $"label={label}, " +
                $"def={(def != null ? def.defName : "null")}, " +
                $"count={count}, " +
                $"stackLimit={stackLimit}, " +
                $"unitValue={marketValue:0.##}, " +
                $"totalValue={totalValue:0.##}, " +
                $"weaponTier={(isWeaponTierSet ? weaponTier.ToString() : "unset")}, " +
                $"techLevel={techLevel}, " +
                $"allowedTypes=[{string.Join(",", allowedTypes)}] ";
        }
    }
}