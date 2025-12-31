namespace CheeseProtocol
{
    public class CheeseCommandConfig
    {
        public CheeseCommand cmd;
        public string label;                 // "!참여"
        public bool enabled = true;

        public CheeseCommandSource source = CheeseCommandSource.Donation;
        public int minDonation = 1000;
        public string minDonationBuf = "1000";

        public int cooldownSeconds = 0;
        public string cooldownBuf = "0";

        public string ScribeKeyPrefix => cmd.ToString(); // "Join" 같은 값
    }
}