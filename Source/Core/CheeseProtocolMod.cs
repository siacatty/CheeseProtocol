using System.Collections.Generic;
using Verse;

namespace CheeseProtocol
{
    public class CheeseProtocolMod : Mod
    {
        public static CheeseProtocolMod Instance;
        public static ChzzkChatClient ChzzkChat;
        public static CheeseSettings Settings;
        public static List<TraitCandidate> TraitCatalog;
        public static List<MeteorCandidate> MeteorCatalog;

        public CheeseProtocolMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<CheeseSettings>();
            Settings.EnsureAdvSettingsInitialized();
            Log.Message("[CheeseProtocol] Loaded.");
            ChzzkChat = new ChzzkChatClient(Settings);
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                TraitCatalog = TraitApplier.BuildCatalogTraitCandidates();
                MeteorCatalog = MeteorApplier.BuildCatalogMeteorCandidates();
                //Log.Message($"[CheeseProtocol] TraitCatalog count = {TraitCatalog.Count}");

                Settings.GetAdvSetting<JoinAdvancedSettings>(CheeseCommand.Join)?.UpdateTraitList();
                Settings.GetAdvSetting<MeteorAdvancedSettings>(CheeseCommand.Meteor)?.UpdateMeteorList();
            });
            Instance = this;
        }

        public override string SettingsCategory() => "Cheese Protocol";

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }
        public override void WriteSettings()
        {
            base.WriteSettings();
            Settings.Write();
        }
    }
}
