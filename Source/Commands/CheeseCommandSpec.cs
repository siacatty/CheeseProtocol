using System.Linq;

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
        public static readonly CheeseCommandSpec[] Specs = new[]
        {
            new CheeseCommandSpec(CheeseCommand.Join, "!참여", "colonist", "Join / 참여"),
            new CheeseCommandSpec(CheeseCommand.Raid, "!습격", "raid",    "Raid / 습격"),
            new CheeseCommandSpec(CheeseCommand.Bully, "!일진", "bully",  "Bully / 일진"),
            new CheeseCommandSpec(CheeseCommand.Teacher, "!교육", "teacher",  "Teacher / 교육"),
            new CheeseCommandSpec(CheeseCommand.Caravan, "!상단", "caravan", "Top donors / 상단"),
            new CheeseCommandSpec(CheeseCommand.Meteor, "!운석", "meteor",  "Meteor / 운석"),
            new CheeseCommandSpec(CheeseCommand.Supply, "!보급", "supply",  "Supply / 보급"),
            new CheeseCommandSpec(CheeseCommand.Tame, "!조련", "tame",  "Tame / 조련"),
            new CheeseCommandSpec(CheeseCommand.Thrumbo, "!트럼보", "thrumbo",  "Thrumbo / 트럼보"),
        };
        public static string GetCommandText(CheeseCommand cmd)
        {
            var spec = CheeseCommands.Specs
                .FirstOrDefault(s => s.cmd == cmd);

            return spec.cmd != default
                ? spec.prefix
                : "";
        }
    }
}