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
        public static CheeseEvent MakeChatEvent(string user, string message, long msgTimeMs)
        {
            //string simDonationId = NewId();
            long receivedAtUtcMsNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return new CheeseEvent
            {
                donationId = null,
                username = user,
                message = message,
                amount = 0,
                isDonation = false,
                msgTimeMs = msgTimeMs,
                receivedAtUtcMs = receivedAtUtcMsNow,
                dedupeKey = $"{receivedAtUtcMsNow}|{user}|{0}|{message}"
            };
        }
        public static CheeseEvent MakeDonationEvent(string user, string message, int amount, string donationType, string donationId, long msgTimeMs)
        {
            long receivedAtUtcMsNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return new CheeseEvent
            {
                donationId = donationId,
                username = user,
                message = message,
                amount = amount,
                isDonation = true,
                donationType = donationType,
                msgTimeMs = msgTimeMs,
                receivedAtUtcMs = receivedAtUtcMsNow,
                dedupeKey = !string.IsNullOrWhiteSpace(donationId)
                            ? "don:" + donationId
                            : $"{receivedAtUtcMsNow}|{user}|{amount}|{message}"
            };
        }
    }
}