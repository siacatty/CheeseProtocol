using Verse;
using static CheeseProtocol.CheeseLog;
namespace CheeseProtocol.Protocols
{
    public class ColonistProtocol : IProtocol
    {
        public string Id => "colonist";
        public string DisplayName => "Colonist Join protocol";

        public bool CanExecute(ProtocolContext ctx)
        {
            bool isMapValid = ctx?.Map != null;
            bool isEvtValid = ctx?.CheeseEvt != null;
            var participantRegistry = CheeseParticipantRegistry.Get();
            if (participantRegistry == null) return false;
            var join = CheeseProtocolMod.Settings?.GetAdvSetting<JoinAdvancedSettings>(CheeseCommand.Join);

            bool restrictParticipants = join?.restrictParticipants ?? CheeseDefaults.RestrictParticipants;
            int  maxParticipants      = join?.maxParticipants      ?? CheeseDefaults.MaxParticipants;
            int count = participantRegistry.Count;

            bool hasRoomForParticipant = !restrictParticipants || (count < maxParticipants);
            return isMapValid && isEvtValid && hasRoomForParticipant;
        }

        public void Execute(ProtocolContext ctx)
        {
            var evt = ctx.CheeseEvt;
            QMsg($"Executing protocol={Id} for {evt}", Channel.Debug);
            ColonistSpawner.Spawn(evt.username, evt.amount, evt.message);
        }
    }
}