using System;
using System.Collections.Generic;
using Verse;

namespace CheeseProtocol
{
    internal enum BullyTextKey
    {
        ArrivedColonyEdge,
        StunColonist,
        StartSteal,
        GrabbedItem,
        ExitNow,
        ResistCapture,
        FailedToFindTarget,
        TauntGeneric
    }

    internal static class LordChats
    {
        private static readonly Dictionary<BullyTextKey, string[]> Pool = new()
        {
            [BullyTextKey.ArrivedColonyEdge] = new[]
            {
                "자~ 드가자~~",
                "집 꼬라지 봐라 ㅋㅋ",
                "반갑습니다~~~ 저희 놀러왔어요~~",
            },
            [BullyTextKey.StunColonist] = new[]
            {
                "{0} 너 좀 귀엽다?",
                "뭐하냐? 자냐?",
                "아파? 아프면 말해~",
                "야 좀 씻어라 ㅋㅋ {0}",
                "눈을 왜 그렇게 떠?",
                "바빠? ㅋㅋ 바쁘냐고",
                "{0}? 이름 꼬라지 ㅋ",
                "{0} 와꾸 살벌한거봐라",
                "야 노래 불러봐",
                "니는 공부 열심히해야겠다"
            },
            [BullyTextKey.StartSteal] = new[]
            {
                "어디보자~~ 집에 뭐 있나 볼까?",
                "뭐 가져갈건있냐?",
                "진짜 둘러만볼게~ ",
            },
            [BullyTextKey.GrabbedItem] = new[]
            {
                "{0} 잠깐만 빌릴게~",
                "{0} 땡큐~ 고마워!",
                "{0} 이거 쓸일없지?",
            },
            [BullyTextKey.ExitNow] = new[]
            {
                "와 진짜 그지들이네... 다음에 올때 꼭 준비해놔",
                "간다~~",
                "재밌었어요~",
            },
            [BullyTextKey.ResistCapture] = new[]
            {
                "이딴 집에 들어가느니 차라리 죽지",
                "뒤질래? 건들지마라",
                "{0}? 니 이름 딱 기억해놨다. 건들지마",
            },
        };

        // 딱 하나만 노출
        internal static string GetText(BullyTextKey key, params object[] args)
        {
            if (!Pool.TryGetValue(key, out var arr) || arr == null || arr.Length == 0)
                return string.Empty;

            string raw = arr.RandomElement();
            if (raw.NullOrEmpty() || args == null || args.Length == 0)
                return raw;

            try
            {
                return string.Format(raw, args);
            }
            catch (FormatException)
            {
                return raw;
            }
        }
    }
}