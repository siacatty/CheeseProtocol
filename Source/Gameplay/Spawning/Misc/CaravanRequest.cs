using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Permissions;
using RimWorld;
using UnityEngine;
using Verse;

namespace CheeseProtocol
{
    public class CaravanRequest
    {
        public TraderKindDef traderDef;
        public IncidentParms parms;
        public bool isOrbital;
        public IncidentDef incidentDef;
        public List<TraderKindDef> traderPool;
        public bool requireRepick;

        public CaravanRequest()
        {
            traderDef = null;
            parms = null;
            incidentDef = null;
            isOrbital = false;
            requireRepick = false;
            traderPool = new List<TraderKindDef>();
        }
    }
}