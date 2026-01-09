using RimWorld;
using UnityEngine;
using Verse;

namespace CheeseProtocol
{
    public class CheeseManualWindow : Window
    {
        private Vector2 scrollPos;

        public override Vector2 InitialSize => new Vector2(820f, 700f);

        public CheeseManualWindow()
        {
            doCloseButton = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont oldGUI = Text.Font;
            Text.Font = GameFont.Medium;
            Widgets.Label(
                new Rect(0f, 0f, inRect.width, 40f),
                "치즈 프로토콜 설명서"
            );

            Text.Font = GameFont.Small;
            Widgets.DrawLineHorizontal(
                0f,
                42f,
                inRect.width
            );

            Rect contentRect = new Rect(
                0f,
                50f,
                inRect.width - 16f,
                Text.CalcHeight(ManualText, inRect.width - 16f) + 10f
            );

            Rect viewRect = new Rect(
                0f,
                50f,
                inRect.width,
                inRect.height - 50f
            );

            Widgets.BeginScrollView(viewRect, ref scrollPos, contentRect);
            Widgets.Label(contentRect, ManualText);
            Widgets.EndScrollView();
            Text.Font = oldGUI;
        }

        private const string ManualText =
            @"
            ■ 시작하기
                1. 모드를 켜고 설정 창에서 치지직 방송 주소를 연결.
                2. 사용할 명령어와 이벤트 조건(채팅/후원, 금액)을 설정.

            기본 설정은 게임 진행에 큰 영향을 주지 않도록 조정되어있음으로 처음 사용시 그대로 사용 추천.

            ■ 이벤트 방식
                - 후원 이벤트는 설정한 최소·최대 금액 범위 내에서 효과가 결정.
                - 후원 금액이 높을수록 이벤트 효과 증폭.
                - 채팅 이벤트는 항상 랜덤하게 처리됨.

            ■ 랜덤성
                - 같은 조건에서도 결과가 반복되지 않도록 랜덤성이 적용된다.
                - 랜덤성은 0%~100% 범위로 설정할 수 있다.

            ■ 고급 설정
                - 고급 설정으로 이벤트 효과의 최소·최대 효과 설정 가능.
                - 값을 과도하게 높이면 난이도가 급격히 상승할 수 있다.
                - 일부 옵션은 방송 연출용이다.

            각 설정의 자세한 설명은
            설정 항목에 마우스를 올려 확인할 수 있다.";
    }
}