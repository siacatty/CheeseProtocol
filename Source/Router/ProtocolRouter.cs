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
            Map homeMap = Find.AnyPlayerHomeMap;
            var ctx = new ProtocolContext(evt, homeMap);
            var protocol = FindProtocol(evt, cmd);

            if (CheeseProtocolMod.Settings == null || !CheeseProtocolMod.Settings.TryGetCommandConfig(cmd, out var cfg))
                return;

            if (!IsProtocolAllowedBySettings(CheeseProtocolMod.Settings, cfg, evt))
                return;

            if (protocol == null)
            {
                QWarn("No protocol matched donation: " + evt);
                return;
            }
            if (homeMap == null)
            {
                CheeseLetter.AlertFail(CheeseCommands.GetCommandText(cmd));
                QWarn("No map available; skipping donation: " + evt);
                return;
            }
            if (!protocol.CanExecute(ctx))
            {
                QWarn($"Protocol {protocol.Id} cannot execute right now.");
                return;
            }
            try
            {
                protocol.Execute(ctx);
                var cdState = CheeseCooldownState.Current;
                cdState.MarkExecuted(cfg.cmd, Find.TickManager.TicksGame);
            }
            catch (Exception ex)
            {
                QErr($"Protocol {protocol.Id} failed: {ex}");
            }
        }

        private static IProtocol FindProtocol(CheeseEvent evt, CheeseCommand cmd)
        {
            string msg = (evt.message ?? "").Trim();
            if (cmd != CheeseCommand.None &&
                CheeseCommandParser.TryGetSpec(cmd, out var spec))
            {
                QMsg($"Command found: \"{spec.protocolId}\"", Channel.Debug);
                return ProtocolRegistry.ById(spec.protocolId);
            }
            QMsg($"Unknown command - Redirect to SpeechBubble: \"{msg}\"", Channel.Debug);

            return ProtocolRegistry.ById("noop");
        }
        private static bool IsProtocolAllowedBySettings(CheeseSettings settings, CheeseCommandConfig cfg, CheeseEvent evt)
        {
            if (!cfg.enabled) return false;

            if (cfg.source == CheeseCommandSource.Donation)
            {
                if (!evt.isDonation) return false;
                if (!(evt.amount >= cfg.minDonation))
                {
                    QMsg($"Command ignored (<min_donation): \"{evt.message}\"", Channel.Debug);
                    return false;
                }
            }
            int nowTick = Find.TickManager.TicksGame;
            var cdState = CheeseCooldownState.Current;
            if (cdState == null)
            {
                QWarn("CooldownState missing; allow by default.", Channel.Debug);
                return true;
            }
            if (!cdState.IsReady(cfg.cmd, cfg.cooldownHours, nowTick))
            {
                QMsg($"Command ignored (on cooldown): \"{evt.message}\"", Channel.Debug);
                return false;
            }
            return true;
        }
    }
}