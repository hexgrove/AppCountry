using System.Text.Json.Serialization;

namespace WebCountry.Models
{
    public class IPLocationResponse
    {
        public string Ip { get; set; } = string.Empty;
        public string? Country { get; set; }
        public string? CountryName { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? IsSuccess { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Message { get; set; }
    }
}
