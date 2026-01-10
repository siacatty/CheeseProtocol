using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace CheeseProtocol
{
    public class MeteorRequest
    {
        public string type;
        public int size;
        public int scatterRadius;
        public float lumpAvg;
        public float score;
        public ThingDef def;
        public string label;

        public MeteorRequest()
        {
            type = "";
            size = 0;
            scatterRadius = 0;
            lumpAvg = 0f;
            score = 10f;
            def = null;
            label = "";
        }
    }
}