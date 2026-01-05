using System;
using System.Collections.Generic;
using System.Web.Profile;
using UnityEngine;
using Verse;


namespace CheeseProtocol
{
    public class CheeseHudWindow : Window
    {
        private const float DefaultWindowMargin = 12f;
        protected override float Margin => 0f;
        private bool minimized;
        private bool slideHidden;
        private bool sizeDirty = true;

        private const float paddingX = 4f;
        private const float paddingY = 10f;
        private const float topBarH = 38f;
        private const float slideBarH = 40f;
        private const float lineH = 22f;

        private const float widthExpanded = 290f;
        private const float widthMinimized = 290f;
        private const float colCmd  = 40f;
        private const float colCd   = 60f;

        private const float statusLinesBase = 3f;
        private const float separatorH = 6f;
        private const float rowH = 22f;
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
            doWindowBackground = false;
            drawShadow = false;
        }
        
        public override void DoWindowContents(Rect inRect)
        {
            float opacity = CheeseProtocolMod.Settings.hudOpacity;

            // Draw background
            Widgets.DrawBoxSolidWithOutline(
                inRect,
                new Color(0.082f, 0.098f, 0.114f, opacity),
                new Color(1f, 1f, 1f, 0.25f * opacity)
            );
            DrawHUDWindowContents(inRect.ContractedBy(DefaultWindowMargin));
        }

        private void DrawHUDWindowContents(Rect inRect)
        {
            float y = inRect.y;
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

            Rect top = inRect.TopPartPixels(topBarH);
            DrawTopBar(top, snap);
            y += topBarH+2f;

            if (!slideHidden)
            {
                Rect slideRect = new Rect(inRect.x, y, inRect.width, slideBarH + separatorH);
                DrawSlideBar(slideRect);
                y += slideBarH;
                y += separatorH;
            }

            if (!minimized)
            {
                Rect body = new Rect(inRect.x, y, inRect.width, inRect.height - y);
                DrawBody(body, snap);
            }
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
            {
                minimized = settings.hudMinimized;
                slideHidden = settings.hudSlideHidden;
            }
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
                settings.hudSlideHidden = slideHidden;

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

        private void DrawSlideBar(Rect rect)
        {
            float lineYTop = rect.y + separatorH * 0.5f;
            float lineYBot = rect.yMax - separatorH * 0.5f;
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            //Widgets.DrawLineHorizontal(rect.x + 8f, lineYTop, rect.width + 4f);
            Widgets.DrawLineHorizontal(rect.x - 4f, lineYBot, rect.width + 4f);
            //Widgets.DrawLineHorizontal(rect.x + 8f, lineYTop+1, rect.width + 4f);
            Widgets.DrawLineHorizontal(rect.x - 4f, lineYBot-1, rect.width + 4f);
            GUI.color = prev;


            float margin = 2f;
            Rect inner = rect.ContractedBy(margin);
            prev = GUI.color;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            string description = "후원 금액↑ = 증폭 확률↑\n초과 금액은 최대 확률";
            //Widgets.Label(inner, description);
            UIUtil.DrawCenteredText(inner, description, TextAlignment.Left);
            GUI.color = prev;
        }
        private void DrawTopBar(Rect rect, CheeseUiStatusSnapshot s)
        {
            Rect mainTopRect = new Rect(rect.x, rect.y, rect.width, 24f); 
            Rect slideBtnRect = new Rect(rect.x, mainTopRect.yMax, rect.width, rect.height - 24f);
            float margin = 2f;
            Rect inner = mainTopRect.ContractedBy(margin);  //
            float y = inner.y;
            float btnW = 44f;

            Rect btnRect = new Rect(inner.xMax - btnW, y, btnW, inner.height);
            Rect connLabel = new Rect(inner.x, inner.y, inner.width - btnW, inner.height);
            var prev = GUI.color;
            GUI.color = ConnColor(s.connectionState);
            //Widgets.Label(connLabel, $"연결상태: {ConnText(s.connectionState)}");
            UIUtil.DrawCenteredText(connLabel, $"연결상태: {ConnText(s.connectionState)}", TextAlignment.Left);
            GUI.color = prev;

            if (Mouse.IsOver(btnRect))
                Widgets.DrawHighlight(btnRect);

            if (Widgets.ButtonInvisible(btnRect))
            {
                minimized = !minimized;
                CheeseProtocolMod.Settings.hudMinimized = minimized;
                sizeDirty = true; // 토글되면 크기 다시 계산
            }

            if (Mouse.IsOver(slideBtnRect))
                Widgets.DrawHighlight(slideBtnRect);

            if (Widgets.ButtonInvisible(slideBtnRect))
            {
                slideHidden = !slideHidden;
                CheeseProtocolMod.Settings.hudSlideHidden = slideHidden;
                sizeDirty = true; // 토글되면 크기 다시 계산
            }

            UIUtil.DrawCenteredText(btnRect, minimized ? "＋" : "－", font: GameFont.Medium);
            y += inner.height;
            y += paddingY;
            UIUtil.DrawCenteredText(slideBtnRect, slideHidden ? "▼" : "▲");
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
            Rect row = new Rect(rect.x, y, rect.width, rowH);
            float colDesc = row.width - colCmd - colCd - paddingX * 2f;
            // 컬럼 폭
            Rect rCmd  = new Rect(row.x, row.y, colCmd, rowH);
            Rect rCd   = new Rect(rCmd.xMax + paddingX, row.y, colCd, rowH);
            Rect rDesc = new Rect(rCd.xMax + paddingX, row.y, colDesc, rowH);
            Color prev = GUI.color;
            GUI.color = new Color(0.75f, 0.78f, 0.82f);
            UIUtil.DrawCenteredText(rCmd, "명령어");
            UIUtil.DrawCenteredText(rCd, "쿨타임");
            UIUtil.DrawCenteredText(rDesc, "효과 기준 금액");
            GUI.color = prev;
            y += lineH;
            float lineY = y + separatorH * 0.5f;
            prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.3f);
            Widgets.DrawLineHorizontal(rect.x + 8f, lineY, rect.width - 16f);
            Widgets.DrawLineHorizontal(rect.x + 8f, lineY+1, rect.width - 16f);
            GUI.color = prev;
            y += separatorH;

            // Commands rows
            if (commandRows == null || commandRows.Count == 0)
                return;

            for (int i = 0; i < commandRows.Count; i++)
            {
                var cfg = commandRows[i];

                DrawHudCommandRow(rect, cfg, y);
                y += lineH;
                if (i < commandRows.Count-1)
                {
                    lineY = y + separatorH * 0.5f;
                    prev = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.25f);
                    Widgets.DrawLineHorizontal(rect.x + 8f, lineY, rect.width - 16f);
                    GUI.color = prev;
                    y += separatorH;
                }
            }
        }
        private void DrawHudCommandRow(
            Rect rect,
            CheeseCommandConfig cfg,
            float y)
        {

            // 전체 HUD 기준 좌표/폭 (이미 있는 값 사용)
            Rect row = new Rect(rect.x, y, rect.width, rowH);

            // 컬럼 폭
            float colDesc = row.width - colCmd - colCd - paddingX * 2f;
            Rect rCmd  = new Rect(row.x, row.y, colCmd, rowH);
            Rect rCd   = new Rect(rCmd.xMax + paddingX, row.y, colCd, rowH);
            Rect rDesc = new Rect(rCd.xMax + paddingX, row.y, colDesc, rowH);
            // 비활성 명령어는 회색
            Color oldColor = GUI.color;
            if (!cfg.enabled)
                GUI.color = Color.gray;

            // ======================
            // 1) 명령어
            // ======================
            UIUtil.DrawCenteredText(rCmd, cfg.label);

            // ======================
            // 2) 쿨타임
            // ======================
            DrawHudCooldownCell(rCd, cfg);

            // ======================
            // 3) 설명
            // ======================
            string desc = cfg.source == CheeseCommandSource.Chat
                ? "채팅"
                : $"₩{cfg.minDonation} ~ ₩{cfg.maxDonation}";

            UIUtil.DrawCenteredText(rDesc, desc);
            //Widgets.Label(rDesc, desc);
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
            Color prev = GUI.color;
            GUI.color = Color.green;
            if (cdState == null || cfg.cooldownHours <= 0 || cdState.GetLastTick(cfg.cmd) < 0)
            {
                UIUtil.DrawCenteredText(rect, "준비됨");
                GUI.color = prev;
                return;
            }

            int remain = cdState.RemainingTicks(cfg.cmd, cfg.cooldownHours, nowTick);

            if (remain <= 0)
            {
                // 준비됨
                UIUtil.DrawCenteredText(rect, "준비됨");
                GUI.color = prev;
                return;
            }
            GUI.color = prev;
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
        private void EnsureSizeForContent(CheeseUiStatusSnapshot s)
        {
            if (!sizeDirty) return;

            float w = minimized ? widthMinimized : widthExpanded;

            int rows = (!minimized && commandRows != null) ? commandRows.Count : 0;
            rows += 1; // + header

            float h = topBarH
                    + paddingY * 2f
                    + 6f; // body top padding (y += 6f)

            if (!minimized && rows > 0)
            {
                h += rows * lineH;
                h += (rows - 1) * separatorH;
                h += paddingY;
            }
            else
            {
                h += paddingY;
            }
            if (!slideHidden)
            {
                h += slideBarH;
                h += separatorH;
            }
            h += paddingY; //extra space

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