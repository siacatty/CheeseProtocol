using Verse;
using UnityEngine;
using RimWorld;

namespace CheeseProtocol
{
    public class CheeseSettings : ModSettings
    {
        public bool useDropPod = true;
        public string chzzkStudioUrl = "";
        public string chzzkStatus = "Disconnected";
        public bool showHud = true;
        public float hudX = -1f;   // -1이면 아직 저장된 위치 없음(기본 위치 사용)
        public float hudY = -1f;
        public bool hudMinimized = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref useDropPod, "useDropPod", true);
            Scribe_Values.Look(ref chzzkStudioUrl, "chzzkStudioUrl", "");
            Scribe_Values.Look(ref chzzkStatus, "chzzkStatus", "Disconnected");
            Scribe_Values.Look(ref showHud, "showHud", true);
            Scribe_Values.Look(ref hudX, "hudX", -1f);
            Scribe_Values.Look(ref hudY, "hudY", -1f);
        }

        public void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.CheckboxLabeled("Spawn via drop pod", ref useDropPod);

            listing.Label("CHZZK Studio URL:");
            chzzkStudioUrl = listing.TextEntry(chzzkStudioUrl);

            listing.Label($"CHZZK Status: {chzzkStatus}");

            if (listing.ButtonText("CHZZK: Connect (stub)"))
            {
                CheeseProtocolMod.ChzzkChat.UserConnect();
            }

            if (listing.ButtonText("CHZZK: Disconnect (stub)"))
            {
                CheeseProtocolMod.ChzzkChat.UserDisconnect();
            }
            listing.GapLine();
            listing.CheckboxLabeled("치지직 프로토콜 HUD 표시", ref showHud, "화면에 작은 상태 표시창을 띄웁니다.");
            listing.End();
        }
    }
}
