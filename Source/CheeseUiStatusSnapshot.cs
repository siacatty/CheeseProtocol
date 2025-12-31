using System;

namespace CheeseProtocol
{
    public sealed class CheeseUiStatusSnapshot
    {
        public ConnectionState connectionState;
        public string lastError;

        public int chatQueueCount;
        public int donationQueueCount;
        public int dedupeCountRecent;
        public int budgetPending;

        public CheeseUiStatusSnapshot Clone()
        {
            return (CheeseUiStatusSnapshot)MemberwiseClone();
        }
    }
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected
    }
}