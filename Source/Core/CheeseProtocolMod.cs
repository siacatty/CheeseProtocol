using System.Collections.Generic;
using Verse;
using static CheeseProtocol.CheeseLog;

namespace CheeseProtocol
{
    public class CheeseProtocolMod : Mod
    {
        public static CheeseProtocolMod Instance;
        public static ChzzkChatClient ChzzkChat;
        public static CheeseSettings Settings;
        public static List<TraitCandidate> TraitCatalog;
        public static List<MeteorCandidate> MeteorCatalog;
        public static List<TameCandidate> TameCatalog;
        public static List<SupplyCandidate> SupplyFoodCatalog;
        public static List<SupplyCandidate> SupplyMedCatalog;
        public static List<SupplyCandidate> SupplyDrugCatalog;
        public static List<SupplyCandidate> SupplyWeaponCatalog;

        public CheeseProtocolMod(ModContentPack content) : base(content)
        {
            Log.Message("[CheeseProtocol] Loaded.");
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                Settings = GetSettings<CheeseSettings>();
                Settings.EnsureAdvSettingsInitialized();
                ChzzkChat = new ChzzkChatClient(Settings);
                TraitCatalog = TraitApplier.BuildCatalogTraitCandidates();
                MeteorCatalog = MeteorApplier.BuildCatalogMeteorCandidates();
                TameCatalog = TameApplier.BuildCatalogTameCandidates();
                SupplyApplier.BuildCatalogSupplyCandidates(out SupplyFoodCatalog, out SupplyMedCatalog, out SupplyDrugCatalog, out SupplyWeaponCatalog);
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
