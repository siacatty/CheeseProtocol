using System.Collections.Generic;
using System;
using System.Linq;
using RimWorld;
using Verse;
namespace CheeseProtocol
{
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

        //!일진
        public const int BullyCountMin = 1;
        public const int BullyCountMax = 10;

        //!교육
        public const int TeachSkillEXPMin = 1000;
        public const int TeachSkillEXPMax = 32000;
        public const int StudentCountMin = 1;
        public const int StudentCountMax = 10;

        //!운석
        public const int MeteorSizeMin = 1;
        public const int MeteorSizeMax = 200;

        //!보급
        public const float SupplyValueMin = 10.0f;
        public const float SupplyValueMax = 3000.0f;
        public static readonly int TechLevelMin = 
            System.Enum.GetValues(typeof(TechLevel)).Cast<TechLevel>().Min(t => (int)t) + 2; // 0: Undefined, Animal 제외
        public static readonly int TechLevelMax = 
            System.Enum.GetValues(typeof(TechLevel)).Cast<TechLevel>().Max(t => (int)t);

        public static readonly int ThingTierMin = 
            System.Enum.GetValues(typeof(QualityCategory)).Cast<QualityCategory>().Min(q => (int)q);

        public static readonly int ThingTierMax = 
            System.Enum.GetValues(typeof(QualityCategory)).Cast<QualityCategory>().Max(q => (int)q);
        
        //!트럼보
        public const int ThrumboMax = 20;
        public const int ThrumboMin = 1;
    }
}