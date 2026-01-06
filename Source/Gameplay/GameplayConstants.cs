using System.Collections.Generic;
using RimWorld;
using Verse;
public static class GameplayConstants
{
    public const int SkillLevelMin = 0;
    public const int SkillLevelMax = 20;
    public const int PassionMin = 0;
    public const int PassionMax = 24;
    public const int TraitCountMin = 1;
    public const int TraitCountMax = 3;

    public const int HediffCountMin = 0;
    public const int HediffCountMax = 3;
    public const int AgeMin = 13;
    public const int AgeMax = 80;
    public static readonly HashSet<ThingDef> AllowedRaces = new()
    {
        ThingDefOf.Human,
    };

    //!습격
    public const int RaidScaleMin = 1;
    public const int RaidScaleMax = 10;
}