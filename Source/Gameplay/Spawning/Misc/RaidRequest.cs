using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Permissions;
using RimWorld;
using UnityEngine;
using Verse;

namespace CheeseProtocol
{
    public class RaidRequest
    {
        public float raidScale;

        public RaidRequest()
        {
            raidScale = 1f;
        }
    }
}