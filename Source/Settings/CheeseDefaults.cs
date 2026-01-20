using System;
using System.Collections.Generic;
using RimWorld;

namespace CheeseProtocol
{
    public static class CheeseDefaults
    {
        //Main setting 
        public const string ChzzkStudioUrl = "";
        public const string ChzzkStatus = "Disconnected";
        public const bool ShowHud = true;
        public const bool HudLocked = false;
        public const float HudOpacity = 0.9f;
        public const bool DrainQueue = true;
        public const float HudX = -1f;   // -1이면 아직 저장된 위치 없음(기본 위치 사용)
        public const float HudY = -1f;
        public const float HudW = -1f;
        public const float HudH = -1f;
        public const float RandomVar = 0.8f;
        public const bool AppendRollLogToLetters = true;
        public const CheeseCommandSource SimSource = CheeseCommandSource.Donation;
        public const int SimDonAmount = 1000;
        public const string SimDonAmountBuf = "1000";
        public const bool HudMinimized = false;
        public const bool HudSlideHidden = false;
        public const bool CmdEnabled = true;
        public const CheeseCommandSource CmdSource = CheeseCommandSource.Donation;
        public const int CmdMinDonation = 1000;
        public const int CmdMaxDonation = 10000;
        public const int CmdCooldownHours = 0;
        public const bool AllowSpeechBubble = true;
        public const int SpeechBubbleCD = 5;
        public const string SpeechBubbleCDBuf = "5";
        

        //Advanced Setting
        //!참여
        public static readonly QualityRange PassionRange = QualityRange.init(1, 10);
        public static readonly QualityRange SkillRange   = QualityRange.init(2, 10);
        public static readonly QualityRange AgeRange = QualityRange.init(18, 40);
        public static readonly QualityRange TraitsRange  = QualityRange.init(0.65f, 1f);
        public static readonly QualityRange HealthRange  = QualityRange.init(0.8f, 1f);
        public static readonly QualityRange ApparelRange = QualityRange.init();
        public static readonly QualityRange WeaponRange  = QualityRange.init();
        public const bool RestrictParticipants = true;
        public const int MaxParticipants = 20;
        public const bool ForcePlayerIdeo = true;
        public const bool ForceHuman = true;
        public const bool AllowWorkDisable = true;
        public const bool UseDropPod = true;
        public static readonly string[] AllowedRaceKeys =
        {
            "Colonist",
        };
        public static readonly string[] NegativeTraitKeys =
        {
            "Nudist(0)",
            "Pyromaniac(0)",
            "Wimp(0)",
            "SlowLearner(0)",
            "BodyPurist(0)",
            "Gourmand(0)",
            "SpeedOffset(-1)",
            "DrugDesire(2)",
            "DrugDesire(-1)",
            "NaturalMood(-1)",
            "NaturalMood(-2)",
            "Nerves(-1)",
            "Nerves(-2)",
            "Industriousness(-1)",
            "Industriousness(-2)",
            "Immunity(-1)",
            "Delicate(0)"
        };
        public static readonly string[] PositiveTraitKeys =
        {
        };
        //!습격
        public static readonly QualityRange RaidScaleRange = QualityRange.init(GameplayConstants.RaidScaleMin, 2f);
        public const bool AllowCenterDrop = true;
        public const bool AllowBreacher = true;
        public const bool AllowSiege = true;
        //!일진
        public static readonly QualityRange BullyCountRange = QualityRange.init(GameplayConstants.BullyCountMin, GameplayConstants.BullyCountMax);
        public static readonly QualityRange StealValueRange = QualityRange.init();
        //!운석
        public static readonly QualityRange MeteorTypeRange = QualityRange.init();
        public static readonly QualityRange MeteorSizeRange = QualityRange.init(20, 100);
        public static readonly string[] AllowedMeteorKeys =
        {
            "MineableSteel",
            "MineableSilver",
            "MineableGold",
            "MineableUranium",
            "MineablePlasteel",
            "MineableJade",
            "MineableComponentsIndustrial",
            "Sandstone",
            "Granite",
            "Limestone",
            "Slate",
            "Marble"
        };
        //!상단
        public static readonly QualityRange OrbitalRange = QualityRange.init(0.4f, 1f);
        public const bool AllowShamanCaravan = true;
        public const bool AllowBulkCaravan = true;
        public const bool AllowSlaverCaravan = true;
        public const bool AllowExoticCaravan = true;
        public const bool AllowCombatCaravan = true;
        public const bool AllowRoyalCaravan = false;
        public const bool AllowImperialCaravan = true;
        //!보급
        public static readonly QualityRange SupplyTierRange = QualityRange.init();
        public static readonly QualityRange SupplyValueRange = QualityRange.init(100, 1000);
        public static readonly QualityRange WeaponTierRange = QualityRange.init(GameplayConstants.ThingTierMin, GameplayConstants.ThingTierMax);
        public static readonly QualityRange WeaponTechRange = QualityRange.init(GameplayConstants.TechLevelMin, GameplayConstants.TechLevelMax);
        public const bool AllowFoodSupply = true;
        public const bool AllowMedSupply = true;
        public const bool AllowDrugSupply = true;
        public const bool AllowWeaponSupply = false;
        //!조련
        public static readonly QualityRange TameValueRange = QualityRange.init(0.3f, 1f);

        //!트럼보
        public static readonly QualityRange AlphaProbRange = QualityRange.init(0.05f, 0.75f);
        public static readonly QualityRange ThrumboCountRange = QualityRange.init(GameplayConstants.ThrumboMin, 10);
        public const bool AllowAlpha = true;
    }
}