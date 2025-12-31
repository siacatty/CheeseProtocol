using System;
using UnityEngine;
using Verse;

namespace CheeseProtocol
{
    public class CheeseHudWindow : Window
    {
        private bool minimized;
        private bool sizeDirty = true;

        private const float padding = 10f;
        private const float topBarH = 28f;     // 버튼 크게 하려고 살짝 키움
        private const float lineH = 22f;

        private const float widthExpanded = 220f;
        private const float widthMinimized = 220f;

        private const float statusLinesBase = 3f; // Conn/Ch/Last
        private const float separatorH = 6f;
        private float lastSavedX = -9999f;
        private float lastSavedY = -9999f;
        private bool firstDrawLogged;
        public override Vector2 InitialSize => new Vector2(widthExpanded, 220f);

        public CheeseHudWindow()
        {
            draggable = true;
            doCloseX = false;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            closeOnCancel = false;
            preventCameraMotion = false;
            absorbInputAroundWindow = false; // HUD처럼 입력 덜 잡아먹게
            resizeable = false;
            layer = WindowLayer.GameUI;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var snap = CheeseGameComponent.Instance?.GetUiStatusSnapshot();

            if (snap == null)
            {
                return;
            }
            if (!firstDrawLogged)
            {
                firstDrawLogged = true;
            }
            EnsureSizeForContent(snap);

            var top = inRect.TopPartPixels(24f);
            var body = new Rect(inRect.x, inRect.y + 26f, inRect.width, inRect.height - 26f);

            DrawTopBar(top, snap);

            if (minimized)
            {
                DrawMinimized(body, snap);
                return;
            }

            DrawExpanded(body, snap);
            SavePositionIfChanged();
        }

        public override void PostOpen()
        {
            base.PostOpen();

            var settings = CheeseProtocolMod.Settings;

            if (settings != null)
                minimized = settings.hudMinimized;

            // 크기 계산 먼저(important): minimized 상태에 맞춰 width/height가 바뀌니까
            sizeDirty = true;
            EnsureSizeForContent(CheeseGameComponent.Instance?.GetUiStatusSnapshot());

            if (settings != null && settings.hudX >= 0f && settings.hudY >= 0f)
            {
                windowRect.x = settings.hudX;
                windowRect.y = settings.hudY;
            }
            else
            {
                windowRect.x = UI.screenWidth - windowRect.width - 20f;
                windowRect.y = 80f;
            }

            ClampToScreen();
        }
        private void SavePositionIfChanged()
        {
            var settings = CheeseProtocolMod.Settings;
            if (settings == null) return;

            if (Mathf.Abs(windowRect.x - lastSavedX) > 0.1f || Mathf.Abs(windowRect.y - lastSavedY) > 0.1f)
            {
                settings.hudX = windowRect.x;
                settings.hudY = windowRect.y;
                settings.hudMinimized = minimized;

                lastSavedX = windowRect.x;
                lastSavedY = windowRect.y;

                settings.Write(); // 즉시 저장
            }
        }
        private void ClampToScreen()
        {
            float screenW = UI.screenWidth;
            float screenH = UI.screenHeight;

            float maxX = Mathf.Max(0f, screenW - windowRect.width);
            float maxY = Mathf.Max(0f, screenH - windowRect.height);

            windowRect.x = Mathf.Clamp(windowRect.x, 0f, maxX);
            windowRect.y = Mathf.Clamp(windowRect.y, 0f, maxY);
        }
        private struct CommandRow
        {
            public string cmd;
            public string desc;
            public CommandRow(string cmd, string desc)
            {
                this.cmd = cmd;
                this.desc = desc;
            }
        }

        private static readonly CommandRow[] commandRows = new[]
        {
            new CommandRow("!참여", "Join / 참여"),
            new CommandRow("!습격", "Raid / 습격"),
            new CommandRow("!상단", "Top donors / 상단"),
            new CommandRow("!운석", "Meteor / 운석"),
            new CommandRow("!운석", "Meteor / 운석"),
            new CommandRow("!운석", "Meteor / 운석"),
            new CommandRow("!운석", "Meteor / 운석"),
            new CommandRow("!운석", "Meteor / 운석")
        };
        private void DrawTopBar(Rect rect, CheeseUiStatusSnapshot s)
        {
            float margin = 2f;
            Rect inner = rect.ContractedBy(margin);  // ✅ 동일한 여백 적용

            float btnW = 44f;
            Rect btnRect = new Rect(inner.xMax - btnW, inner.y, btnW, inner.height);
            Rect connLabel = new Rect(inner.x, inner.y, inner.width - btnW, inner.height);
            var prev = GUI.color;
            GUI.color = ConnColor(s.connectionState);
            //Widgets.Label(connLabel, $"연결상태: {ConnText(s.connectionState)}");
            DrawCenteredText(connLabel, $"연결상태: {ConnText(s.connectionState)}", GameFont.Small, false);
            GUI.color = prev;

            if (Mouse.IsOver(btnRect))
                Widgets.DrawHighlight(btnRect);

            if (Widgets.ButtonInvisible(btnRect))
            {
                minimized = !minimized;
                sizeDirty = true; // 토글되면 크기 다시 계산
            }

            DrawCenteredText(btnRect, minimized ? "＋" : "－", GameFont.Medium, true);
        }
        
        private void DrawMinimized(Rect rect, CheeseUiStatusSnapshot s)
        {
            
        }

        private void DrawExpanded(Rect rect, CheeseUiStatusSnapshot s)
        {
            //status
            float y = 0f;

            y += 6f;

            // Commands header
            /*
            var header = new Rect(rect.x, rect.y + y, rect.width, lineH);
            Widgets.Label(header, "명령어:");
            y += lineH;
            y += 6f;
            */
            // Commands rows
            for (int i = 0; i < commandRows.Length; i++)
            {

                var row = new Rect(rect.x, rect.y + y, rect.width, lineH);
                var left = row.LeftPartPixels(90f);
                var right = new Rect(row.x + 96f, row.y, row.width - 96f, row.height);

                Widgets.Label(left, commandRows[i].cmd);
                Widgets.Label(right, commandRows[i].desc);

                y += lineH;
                if (i < commandRows.Length-1)
                {
                    float lineY = rect.y + y + separatorH * 0.5f;
                    Color prev = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.25f);
                    Widgets.DrawLineHorizontal(rect.x + 8f, lineY, rect.width - 16f);
                    GUI.color = prev;
                    y += separatorH;
                }
            }
        }
        private void DrawCenteredText(Rect rect, string text, GameFont font, bool horCenter)
        {
            var prevFont = Verse.Text.Font;
            Verse.Text.Font = font;

            // 텍스트의 픽셀 크기 측정
            Vector2 size = Text.CalcSize(text);

            // rect 중앙에 오도록 위치 계산
            float x = rect.x;
            if (horCenter)
                x = rect.x + (rect.width - size.x) * 0.5f;
            float y = rect.y + (rect.height - size.y) * 0.5f;

            Widgets.Label(new Rect(x, y, size.x, size.y), text);

            Verse.Text.Font = prevFont;
        }
        private float DrawStatusLine(Rect rect, float y, string text, Color? color = null, bool colored = false)
        {
            var r = new Rect(rect.x, rect.y + y, rect.width, lineH);
            var prev = GUI.color;
            if (colored && color.HasValue) GUI.color = color.Value;
            Widgets.Label(r, text);
            GUI.color = prev;
            return lineH;
        }

        private void EnsureSizeForContent(CheeseUiStatusSnapshot s)
        {
            if (!sizeDirty) return;

            float w = minimized ? widthMinimized : widthExpanded;

            // 높이 계산: top bar + padding*2 + 내용 줄 수
            float contentLines = 0f;

            if (minimized)
            {
                contentLines = 0f;
            }
            else
            {
                contentLines += 0f;   // "명령어:"
                contentLines += commandRows.Length;
            }

            float h = topBarH + 2f + padding * 3f + (contentLines * (lineH+separatorH));

            // 현재 위치 유지하면서 크기만 변경
            windowRect.width = w;
            windowRect.height = h;
            sizeDirty = false;
            ClampToScreen();
        }

        private Color ConnColor(ConnectionState state)
        {
            switch (state)
            {
                case ConnectionState.Connected: return Color.green;
                case ConnectionState.Connecting: return Color.yellow;
                default: return Color.red;
            }
        }

        private string ConnText(ConnectionState state)
        {
            switch (state)
            {
                case ConnectionState.Connected: return "연결됨";
                case ConnectionState.Connecting: return "연결 시도 중...";
                default: return "연결 끊김";
            }
        }

        private string Shorten(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "-";
            if (s.Length <= max) return s;
            return s.Substring(0, max - 1) + "…";
        }
    }
}