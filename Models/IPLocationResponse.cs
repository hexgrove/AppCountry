using System.Text.Json.Serialization;

namespace WebCountry.Models
{
    public class IPLocationResponse
    {
        public string Ip { get; set; } = string.Empty;

        /// <summary>
        /// ISO Country Code, such as "US"
        /// </summary>
        public string? Country { get; set; }

        /// <summary>
        /// Full country name, such as "United States"
        /// </summary>
        public string? CountryName { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Source { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? IsSuccess { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Message { get; set; }
    }
}
