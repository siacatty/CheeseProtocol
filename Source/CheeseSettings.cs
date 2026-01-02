using Verse;
using UnityEngine;
using System.Collections.Generic;
using RimWorld;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Xml.Schema;
using System;

namespace CheeseProtocol
{
    public class CheeseSettings : ModSettings
    {
        private Vector2 scrollPos = Vector2.zero;
        private float viewHeight = 800f; // 대충 크게 잡아두면 OK
        public bool useDropPod = true;
        public string chzzkStudioUrl = "";
        public string chzzkStatus = "Disconnected";
        public bool showHud = true;
        public bool hudLocked = false;
        public float hudX = -1f;   // -1이면 아직 저장된 위치 없음(기본 위치 사용)
        public float hudY = -1f;
        public CheeseCommandSource simSource = CheeseCommandSource.Donation;
        public int simDonAmount = 1000;
        public string simDonAmountBuf = "1000";
        public bool hudMinimized = false;
        private enum CheeseSettingsTab { General, Command, Advanced, Simulation, Credits }
        private CheeseSettingsTab activeTab = CheeseSettingsTab.General;

        private readonly Dictionary<string, float> sectionContentHeights = new Dictionary<string, float>();
        private const float SectionPad = 10f;
        private const float SectionHeaderH = 26f;
        private const float SectionGap = 10f;
        private const float separatorH = 12f;
        public List<CheeseCommandConfig> commandConfigs;
        public CheeseCommandConfig selectedConfig;
        private const int maxAllowedDonation = 1000000;
        private const int minAllowedDonation = 1000;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref useDropPod, "useDropPod", true);
            Scribe_Values.Look(ref chzzkStudioUrl, "chzzkStudioUrl", "");
            Scribe_Values.Look(ref chzzkStatus, "chzzkStatus", "Disconnected");
            Scribe_Values.Look(ref showHud, "showHud", true);
            Scribe_Values.Look(ref hudLocked, "hudLocked", false);
            Scribe_Values.Look(ref hudX, "hudX", -1f);
            Scribe_Values.Look(ref hudY, "hudY", -1f);
            Scribe_Values.Look(ref simSource, "simSource", CheeseCommandSource.Donation);
            Scribe_Values.Look(ref simDonAmount, "simDonAmount", 1000);
            Scribe_Values.Look(ref simDonAmountBuf, "simDonAmountBuf", "1000");
            EnsureCommandConfigs();

            for (int i = 0; i < commandConfigs.Count; i++)
            {
                var c = commandConfigs[i];
                string p = "cmd_" + c.ScribeKeyPrefix + "_"; // e.g. cmd_Join_

                Scribe_Values.Look(ref c.enabled,        p + "enabled", true);
                Scribe_Values.Look(ref c.source,         p + "source", CheeseCommandSource.Donation);
                Scribe_Values.Look(ref c.minDonation, p + "minDonation", 1000);
                Scribe_Values.Look(ref c.cooldownHours, p + "cooldownHours", 0);
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit || Scribe.mode == LoadSaveMode.Saving)
            {
                FixupCommandDefaults();
                for (int i = 0; i < commandConfigs.Count; i++)
                {
                    var c = commandConfigs[i];

                    if (c.source == CheeseCommandSource.Donation)
                        c.minDonation = Mathf.Max(1000, c.minDonation);
                    c.cooldownHours = Mathf.Max(0, c.cooldownHours);
                    c.minDonationBuf = c.minDonation.ToString();
                    c.cooldownBuf = c.cooldownHours.ToString();
                }
            }
        }

        public void DoWindowContents(Rect inRect)
        {
            if (commandConfigs != null){
                foreach (var c in commandConfigs)
                {
                    c.EnsureBuffers();
                }
            }

            EnsureCommandConfigs();
            if (selectedConfig == null)
                selectedConfig = commandConfigs == null ? null : commandConfigs[0];

            //const float pad = 10f;
            const float tabH = 32f;
            const float gap = 10f;

            // 1) 탭 바 영역
            Rect tabRect = new Rect(inRect.x, inRect.y, inRect.width, tabH);
            Rect bodyRect = new Rect(inRect.x, inRect.y + tabH + gap, inRect.width, inRect.height - tabH - gap);

            DrawTabs(tabRect); // <- 아래에 구현 (activeTab 변경)

            // 2) 본문 (스크롤 유지)
            float contentHeight = Mathf.Max(viewHeight, bodyRect.height, 900f);
            Rect viewRect = new Rect(0f, 0f, bodyRect.width - 16f, contentHeight);

            Widgets.BeginScrollView(bodyRect, ref scrollPos, viewRect);

            // 3) 2컬럼
            float colGap = 12f;
            float colW = (viewRect.width - colGap) / 2f;

            Rect left = new Rect(0f, 0f, colW, viewRect.height);
            Rect right = new Rect(colW + colGap, 0f, colW, viewRect.height);

            float yL = 0f;
            float yR = 0f;

            // ---- 탭별로 내용 바꾸기 ----
            switch (activeTab)
            {
                case CheeseSettingsTab.General:
                    DrawSection(left, ref yL, "치지직 연결", listing =>
                    {
                        listing.Label("치지직 스튜디오 URL:");
                        listing.Label("예시 1 ) studio.chzzk.naver.com/xxxxxx");
                        listing.Label("예시 2 ) https://chzzk.naver.com/live/xxxxxx");
                        chzzkStudioUrl = listing.TextEntry(chzzkStudioUrl);
                        listing.Gap(8);
                        Rect row = listing.GetRect(24f);
                        Widgets.Label(
                            row.LeftPart(0.20f),
                            "연결 상태:"
                        );

                        Color prev = GUI.color;
                        GUI.color = GetStatusColor(chzzkStatus);

                        Widgets.Label(
                            row.RightPart(0.80f),
                            chzzkStatus
                        );

                        GUI.color = prev;
                        //listing.Label($"연결 상태: {chzzkStatus}");

                        Rect connectionBtnRow = listing.GetRect(40f);
                        Rect connectBtn  = connectionBtnRow.LeftHalf().ContractedBy(5f);
                        Rect disconnectBtn = connectionBtnRow.RightHalf().ContractedBy(5f);

                        if (Widgets.ButtonText(connectBtn, "연결"))
                            CheeseProtocolMod.ChzzkChat.UserConnect();

                        if (Widgets.ButtonText(disconnectBtn, "연결 해제"))
                            CheeseProtocolMod.ChzzkChat.UserDisconnect();
                    });
                    DrawSection(right, ref yR, "HUD", listing =>
                    {
                        listing.CheckboxLabeled("HUD 표시", ref showHud);
                        listing.CheckboxLabeled("HUD 현재 위치 고정", ref hudLocked);
                    });
                    break;

                case CheeseSettingsTab.Command:
                    float lineH = 26f;
                    float paddingX = 6f;
                    float paddingY = 12f;
                    float rowH = lineH * 2f + paddingY;
                    float gridH = (rowH +separatorH)* (commandConfigs.Count+1); //header 포함
                    Rect commandsRect = new Rect(
                        0f,
                        0f,
                        viewRect.width,
                        gridH
                    );

                    DrawCommandsGridTwoLine(commandsRect, lineH, paddingX, paddingY);

                    yL = gridH + 10f;
                    break;

                case CheeseSettingsTab.Advanced:
                    DrawSection(left, ref yL, "고급", listing =>
                    {
                        listing.Label("Later: accent colors, HUD opacity, etc.");
                    });
                    break;
                case CheeseSettingsTab.Simulation:
                    DrawSection(left, ref yL, "시뮬레이션", listing =>
                    {
                        float lineH = 26f;
                        float paddingX = 6f;
                        float paddingY = 12f;
                        float btnSize = 24f;
                        listing.Label("시뮬레이션 (개발자 모드 전용)\n환경설정 → 개발자 모드 활성화");
                        listing.GapLine();
                        bool oldGUI = GUI.enabled;
                        GUI.enabled = Prefs.DevMode;
                        Rect simCommandRect = listing.GetRect(lineH*2f);
                        //Widgets.DrawBox(simCommandRect);
                        SplitVerticallyByRatio(
                            simCommandRect,
                            out Rect simCommandDesc,
                            out Rect simCommandDropdown,
                            0.4f,
                            paddingX
                        );
                        simCommandDropdown = ShrinkRect(simCommandDropdown, 0, simCommandDropdown.width*0.5f, simCommandDropdown.height*0.20f, simCommandDropdown.height*0.20f);
                        var oldAnchor = Text.Anchor;
                        Text.Anchor = TextAnchor.MiddleCenter;
                        Widgets.Label(simCommandDesc, "명령어 :");
                        Text.Anchor = oldAnchor;
                        Widgets.Dropdown(
                            simCommandDropdown,
                            selectedConfig,
                            c => c,
                            _ => GenerateCommandMenu(),
                            selectedConfig != null ? selectedConfig.label : "(No commands)"
                        );

                        listing.Gap(8);
                        Rect simSrcRect=listing.GetRect(btnSize*2f+paddingY);
                        //Widgets.DrawBox(simSrcRect);
                        SplitVerticallyByRatio(
                            simSrcRect,
                            out Rect simSrcRad,
                            out Rect simDonAmountRect,
                            0.5f,
                            paddingX
                        );
                        SplitHorizontallyByRatio(
                            simSrcRad,
                            out Rect simSrcChat,
                            out Rect simSrcDon,
                            0.5f,
                            paddingY
                        );
                        SplitVerticallyByRatio(
                            simSrcChat,
                            out Rect simSrcChatRadBtn,
                            out Rect simSrcChatDesc,
                            0.5f,
                            paddingX
                        );
                        SplitVerticallyByRatio(
                            simSrcDon,
                            out Rect simSrcDonRadBtn,
                            out Rect simSrcDonDesc,
                            0.5f,
                            paddingX
                        );
                        SplitVerticallyByRatio(
                            simDonAmountRect,
                            out Rect simDonAmountDesc,
                            out Rect simDonAmountField,
                            0.5f,
                            paddingX
                        );
                        oldAnchor = Text.Anchor;
                        Text.Anchor = TextAnchor.MiddleLeft;
                        simSrcChatRadBtn = ResizeRectCentered(simSrcChatRadBtn, btnSize, btnSize);
                        simSrcDonRadBtn = ResizeRectCentered(simSrcDonRadBtn, btnSize, btnSize);
                        if (Widgets.RadioButton(simSrcChatRadBtn.position, simSource == CheeseCommandSource.Chat))
                            simSource = CheeseCommandSource.Chat;
                        Widgets.Label(simSrcChatDesc, "채팅");
                        if (Widgets.RadioButton(simSrcDonRadBtn.position, simSource == CheeseCommandSource.Donation))
                            simSource = CheeseCommandSource.Donation;
                        Widgets.Label(simSrcDonDesc, "후원");

                        bool oldGUI2 = GUI.enabled;
                        GUI.enabled = simSource == CheeseCommandSource.Donation && Prefs.DevMode;

                        Text.Anchor = TextAnchor.MiddleCenter;
                        Widgets.Label(simDonAmountDesc, "후원 금액 :");
                        Text.Anchor = oldAnchor;
                        simDonAmountField = ShrinkRect(simDonAmountField, 0, 0, simDonAmountField.height*0.25f, simDonAmountField.height*0.25f);
                        UiNumericField.IntFieldDigitsOnly(simDonAmountField, ref simDonAmount, ref simDonAmountBuf, 0, maxAllowedDonation);
                        GUI.enabled = oldGUI2;

                        listing.Gap(lineH);
                        Rect simBtn  = listing.GetRect(btnSize);
                        
                        if (Widgets.ButtonText(simBtn, "시뮬레이션 실행"))
                        {
                            Log.Message("[CheeseProtocol] Run Simulation");
                            //CheeseProtocolMod.ChzzkChat.UserConnect();
                        }
                        
                        
                        GUI.enabled = oldGUI;
                    });
                    break;

                case CheeseSettingsTab.Credits:
                    DrawSection(left, ref yL, "Credits", listing =>
                    {
                        listing.Label("Cheese Protocol");
                        listing.Label("by SiaCatty");
                    });
                    break;
            }
            viewHeight = Mathf.Max(yL, yR) + 20f;

            Widgets.EndScrollView();
        }
        private IEnumerable<Widgets.DropdownMenuElement<CheeseCommandConfig>> GenerateCommandMenu()
        {
            foreach (var cfg in commandConfigs)
            {
                var captured = cfg;
                yield return new Widgets.DropdownMenuElement<CheeseCommandConfig>
                {
                    option = new FloatMenuOption(
                        captured.label,
                        () => selectedConfig = captured
                    ),
                    payload = captured
                };
            }
        }
        private int DefaultMinDonationFor(CheeseCommand cmd)
        {
            switch (cmd)
            {
                case CheeseCommand.Join:   return 1000;
                case CheeseCommand.Raid:   return 1000;
                case CheeseCommand.Caravan:return 1000;
                case CheeseCommand.Meteor: return 1000;
                default: return 1000;
            }
        }

        private void FixupCommandDefaults()
        {
            EnsureCommandConfigs();

            for (int i = 0; i < commandConfigs.Count; i++)
            {
                var c = commandConfigs[i];
                if (c.minDonation <= 0)
                    c.minDonation = DefaultMinDonationFor(c.cmd);
                c.minDonationBuf = c.minDonation.ToString();
                c.cooldownBuf = c.cooldownHours.ToString();
            }
        }
        public void EnsureCommandConfigs()
        {
            if (commandConfigs != null && commandConfigs.Count > 0) return;

            commandConfigs = new List<CheeseCommandConfig>
            {
                new CheeseCommandConfig { cmd = CheeseCommand.Join,   label = "!참여" },
                new CheeseCommandConfig { cmd = CheeseCommand.Raid,   label = "!습격" },
                new CheeseCommandConfig { cmd = CheeseCommand.Caravan,    label = "!상단" },
                new CheeseCommandConfig { cmd = CheeseCommand.Meteor, label = "!운석" },
            };
        }
        private void DrawSection(
            Rect column,
            ref float curY,
            string title,
            Action<Listing_Standard> drawListing)
        {
            string key = title;

            // Use cached height (or fallback) for this frame
            float contentH = sectionContentHeights.TryGetValue(key, out var h) ? h : 200f;

            float sectionH = SectionPad + SectionHeaderH + 10f + contentH + SectionPad;

            //Log.Message($"[CheeseProtocol] Title: {title}, SectionH = {sectionH}");

            Rect outer = new Rect(column.x, curY, column.width, sectionH);
            Widgets.DrawMenuSection(outer);

            Rect inner = outer.ContractedBy(SectionPad);

            Rect head = new Rect(inner.x, inner.y, inner.width, SectionHeaderH);
            var oldFont = Text.Font;
            Text.Font = GameFont.Medium;
            Widgets.Label(head, title);
            Text.Font = oldFont;

            Widgets.DrawLineHorizontal(inner.x, head.yMax + 4f, inner.width);

            Rect contentRect = new Rect(
                inner.x,
                head.yMax + 10f,
                inner.width,
                inner.yMax - (head.yMax + 10f)
            );

            var listing = new Listing_Standard();
            listing.maxOneColumn = true; 
            listing.Begin(contentRect);
            drawListing(listing);
            if (Event.current.type == EventType.Layout)
                sectionContentHeights[key] = listing.CurHeight;
            listing.End();

            curY += sectionH + SectionGap;
        }
        private void DrawTabs(Rect tabRect)
        {
            float w = 140f;
            float h = tabRect.height;

            if (Widgets.ButtonText(new Rect(tabRect.x + 0f, tabRect.y, w, h), "일반")) activeTab = CheeseSettingsTab.General;
            if (Widgets.ButtonText(new Rect(tabRect.x + w + 6f, tabRect.y, w, h), "명령어")) activeTab = CheeseSettingsTab.Command;
            if (Widgets.ButtonText(new Rect(tabRect.x + (w + 6f) * 2, tabRect.y, w, h), "고급")) activeTab = CheeseSettingsTab.Advanced;
            if (Widgets.ButtonText(new Rect(tabRect.x + (w + 6f) * 3, tabRect.y, w, h), "시뮬레이션")) activeTab = CheeseSettingsTab.Simulation;
            if (Widgets.ButtonText(new Rect(tabRect.x + (w + 6f) * 4, tabRect.y, w, h), "Credits")) activeTab = CheeseSettingsTab.Credits;
        }

        private void DrawCommandsGridTwoLine(Rect rect, float lineH, float paddingX, float paddingY)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(8f);

            float rowH = lineH * 2f + paddingY;
            float gap = 24f;
            float leftW = (inner.width - gap)/2f;

            Rect leftCol = new Rect(inner.x, inner.y, leftW, inner.height);
            Rect rightCol = new Rect(leftCol.xMax + gap, inner.y, inner.width - leftW - gap, inner.height);

            float y = inner.y;
            float lineY;
            List<float> separatorPosX = new List<float>();
            var ratios = new float[] { 0.2f, 0.1f, 0.16f, 0.27f, 0.27f };
            Rect rowRect = new Rect(inner.x, y, inner.width, rowH);

            DrawCommandRowHeader(rowRect, ref y, rowH, paddingX, ratios, separatorPosX);
            
            lineY = rect.y + y + separatorH * 0.5f;
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.3f);
            Widgets.DrawLineHorizontal(rect.x + 8f, lineY, rect.width - 16f);
            GUI.color = prev;
            y += separatorH;
            EnsureCommandConfigs();
            for (int i = 0; i < commandConfigs.Count; i++)
            {
                var c = commandConfigs[i];
                rowRect = new Rect(inner.x, y, inner.width, rowH);

                DrawCommandRow(
                    rowRect,
                    ref y,
                    rowH,
                    paddingX,
                    paddingY,
                    ratios,
                    c.label,
                    ref c.enabled,
                    ref c.source,
                    ref c.minDonation,
                    ref c.minDonationBuf,
                    ref c.cooldownHours,
                    ref c.cooldownBuf
                );
                if (i < commandConfigs.Count - 1)
                {
                    lineY = rect.y + y + separatorH * 0.5f;
                    prev = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.3f);
                    Widgets.DrawLineHorizontal(rect.x + 8f, lineY, rect.width - 16f);
                    GUI.color = prev;
                    y += separatorH;
                }
            }
            prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.3f);
            for (int i = 0; i < separatorPosX.Count; i++)
            {
                float xPos = separatorPosX[i];
                Widgets.DrawLineVertical(
                    xPos + paddingX*0.5f,
                    inner.y,
                    inner.height
                );
            }
            GUI.color = prev;
        }
        private void DrawCommandRowHeader(Rect rect, ref float curY, float rowH, float paddingX, float[] ratios, List<float> separatorPosX)
        {
            var cols = new List<Rect>(5);
            SplitVerticallyByRatios(rect, ratios, paddingX, cols);
            Rect cmdRect = cols[0];
            Rect enableRect  = cols[1];
            Rect srcRadioRect = cols[2];
            Rect minDonRect = cols[3];
            Rect cdRect = cols[4];
            for (int i = 0; i < cols.Count-1; i++)
            {
                separatorPosX.Add(cols[i].xMax);
            }

            //command
            var oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(cmdRect, "명령어");
            Widgets.Label(enableRect, "활성화");
            Widgets.Label(srcRadioRect, "트리거 방식");
            Widgets.Label(minDonRect, "최소 후원 금액 (₩)");
            Widgets.Label(cdRect, "쿨타임 (인게임 시간)");
            Text.Anchor = oldAnchor;

            curY += rowH;
        }
        private void DrawCommandRow(
            Rect rect,
            ref float curY,
            float rowH,
            float paddingX,
            float paddingY,
            float[] ratios,
            string label,
            ref bool enabled,
            ref CheeseCommandSource source,
            ref int minDonation,
            ref string minDonationBuf,
            ref int cooldownHours,
            ref string cooldownBuf)
        {
            Widgets.DrawHighlightIfMouseover(rect);
            var cols = new List<Rect>(5);
            SplitVerticallyByRatios(rect, ratios, paddingX, cols);

            Rect cmdRect = cols[0];
            Rect enableRect  = cols[1];
            Rect srcRadioRect = cols[2];
            Rect minDonRect = cols[3];
            Rect cdRect = cols[4];
            float btnSize = 24f;

            //command (e.g. !참여)
            var oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(cmdRect, label);
            Text.Anchor = oldAnchor;

            //Enable checkbox
            Vector2 enableBtnPos = new Vector2(
                enableRect.x + (enableRect.width  - btnSize) * 0.5f,
                enableRect.y + (enableRect.height - btnSize) * 0.5f
            );
            Widgets.Checkbox(enableBtnPos, ref enabled);

            bool oldGui = GUI.enabled;
            GUI.enabled = enabled;

            //Source radiobuttons
            SplitHorizontallyByRatio(
                srcRadioRect,
                out Rect srcChatRad,
                out Rect srcDonRad,
                0.5f,
                paddingY
            );
            SplitVerticallyByRatio(
                srcChatRad,
                out Rect chatRadBtn,
                out Rect chatRadDesc,
                0.3f,
                paddingX
            );
            SplitVerticallyByRatio(
                srcDonRad,
                out Rect donRadBtn,
                out Rect donRadDesc,
                0.3f,
                paddingX
            );
            Vector2 chatRadBtnPos = new Vector2(
                chatRadBtn.x + (chatRadBtn.width  - btnSize) * 0.5f,
                chatRadBtn.y + (chatRadBtn.height - btnSize) * 0.5f
            );
            if (Widgets.RadioButton(chatRadBtnPos, source == CheeseCommandSource.Chat))
                source = CheeseCommandSource.Chat;
            oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(chatRadDesc, "채팅");
            Text.Anchor = oldAnchor;

            Vector2 donRadBtnPos = new Vector2(
                donRadBtn.x + (donRadBtn.width  - btnSize) * 0.5f,
                donRadBtn.y + (donRadBtn.height - btnSize) * 0.5f
            );
            if (Widgets.RadioButton(donRadBtnPos, source == CheeseCommandSource.Donation))
                source = CheeseCommandSource.Donation;
            oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(donRadDesc, "후원");
            Text.Anchor = oldAnchor;

            //Minimum donation amount text field
            SplitHorizontallyByRatio(
                minDonRect,
                out Rect minDonSlider,
                out Rect minDonText,
                0.5f,
                paddingY
            );
            SplitVerticallyByRatio(
                minDonText,
                out Rect minDonField,
                out Rect minDonWarning,
                0.4f,
                paddingX
            );
            bool oldGui2 = GUI.enabled;
            GUI.enabled = enabled && source == CheeseCommandSource.Donation;
            UiNumericField.IntFieldDigitsOnly(minDonField, ref minDonation, ref minDonationBuf, 0, maxAllowedDonation);

            float sliderDonValue = minDonation;
            int stepDon = 1000;
            float newSliderDonValue = Widgets.HorizontalSlider(
                minDonSlider,
                sliderDonValue,
                minAllowedDonation,
                maxAllowedDonation,
                middleAlignment: true,
                label: null,
                leftAlignedLabel: null,
                rightAlignedLabel: null,
                roundTo: 1f
            );
            if (Mathf.Abs(newSliderDonValue - sliderDonValue) > 0.01f)
            {
                int snapped = SnapToStep(Mathf.RoundToInt(newSliderDonValue), stepDon);
                snapped = Mathf.Clamp(snapped, minAllowedDonation, maxAllowedDonation);

                minDonation = snapped;
                minDonationBuf = snapped.ToString();
            }

            if (minDonation < 1000){
                Color prev = GUI.color;
                GUI.color = Color.yellow;
                oldAnchor = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(minDonWarning, "최소 ₩1000");
                Text.Anchor = oldAnchor;
                GUI.color = prev;
            }
            GUI.enabled = oldGui2;

            //Cooldown text field
            SplitHorizontallyByRatio(
                cdRect,
                out Rect cdSlider,
                out Rect cdText,
                0.5f,
                paddingY
            );
            SplitVerticallyByRatio(
                cdText,
                out Rect cdField,
                out Rect cdDesc,
                0.4f,
                paddingX
            );

            int maxAllowedCD = 1440;
            int minAllowedCD = 0;
            UiNumericField.IntFieldDigitsOnly(cdField, ref cooldownHours, ref cooldownBuf, minAllowedCD, maxAllowedCD);

            float sliderCDValue = cooldownHours;
            int stepCD = 1;
            float newSliderCDValue = Widgets.HorizontalSlider(
                cdSlider,
                sliderCDValue,
                minAllowedCD,
                maxAllowedCD,
                middleAlignment: true,
                label: null,
                leftAlignedLabel: null,
                rightAlignedLabel: null,
                roundTo: 1f
            );
            if (Mathf.Abs(newSliderCDValue - sliderCDValue) > 0.01f)
            {
                int snapped = SnapToStep(Mathf.RoundToInt(newSliderCDValue), stepCD);
                snapped = Mathf.Clamp(snapped, minAllowedCD, maxAllowedCD);

                cooldownHours = snapped;
                cooldownBuf = snapped.ToString();
            }



            var t = TimeSpan.FromHours(cooldownHours);
            string cdString = $"{t.Days}일 {t.Hours}시간";
            oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(cdDesc, cdString);
            Text.Anchor = oldAnchor;

            GUI.enabled = oldGui;
            

            curY += rowH;
        }

        public bool TryGetCommandConfig(CheeseCommand cmd, out CheeseCommandConfig cfg)
        {
            EnsureCommandConfigs();

            for (int i = 0; i < commandConfigs.Count; i++)
            {
                if (commandConfigs[i].cmd == cmd)
                {
                    cfg = commandConfigs[i];
                    if (cfg.minDonation <= 0)
                    {
                        cfg.minDonation = DefaultMinDonationFor(cfg.cmd);
                        cfg.minDonationBuf = cfg.minDonation.ToString();
                    }

                    return true;
                }
            }
            cfg = null;
            return false;
        }
        private static int SnapToStep(int value, int step)
        {
            if (step <= 1) return value;
            return Mathf.RoundToInt((float)value / step) * step;
        }
        private void SplitVerticallyByRatio(
            Rect rect,
            out Rect left,
            out Rect right,
            float leftRatio,
            float margin)
        {
            leftRatio = Mathf.Clamp01(leftRatio);

            float leftWidth = (rect.width - margin) * leftRatio;

            left = new Rect(rect.x, rect.y, leftWidth, rect.height);
            right = new Rect(
                rect.x + leftWidth + margin,
                rect.y,
                rect.width - leftWidth - margin,
                rect.height
            );
        }
        private void SplitHorizontallyByRatio(
            Rect rect,
            out Rect top,
            out Rect bottom,
            float topRatio,
            float margin)
        {
            topRatio = Mathf.Clamp01(topRatio);

            float topHeight = (rect.height - margin) * topRatio;

            top = new Rect(rect.x, rect.y, rect.width, topHeight);

            bottom = new Rect(
                rect.x,
                rect.y + topHeight + margin,
                rect.width,
                rect.height - topHeight - margin
            );
        }

        
        private void SplitVerticallyByRatios(
            Rect rect,
            IList<float> ratios,
            float margin,
            List<Rect> outRects)
        {
            outRects.Clear();
            if (ratios == null || ratios.Count == 0) return;

            // margin 때문에 실제 사용 가능한 폭
            float totalMargin = margin * (ratios.Count - 1);
            float usableW = Mathf.Max(0f, rect.width - totalMargin);

            // ratios 합으로 정규화(합이 1이 아니어도 동작)
            float sum = 0f;
            for (int i = 0; i < ratios.Count; i++)
                sum += Mathf.Max(0f, ratios[i]);

            if (sum <= 0f) return;

            float x = rect.x;

            for (int i = 0; i < ratios.Count; i++)
            {
                float r = Mathf.Max(0f, ratios[i]) / sum;
                float w = (i == ratios.Count - 1)
                    ? (rect.xMax - x)
                    : usableW * r;

                var part = new Rect(x, rect.y, Mathf.Max(0f, w), rect.height);
                outRects.Add(part);

                x = part.xMax + margin;
            }
        }
        private Color GetStatusColor(string status)
        {
            if (status.StartsWith("Connected"))
                return Color.green;

            if (status.StartsWith("Connecting"))
                return Color.yellow;

            if (status.StartsWith("Disconnected"))
                return Color.red;

            return Color.white;
        }
        private void SplitHorizontallyByRatios(
            Rect rect,
            IList<float> ratios,
            float margin,
            List<Rect> outRects)
        {
            outRects.Clear();
            if (ratios == null || ratios.Count == 0) return;

            float totalMargin = margin * (ratios.Count - 1);
            float usableH = Mathf.Max(0f, rect.height - totalMargin);

            float sum = 0f;
            for (int i = 0; i < ratios.Count; i++)
                sum += Mathf.Max(0f, ratios[i]);

            if (sum <= 0f) return;

            float y = rect.y;

            for (int i = 0; i < ratios.Count; i++)
            {
                float r = Mathf.Max(0f, ratios[i]) / sum;
                float h = (i == ratios.Count - 1)
                    ? (rect.yMax - y)
                    : usableH * r;

                var part = new Rect(rect.x, y, rect.width, Mathf.Max(0f, h));
                outRects.Add(part);

                y = part.yMax + margin;
            }
        }
        public static Rect ShrinkRect(Rect rect, float left, float right, float top, float bottom)
        {
            return new Rect(
                rect.x + left,
                rect.y + top,
                rect.width - left - right,
                rect.height - top - bottom
            );
        }
        public static Rect ResizeRectCentered(Rect rect, float targetWidth, float targetHeight)
        {
            float newWidth  = rect.width  > targetWidth  ? targetWidth  : rect.width;
            float newHeight = rect.height > targetHeight ? targetHeight : rect.height;

            return new Rect(
                rect.x + (rect.width  - newWidth)  * 0.5f,
                rect.y + (rect.height - newHeight) * 0.5f,
                newWidth,
                newHeight
            );
        }
            }
}
