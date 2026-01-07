namespace CheeseProtocol
{
    public struct CheeseCommandSpec
    {
        public CheeseCommand cmd;
        public string prefix;     // "!참여"
        public string protocolId; // "colonist"
        public string desc;       // HUD용

        public CheeseCommandSpec(CheeseCommand cmd, string prefix, string protocolId, string desc)
        {
            this.cmd = cmd;
            this.prefix = prefix;
            this.protocolId = protocolId;
            this.desc = desc;
        }
    }

    public static class CheeseCommands
    {
        // ✅ 여기만 수정하면 Parser/HUD/Router 전부 반영됨
        public static readonly CheeseCommandSpec[] Specs = new[]
        {
            new CheeseCommandSpec(CheeseCommand.Join, "!참여", "colonist", "Join / 참여"),
            new CheeseCommandSpec(CheeseCommand.Raid, "!습격", "raid",    "Raid / 습격"),
            new CheeseCommandSpec(CheeseCommand.Caravan, "!상단", "caravan", "Top donors / 상단"),
            new CheeseCommandSpec(CheeseCommand.Meteor, "!운석", "meteor",  "Meteor / 운석"),
            new CheeseCommandSpec(CheeseCommand.Supply, "!보급", "supply",  "Supply / 보급"),
            new CheeseCommandSpec(CheeseCommand.Tame, "!조련", "tame",  "Tame / 조련"),
            new CheeseCommandSpec(CheeseCommand.Thrumbo, "!트럼보", "thrumbo",  "Thrumbo / 트럼보"),
        };
    }
}