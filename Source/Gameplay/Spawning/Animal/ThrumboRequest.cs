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
    public class ThrumboRequest
    {
        public int alphaCount;
        public int thrumboCount;
        public PawnKindDef thrumboDef;
        public PawnKindDef alphaDef;
        public IncidentParms parms;
        public Pawn leader;
        public Pawn alphaThrumbo;
        public List<Pawn> thrumboList;
        public bool IsValid =>
            (alphaCount > 0 || thrumboCount > 0) && thrumboList != null;
        public ThrumboRequest(IncidentParms parms)
        {
            alphaCount = 0;
            thrumboCount = 0;
            thrumboDef = null;
            alphaDef = null;
            this.parms = parms;
            leader = null;
            alphaThrumbo = null;
            thrumboList = new List<Pawn>(GameplayConstants.ThrumboMax);
        }
        public override string ToString()
        {
            return $"alphaCount={alphaCount}"
                + $" alphaDef={alphaDef?.defName}"
                + $" alphaDefendRadius={alphaDef?.defendPointRadius}"

                + $" thrumboCount={thrumboCount}"
                + $" thrumboDef={thrumboDef?.defName}"
                + $" thrumboDefendRadius={thrumboDef?.defendPointRadius}"

                + $" spawnedCount={thrumboList?.Count}"
                + $" leader={leader?.LabelCap}";
        }
    }
}