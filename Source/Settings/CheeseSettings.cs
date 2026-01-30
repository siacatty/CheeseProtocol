using Verse;
using UnityEngine;
using System.Collections.Generic;
using RimWorld;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Xml.Schema;
using System;
using Newtonsoft.Json;
using System.Security.Cryptography;
using static CheeseProtocol.CheeseLog;

namespace CheeseProtocol
{
    public class CheeseSettings : ModSettings
    {
        private Vector2 mainScrollPos = Vector2.zero;
        private float viewHeight = 800f; // 대충 크게 잡아두면 OK
        public string chzzkStudioUrl = CheeseDefaults.ChzzkStudioUrl;
        public string chzzkStatus = CheeseDefaults.ChzzkStatus;
        public bool showHud = CheeseDefaults.ShowHud;
        public bool hudLocked = CheeseDefaults.HudLocked;
        public float hudOpacity = CheeseDefaults.HudOpacity;
        public bool drainQueue = CheeseDefaults.DrainQueue;
        public float hudX = CheeseDefaults.HudX;   // -1이면 아직 저장된 위치 없음(기본 위치 사용)
        public float hudY = CheeseDefaults.HudY;
        public float hudW = CheeseDefaults.HudW;
        public float hudH = CheeseDefaults.HudH;
        public CheeseCommandSource simSource = CheeseDefaults.SimSource;
        public int simDonAmount = CheeseDefaults.SimDonAmount;
        public string simDonAmountBuf = CheeseDefaults.SimDonAmountBuf;
        public bool hudMinimized = CheeseDefaults.HudMinimized;
        public bool hudSlideHidden = CheeseDefaults.HudSlideHidden;
        public float randomVar = CheeseDefaults.RandomVar;
        public bool allowSpeechBubble = CheeseDefaults.AllowSpeechBubble;
        public int speechBubbleCD = CheeseDefaults.SpeechBubbleCD;
        public string speechBubbleCDBuf;
        public bool appendRollLogToLetters = CheeseDefaults.AppendRollLogToLetters;
        private enum CheeseSettingsTab { General, Command, Advanced, Simulation, Credits }
        private CheeseSettingsTab activeTab = CheeseSettingsTab.General;
        public float resultDonation01 = 0f;
        private readonly Dictionary<string, float> sectionContentHeights = new Dictionary<string, float>();
        private const float SectionPad = 10f;
        private const float SectionHeaderH = 26f;
        private const float SectionGap = 10f;
        private const float separatorH = 12f;
        private const float lineH = 26f;
        private float cachedScrollHeight = 900f;
        public List<CheeseCommandConfig> commandConfigs;
        public CheeseCommandConfig selectedConfigSim;
        public CheeseCommandConfig selectedConfigAdv;
        private const int maxAllowedDonation = 1000000;
        private const int minAllowedDonation = 1000;
        private readonly PreviewDirtyDebouncer _previewDebounce = new PreviewDirtyDebouncer();


        //Advanced settings
        public List<CommandAdvancedSettingsBase> advancedSettings;
        private static readonly Dictionary<CheeseCommand, Func<CommandAdvancedSettingsBase>> AdvFactories
        = new()
        {
            { CheeseCommand.Join,    () => new JoinAdvancedSettings() },
            { CheeseCommand.Raid,    () => new RaidAdvancedSettings() },
            { CheeseCommand.Bully,    () => new BullyAdvancedSettings() },
            { CheeseCommand.Teacher,  () => new TeacherAdvancedSettings() },
            { CheeseCommand.Caravan, () => new CaravanAdvancedSettings() },
            { CheeseCommand.Meteor,  () => new MeteorAdvancedSettings() },
            { CheeseCommand.Supply,  () => new SupplyAdvancedSettings() },
            { CheeseCommand.Tame,  () => new TameAdvancedSettings() },
            { CheeseCommand.Thrumbo,  () => new ThrumboAdvancedSettings() },
        };
        public T GetAdvSetting<T>(CheeseCommand cmd) where T : CommandAdvancedSettingsBase
        {
            var a = GetAdvSetting(cmd);
            if (a is T t) return t;

            QErr($"AdvSetting type mismatch for {cmd}. Got={a?.GetType().Name}, expected={typeof(T).Name}");
            return null;
        }
        public CommandAdvancedSettingsBase GetAdvSetting(CheeseCommand cmd)
        {
            var a = advancedSettings.FirstOrDefault(x => x.Command == cmd);
            if (a != null) return a;
            if (!AdvFactories.TryGetValue(cmd, out var factory))
            {
                QErr($"No AdvancedSettings factory for {cmd}", Channel.Verse);
                return null;
            }

            var created = factory();
            advancedSettings.Add(created);
            return created;
        }
        public void ResetToDefaults()
        {
            chzzkStudioUrl = CheeseDefaults.ChzzkStudioUrl;
            chzzkStatus = CheeseDefaults.ChzzkStatus;
            showHud = CheeseDefaults.ShowHud;
            hudLocked = CheeseDefaults.HudLocked;
            hudOpacity = CheeseDefaults.HudOpacity;
            hudX = CheeseDefaults.HudX;
            hudY = CheeseDefaults.HudY;
            hudW = CheeseDefaults.HudW;
            hudH = CheeseDefaults.HudH;
            randomVar = CheeseDefaults.RandomVar;
            simSource = CheeseDefaults.SimSource;
            simDonAmount = CheeseDefaults.SimDonAmount;
            simDonAmountBuf = CheeseDefaults.SimDonAmountBuf;
            drainQueue = CheeseDefaults.DrainQueue;
            appendRollLogToLetters = CheeseDefaults.AppendRollLogToLetters;
            allowSpeechBubble = CheeseDefaults.AllowSpeechBubble;
            speechBubbleCD = CheeseDefaults.SpeechBubbleCD;
            speechBubbleCDBuf = CheeseDefaults.SpeechBubbleCDBuf;
            if (CheeseGameComponent.Instance != null && CheeseGameComponent.Instance.hudWindow != null)
                CheeseGameComponent.Instance.hudWindow.ResetToDefaults();
            EnsureCommandConfigs();
            for (int i = 0; i < commandConfigs.Count; i++)
            {
                var c = commandConfigs[i];
                c.enabled = CheeseDefaults.CmdEnabled;
                c.source = CheeseDefaults.CmdSource;
                c.minDonation = CheeseDefaults.CmdMinDonation;
                c.maxDonation = CheeseDefaults.CmdMaxDonation;
                c.cooldownHours = CheeseDefaults.CmdCooldownHours;
            }
            foreach (var adv in advancedSettings)
            {
                adv.ResetToDefaults();
            }

        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref chzzkStudioUrl, "chzzkStudioUrl", CheeseDefaults.ChzzkStudioUrl);
            Scribe_Values.Look(ref chzzkStatus, "chzzkStatus", CheeseDefaults.ChzzkStatus);
            Scribe_Values.Look(ref showHud, "showHud", CheeseDefaults.ShowHud);
            Scribe_Values.Look(ref hudLocked, "hudLocked", CheeseDefaults.HudLocked);
            Scribe_Values.Look(ref hudOpacity, "hudOpacity", CheeseDefaults.HudOpacity);
            Scribe_Values.Look(ref hudMinimized, "hudMinimized", CheeseDefaults.HudMinimized);
            Scribe_Values.Look(ref hudSlideHidden, "hudSlideHidden", CheeseDefaults.HudSlideHidden);
            Scribe_Values.Look(ref hudX, "hudX", CheeseDefaults.HudX);
            Scribe_Values.Look(ref hudY, "hudY", CheeseDefaults.HudY);
            Scribe_Values.Look(ref hudW, "hudW", CheeseDefaults.HudW);
            Scribe_Values.Look(ref hudH, "hudH", CheeseDefaults.HudH);
            Scribe_Values.Look(ref randomVar, "randomVar", CheeseDefaults.RandomVar);
            Scribe_Values.Look(ref simSource, "simSource", CheeseDefaults.SimSource);
            Scribe_Values.Look(ref simDonAmount, "simDonAmount", CheeseDefaults.SimDonAmount);
            Scribe_Values.Look(ref simDonAmountBuf, "simDonAmountBuf", CheeseDefaults.SimDonAmountBuf);
            Scribe_Values.Look(ref drainQueue, "drainQueue", CheeseDefaults.DrainQueue);
            Scribe_Values.Look(ref appendRollLogToLetters, "appendRollLogToLetters", CheeseDefaults.AppendRollLogToLetters);
            Scribe_Values.Look(ref allowSpeechBubble, "allowSpeechBubble", CheeseDefaults.AllowSpeechBubble);
            Scribe_Values.Look(ref speechBubbleCD, "speechBubbleCD", CheeseDefaults.SpeechBubbleCD);


            //Advanced settings
            Scribe_Collections.Look(ref advancedSettings, "advancedSettings", LookMode.Deep);
            EnsureCommandConfigs();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                advancedSettings ??= new List<CommandAdvancedSettingsBase>();
                DedupAdvancedSettings();
                EnsureAdvSettingsInitialized();
                FixupCommandDefaults();
            }

            for (int i = 0; i < commandConfigs.Count; i++)
            {
                var c = commandConfigs[i];
                string p = "cmd_" + c.ScribeKeyPrefix + "_"; // e.g. cmd_Join_

                Scribe_Values.Look(ref c.enabled,        p + "enabled", CheeseDefaults.CmdEnabled);
                Scribe_Values.Look(ref c.source,         p + "source", CheeseDefaults.CmdSource);
                Scribe_Values.Look(ref c.minDonation, p + "minDonation", CheeseDefaults.CmdMinDonation);
                Scribe_Values.Look(ref c.maxDonation, p + "maxDonation", CheeseDefaults.CmdMaxDonation);
                Scribe_Values.Look(ref c.cooldownHours, p + "cooldownHours", CheeseDefaults.CmdCooldownHours);
            }
        }
        private void DedupAdvancedSettings()
        {
            advancedSettings.RemoveAll(a => a == null);

            var seen = new HashSet<CheeseCommand>();
            for (int i = advancedSettings.Count - 1; i >= 0; i--)
            {
                var cmd = advancedSettings[i].Command;
                if (!seen.Add(cmd))
                    advancedSettings.RemoveAt(i); // 뒤에서부터 지우면 안전
            }
        }
        public void EnsureAdvSettingsInitialized()
        {
            advancedSettings ??= new List<CommandAdvancedSettingsBase>();
            EnsureAdv(CheeseCommand.Join, () => new JoinAdvancedSettings());
            EnsureAdv(CheeseCommand.Raid, () => new RaidAdvancedSettings());
            EnsureAdv(CheeseCommand.Bully, () => new BullyAdvancedSettings());
            EnsureAdv(CheeseCommand.Teacher, () => new TeacherAdvancedSettings());
            EnsureAdv(CheeseCommand.Caravan, () => new CaravanAdvancedSettings());
            EnsureAdv(CheeseCommand.Meteor, () => new MeteorAdvancedSettings());
            EnsureAdv(CheeseCommand.Supply, () => new SupplyAdvancedSettings());
            EnsureAdv(CheeseCommand.Tame, () => new TameAdvancedSettings());
            EnsureAdv(CheeseCommand.Thrumbo, () => new ThrumboAdvancedSettings());
        }
        private void EnsureAdv(CheeseCommand command, Func<CommandAdvancedSettingsBase> factory)
        {
            if (!advancedSettings.Any(a => a.Command == command))
                advancedSettings.Add(factory());
        }
        public void DoWindowContents(Rect inRect)
        {
            float curY = 0;
            if (commandConfigs != null){
                foreach (var c in commandConfigs)
                {
                    c.EnsureBuffers();
                }
            }

            EnsureCommandConfigs();
            if (selectedConfigSim == null)
                selectedConfigSim = commandConfigs == null ? null : commandConfigs[0];
            if (selectedConfigAdv == null)
                selectedConfigAdv = commandConfigs == null ? null : commandConfigs[0];

            const float tabH = 32f;
            const float gap = 10f;

            Rect tabRect = new Rect(inRect.x, inRect.y, inRect.width, tabH);
            Rect bodyScrollRect = new Rect(inRect.x, inRect.y + tabH + gap, inRect.width, inRect.height - tabH - gap);

            DrawTabs(tabRect);
            curY += tabH;

            UIUtil.AutoScrollView(
                    bodyScrollRect,
                    ref mainScrollPos,
                    ref cachedScrollHeight,
                    viewRect =>
                    {
                        return DrawScrollView(new Rect(0f, 0f, bodyScrollRect.width - 16f, cachedScrollHeight));
                    }
                );
            curY += bodyScrollRect.height;
        }

        private float DrawScrollView(Rect viewRect)
        {
            const float paddingX = 6f;
            const float paddingY = 12f;
            float colGap = 12f;
            float colW = (viewRect.width - colGap) / 2f;

            Rect left = new Rect(0f, 0f, colW, viewRect.height);
            Rect right = new Rect(colW + colGap, 0f, colW, viewRect.height);

            float yL = 0f;
            float yR = 0f;

            float btnSize = 24f;

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
                    DrawSection(left, ref yL, "HUD", listing =>
                    {
                        float paddingX = 6f;
                        listing.CheckboxLabeled("HUD 표시", ref showHud);
                        listing.CheckboxLabeled("HUD 현재 위치 고정", ref hudLocked);
                        Rect opacityRect = listing.GetRect(lineH*2f);
                        UIUtil.SplitVerticallyByRatio(opacityRect, out Rect opacityDesc, out Rect opacitySlider, 0.4f, paddingX);
                        var prevAnchor = Text.Anchor;
                        Text.Anchor = TextAnchor.MiddleLeft;
                        Widgets.Label(opacityDesc, "HUD 배경 불투명도");
                        Text.Anchor = prevAnchor;
                        hudOpacity = Widgets.HorizontalSlider(opacitySlider, hudOpacity, 0.05f, 1f, true, label:$"{Mathf.RoundToInt(hudOpacity * 100f)}%");
                    });
                    DrawSection(right, ref yR, "기타", listing =>
                    {
                        float paddingX = 6f;
                        //float paddingY = 12f;
                        listing.CheckboxLabeled("퍼즈 상태일때 명령어 처리", ref drainQueue);
                        listing.Gap(4);
                        var prev = GUI.color;
                        GUI.color = new Color(230f / 255f, 195f / 255f, 92f / 255f);
                        listing.Label("[WARNING!]\n모드가 많거나 렉이 심하면 비활성화를 권장.\n비활성화 시, 퍼즈 중 명령어는 퍼즈 해제 후 일괄 처리");
                        GUI.color = prev;
                        listing.GapLine();
                        listing.Gap(4);
                        Rect allowFeedbackRect = listing.GetRect(lineH);
                        Widgets.CheckboxLabeled(allowFeedbackRect, "알림 메세지에 상세결과 표시", ref appendRollLogToLetters);
                        TooltipHandler.TipRegion(
                            allowFeedbackRect,
                            () => "시청자의 운을 수치로 표시합니다.",
                            allowFeedbackRect.GetHashCode()
                        );


                        listing.Gap(4);
                        listing.GapLine();
                        listing.Gap(4);
                        Rect allowSpeechRect = listing.GetRect(lineH);
                        Widgets.CheckboxLabeled(allowSpeechRect, "참여자 채팅 말풍선 허용", ref allowSpeechBubble);
                        Rect speechCDRect = listing.GetRect(lineH);
                        UIUtil.SplitVerticallyByRatio(speechCDRect, out Rect speechCDLabel, out Rect speechCDField, 0.3f, 0f);
                        UIUtil.DrawCenteredText(speechCDLabel, "채팅 주기 (초) : ", TextAlignment.Left);
                        speechCDField = UIUtil.ResizeRectAligned(speechCDField, 60f, lineH, TextAlignment.Left);
                        bool oldGUI = GUI.enabled;
                        GUI.enabled = allowSpeechBubble;
                        UIUtil.IntFieldDigitsOnly(speechCDField, ref speechBubbleCD, ref speechBubbleCDBuf, 0, 1000);
                        GUI.enabled = oldGUI;
                        listing.Gap(4);
                        listing.GapLine();
                        listing.Gap(4);
                        

                        Rect randomVarRect = listing.GetRect(lineH*3);
                        UIUtil.SplitVerticallyByRatio(randomVarRect, out Rect randomVarLabel, out Rect randomVarSlider, 0.4f, paddingX);
                        TextAnchor oldAnchor = Text.Anchor;
                        Text.Anchor = TextAnchor.MiddleLeft;
                        Widgets.Label(randomVarLabel, "이벤트 효과 강도 랜덤성:");
                        TooltipHandler.TipRegion(
                            randomVarRect,
                            () => "100%: 완전 랜덤(후원금액 반영 X)\n0%: 금액 정확 반영 (소액 위주 환경 비추천)\n80%: 적당히 섞인 추천 설정",
                            randomVarRect.GetHashCode()
                        );
                        Text.Anchor = oldAnchor;
                        randomVarSlider = UIUtil.ResizeRectAligned(randomVarSlider, randomVarSlider.width, lineH);
                        randomVar = Widgets.HorizontalSlider(randomVarSlider, randomVar, 0f, 1f, true, label:$"{Mathf.RoundToInt(randomVar * 100f)}%");
                        listing.Gap(4);
                        listing.GapLine();
                        listing.Gap(4);


                        Rect cooldownResetRect = listing.GetRect(btnSize*2f);
                        cooldownResetRect = UIUtil.ResizeRectAligned(cooldownResetRect, cooldownResetRect.width*0.5f, btnSize*1.5f);
                        if (Widgets.ButtonText(cooldownResetRect, "모든 명령어 쿨타임 리셋"))
                            CheeseProtocolMod.ChzzkChat.ResetCooldown();
                        listing.Gap(4);
                        Rect resetSettingRect = listing.GetRect(btnSize*2f);
                        resetSettingRect = UIUtil.ResizeRectAligned(resetSettingRect, resetSettingRect.width*0.5f, btnSize*1.5f);

                        if (Widgets.ButtonText(resetSettingRect, "모든 설정 초기화"))
                        {
                            LongEventHandler.ExecuteWhenFinished(() =>
                            {
                                Find.WindowStack.Add(
                                    Dialog_MessageBox.CreateConfirmation(
                                        "모든 설정을 기본값으로 되돌릴까요?\n(되돌릴 수 없습니다)",
                                        confirmedAct: () =>
                                        {
                                            ResetToDefaults();
                                            Write();
                                            CheeseProtocolMod.ChzzkChat.UserConnect();
                                        },
                                        destructive: true
                                    )
                                );
                            });
                        }
                    });
                    DrawSectionNoListing(left, ref yL, "설명서", false, rect =>
                    {
                        float usedH = 0;
                        Rect manualBtnRect = new Rect(rect.x, rect.y, rect.width, btnSize*2);
                        usedH += btnSize*2;
                        manualBtnRect = UIUtil.ResizeRectAligned(manualBtnRect, manualBtnRect.width*0.5f, btnSize*1.5f);
                        if (Widgets.ButtonText(manualBtnRect, "설명서"))
                        {
                            Find.WindowStack.Add(new CheeseManualWindow());
                        }
                        return usedH;
                    });
                    break;

                case CheeseSettingsTab.Command:
                    float rowH = lineH * 2f + paddingY;
                    float gridH = (rowH +separatorH)* (commandConfigs.Count+1); //header 포함
                    Rect commandsRect = new Rect(
                        0f,
                        0f,
                        viewRect.width,
                        gridH
                    );

                    DrawCommandSection(commandsRect, lineH, paddingX, paddingY);

                    yL = gridH + SectionGap;
                    break;

                case CheeseSettingsTab.Advanced:
                    //float gridH = (rowH +separatorH)* (commandConfigs.Count+1); //header 포함
                    float advPageH = 300f;
                    Rect advPageRect = new Rect(
                        0f,
                        0f,
                        viewRect.width,
                        advPageH
                    );

                    DrawAdvancedPage(advPageRect, ref yL, ref yR, lineH, paddingX, paddingY);

                    //yL = gridH + SectionGap;
                    break;
                case CheeseSettingsTab.Simulation:
                    DrawSection(left, ref yL, "시뮬레이션", listing =>
                    {
                        float paddingX = 6f;
                        float paddingY = 12f;
                        listing.Label("시뮬레이션 (개발자 모드 전용)\n환경설정 → 개발자 모드 활성화");
                        listing.GapLine();
                        bool oldGUI = GUI.enabled;
                        GUI.enabled = Prefs.DevMode;
                        Rect simCommandRect = listing.GetRect(lineH*2f);
                        //Widgets.DrawBox(simCommandRect);
                        UIUtil.SplitVerticallyByRatio(
                            simCommandRect,
                            out Rect simCommandDesc,
                            out Rect simCommandDropdown,
                            0.4f,
                            paddingX
                        );
                        simCommandDropdown = UIUtil.ShrinkRect(simCommandDropdown, 0, simCommandDropdown.width*0.5f, simCommandDropdown.height*0.20f, simCommandDropdown.height*0.20f);
                        var oldAnchor = Text.Anchor;
                        Text.Anchor = TextAnchor.MiddleCenter;
                        Widgets.Label(simCommandDesc, "명령어 :");
                        Text.Anchor = oldAnchor;
                        Widgets.Dropdown(
                            simCommandDropdown,
                            selectedConfigSim,
                            c => c,
                            _ => GenerateCommandMenu(cfg => selectedConfigSim = cfg),
                            selectedConfigSim != null ? selectedConfigSim.label : "(No commands)"
                        );

                        listing.Gap(8);
                        Rect simSrcRect=listing.GetRect(btnSize*2f+paddingY);
                        //Widgets.DrawBox(simSrcRect);
                        UIUtil.SplitVerticallyByRatio(
                            simSrcRect,
                            out Rect simSrcRad,
                            out Rect simDonAmountRect,
                            0.5f,
                            paddingX
                        );
                        UIUtil.SplitHorizontallyByRatio(
                            simSrcRad,
                            out Rect simSrcChat,
                            out Rect simSrcDon,
                            0.5f,
                            paddingY
                        );
                        UIUtil.SplitVerticallyByRatio(
                            simSrcChat,
                            out Rect simSrcChatRadBtn,
                            out Rect simSrcChatDesc,
                            0.5f,
                            paddingX
                        );
                        UIUtil.SplitVerticallyByRatio(
                            simSrcDon,
                            out Rect simSrcDonRadBtn,
                            out Rect simSrcDonDesc,
                            0.5f,
                            paddingX
                        );
                        UIUtil.SplitVerticallyByRatio(
                            simDonAmountRect,
                            out Rect simDonAmountDesc,
                            out Rect simDonAmountField,
                            0.5f,
                            paddingX
                        );
                        oldAnchor = Text.Anchor;
                        Text.Anchor = TextAnchor.MiddleLeft;
                        simSrcChatRadBtn = UIUtil.ResizeRectAligned(simSrcChatRadBtn, btnSize, btnSize);
                        simSrcDonRadBtn = UIUtil.ResizeRectAligned(simSrcDonRadBtn, btnSize, btnSize);
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
                        simDonAmountField = UIUtil.ShrinkRect(simDonAmountField, 0, 0, simDonAmountField.height*0.25f, simDonAmountField.height*0.25f);
                        UIUtil.IntFieldDigitsOnly(simDonAmountField, ref simDonAmount, ref simDonAmountBuf, 0, maxAllowedDonation);
                        GUI.enabled = oldGUI2;

                        listing.Gap(lineH);
                        Rect simBtn  = listing.GetRect(btnSize*2f);
                        simBtn = UIUtil.ResizeRectAligned(simBtn, simBtn.width*0.5f, btnSize*1.5f);
                        
                        if (Widgets.ButtonText(simBtn, "시뮬레이션 실행"))
                        {
                            Msg("Run Simulation", Channel.Debug);
                            long simAtUtcMsNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            string simUserName = $"SIM{DateTimeOffset.FromUnixTimeMilliseconds(simAtUtcMsNow):HHmmssfff}";
                            string simMessage = selectedConfigSim.label;
                            bool simIsDonation = simSource == CheeseCommandSource.Donation;
                            int simAmount = simDonAmount;
                            string simDonationType = "CHAT";
                            string simDonationId = null;
                            CheeseProtocolMod.ChzzkChat.RunSimulation(
                                simUserName,
                                simMessage,
                                simAtUtcMsNow,
                                simIsDonation,
                                selectedConfigSim.cmd,
                                simAmount,
                                simDonationType,
                                simDonationId
                            );
                            CheeseProtocolMod.ChzzkChat.ProcessEventQueues();
                        }
                        listing.Gap(8);
                        Rect cooldownResetRect = listing.GetRect(btnSize*2f);
                        cooldownResetRect = UIUtil.ResizeRectAligned(cooldownResetRect, cooldownResetRect.width*0.5f, btnSize*1.5f);
                        if (Widgets.ButtonText(cooldownResetRect, "모든 명령어 쿨타임 리셋"))
                            CheeseProtocolMod.ChzzkChat.ResetCooldown();
                        /*
                        listing.Gap(8);
                        Rect DebugRect = listing.GetRect(btnSize*2f);
                        DebugRect = UIUtil.ResizeRectAligned(DebugRect, DebugRect.width*0.5f, btnSize*1.5f);
                        if (Widgets.ButtonText(DebugRect, "벽 채우기 (전체 맵/생물 파괴)"))
                        {
                            Map map = Find.AnyPlayerHomeMap;
                            ThingDef wallDef = ThingDefOf.Wall;
                            ThingDef stuff = ThingDefOf.Steel;

                            foreach (IntVec3 c in map.AllCells)
                            {
                                var list = c.GetThingList(map);
                                for (int i = list.Count - 1; i >= 0; i--)
                                {
                                    list[i].Destroy(DestroyMode.Vanish);
                                }

                                Thing wall = ThingMaker.MakeThing(wallDef, stuff);
                                GenSpawn.Spawn(wall, c, map, WipeMode.Vanish);
                            }
                        }
                        */
                        
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
            return viewHeight = Mathf.Max(yL, yR) + 20f;
            //return Mathf.Max(curYL, curYR) + 20f;
        }
        private IEnumerable<Widgets.DropdownMenuElement<CheeseCommandConfig>>
        GenerateCommandMenu(Action<CheeseCommandConfig> onSelect)
        {
            foreach (var cfg in commandConfigs)
            {
                var captured = cfg;
                yield return new Widgets.DropdownMenuElement<CheeseCommandConfig>
                {
                    option = new FloatMenuOption(
                        captured.label,
                        () => onSelect(captured)
                    ),
                    payload = captured
                };
            }
        }

        private void FixupCommandDefaults()
        {
            EnsureCommandConfigs();

            for (int i = 0; i < commandConfigs.Count; i++)
            {
                var c = commandConfigs[i];
                if (c.minDonation < CheeseCommandConfig.defaultMinDonation)
                    c.minDonation = CheeseCommandConfig.defaultMinDonation;
                if (c.maxDonation < CheeseCommandConfig.defaultMinDonation)
                    c.maxDonation = CheeseCommandConfig.defaultMaxDonation;
                if (c.minDonation > c.maxDonation)
                    c.maxDonation = c.minDonation;
                c.minDonationBuf = c.minDonation.ToString();
                c.maxDonationBuf = c.maxDonation.ToString();
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
                new CheeseCommandConfig { cmd = CheeseCommand.Bully,    label = "!일진" },
                new CheeseCommandConfig { cmd = CheeseCommand.Teacher,    label = "!교육" },
                new CheeseCommandConfig { cmd = CheeseCommand.Caravan,    label = "!상단" },
                new CheeseCommandConfig { cmd = CheeseCommand.Meteor, label = "!운석" },
                new CheeseCommandConfig { cmd = CheeseCommand.Supply, label = "!보급" },
                new CheeseCommandConfig { cmd = CheeseCommand.Tame, label = "!조련" },
                new CheeseCommandConfig { cmd = CheeseCommand.Thrumbo, label = "!트럼보" },
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

        private void DrawSectionNoListing(
            Rect column,
            ref float curY,
            string title,
            bool drawTitle,
            Func<Rect, float> drawContentAndReturnUsedHeight
        )
        {
            string key = title;
            // 이전 프레임에서 캐시된 content height (fallback)
            float contentH = sectionContentHeights.TryGetValue(key, out var h) ? h : 200f;
            float sectionH = SectionPad + contentH + SectionPad;
            if (drawTitle) sectionH += SectionHeaderH + 10f;

            // 전체 섹션 박스
            Rect outer = new Rect(column.x, curY, column.width, sectionH);
            Widgets.DrawMenuSection(outer);

            Rect inner = outer.ContractedBy(SectionPad);
            float contentY = inner.y;
            // 헤더
            if (drawTitle)
            {
                Rect head = new Rect(inner.x, inner.y, inner.width, SectionHeaderH);
                var oldFont = Text.Font;
                Text.Font = GameFont.Medium;
                Widgets.Label(head, title);
                Text.Font = oldFont;

                Widgets.DrawLineHorizontal(inner.x, head.yMax + 4f, inner.width);
                contentY += SectionHeaderH +10f;
            }

            // 컨텐츠 영역 (content 좌표 기준)
            Rect contentRect = new Rect(
                inner.x,
                contentY,
                inner.width,
                inner.yMax - contentY
            );

            // 컨텐츠 그리기 + 실제 사용 높이 측정
            float usedH = 0f;
            if (drawContentAndReturnUsedHeight != null)
                usedH = drawContentAndReturnUsedHeight(contentRect);

            // Layout 패스에서만 높이 캐시 갱신
            if (Event.current.type == EventType.Layout)
                sectionContentHeights[key] = usedH;

            // 다음 섹션으로 y 이동
            curY += sectionH + SectionGap;
        }

        private void DrawAdvancedPage(Rect rect, ref float yL, ref float yR, float lineH, float paddingX, float paddingY)
        {
            float headerH = 54f;
            UIUtil.SplitHorizontallyByHeight(rect, out Rect headerRect, out Rect contentRect, headerH, SectionGap);
            Widgets.DrawMenuSection(headerRect);
            yL+= headerH+paddingY;
            yR+= headerH+paddingY;
            var oldFont = Text.Font;
            Text.Font = GameFont.Medium;
            string headerTitle = "고급설정 대상 명령어 :";
            UIUtil.SplitVerticallyByWidth(headerRect, out Rect headerTitleRect, out Rect headerButtonRect, Text.CalcSize(headerTitle).x + 12f, paddingX);
            UIUtil.SplitVerticallyByWidth(headerButtonRect, out Rect headerCommandRect, out Rect headerResetRect, headerTitleRect.width, paddingX);
            headerCommandRect = headerCommandRect.ContractedBy(8f);
            headerResetRect = UIUtil.ResizeRectAligned(headerResetRect, headerTitleRect.width, headerResetRect.height, TextAlignment.Right).ContractedBy(12f);
            UIUtil.DrawCenteredText(headerTitleRect, headerTitle, font: GameFont.Medium);
            bool hasAny = commandConfigs != null && commandConfigs.Count > 0;
            using (new UIUtil.GUIStateScope(hasAny))
            {
                Widgets.Dropdown(
                    headerCommandRect,
                    selectedConfigAdv,
                    c => c, // dummy payload
                    _ => GenerateCommandMenu(cfg => selectedConfigAdv = cfg),
                    selectedConfigAdv != null ? selectedConfigAdv.label : "(No commands)");
            }
            Text.Font = oldFont;
            if (Widgets.ButtonText(headerResetRect, "선택 명령어 설정 초기화"))
                GetAdvSetting(selectedConfigAdv.cmd).ResetToDefaults();
            UIUtil.SplitVerticallyByRatio(contentRect, out Rect leftAdv, out Rect rightAdv, 0.5f, paddingX);
            switch(selectedConfigAdv.cmd)
            {
                case CheeseCommand.Join:
                    DrawSectionNoListing(leftAdv, ref yL, "!참여", false, rect =>
                    {
                        return GetAdvSetting(CheeseCommand.Join).Draw(rect);
                    });
                    DrawSectionNoListing(leftAdv, ref yL, "허용 종족", false, rect =>
                    {
                        return GetAdvSetting<JoinAdvancedSettings>(CheeseCommand.Join).DrawEditableRaceList(rect);
                    });
                    DrawSectionNoListing(leftAdv, ref yL, "선호/비선호 특성", false, rect =>
                    {
                        return GetAdvSetting<JoinAdvancedSettings>(CheeseCommand.Join).DrawEditableList(rect, "선호/비선호 특성", lineH, paddingX, paddingY);
                    });
                    break;
                case CheeseCommand.Raid:
                    DrawSectionNoListing(leftAdv, ref yL, "!습격", false, rect =>
                    {
                        return GetAdvSetting(CheeseCommand.Raid).Draw(rect);
                    });
                    break;
                case CheeseCommand.Bully:
                    DrawSectionNoListing(leftAdv, ref yL, "!일진", false, rect =>
                    {
                        return GetAdvSetting(CheeseCommand.Bully).Draw(rect);
                    });
                    break;
                case CheeseCommand.Teacher:
                    DrawSectionNoListing(leftAdv, ref yL, "!교육", false, rect =>
                    {
                        return GetAdvSetting(CheeseCommand.Teacher).Draw(rect);
                    });
                    break;
                case CheeseCommand.Meteor:
                    DrawSectionNoListing(leftAdv, ref yL, "!운석", false, rect =>
                    {
                        return GetAdvSetting(CheeseCommand.Meteor).Draw(rect);
                    });
                    DrawSectionNoListing(leftAdv, ref yL, "허용 운석", false, rect =>
                    {
                        return GetAdvSetting<MeteorAdvancedSettings>(CheeseCommand.Meteor).DrawEditableList(rect, "허용 운석", lineH, paddingX, paddingY);
                    });
                    break;
                case CheeseCommand.Caravan:
                    DrawSectionNoListing(leftAdv, ref yL, "!상단", false, rect =>
                    {
                        return GetAdvSetting(CheeseCommand.Caravan).Draw(rect);
                    });
                    break;
                case CheeseCommand.Supply:
                    DrawSectionNoListing(leftAdv, ref yL, "!보급", false, rect =>
                    {
                        return GetAdvSetting(CheeseCommand.Supply).Draw(rect);
                    });
                    break;
                case CheeseCommand.Tame:
                    DrawSectionNoListing(leftAdv, ref yL, "!조련", false, rect =>
                    {
                        return GetAdvSetting(CheeseCommand.Tame).Draw(rect);
                    });
                    break;
                case CheeseCommand.Thrumbo:
                    DrawSectionNoListing(leftAdv, ref yL, "!트럼보", false, rect =>
                    {
                        return GetAdvSetting(CheeseCommand.Thrumbo).Draw(rect);
                    });
                    break;
                default:
                    break;
            }
            DrawSectionNoListing(rightAdv, ref yR, "결과", true, rect =>
                {
                    float gapH = 8f;
                    float usedH = 0;
                    float curY = rect.y;
                    curY += gapH;
                    float sliderH = 34f;
                    Rect slidersRect = new Rect(rect.x, curY, rect.width, sliderH);
                    curY += sliderH;
                    UIUtil.SplitVerticallyByRatio(slidersRect, out Rect resultDonationRect, out Rect resultRandomRect, 0.5f, 8f);
                    resultDonationRect = resultDonationRect.ContractedBy(4f);
                    resultRandomRect = resultRandomRect.ContractedBy(4f);
                    UIUtil.SplitVerticallyByRatio(resultDonationRect, out Rect resultDonationLabel, out Rect resultDonationSlider, 0.3f, 4f);
                    UIUtil.SplitVerticallyByRatio(resultRandomRect, out Rect resultRandomLabel, out Rect resultRandomSlider, 0.3f, 4f);
                    UIUtil.DrawCenteredText(resultDonationLabel, "후원액 :");
                    resultDonation01 = Widgets.HorizontalSlider(resultDonationSlider, resultDonation01, 0f, 1f, true, label:$"{Mathf.RoundToInt(resultDonation01 * 100f)}%");
                    UIUtil.DrawCenteredText(resultRandomLabel, "랜덤성 :");
                    randomVar = Widgets.HorizontalSlider(resultRandomSlider, randomVar, 0f, 1f, true, label:$"{Mathf.RoundToInt(randomVar * 100f)}%");
                    TooltipHandler.TipRegion(
                            resultRandomRect,
                            () => "100%: 완전 랜덤(후원금액 반영 X)\n0%: 금액 정확 반영 (소액 위주 환경 비추천)\n80%: 적당히 섞인 추천 설정",
                            resultRandomRect.GetHashCode()
                        );
                    TooltipHandler.TipRegion(
                            resultDonationRect,
                            () => "0%: 최소 금액\n100%: 최대 금액",
                            resultDonationRect.GetHashCode()
                        );
                    curY += 4f;
                    Widgets.DrawLineHorizontal(rect.x, curY, rect.width);
                    curY += 4f;
                    usedH = curY - rect.y;
                    float resultH = Current.ProgramState == ProgramState.Playing ? GetAdvSetting(selectedConfigAdv.cmd).DrawResults(new Rect(rect.x, curY, rect.width, 1f)) : 0f;
                    return usedH + resultH;
                });

            int hash = Combine(GetAdvSetting(selectedConfigAdv.cmd).GetPreviewDirtyHash(), Quantize(resultDonation01), Quantize(randomVar), (int)selectedConfigAdv.cmd);

            _previewDebounce.Tick(hash, () =>
            {
                GetAdvSetting(selectedConfigAdv.cmd).UpdateResults(); // or GeneratePreview(); or whatever
            });
        }

        private void DrawCommandSection(Rect rect, float lineH, float paddingX, float paddingY)
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
            var ratios = new float[] { 0.1f, 0.1f, 0.16f, 0.25f, 0.19f, 0.20f };
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
                    ref c.maxDonation,
                    ref c.maxDonationBuf,
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
            var cols = new List<Rect>(ratios.Length);
            UIUtil.SplitVerticallyByRatios(rect, ratios, paddingX, cols);
            Rect cmdRect = cols[0];
            Rect enableRect  = cols[1];
            Rect srcRadioRect = cols[2];
            Rect minDonRect = cols[3];
            Rect maxDonRect = cols[4];
            Rect cdRect = cols[5];
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
            Widgets.Label(maxDonRect, "효과 상한 금액 (₩)");
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
            ref int maxDonation,
            ref string maxDonationBuf,
            ref int cooldownHours,
            ref string cooldownBuf)
        {
            Widgets.DrawHighlightIfMouseover(rect);
            var cols = new List<Rect>(ratios.Length);
            UIUtil.SplitVerticallyByRatios(rect, ratios, paddingX, cols);

            Rect cmdRect = cols[0];
            Rect enableRect  = cols[1];
            Rect srcRadioRect = cols[2];
            Rect minDonRect = cols[3];
            Rect maxDonRect = cols[4];
            Rect cdRect = cols[5];
            float btnSize = 24f;
            TooltipHandler.TipRegion(
                srcRadioRect,
                () => "채팅 선택 시 이벤트 효과가 랜덤하게 결정됩니다.",
                srcRadioRect.GetHashCode()
            );
            TooltipHandler.TipRegion(
                minDonRect,
                () => "최소/최대 금액이 동일하면 이벤트 효과가 랜덤하게 결정됩니다.",
                minDonRect.GetHashCode()
            );
            TooltipHandler.TipRegion(
                maxDonRect,
                () => "초과되는 금액은 최대 확률로 적용됩니다.",
                maxDonRect.GetHashCode()
            );
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
            UIUtil.SplitHorizontallyByRatio(
                srcRadioRect,
                out Rect srcChatRad,
                out Rect srcDonRad,
                0.5f,
                paddingY
            );
            UIUtil.SplitVerticallyByRatio(
                srcChatRad,
                out Rect chatRadBtn,
                out Rect chatRadDesc,
                0.3f,
                paddingX
            );
            UIUtil.SplitVerticallyByRatio(
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
            UIUtil.SplitHorizontallyByRatio(
                minDonRect,
                out Rect minDonSlider,
                out Rect minDonText,
                0.5f,
                paddingY
            );
            UIUtil.SplitVerticallyByRatio(
                minDonText,
                out Rect minDonField,
                out Rect minDonWarning,
                0.4f,
                paddingX
            );
            bool oldGui2 = GUI.enabled;
            GUI.enabled = enabled && source == CheeseCommandSource.Donation;
            UIUtil.IntFieldDigitsOnly(minDonField, ref minDonation, ref minDonationBuf, 0, maxDonation);
            float sliderDonValue = minDonation;
            int stepDon = 500;
            float newSliderDonValue = Widgets.HorizontalSlider(
                minDonSlider,
                sliderDonValue,
                minAllowedDonation,
                maxDonation,
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
            else if (minDonation > maxDonation)
            {
                Color prev = GUI.color;
                GUI.color = Color.yellow;
                oldAnchor = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(minDonWarning, "> 효과최대 금액");
                Text.Anchor = oldAnchor;
                GUI.color = prev;
            }
            //Maximum donation amount text field
            UIUtil.SplitHorizontallyByRatio(
                maxDonRect,
                out Rect maxDonEmpty,
                out Rect maxDonField,
                0.5f,
                paddingY
            );
            maxDonField = UIUtil.ResizeRectAligned(maxDonField, maxDonField.width*0.6f, maxDonField.height);
            UIUtil.IntFieldDigitsOnly(maxDonField, ref maxDonation, ref maxDonationBuf, 0, maxAllowedDonation);

            GUI.enabled = oldGui2;

            //Cooldown text field
            UIUtil.SplitHorizontallyByRatio(
                cdRect,
                out Rect cdSlider,
                out Rect cdText,
                0.5f,
                paddingY
            );
            UIUtil.SplitVerticallyByRatio(
                cdText,
                out Rect cdField,
                out Rect cdDesc,
                0.4f,
                paddingX
            );

            int maxAllowedCD = 1440;
            int minAllowedCD = 0;
            UIUtil.IntFieldDigitsOnly(cdField, ref cooldownHours, ref cooldownBuf, minAllowedCD, maxAllowedCD);

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
        private static int Quantize(float v, int scale = 1000)
        {
            return Mathf.RoundToInt(v * scale);
        }
        private static int Combine(params int[] values)
        {
            unchecked
            {
                int h = 17;
                for (int i = 0; i < values.Length; i++)
                    h = h * 31 + values[i];
                return h;
            }
        }
        public bool TryGetCommandConfig(CheeseCommand cmd, out CheeseCommandConfig cfg)
        {
            EnsureCommandConfigs();
            FixupCommandDefaults();
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
        private static int SnapToStep(int value, int step)
        {
            if (step <= 1) return value;
            return Mathf.RoundToInt((float)value / step) * step;
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
    }
}
