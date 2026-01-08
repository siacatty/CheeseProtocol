using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using System.Text;
using System.Linq;
using Verse;
using RimWorld.BaseGen;

namespace CheeseProtocol
{
    public static class ThrumboApplier
    {
        public static bool TryApplyAlphaProbHelper(ThrumboRequest request, float alhphaProb01)
        {
            List<TameCandidate> animalList = CheeseProtocolMod.TameCatalog;
            if (animalList == null || animalList.Count == 0)
                return false;

            alhphaProb01 = Mathf.Clamp01(alhphaProb01);

            if (Rand.Chance(alhphaProb01))
            {
                TameCandidate alphaThrumbo = animalList.FirstOrDefault(c => c.defName == "AlphaThrumbo");
                if (alphaThrumbo.IsValid)
                {
                    request.alphaCount = 1;
                    request.alphaDef = alphaThrumbo.def;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }
        public static bool TryApplyThrumboCountHelper(ThrumboRequest request, float thrumboCountF)
        {
            List<TameCandidate> animalList = CheeseProtocolMod.TameCatalog;
            if (animalList == null || animalList.Count == 0)
                return false;

            int thrumboCount = Mathf.RoundToInt(Mathf.Clamp(thrumboCountF, GameplayConstants.ThrumboMin, GameplayConstants.ThrumboMax));
            TameCandidate thrumbo = animalList.FirstOrDefault(c => c.defName == "Thrumbo");
            if (thrumbo.IsValid)
            {
                request.thrumboCount = thrumboCount - request.alphaCount;
                request.thrumboDef = thrumbo.def;
            }
            else
            {
                return false;
            }
            return true;
        }
    }
}