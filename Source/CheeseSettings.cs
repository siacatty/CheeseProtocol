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

        public override void ExposeData()
        {
            Scribe_Values.Look(ref useDropPod, "useDropPod", true);
            Scribe_Values.Look(ref chzzkStudioUrl, "chzzkStudioUrl", "");
            Scribe_Values.Look(ref chzzkStatus, "chzzkStatus", "Disconnected");
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

            listing.End();
        }
    }
}
