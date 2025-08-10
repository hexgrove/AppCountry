using WebCountry.Models;

namespace WebCountry.Services
{
    public interface IGeoIPService
    {
        Task<IPLocationResponse> GetCountryByIPAsync(string ipAddress);
        Task<bool> UpdateDatabaseAsync();
        bool IsDatabaseAvailable();
        DatabaseStatusResponse GetDatabaseStatus();
    }
}
