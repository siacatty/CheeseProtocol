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
        public const float RandomVar = 0.8f;
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
        

        //Advanced Setting
        //!참여
        public static readonly QualityRange PassionRange = QualityRange.init(GameplayConstants.PassionMin, GameplayConstants.PassionMax);
        public static readonly QualityRange SkillRange   = QualityRange.init(GameplayConstants.SkillLevelMin, GameplayConstants.SkillLevelMax);
        public static readonly QualityRange AgeRange = QualityRange.init(GameplayConstants.AgeMin, GameplayConstants.AgeMax);
        public static readonly QualityRange TraitsRange  = QualityRange.init();
        public static readonly QualityRange HealthRange  = QualityRange.init();
        public static readonly QualityRange ApparelRange = QualityRange.init();
        public static readonly QualityRange WeaponRange  = QualityRange.init();
        public const bool ForcePlayerIdeo = true;
        public const bool ForceHuman = true;
        public const bool AllowWorkDisable = true;
        public const bool UseDropPod = true;
        public static readonly string[] NegativeTraitKeys =
        {
            "TorturedArtist(0)",
            "Gourmand(0)",
            "SpeedOffset(-1)"
        };
        public static readonly string[] PositiveTraitKeys =
        {
        };
        //!습격
        public static readonly QualityRange RaidScaleRange = QualityRange.init(GameplayConstants.RaidScaleMin, GameplayConstants.RaidScaleMax);
        public const bool AllowCenterDrop = true;
        public const bool AllowBreacher = true;
        public const bool AllowSiege = true;
        //!운석
        public static readonly QualityRange MeteorTypeRange = QualityRange.init();
        public static readonly QualityRange MeteorSizeRange = QualityRange.init(GameplayConstants.MeteorSizeMin, GameplayConstants.MeteorSizeMax);
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
            "Slate"
            //"Marble"
        };
        //!상단
        public static readonly QualityRange OrbitalRange = QualityRange.init();
        public const bool AllowShamanCaravan = true;
        public const bool AllowBulkCaravan = true;
        public const bool AllowSlaverCaravan = true;
        public const bool AllowExoticCaravan = true;
        public const bool AllowCombatCaravan = true;
        public const bool AllowRoyalCaravan = true;
        public const bool AllowImperialCaravan = true;
        //!보급
        public static readonly QualityRange SupplyTierRange = QualityRange.init();
        public static readonly QualityRange SupplyValueRange = QualityRange.init(GameplayConstants.SupplyValueMin, GameplayConstants.SupplyValueMax);
        public static readonly QualityRange WeaponTierRange = QualityRange.init(GameplayConstants.ThingTierMin, GameplayConstants.ThingTierMax);
        public static readonly QualityRange WeaponTechRange = QualityRange.init(GameplayConstants.TechLevelMin, GameplayConstants.TechLevelMax);
        public const bool AllowFoodSupply = true;
        public const bool AllowMedSupply = true;
        public const bool AllowDrugSupply = true;
        public const bool AllowWeaponSupply = true;
        //!조련
        public static readonly QualityRange TameValueRange = QualityRange.init();
    }
}