using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using System.Linq;
using Verse;
using RimWorld.BaseGen;

namespace CheeseProtocol
{
    public class TameRequest
    {
        public string label;
        public float marketValue;
        public PawnKindDef def;
        public IncidentParms parms;
        public TameCandidate chosen;
        public bool IsValid =>
            def != null &&
            !string.IsNullOrWhiteSpace(label);
        public TameRequest(IncidentParms parms)
        {
            label = "";
            marketValue = 0f;
            def = null;
            this.parms = parms;
        }
        public void setChosen(TameCandidate chosen)
        {
            this.chosen = chosen;
            def = chosen.def;
            label = chosen.label;
            marketValue = chosen.marketValue;
        }
        public override string ToString()
        {
            return $"label={label}"
                + $" marketValue={marketValue}";
        }
    }
}