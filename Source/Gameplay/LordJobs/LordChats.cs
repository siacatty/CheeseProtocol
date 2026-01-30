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
    internal enum TeacherTextKey
    {
        Arrived,
        Wait,
        GatherStudentsOutdoor,
        GatherStudentsPlain,
        GatherStudentsTable,
        GatherStudentsBlackboard,
        GatherStudents,
        TakeSeats,
        LessonStart,
        LessonResume,
        TeachLesson,
        TeachLessonQuiz,
        TeachLessonDrafted,
        DetectEscape,
        SubdueStudent,
        SubdueNonStudent,
        EndLessonSuccess,
        EndLessonFail,
        EatSnackFail,
        EatSnackSuccess,
        Harmed,
        Gas,
        Temperature,
        TimeOut,
        InMental,
    }

    internal static class LordChats
    {
        private static readonly Dictionary<TeacherTextKey, string[]> TeacherPool = new()
        {
            [TeacherTextKey.Arrived] = new[]
            {
                "반가워요~",
                "안녕하세요? 변방계 『최강』 교사 {0}입니다.",
            },
            [TeacherTextKey.Wait] = new[]
            {
                "선생님을 계속 기다리게 할텐가...",
                "현자가 되고 싶은 자는 나에게...",
                "변방계 최고 지식을 가르쳐 드립니다.",
            },
            [TeacherTextKey.GatherStudentsOutdoor] = new[]
            {
                "이 집은 적당한 교실이 없네요. 오늘은 특별히 야외수업으로 진행할게요.",
            },
            [TeacherTextKey.GatherStudentsPlain] = new[]
            {
                "음 집에 칠판 하나 없네요. 그냥 이 방에서 진행할게요.",
            },
            [TeacherTextKey.GatherStudentsTable] = new[]
            {
                "집에 칠판 하나 없네요... 대충 여기 책상으로 모이죠.",
            },
            [TeacherTextKey.GatherStudentsBlackboard] = new[]
            {
                "집에 교실도 있고, 이 집은 학구열이 대단하네!",
            },
            [TeacherTextKey.GatherStudents] = new[]
            {
                "학생들 모두 모이세요~",
            },
            
            [TeacherTextKey.TakeSeats] = new[]
            {
                "모두 자리로!",
            },
            [TeacherTextKey.LessonStart] = new[]
            {
                "수업 시작하겠습니다",
            },
            [TeacherTextKey.LessonResume] = new[]
            {
                "잠시 소란이 있었어요.",
                "학생들이 겁이 없는건지 버르장머리가 없는건지 참",
            },
            [TeacherTextKey.TeachLesson] = new[]
            {
                "1 더하기 1은 2다 이말이야",
                "이건 시험에 나온다.",
                "지구는 둥글다 이말이야.",
                "원래 머리가 나쁘면 몸이 고생해. 그러니까 공부해야겠지?",
                "2 더하기 2는 22야. 참 쉽지?",
                "4 더하기 2는 뭐겠어? 42다 이말이야.",
            },
            [TeacherTextKey.TeachLessonQuiz] = new[]
            {
                "{0} 학생. 우주선 추진에서 Δv = Isp·g₀·ln(m₀/mf) 이 왜 중요한지 설명해볼까?",
                "{0} 학생. 메카노이드의 행동 함수 U(s,a)가 비선형인 이유를 설명해볼까?",
                "{0} 학생. 생귀오파지 유전자 발현 모델 G(t)=G₀(1-e^(-kt)) 에서 k는 무엇을 뜻할까?",
                "{0} 학생. 사격 정확도 A(r)=A₀/(1+kr²) 가 거리 제곱에 민감한 이유는?",
                "{0} 학생. 메카노이드 제어식 xₜ₊₁ = A·xₜ + B·uₜ 가 불안정해지는 조건은?",
                "{0} 학생. 독성 낙진 축적량 X(t)=∫ Φ(t)dt 이 생태계 회복을 지연시키는 이유는?",
                "{0} 학생. 흑점 폭발 세기 F ∝ B² 가 전자기기에 치명적인 이유는?",
            },
            [TeacherTextKey.TeachLessonDrafted] = new[]
            {
                "{0} 학생? 자리로 돌아가주세요.",
                "{0} 학생? 급한 일 있으신가?",
                "{0}! 수업 중에 돌아다니지 마세요!",
                "{0} 학생? 집중안해?",
            },
            [TeacherTextKey.DetectEscape] = new[]
            {
                "어디가니?",
                "누가 나가도 된대?",
                "넌 어디가니?",
                "그렇게 나가면 모를줄 알았어?",
                "『사 자 후』",
            },
            [TeacherTextKey.SubdueStudent] = new[]
            {
                "이건 첫번째 레슨 ~",
                "{0} 학생? 급한일 있나?",
                "『스승 펀치』",
                "느려",
                "{0} 학생. 다음에 부모님 모셔오도록",
                "느그 아부지 뭐하시노!!!",
            },
            [TeacherTextKey.SubdueNonStudent] = new[]
            {
                "자네는 뭔데 방해하지?",
                "{0}! 넌 뭔데 내 학생을 납치하는가!",
                "내 학생이 납치되고있어! 구해줘야해!",
            },
            [TeacherTextKey.EndLessonSuccess] = new[]
            {
                "수고했어요~",
                "수업 끝~ 끝나니까 출출하네",
            },
            [TeacherTextKey.EndLessonFail] = new[]
            {
                "뭐야. 다들 어디갔어. 참 문제아뿐이구만 이 집은",
                "너희들은 희망이 없다.",
                "선생님은 실망했다.",
            },
            [TeacherTextKey.EatSnackFail] = new[]
            {
                "음.. 수업 끝나니 출출한데, 다음엔 꼭 챙겨놔라",
                "학생들이 잔머리만 굴리고 참.. 다음엔 밥이라도 챙겨놔",
            },
            [TeacherTextKey.EatSnackSuccess] = new[]
            {
                "아이고, 음식 대접까지야. 고마워요~",
                "역시 수업끝나고 먹는 밥이 제일 맛있어요. 다음에 또 봬요~",
            },
            [TeacherTextKey.TimeOut] = new[]
            {
                "이 집은 학구열이 부족하구만...",
            },
            [TeacherTextKey.Harmed] = new[]
            {
                "아 잠깐 뼈 맞았어",
            },
            [TeacherTextKey.Gas] = new[]
            {
                "어디서 무슨 썩은 냄새가... 이런 곳에선 수업 못해요",
            },
            [TeacherTextKey.Temperature] = new[]
            {
                "온도 킬존이라도 만드시나? ㅌㅌㅌ",
            },
            [TeacherTextKey.InMental] = new[]
            {
                "ㅁㄴ암나ㅢㅏㅁㄴ으!!@$@!!!!",
            },
        };

        private static readonly Dictionary<BullyTextKey, string[]> BullyPool = new()
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

        internal static string GetText(BullyTextKey key, params object[] args)
            => GetFromPool(BullyPool, key, args);

    // 🔹 Teacher 전용 함수 추가
        internal static string GetText(TeacherTextKey key, params object[] args)
            => GetFromPool(TeacherPool, key, args);
        
        private static string GetFromPool<TKey>(
            Dictionary<TKey, string[]> pool,
            TKey key,
            params object[] args)
            where TKey : struct, Enum
        {
            if (!pool.TryGetValue(key, out var arr) || arr.NullOrEmpty())
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