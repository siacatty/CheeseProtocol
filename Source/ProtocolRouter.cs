using System;
using Verse;

namespace CheeseProtocol
{
    public static class ProtocolRouter
    {
        public static void RouteAndExecute(DonationEvent donation)
        {
            if (donation == null) return;

            Map map = Find.AnyPlayerHomeMap ?? Find.CurrentMap;
            if (map == null)
            {
                Log.Warning("[CheeseProtocol] No map available; skipping donation: " + donation);
                return;
            }

            var ctx = new ProtocolContext(donation, map);

            string msg = (donation.message ?? "").Trim();
            CheeseCommand cmd = CheeseCommand.None;
            string args = string.Empty;

            if (msg.StartsWith("!"))
                cmd = CheeseCommandParser.Parse(msg, out args);

            var protocol = FindProtocolForDonation(donation, cmd);

            if (cmd == CheeseCommand.None || CheeseProtocolMod.Settings == null || !CheeseProtocolMod.Settings.TryGetCommandConfig(cmd, out var cfg))
                return;

            if (!IsProtocolAllowedBySettings(CheeseProtocolMod.Settings, cfg, donation))
                return;

            if (protocol == null)
            {
                Log.Warning("[CheeseProtocol] No protocol matched donation: " + donation);
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

        private static IProtocol FindProtocolForDonation(DonationEvent donation, CheeseCommand cmd)
        {
            string msg = (donation.message ?? "").Trim();
            if (cmd != CheeseCommand.None &&
                CheeseCommandParser.TryGetSpec(cmd, out var spec))
            {
                Log.Message($"[CheeseProtocol] command found: \"{spec.protocolId}\"");
                return ProtocolRegistry.ById(spec.protocolId);
            }
            Log.Message($"[CheeseProtocol] Unknown command ignored: \"{msg}\"");

            return ProtocolRegistry.ById("noop");
        }
        private static bool IsProtocolAllowedBySettings(CheeseSettings settings, CheeseCommandConfig cfg, DonationEvent ev)
        {
            if (!cfg.enabled) return false;

            if (cfg.source == CheeseCommandSource.Donation)
            {
                if (!ev.isDonation) return false;
                if (!(ev.amount >= cfg.minDonation))
                {
                    Log.Message($"[CheeseProtocol] Command ignored (<min_donation): \"{ev.message}\"");
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
                Log.Message($"[CheeseProtocol] Command ignored (on cooldown): \"{ev.message}\"");
                return false;
            }
            return true;
        }
    }
}