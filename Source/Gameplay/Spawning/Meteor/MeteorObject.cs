using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace CheeseProtocol
{
    public class MeteorObject
    {
        public string type;
        public int size;
        public int scatterRadius;
        public float lumpAvg;
        public float score;
        public ThingDef def;
        public IncidentParms parms;
        public string label;

        public MeteorObject(IncidentParms parms)
        {
            type = "";
            size = 0;
            scatterRadius = 0;
            lumpAvg = 0f;
            score = 10f;
            this.parms = parms;
            def = null;
            label = "";
        }
    }
}