using System.IO;
using Verse;

namespace CheeseProtocol
{
    internal static class PathUtil
    {
        public static string DefaultDonationFilePath()
        {
            string root = GenFilePaths.SaveDataFolderPath;
            string dir = Path.Combine(root, "CheeseProtocol");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "donations.ndjson");
        }
    }
}
