using Verse;
using UnityEngine;
using System.Collections.Generic;
using RimWorld;

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
        public float hudX = -1f;   // -1이면 아직 저장된 위치 없음(기본 위치 사용)
        public float hudY = -1f;
        public bool hudMinimized = false;
        private enum CheeseSettingsTab { Design, Content, Colors, Credits }
        private CheeseSettingsTab activeTab = CheeseSettingsTab.Design;

        private readonly Dictionary<string, float> sectionContentHeights = new Dictionary<string, float>();
        private const float SectionPad = 10f;
        private const float SectionHeaderH = 26f;
        private const float SectionGap = 10f;
        private const float separatorH = 12f;
        public List<CheeseCommandConfig> commandConfigs;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref useDropPod, "useDropPod", true);
            Scribe_Values.Look(ref chzzkStudioUrl, "chzzkStudioUrl", "");
            Scribe_Values.Look(ref chzzkStatus, "chzzkStatus", "Disconnected");
            Scribe_Values.Look(ref showHud, "showHud", true);
            Scribe_Values.Look(ref hudX, "hudX", -1f);
            Scribe_Values.Look(ref hudY, "hudY", -1f);

            EnsureCommandConfigs();

            for (int i = 0; i < commandConfigs.Count; i++)
            {
                var c = commandConfigs[i];
                string p = "cmd_" + c.ScribeKeyPrefix + "_"; // e.g. cmd_Join_

                Scribe_Values.Look(ref c.enabled,        p + "enabled", true);
                Scribe_Values.Look(ref c.source,         p + "source", CheeseCommandSource.Donation);
                Scribe_Values.Look(ref c.minDonation,    p + "minDonation", 1000);
                Scribe_Values.Look(ref c.cooldownHours,p + "cooldownHours", 0);
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                EnsureCommandConfigs();
                for (int i = 0; i < commandConfigs.Count; i++)
                {
                    commandConfigs[i].EnsureBuffers();
                }
            }
        }

        public void DoWindowContents(Rect inRect)
        {
            foreach (var c in commandConfigs)
            {
                c.EnsureBuffers();
            }
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
                case CheeseSettingsTab.Design:
                    DrawSection(left, ref yL, "General", listing =>
                    {
                        listing.CheckboxLabeled("Spawn via drop pod", ref useDropPod);
                        listing.CheckboxLabeled("Show HUD", ref showHud);
                    });

                    DrawSection(left, ref yL, "Connection", listing =>
                    {
                        listing.Label("CHZZK Studio URL:");
                        chzzkStudioUrl = listing.TextEntry(chzzkStudioUrl);

                        listing.Label($"CHZZK Status: {chzzkStatus}");

                        if (listing.ButtonText("Connect")) CheeseProtocolMod.ChzzkChat.UserConnect();
                        if (listing.ButtonText("Disconnect")) CheeseProtocolMod.ChzzkChat.UserDisconnect();
                    });

                    DrawSection(right, ref yR, "HUD", listing =>
                    {
                        listing.Label("HUD is controlled here. You can also reset position.");
                    });
                    break;

                case CheeseSettingsTab.Content:
                    float rowH = 26f * 2f + 6f;
                    float gridH = (rowH +separatorH)* 4; //커맨드 개수!!!!!!
                    Rect commandsRect = new Rect(
                        0f,
                        0f,
                        viewRect.width,
                        gridH
                    );

                    DrawCommandsGridTwoLine(commandsRect);

                    yL = gridH + 10f;
                    break;

                case CheeseSettingsTab.Colors:
                    DrawSection(left, ref yL, "Theme", listing =>
                    {
                        listing.Label("Later: accent colors, HUD opacity, etc.");
                    });
                    break;

                case CheeseSettingsTab.Credits:
                    DrawSection(left, ref yL, "Credits", listing =>
                    {
                        listing.Label("Cheese Protocol");
                        listing.Label("by ...");
                    });
                    break;
            }
            viewHeight = Mathf.Max(yL, yR) + 20f;

            Widgets.EndScrollView();
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
            System.Action<Listing_Standard> drawListing)
        {
            string key = title;
            // ---- measure pass (Layout only) ----
            if (Event.current.type == EventType.Layout)
            {
                float innerW = column.width - SectionPad * 2f;

                // "very tall" rect for measurement
                Rect measureRect = new Rect(0f, 0f, innerW, 100000f);

                var listingM = new Listing_Standard();
                listingM.Begin(measureRect);
                drawListing(listingM);
                listingM.End();

                sectionContentHeights[key] = listingM.CurHeight;
            }

            // fallback if not measured yet (first frame)
            float contentH = sectionContentHeights.TryGetValue(key, out var h) ? h : 200f;

            // total section height: pad + header + line + content + pad
            float sectionH =
                SectionPad +                 // top pad
                SectionHeaderH +             // title
                10f +                        // title->content spacing + separator
                contentH +                   // measured content
                SectionPad;                  // bottom pad

            Rect outer = new Rect(column.x, curY, column.width, sectionH);
            Widgets.DrawMenuSection(outer);

            Rect inner = outer.ContractedBy(SectionPad);

            // title
            Rect head = new Rect(inner.x, inner.y, inner.width, SectionHeaderH);
            var oldFont = Text.Font;
            Text.Font = GameFont.Medium;
            Widgets.Label(head, title);
            Text.Font = oldFont;

            Widgets.DrawLineHorizontal(inner.x, head.yMax + 4f, inner.width);

            // content area
            Rect contentRect = new Rect(
                inner.x,
                head.yMax + 10f,
                inner.width,
                inner.yMax - (head.yMax + 10f)
            );

            var listing = new Listing_Standard();
            listing.Begin(contentRect);
            drawListing(listing);
            listing.End();

            curY += sectionH + SectionGap;
        }
        /*
        private void DrawSection(Rect column, ref float curY, string title, System.Action<Rect> drawContent)
        {
            const float pad = 10f;
            const float headerH = 26f;
            const float gap = 10f;

            // 섹션 높이는 "대충" 크게 잡고, 내부 listing.CurHeight로 확정하는 방식도 가능.
            // 여기선 간단히 고정 높이 섹션으로 시작하고, 나중에 동적 높이로 개선 추천.
            float sectionH = 180f;

            Rect outer = new Rect(column.x, curY, column.width, sectionH);
            Widgets.DrawMenuSection(outer);

            Rect inner = outer.ContractedBy(pad);

            // 제목
            Rect head = new Rect(inner.x, inner.y, inner.width, headerH);
            Text.Font = GameFont.Medium;
            Widgets.Label(head, title);
            Text.Font = GameFont.Small;

            Widgets.DrawLineHorizontal(inner.x, head.yMax + 4f, inner.width);

            // 내용
            Rect content = new Rect(inner.x, head.yMax + 10f, inner.width, inner.height - headerH - 14f);
            drawContent(content);

            curY += sectionH + gap;
        }
        */
        
        private void DrawTabs(Rect tabRect)
        {
            float w = 140f;
            float h = tabRect.height;

            if (Widgets.ButtonText(new Rect(tabRect.x + 0f, tabRect.y, w, h), "Design")) activeTab = CheeseSettingsTab.Design;
            if (Widgets.ButtonText(new Rect(tabRect.x + w + 6f, tabRect.y, w, h), "Content")) activeTab = CheeseSettingsTab.Content;
            if (Widgets.ButtonText(new Rect(tabRect.x + (w + 6f) * 2, tabRect.y, w, h), "Colors")) activeTab = CheeseSettingsTab.Colors;
            if (Widgets.ButtonText(new Rect(tabRect.x + (w + 6f) * 3, tabRect.y, w, h), "Credits")) activeTab = CheeseSettingsTab.Credits;
        }

        private void DrawCommandsGridTwoLine(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(8f);

            float lineH = 26f;               // 한 줄 높이
            float rowH = lineH * 2f + 6f;    // 두 줄 + 약간 여백
            float gap = 24f;                // LEFT/RIGHT 사이 간격 넉넉하게
            float leftW = (inner.width - gap)/2f;

            Rect leftCol = new Rect(inner.x, inner.y, leftW, inner.height);
            Rect rightCol = new Rect(leftCol.xMax + gap, inner.y, inner.width - leftW - gap, inner.height);

            float y = 0f;
            float lineY = 0f;
            for (int i = 0; i < commandConfigs.Count; i++)
            {
                var c = commandConfigs[i];

                DrawCommandRowTwoLine(
                    leftCol,
                    rightCol,
                    ref y,
                    rowH,
                    lineH,
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
                    Color prev = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.3f);
                    Widgets.DrawLineHorizontal(rect.x + 8f, lineY, rect.width - 16f);
                    GUI.color = prev;
                    y += separatorH;
                }
            }
        }

        private void DrawCommandRowTwoLine(
            Rect leftCol,
            Rect rightCol,
            ref float curY,
            float rowH,
            float lineH,
            string label,
            ref bool enabled,
            ref CheeseCommandSource source,
            ref int minDonation,
            ref string minDonationBuf,
            ref int cooldownSeconds,
            ref string cooldownBuf)
        {
            Rect left = new Rect(leftCol.x, leftCol.y + curY, leftCol.width, rowH);
            Rect right = new Rect(rightCol.x, rightCol.y + curY, rightCol.width, rowH);

            Widgets.DrawHighlightIfMouseover(new Rect(left.x, left.y, left.width + right.width, rowH));

            float checkSize = 24f;
            float paddingX = 6f;
            float checkWidth = (left.width - paddingX)*0.5f;
            //Log.Warning($"left.x = {left.x}, checkwidth = {checkWidth}");
            float midY = left.y + (left.height - checkSize) * 0.5f;

            // label (vertically centered)
            Rect labelRect = new Rect(left.x, left.y, checkWidth, left.height);

            var oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(labelRect, label);
            Text.Anchor = oldAnchor;

            // checkbox immediately after label (not hard-right aligned)
            Rect checkRect = new Rect(labelRect.xMax + paddingX, midY, checkWidth, checkSize);
            Widgets.Checkbox(checkRect.position, ref enabled);

            // =========================================================
            // RIGHT: 2 lines (unchanged)
            // =========================================================
            Rect r1 = new Rect(right.x, right.y, right.width, lineH);
            Rect r2 = new Rect(right.x, right.y + lineH, right.width, lineH);

            bool oldGui = GUI.enabled;
            GUI.enabled = enabled;

            // Line 1: source + min donation
            float x = r1.x;
            float midY1 = r1.y + (lineH - 24f) / 2f;

            Rect chatBtn = new Rect(x, midY1, 24f, 24f);
            if (Widgets.RadioButton(chatBtn.position, source == CheeseCommandSource.Chat))
                source = CheeseCommandSource.Chat;
            Widgets.Label(new Rect(chatBtn.xMax + 4f, r1.y, 70f, lineH), "Chat");

            Rect donBtn = new Rect(chatBtn.xMax + 80f, midY1, 24f, 24f);
            if (Widgets.RadioButton(donBtn.position, source == CheeseCommandSource.Donation))
                source = CheeseCommandSource.Donation;
            Widgets.Label(new Rect(donBtn.xMax + 4f, r1.y, 90f, lineH), "Donation");

            Rect minLabel = new Rect(donBtn.xMax + 100f, r1.y, 60f, lineH);
            Widgets.Label(minLabel, "Min ₩");

            bool old2 = GUI.enabled;
            GUI.enabled = enabled && source == CheeseCommandSource.Donation;

            Rect minField = new Rect(minLabel.xMax + 4f, midY1, 90f, 24f);
            Widgets.TextFieldNumeric(minField, ref minDonation, ref minDonationBuf, 0, 100000000);

            GUI.enabled = old2;

            if (source == CheeseCommandSource.Chat)
                minDonation = 0;

            // Line 2: cooldown
            Rect cdLabel = new Rect(r2.x, r2.y, 90f, lineH);
            Widgets.Label(cdLabel, "Cooldown(s)");

            Rect cdField = new Rect(
                cdLabel.xMax + 4f,
                r2.y + (lineH - 24f) / 2f,
                90f,
                24f
            );
            Widgets.TextFieldNumeric(cdField, ref cooldownSeconds, ref cooldownBuf, 0, 86400);

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
                    return true;
                }
            }
            cfg = null;
            return false;
        }
    }
}
