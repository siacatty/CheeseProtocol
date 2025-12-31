using Verse;

namespace CheeseProtocol
{
    public class ProtocolContext
    {
        public DonationEvent Donation { get; }
        public Map Map { get; }

        public ProtocolContext(DonationEvent donation, Map map)
        {
            Donation = donation;
            Map = map;
        }
    }
}