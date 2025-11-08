using Microsoft.Extensions.Options;
using WebCountry.Models;

namespace WebCountry.Services
{
    public class DatabaseUpdateService : BackgroundService
    {
        private readonly IGeoIPService _geoIPService;
        private readonly MaxMindOptions _maxMindOptions;
        private readonly IPInfoOptions _ipInfoOptions;
        private readonly ILogger<DatabaseUpdateService> _logger;

        public DatabaseUpdateService(
            IGeoIPService geoIPService,
            IOptions<MaxMindOptions> maxMindOptions,
            IOptions<IPInfoOptions> ipInfoOptions,
            ILogger<DatabaseUpdateService> logger)
        {
            _geoIPService = geoIPService;
            _maxMindOptions = maxMindOptions.Value;
            _ipInfoOptions = ipInfoOptions.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Database update service started");

            // Initial database check and download if not exists
            if (!_geoIPService.IsDatabaseAvailable())
            {
                _logger.LogInformation("Database not available, attempting initial download");
                await _geoIPService.UpdateDatabaseAsync();
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Use the minimum update interval to ensure both databases are updated regularly
                    var updateIntervalHours = Math.Min(_ipInfoOptions.UpdateIntervalHours, _maxMindOptions.UpdateIntervalHours);
                    var delay = TimeSpan.FromHours(updateIntervalHours);
                    _logger.LogInformation("Next database update scheduled in {Hours} hours", updateIntervalHours);

                    await Task.Delay(delay, stoppingToken);

                    if (!stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Starting scheduled database update");
                        var success = await _geoIPService.UpdateDatabaseAsync();

                        if (success)
                        {
                            _logger.LogInformation("Scheduled database update completed successfully");
                        }
                        else
                        {
                            _logger.LogWarning("Scheduled database update failed");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Database update service is stopping");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in database update service");
                    // Continue running even if there's an error
                    await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                }
            }

            _logger.LogInformation("Database update service stopped");
        }
    }
}

