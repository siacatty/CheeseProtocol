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

        public int cooldownHours = 0;
        public string cooldownBuf = "0";
        public string ScribeKeyPrefix => cmd.ToString(); // "Join" 같은 값

        public void EnsureBuffers()
        {
            if (string.IsNullOrEmpty(minDonationBuf))
                minDonationBuf = minDonation.ToString();
            if (string.IsNullOrEmpty(cooldownBuf))
                cooldownBuf = cooldownHours.ToString();
            if (minDonationBuf == "0" && minDonation != 0)
                minDonationBuf = minDonation.ToString();
            if (cooldownBuf == "0" && cooldownHours != 0)
                cooldownBuf = cooldownHours.ToString();
        }
    }
}