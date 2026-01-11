using System;
using static CheeseProtocol.CheeseLog;
namespace CheeseProtocol
{
    public static class CheeseCommandParser
    {
        public static CheeseCommand Parse(string message, out string args)
        {
            args = string.Empty;
            if (string.IsNullOrWhiteSpace(message)) return CheeseCommand.None;

            int bang = message.IndexOf('!');
            if (bang < 0) return CheeseCommand.None;

            // 여러 커맨드면 첫 번째만: 첫 ! 이후, 다음 ! 전까지만 “처리 구간”
            int nextBang = message.IndexOf('!', bang + 1);
            string segment = nextBang >= 0 ? message.Substring(bang, nextBang - bang) : message.Substring(bang);
            // 공백을 무시하면서 prefix를 매칭
            var specs = CheeseCommands.Specs;

            for (int i = 0; i < specs.Length; i++)
            {
                if (TryMatchPrefix(segment, specs[i].prefix, out int endPos))
                {
                    args = segment.Substring(endPos).TrimStart();
                    return specs[i].cmd;
                }
            }
            return CheeseCommand.None;
        }

        private static bool TryMatchPrefix(string s, string prefix, out int endPos)
        {
            endPos = 0;
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(prefix)) return false;

            int i = 0; // s index
            int j = 0; // prefix index

            // prefix도 혹시 실수로 공백 들어갈 수 있으니 prefix 쪽 공백도 스킵
            while (j < prefix.Length)
            {
                char pj = prefix[j];
                if (char.IsWhiteSpace(pj)) { j++; continue; }

                // s에서 공백 스킵
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;

                if (i >= s.Length) return false;
                if (s[i] != pj) return false;

                i++; j++;
            }
            endPos = i;
            return true;
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
