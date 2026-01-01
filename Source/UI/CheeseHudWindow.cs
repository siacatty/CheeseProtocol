using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;


namespace CheeseProtocol
{
    public class CheeseHudWindow : Window
    {
        private bool minimized;
        private bool sizeDirty = true;

        private const float padding = 10f;
        private const float topBarH = 28f;
        private const float lineH = 22f;

        private const float widthExpanded = 290f;
        private const float widthMinimized = 290f;

        private const float statusLinesBase = 3f;
        private const float separatorH = 6f;
        private float lastSavedX = -9999f;
        private float lastSavedY = -9999f;
        private int lastRowsCount = -1;
        private Vector2 lastRectPos;
        private bool lastPosInit;
        public override Vector2 InitialSize => new Vector2(widthExpanded, 220f);
        private List<CheeseCommandConfig> commandRows;

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
            if (!minimized)
            {
                commandRows = GetActiveCommands();
                int newCount = commandRows?.Count ?? 0;
                if (newCount != lastRowsCount)
                {
                    lastRowsCount = newCount;
                    sizeDirty = true;
                }
            }
            else
            {
                if (lastRowsCount != 0)
                {
                    lastRowsCount = 0;
                    sizeDirty = true;
                }
            }
            EnsureSizeForContent(snap);

            var top = inRect.TopPartPixels(24f);

            DrawTopBar(top, snap);

            if (!minimized)
            {
                var body = new Rect(inRect.x, inRect.y + 26f, inRect.width, inRect.height - 26f);
                DrawBody(body, snap);
            }
            //EnsureSizeForContent(snap);
            EnforceHudLock();
            SavePositionIfChanged();
        }
        private void EnforceHudLock()
        {
            var settings = CheeseProtocolMod.Settings;
            if (settings == null || !settings.hudLocked) 
            {
                // 잠금 해제 상태면 현재 위치를 기준으로 갱신
                lastRectPos = new Vector2(windowRect.x, windowRect.y);
                lastPosInit = true;
                return;
            }

            if (!lastPosInit)
            {
                lastRectPos = new Vector2(windowRect.x, windowRect.y);
                lastPosInit = true;
                return;
            }

            // 잠금 상태면 위치가 바뀌었을 경우 즉시 되돌림
            if (Mathf.Abs(windowRect.x - lastRectPos.x) > 0.01f ||
                Mathf.Abs(windowRect.y - lastRectPos.y) > 0.01f)
            {
                windowRect.x = lastRectPos.x;
                windowRect.y = lastRectPos.y;
            }
        }
        public override void PostOpen()
        {
            base.PostOpen();

            var settings = CheeseProtocolMod.Settings;

            if (settings != null)
                minimized = settings.hudMinimized;

            if (!minimized)
            {
                commandRows = GetActiveCommands();
                lastRowsCount = commandRows?.Count ?? 0;
            }
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
            lastRectPos = new Vector2(windowRect.x, windowRect.y);
            lastPosInit = true;
            ClampToScreen();
        }

        private List<CheeseCommandConfig> GetActiveCommands()
        {
            var settings = CheeseProtocolMod.Settings;
            if (settings == null) return null;

            settings.EnsureCommandConfigs();

            var result = new List<CheeseCommandConfig>();
            var configs = settings.commandConfigs;

            for (int i = 0; i < configs.Count; i++)
            {
                var c = configs[i];
                if (!c.enabled) continue;
                result.Add(c);
            }

            return result;
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
        private void DrawTopBar(Rect rect, CheeseUiStatusSnapshot s)
        {
            float margin = 2f;
            Rect inner = rect.ContractedBy(margin);  //

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

        private void DrawBody(Rect rect, CheeseUiStatusSnapshot s)
        {
            //float margin = 2f;
            //Rect inner = rect.ContractedBy(margin);  //

            //float btnW = 44f;
            //Rect btnRect = new Rect(inner.xMax - btnW, inner.y, btnW, inner.height);
            float y = rect.y;

            y += 6f;

            // Commands header
            /*
            var header = new Rect(rect.x, rect.y + y, rect.width, lineH);
            Widgets.Label(header, "명령어:");
            y += lineH;
            y += 6f;
            */
            // Commands rows
            if (commandRows == null || commandRows.Count == 0)
                return;

            for (int i = 0; i < commandRows.Count; i++)
            {
                var cfg = commandRows[i];

                DrawHudCommandRow(cfg, y);
                y += lineH;
                if (i < commandRows.Count-1)
                {
                    float lineY = y + separatorH * 0.5f;
                    Color prev = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.25f);
                    Widgets.DrawLineHorizontal(rect.x + 8f, lineY, rect.width - 16f);
                    GUI.color = prev;
                    y += separatorH;
                }
            }
        }
        private void DrawHudCommandRow(
            CheeseCommandConfig cfg,
            float y)
        {
            float rowH = 22f;
            float padding = 4f;

            // 전체 HUD 기준 좌표/폭 (이미 있는 값 사용)
            Rect row = new Rect(0f, y, windowRect.width, rowH);

            // 컬럼 폭
            float colCmd  = 60f;
            float colCd   = 70f;
            float colDesc = row.width - colCmd - colCd - padding * 2f;

            Rect rCmd  = new Rect(row.x, row.y, colCmd, rowH);
            Rect rCd   = new Rect(rCmd.xMax + padding, row.y, colCd, rowH);
            Rect rDesc = new Rect(rCd.xMax + padding, row.y, colDesc, rowH);

            // 비활성 명령어는 회색
            Color oldColor = GUI.color;
            if (!cfg.enabled)
                GUI.color = Color.gray;

            // ======================
            // 1) 명령어
            // ======================
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rCmd, cfg.label);

            // ======================
            // 2) 쿨타임
            // ======================
            DrawHudCooldownCell(rCd, cfg);

            // ======================
            // 3) 설명
            // ======================
            string desc = cfg.source == CheeseCommandSource.Chat
                ? "채팅"
                : $"₩{cfg.minDonation} ~";

            DrawCenteredText(rDesc, desc, GameFont.Small, true);
            //Widgets.Label(rDesc, desc);

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = oldColor;
        }

        private void DrawHudCooldownCell(
            Rect rect,
            CheeseCommandConfig cfg)
        {
            var cdState = CheeseCooldownState.Current;
            int nowTick = Find.TickManager.TicksGame;
            float cdTicks = cfg.cooldownHours * 2500f;
            // 안전장치
            if (cdState == null || cfg.cooldownHours <= 0 || cdState.GetLastTick(cfg.cmd) < 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, "준비됨");
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            int remain = cdState.RemainingTicks(cfg.cmd, cfg.cooldownHours, nowTick);

            if (remain <= 0)
            {
                // 준비됨
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, "준비됨");
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // 진행률 계산
            float pct = 1f - (remain / cdTicks);
            pct = Mathf.Clamp01(pct);

            // 배경
            Widgets.DrawBoxSolid(rect, new Color(0.12f, 0.13f, 0.14f));

            // 채워진 바
            Rect fill = new Rect(rect.x, rect.y, rect.width * pct, rect.height);
            Widgets.DrawBoxSolid(fill, new Color(0.22f, 0.52f, 0.26f));

            // 텍스트
            string remainingTime = FormatCooldownTicks(remain);
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = new Color(0.9f, 0.9f, 0.9f);
            Widgets.Label(rect, remainingTime);
            Text.Anchor = TextAnchor.UpperLeft;

            
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

        private void EnsureSizeForContent(CheeseUiStatusSnapshot s)
        {
            if (!sizeDirty) return;

            float w = minimized ? widthMinimized : widthExpanded;

            int rows = (!minimized && commandRows != null) ? commandRows.Count : 0;

            float h = topBarH
                    + padding * 2f
                    + 6f; // body top padding (y += 6f)

            if (!minimized && rows > 0)
            {
                h += rows * lineH;
                h += (rows - 1) * separatorH;
                h += padding;
            }
            else
            {
                h += padding;
            }

            windowRect.width = w;
            windowRect.height = h;

            sizeDirty = false;
            ClampToScreen();
        }

        private static string FormatCooldownTicks(int remainTicks)
        {
            if (remainTicks <= 0) return "준비됨"; //돌아갈일 없음. safeguard 용

            const int ticksPerSecond = 60;
            const int ticksPerHour = 2500;
            const int ticksPerDay = ticksPerHour * 24; // 60000

            if (remainTicks < ticksPerHour)
            {
                int sec = Mathf.CeilToInt(remainTicks / (float)ticksPerSecond);
                return $"{sec}s";
            }

            if (remainTicks >= ticksPerDay)
            {
                int days = remainTicks / ticksPerDay;
                int rem = remainTicks % ticksPerDay;
                int hours = Mathf.CeilToInt(rem / (float)ticksPerHour); // 남은 시간은 올림
                if (hours >= 24) { days += 1; hours = 0; }

                return hours > 0 ? $"{days}d {hours}h" : $"{days}d";
            }
            int h = Mathf.CeilToInt(remainTicks / (float)ticksPerHour);
            return $"{h}h";
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
    }
}