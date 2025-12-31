using System;

namespace CheeseProtocol
{
    public static class CheeseCommandParser
    {
        public static CheeseCommand Parse(string message, out string args)
        {
            args = string.Empty;
            if (string.IsNullOrWhiteSpace(message)) return CheeseCommand.None;

            string trimmed = message.Trim();
            if (!trimmed.StartsWith("!")) return CheeseCommand.None;

            var specs = CheeseCommands.Specs;
            for (int i = 0; i < specs.Length; i++)
            {
                var p = specs[i].prefix;
                if (trimmed.StartsWith(p, StringComparison.Ordinal))
                {
                    args = trimmed.Substring(p.Length).TrimStart();
                    return specs[i].cmd;
                }
            }

            return CheeseCommand.None;
        }

        // 필요하면: cmd -> spec 찾기
        public static bool TryGetSpec(CheeseCommand cmd, out CheeseCommandSpec spec)
        {
            var specs = CheeseCommands.Specs;
            for (int i = 0; i < specs.Length; i++)
            {
                if (specs[i].cmd == cmd)
                {
                    spec = specs[i];
                    return true;
                }
            }
            spec = default;
            return false;
        }
    }
}
