using System;

namespace CheeseProtocol
{
    public static class CheeseDefaults
    {
        public const bool UseDropPod = true;
        public const string ChzzkStudioUrl = "";
        public const string ChzzkStatus = "Disconnected";
        public const bool ShowHud = true;
        public const bool HudLocked = false;
        public const float HudOpacity = 0.9f;
        public const bool DrainQueue = true;
        public const float HudX = -1f;   // -1이면 아직 저장된 위치 없음(기본 위치 사용)
        public const float HudY = -1f;
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
        
    }
}