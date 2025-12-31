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

            // Simple routing v1:
            // - Always spawn colonist for now
            // - Later: amount tiers + keywords + random
            var protocol = FindProtocolForDonation(donation);

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
            }
            catch (Exception ex)
            {
                Log.Error($"[CheeseProtocol] Protocol {protocol.Id} failed: {ex}");
            }
        }

        private static IProtocol FindProtocolForDonation(DonationEvent donation)
        {
            string msg = (donation.message ?? "").Trim();
            if (msg.StartsWith("!"))
            {
                var cmd = CheeseCommandParser.Parse(msg, out var args);
                Log.Message($"[CheeseProtocol] message starts with ! : \"{msg}\"");
                if (cmd != CheeseCommand.None &&
                    CheeseCommandParser.TryGetSpec(cmd, out var spec))
                {
                    Log.Message($"[CheeseProtocol] command found: \"{spec.protocolId}\"");
                    if (IsCommandAllowedBySettings(CheeseProtocolMod.Settings, cmd, donation))
                        return ProtocolRegistry.ById(spec.protocolId);
                    else
                    {
                        Log.Message($"[CheeseProtocol] Command ignored (<min_donation): \"{msg}\"");
                    }
                }
                Log.Message($"[CheeseProtocol] Unknown command ignored: \"{msg}\"");
            }

            return ProtocolRegistry.ById("noop");
        }
        private static bool IsCommandAllowedBySettings(CheeseSettings settings, CheeseCommand cmd, DonationEvent ev)
        {
            if (settings == null) return false;

            if (!settings.TryGetCommandConfig(cmd, out var cfg))
                return false;

            if (!cfg.enabled) return false;

            // Chat 모드면 채팅/도네 둘 다 OK, 금액 무시
            if (cfg.source == CheeseCommandSource.Chat)
                return true;

            // Donation 모드면 도네만 + 최소금액
            if (cfg.source == CheeseCommandSource.Donation)
                return ev.amount >= cfg.minDonation;

            return false;
        }
    }
}