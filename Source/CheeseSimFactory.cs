using System;
using Verse;

namespace CheeseProtocol
{
    public static class CheeseSimFactory
    {
        private static string NewId() => $"SIM-{Guid.NewGuid():N}";

        /// <summary>
        /// Chat 시뮬레이션: isDonation=false, amount=0
        /// </summary>
        /// 
        public static DonationEvent MakeChatLike(string user, string message)
        {
            string simDonationId = NewId();
            return new DonationEvent
            {
                donationId = simDonationId,
                donor = user,
                message = message,
                amount = 0,
                isDonation = false,
                receivedAtUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                dedupeKey = !string.IsNullOrWhiteSpace(simDonationId)
                            ? "don:" + simDonationId
                            : $"don:{user}|{0}|{message}"
            };
        }
        public static DonationEvent MakeDonation(string user, string message, int amount)
        {
            string simDonationId = NewId();
            return new DonationEvent
            {
                donationId = simDonationId,
                donor = user,
                message = message,
                amount = amount,
                isDonation = true,
                donationType = "CHAT",
                receivedAtUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                dedupeKey = !string.IsNullOrWhiteSpace(simDonationId)
                            ? "don:" + simDonationId
                            : $"don:{user}|{amount}|{message}"
            };
        }
    }
}