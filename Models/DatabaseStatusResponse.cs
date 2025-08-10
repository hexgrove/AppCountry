namespace WebCountry.Models
{
    public class DatabaseStatusResponse
    {
        public bool IsAvailable { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime? LastModified { get; set; }
    }
}

