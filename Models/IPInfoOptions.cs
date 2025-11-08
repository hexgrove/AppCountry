namespace WebCountry.Models
{
    public class IPInfoOptions
    {
        public const string SectionName = "IPInfo";

        public string Token { get; set; } = string.Empty;
        public string DatabasePath { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public int UpdateIntervalHours { get; set; } = 168; // Default 1 week
        public string DatabaseEdition { get; set; } = "ipinfo_lite";
    }
}
