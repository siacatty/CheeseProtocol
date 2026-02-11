using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using static CheeseProtocol.CheeseLog;
namespace CheeseProtocol
{
    public static class ProtocolRouter
    {
        public static void RouteAndExecute(CheeseEvent evt)
        {
            if (evt == null) return;
            CheeseCommand cmd = CheeseCommand.None;

            //string msg = (evt.message ?? "").Trim();
            //CheeseCommand cmd = CheeseCommand.None;

            cmd = evt.cmd;

            if (cmd == CheeseCommand.None && (CheeseProtocolMod.Settings?.allowSpeechBubble ?? false)) //SpeechBubble only
            {
                string username = evt.username;
                string message = evt.message ?? "";
                List<ParticipantRecord> records;
                if (!CheeseParticipantRegistry.Get().TryGetRecords(username, out records))
                    return;

                var candidates = records
                    .Select(r => r.pawn)
                    .Where(p => p != null && p.Name != null)
                    .Where(p => p.Name.ToStringShort == username)
                    .ToList();

                Pawn pawn;
                if (candidates.Count > 0)
                    pawn = candidates.RandomElement();
                else
                    pawn = records.Select(r => r.pawn).Where(p => p != null).RandomElement();
                if (pawn == null)
                {
                    QMsg("No matching pawn is found");
                    return;
                }
                var registry = CheeseParticipantRegistry.Get();
                if (registry == null) return;

                if (registry.GetPawnStatus(pawn) != ParticipantPawnStatus.OkOnMap) return;

                SpeechBubbleManager.Get(pawn.Map)?.AddChat(username, message, pawn);
                return;
            }
            var protocol = FindProtocol(cmd);

            if (CheeseProtocolMod.Settings == null || !CheeseProtocolMod.Settings.TryGetCommandConfig(cmd, out var cfg))
                return;

            if (!IsProtocolAllowedBySettings(CheeseProtocolMod.Settings, cfg, evt))
                return;

            if (protocol == null)
            {
                QWarn("No protocol matched donation: " + evt);
                return;
            }
            var allowPendingCmd = CheeseProtocolMod.Settings?.allowQueueCDCmd ?? false;
            if (IsProtocolCD_Ready(cfg) && CanExecuteProtocol(evt))
            {
                try
                {
                    Map homeMap = Find.AnyPlayerHomeMap;
                    var ctx = new ProtocolContext(evt, homeMap);
                    protocol.Execute(ctx);
                    var cdState = CheeseCooldownState.Current;
                    cdState.MarkExecuted(cfg.cmd, Find.TickManager.TicksGame);
                }
                catch (Exception ex)
                {
                    CheeseLetter.AlertFail(CheeseCommands.GetCommandText(cmd), $"Error: {ex}");
                    QErr($"Protocol {protocol.Id} failed: {ex}");
                }
            }
            else
            {
                if (allowPendingCmd)
                {
                    CheeseLetter.AlertCmdOnCd(CheeseCommands.GetCommandText(cmd));
                    CheeseCommandQueue.Current?.Enqueue(evt);
                }
                else
                {
                    CheeseLetter.AlertFail(CheeseCommands.GetCommandText(cfg.cmd), "쿨타임 대기 중.\n쿨타임 종료 시 자동 실행을 허용 하려면 설정을 변경해주세요.");
                }
            }
        }

        public static bool TryExecuteFromQueue(CheeseEvent evt)
        {
            if (evt == null) return false;
            CheeseCommand cmd = evt.cmd;
            
            var protocol = FindProtocol(cmd);

            if (CheeseProtocolMod.Settings == null || !CheeseProtocolMod.Settings.TryGetCommandConfig(cmd, out var cfg))
                return false;

            if (!IsProtocolAllowedBySettings(CheeseProtocolMod.Settings, cfg, evt))
                return false;

            if (protocol == null)
            {
                QWarn("No protocol matched donation: " + evt);
                return false;
            }
            try
            {
                Map homeMap = Find.AnyPlayerHomeMap;
                var ctx = new ProtocolContext(evt, homeMap);
                protocol.Execute(ctx);
                var cdState = CheeseCooldownState.Current;
                cdState.MarkExecuted(cfg.cmd, Find.TickManager.TicksGame);
            }
            catch (Exception ex)
            {
                CheeseLetter.AlertFail(CheeseCommands.GetCommandText(cmd), $"Error: {ex}");
                QErr($"Protocol {protocol.Id} failed: {ex}");
                return false;
            }
            return true;
        }

        public static bool CanExecuteProtocol(CheeseEvent evt)
        {
            var ctx = new ProtocolContext(evt, Find.AnyPlayerHomeMap);
            var protocol = FindProtocol(evt.cmd);
            if (protocol == null) return false;
            return protocol.CanExecute(ctx);
        }

        private static IProtocol FindProtocol(CheeseCommand cmd)
        {
            if (cmd != CheeseCommand.None &&
                CheeseCommandParser.TryGetSpec(cmd, out var spec))
            {
                return ProtocolRegistry.ById(spec.protocolId);
            }

            return ProtocolRegistry.ById("noop");
        }

        private static bool IsProtocolAllowedBySettings(CheeseSettings settings, CheeseCommandConfig cfg, CheeseEvent evt)
        {
            if (!cfg.enabled) return false;

            if (cfg.source == CheeseCommandSource.Donation)
            {
                if (!evt.isDonation) return false;
                if (!(evt.amount >= cfg.minDonation)) return false;
            }
            return true;
        }
        private static bool IsProtocolCD_Ready(CheeseCommandConfig cfg)
        {
            int nowTick = Find.TickManager.TicksGame;
            var cdState = CheeseCooldownState.Current;
            if (cdState == null)
            {
                QWarn("CooldownState missing; disallow by default.", Channel.Debug);
                return false;
            }
            if (!cdState.IsReady(cfg.cmd, cfg.cooldownHours, nowTick))
            {
                return false;
            }
            return true;
        }
    }
}