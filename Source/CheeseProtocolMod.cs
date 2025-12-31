using Verse;

namespace CheeseProtocol
{
    public class CheeseProtocolMod : Mod
    {
        public static ChzzkChatClient ChzzkChat;
        public static CheeseSettings Settings;

        public CheeseProtocolMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<CheeseSettings>();
            Log.Message("[CheeseProtocol] Loaded.");
            ChzzkChat = new ChzzkChatClient(Settings);
        }

        public override string SettingsCategory() => "Cheese Protocol";

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }
    }
}
