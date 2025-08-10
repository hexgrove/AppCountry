using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;
using Microsoft.Extensions.Options;
using WebCountry.Models;
using WebCountry.Utils;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

namespace WebCountry.Services
{
    public class GeoIPService : IGeoIPService, IDisposable
    {
        private readonly MaxMindOptions _options;
        private readonly ILogger<GeoIPService> _logger;
        private readonly HttpClient _httpClient;
        private DatabaseReader? _reader;
        private readonly object _lockObject = new();

        public GeoIPService(IOptions<MaxMindOptions> options, ILogger<GeoIPService> logger, HttpClient httpClient)
        {
            _options = options.Value;
            _logger = logger;
            _httpClient = httpClient;
            InitializeDatabase();
        }

        public async Task<IPLocationResponse> GetCountryByIPAsync(string ipAddress)
        {
            await Task.CompletedTask;
            try
            {
                // Validate IP address format and check if it's suitable for geolocation
                if (!IPAddressValidator.IsValidForGeolocation(ipAddress, out var ip, out var validationError))
                {
                    return new IPLocationResponse
                    {
                        Ip = ipAddress,
                        IsSuccess = false,
                        Message = validationError
                    };
                }

                // Use lock to ensure thread safety when accessing _reader
                lock (_lockObject)
                {
                    if (_reader == null)
                    {
                        return new IPLocationResponse
                        {
                            Ip = ipAddress,
                            IsSuccess = false,
                            Message = "GeoIP database is not available. Please try again later."
                        };
                    }

                    try
                    {
                        var response = _reader.Country(ip!);
                        return new IPLocationResponse
                        {
                            Ip = ipAddress,
                            Country = response.Country.IsoCode,
                            CountryName = response.Country.Name,
                            IsSuccess = null,  // Success - don't include in JSON
                            Message = null     // Success - don't include in JSON
                        };
                    }
                    catch (AddressNotFoundException)
                    {
                        return new IPLocationResponse
                        {
                            Ip = ipAddress,
                            IsSuccess = false,
                            Message = "IP address not found in database"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while looking up IP address: {Ip}", ipAddress);
                return new IPLocationResponse
                {
                    Ip = ipAddress,
                    IsSuccess = false,
                    Message = "An error occurred while processing the request"
                };
            }
        }

        public async Task<bool> UpdateDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("Starting MaxMind database update");

                // Create data directory if it doesn't exist
                var dataDirectory = Path.GetDirectoryName(_options.DatabasePath);
                if (!string.IsNullOrEmpty(dataDirectory) && !Directory.Exists(dataDirectory))
                {
                    Directory.CreateDirectory(dataDirectory);
                }

                // Download the database
                var downloadUrl = string.Format(_options.DownloadUrl, _options.LicenseKey);
                var tempFile = Path.GetTempFileName();

                try
                {
                    using var response = await _httpClient.GetAsync(downloadUrl);
                    response.EnsureSuccessStatusCode();

                    await using var fileStream = File.Create(tempFile);
                    await response.Content.CopyToAsync(fileStream);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to download MaxMind database");
                    File.Delete(tempFile);
                    return false;
                }

                // Extract the database from tar.gz
                var extractedPath = await ExtractDatabaseAsync(tempFile);
                if (string.IsNullOrEmpty(extractedPath))
                {
                    File.Delete(tempFile);
                    return false;
                }

                // Replace the old database
                lock (_lockObject)
                {
                    _reader?.Dispose();
                    _reader = null;

                    if (File.Exists(_options.DatabasePath))
                    {
                        File.Delete(_options.DatabasePath);
                    }

                    File.Move(extractedPath, _options.DatabasePath);
                    InitializeDatabase();
                }

                File.Delete(tempFile);
                _logger.LogInformation("MaxMind database updated successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update MaxMind database");
                return false;
            }
        }

        public bool IsDatabaseAvailable()
        {
            lock (_lockObject)
            {
                return _reader != null && File.Exists(_options.DatabasePath);
            }
        }

        public DatabaseStatusResponse GetDatabaseStatus()
        {
            lock (_lockObject)
            {
                var response = new DatabaseStatusResponse();

                if (File.Exists(_options.DatabasePath))
                {
                    try
                    {
                        var fileInfo = new FileInfo(_options.DatabasePath);
                        response.IsAvailable = _reader != null;
                        response.LastModified = fileInfo.LastWriteTime;
                        response.Message = response.IsAvailable
                            ? "Database is available and loaded"
                            : "Database file exists but not loaded";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error getting database file information");
                        response.IsAvailable = false;
                        response.Message = "Error accessing database file";
                    }
                }
                else
                {
                    response.IsAvailable = false;
                    response.Message = "Database file not found";
                }

                return response;
            }
        }

        private void InitializeDatabase()
        {
            lock (_lockObject)
            {
                try
                {
                    _reader?.Dispose();
                    _reader = null;

                    if (File.Exists(_options.DatabasePath))
                    {
                        _reader = new DatabaseReader(_options.DatabasePath);
                        _logger.LogInformation("MaxMind database loaded from: {DatabasePath}", _options.DatabasePath);
                    }
                    else
                    {
                        _logger.LogWarning("MaxMind database not found at: {DatabasePath}", _options.DatabasePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize MaxMind database");
                    _reader?.Dispose();
                    _reader = null;
                }
            }
        }

        private async Task<string?> ExtractDatabaseAsync(string tarGzPath)
        {
            try
            {
                var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDirectory);

                // Extract tar.gz file using SharpZipLib
                await Task.Run(() =>
                {
                    using var fileStream = File.OpenRead(tarGzPath);
                    using var gzipStream = new GZipInputStream(fileStream);
                    using var tarArchive = TarArchive.CreateInputTarArchive(gzipStream, System.Text.Encoding.UTF8);

                    tarArchive.ExtractContents(tempDirectory);
                });

                // Find the .mmdb file in the extracted content
                var mmdbFiles = Directory.GetFiles(tempDirectory, "*.mmdb", SearchOption.AllDirectories);
                if (mmdbFiles.Length > 0)
                {
                    var mmdbFile = mmdbFiles[0];
                    var targetPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mmdb");
                    File.Copy(mmdbFile, targetPath);

                    // Cleanup temp directory
                    Directory.Delete(tempDirectory, true);

                    return targetPath;
                }

                // Cleanup temp directory
                Directory.Delete(tempDirectory, true);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract MaxMind database");
                return null;
            }
        }

        public void Dispose()
        {
            lock (_lockObject)
            {
                _reader?.Dispose();
                _reader = null;
            }
        }
    }
}
