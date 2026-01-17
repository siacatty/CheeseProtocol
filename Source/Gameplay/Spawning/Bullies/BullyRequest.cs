using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Permissions;
using RimWorld;
using UnityEngine;
using Verse;

namespace CheeseProtocol
{
    public class BullyRequest
    {
        public int bullyCount;
        public float stealValue;
        public List<Pawn> bullyList;
        public Dictionary<Pawn, Pawn> initialTargets;
        public Pawn leader;

        public bool IsValid => bullyCount > 0 && stealValue >= 0;

        public BullyRequest()
        {
            bullyCount = -1;
            stealValue = -1;
            bullyList = new List<Pawn>();
        }

        public override string ToString()
        {
            return $"bullyCount={bullyCount}, stealValue={stealValue:0.##}";
        }
    }
}