namespace WebCountry.Models
{
    public class MaxMindOptions
    {
        public const string SectionName = "MaxMind";

        public string AccountId { get; set; } = string.Empty;
        public string LicenseKey { get; set; } = string.Empty;
        public string DatabasePath { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public int UpdateIntervalHours { get; set; } = 168; // Default 1 week
        public string DatabaseEdition { get; set; } = "GeoLite2-Country";
    }
}

