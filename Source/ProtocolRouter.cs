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

            // Only treat as command if message starts with "!"
            // (prevents accidental triggers)
            if (msg.StartsWith("!"))
            {
                // You can allow multiple aliases per command
                if (StartsWithCommand(msg, "!참여"))
                    return ProtocolRegistry.ById("colonist");

                if (StartsWithCommand(msg, "!습격"))
                    return ProtocolRegistry.ById("raid");

                if (StartsWithCommand(msg, "!상단"))
                    return ProtocolRegistry.ById("caravan");
            }

            // Default behavior if no command found:
            // choose what you want (colonist, or "none")
            return ProtocolRegistry.ById("colonist");
        }
        private static bool StartsWithCommand(string msg, string command)
        {
            // Matches:
            // "!참여"
            // "!참여 hello"
            // "!참여\t..."
            if (!msg.StartsWith(command)) return false;
            if (msg.Length == command.Length) return true;

            char next = msg[command.Length];
            return char.IsWhiteSpace(next);
        }
    }
}