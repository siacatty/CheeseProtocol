using System;
using Verse;

namespace CheeseProtocol
{
    public static class ProtocolRouter
    {
        public static void RouteAndExecute(CheeseEvent evt)
        {
            if (evt == null) return;

            Map map = Find.AnyPlayerHomeMap;

            var ctx = new ProtocolContext(evt, map);

            string msg = (evt.message ?? "").Trim();
            CheeseCommand cmd = CheeseCommand.None;
            string args = string.Empty;

            if (msg.StartsWith("!"))
                cmd = CheeseCommandParser.Parse(msg, out args);

            var protocol = FindProtocolForDonation(evt, cmd);

            if (cmd == CheeseCommand.None || CheeseProtocolMod.Settings == null || !CheeseProtocolMod.Settings.TryGetCommandConfig(cmd, out var cfg))
                return;

            if (!IsProtocolAllowedBySettings(CheeseProtocolMod.Settings, cfg, evt))
                return;

            if (protocol == null)
            {
                Log.Warning("[CheeseProtocol] No protocol matched donation: " + evt);
                return;
            }
            if (map == null)
            {
                CheeseLetter.AlertFail(CheeseCommands.GetCommandText(cmd));
                Log.Warning("[CheeseProtocol] No map available; skipping donation: " + evt);
                return;
            }
            if (!protocol.CanExecute(ctx))
            {
                Log.Warning($"[CheeseProtocol] Protocol {protocol.Id} cannot execute right now.");
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
                Log.Error($"[CheeseProtocol] Protocol {protocol.Id} failed: {ex}");
            }
        }

        private static IProtocol FindProtocolForDonation(CheeseEvent evt, CheeseCommand cmd)
        {
            string msg = (evt.message ?? "").Trim();
            if (cmd != CheeseCommand.None &&
                CheeseCommandParser.TryGetSpec(cmd, out var spec))
            {
                Log.Message($"[CheeseProtocol] command found: \"{spec.protocolId}\"");
                return ProtocolRegistry.ById(spec.protocolId);
            }
            Log.Message($"[CheeseProtocol] Unknown command ignored: \"{msg}\"");

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
                    Log.Message($"[CheeseProtocol] Command ignored (<min_donation): \"{evt.message}\"");
                    return false;
                }
            }
            int nowTick = Find.TickManager.TicksGame;
            var cdState = CheeseCooldownState.Current;
            if (cdState == null)
            {
                Log.Warning("[CheeseProtocol] CooldownState missing; allow by default.");
                return true;
            }
            //Log.Warning($"[CheeseProtocol] command config cooldown: {cfg.cooldownHours} | last executed: {cdState.GetLastTick(cfg.cmd)} | now:  {nowTick}");

            if (!cdState.IsReady(cfg.cmd, cfg.cooldownHours, nowTick))
            {
                Log.Message($"[CheeseProtocol] Command ignored (on cooldown): \"{evt.message}\"");
                return false;
            }
            return true;
        }
    }
}