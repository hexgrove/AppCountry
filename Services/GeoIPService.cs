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
        private readonly MaxMindOptions _maxMindOptions;
        private readonly IPInfoOptions _ipInfoOptions;
        private readonly ILogger<GeoIPService> _logger;
        private readonly HttpClient _httpClient;
        private DatabaseReader? _ipInfoReader;
        private DatabaseReader? _maxMindReader;
        private readonly object _lockObject = new();

        public GeoIPService(
            IOptions<MaxMindOptions> maxMindOptions,
            IOptions<IPInfoOptions> ipInfoOptions,
            ILogger<GeoIPService> logger,
            HttpClient httpClient)
        {
            _maxMindOptions = maxMindOptions.Value;
            _ipInfoOptions = ipInfoOptions.Value;
            _logger = logger;
            _httpClient = httpClient;
            InitializeDatabases();
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

                // Use lock to ensure thread safety when accessing readers
                lock (_lockObject)
                {
                    if (_ipInfoReader == null && _maxMindReader == null)
                    {
                        return new IPLocationResponse
                        {
                            Ip = ipAddress,
                            IsSuccess = false,
                            Message = "GeoIP database is not available. Please try again later."
                        };
                    }

                    // Try IPInfo first
                    if (_ipInfoReader != null)
                    {
                        try
                        {
                            var response = _ipInfoReader.Country(ip!);
                            return new IPLocationResponse
                            {
                                Ip = ipAddress,
                                Country = response.Country.IsoCode,
                                CountryName = response.Country.Name,
                                Source = "ipinfo",
                                IsSuccess = null,  // Success - don't include in JSON
                                Message = null     // Success - don't include in JSON
                            };
                        }
                        catch (AddressNotFoundException)
                        {
                            _logger.LogDebug("IP address {Ip} not found in IPInfo database, trying MaxMind", ipAddress);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error querying IPInfo database for IP {Ip}, trying MaxMind", ipAddress);
                        }
                    }

                    // Fallback to MaxMind
                    if (_maxMindReader != null)
                    {
                        try
                        {
                            var response = _maxMindReader.Country(ip!);
                            return new IPLocationResponse
                            {
                                Ip = ipAddress,
                                Country = response.Country.IsoCode,
                                CountryName = response.Country.Name,
                                Source = "maxmind",
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

                    return new IPLocationResponse
                    {
                        Ip = ipAddress,
                        IsSuccess = false,
                        Message = "IP address not found in database"
                    };
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
            var ipInfoSuccess = await UpdateIPInfoDatabaseAsync();
            var maxMindSuccess = await UpdateMaxMindDatabaseAsync();

            return ipInfoSuccess && maxMindSuccess;
        }

        private async Task<bool> UpdateIPInfoDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("Starting IPInfo database update");

                // Create data directory if it doesn't exist
                var dataDirectory = Path.GetDirectoryName(_ipInfoOptions.DatabasePath);
                if (!string.IsNullOrEmpty(dataDirectory) && !Directory.Exists(dataDirectory))
                {
                    Directory.CreateDirectory(dataDirectory);
                }

                // Download the database (direct .mmdb file, no need to extract)
                var downloadUrl = string.Format(_ipInfoOptions.DownloadUrl, _ipInfoOptions.Token);
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
                    _logger.LogError(ex, "Failed to download IPInfo database");
                    File.Delete(tempFile);
                    return false;
                }

                // Replace the old database
                lock (_lockObject)
                {
                    _ipInfoReader?.Dispose();
                    _ipInfoReader = null;

                    if (File.Exists(_ipInfoOptions.DatabasePath))
                    {
                        File.Delete(_ipInfoOptions.DatabasePath);
                    }

                    File.Move(tempFile, _ipInfoOptions.DatabasePath);
                    InitializeIPInfoDatabase();
                }

                _logger.LogInformation("IPInfo database updated successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update IPInfo database");
                return false;
            }
        }

        private async Task<bool> UpdateMaxMindDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("Starting MaxMind database update");

                // Create data directory if it doesn't exist
                var dataDirectory = Path.GetDirectoryName(_maxMindOptions.DatabasePath);
                if (!string.IsNullOrEmpty(dataDirectory) && !Directory.Exists(dataDirectory))
                {
                    Directory.CreateDirectory(dataDirectory);
                }

                // Download the database
                var downloadUrl = string.Format(_maxMindOptions.DownloadUrl, _maxMindOptions.LicenseKey);
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
                    _maxMindReader?.Dispose();
                    _maxMindReader = null;

                    if (File.Exists(_maxMindOptions.DatabasePath))
                    {
                        File.Delete(_maxMindOptions.DatabasePath);
                    }

                    File.Move(extractedPath, _maxMindOptions.DatabasePath);
                    InitializeMaxMindDatabase();
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
                return (_ipInfoReader != null && File.Exists(_ipInfoOptions.DatabasePath)) ||
                       (_maxMindReader != null && File.Exists(_maxMindOptions.DatabasePath));
            }
        }

        public DatabaseStatusResponse GetDatabaseStatus()
        {
            lock (_lockObject)
            {
                var response = new DatabaseStatusResponse();
                var statuses = new List<string>();

                // Check IPInfo database
                if (File.Exists(_ipInfoOptions.DatabasePath))
                {
                    try
                    {
                        var fileInfo = new FileInfo(_ipInfoOptions.DatabasePath);
                        var available = _ipInfoReader != null;
                        statuses.Add($"IPInfo: {(available ? "available" : "file exists but not loaded")} (last modified: {fileInfo.LastWriteTime})");

                        if (available && response.LastModified == null)
                        {
                            response.LastModified = fileInfo.LastWriteTime;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error getting IPInfo database file information");
                        statuses.Add("IPInfo: error accessing database file");
                    }
                }
                else
                {
                    statuses.Add("IPInfo: database file not found");
                }

                // Check MaxMind database
                if (File.Exists(_maxMindOptions.DatabasePath))
                {
                    try
                    {
                        var fileInfo = new FileInfo(_maxMindOptions.DatabasePath);
                        var available = _maxMindReader != null;
                        statuses.Add($"MaxMind: {(available ? "available" : "file exists but not loaded")} (last modified: {fileInfo.LastWriteTime})");

                        if (available && response.LastModified == null)
                        {
                            response.LastModified = fileInfo.LastWriteTime;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error getting MaxMind database file information");
                        statuses.Add("MaxMind: error accessing database file");
                    }
                }
                else
                {
                    statuses.Add("MaxMind: database file not found");
                }

                response.IsAvailable = _ipInfoReader != null || _maxMindReader != null;
                response.Message = string.Join("; ", statuses);

                return response;
            }
        }

        private void InitializeDatabases()
        {
            lock (_lockObject)
            {
                InitializeIPInfoDatabase();
                InitializeMaxMindDatabase();
            }
        }

        private void InitializeIPInfoDatabase()
        {
            try
            {
                _ipInfoReader?.Dispose();
                _ipInfoReader = null;

                if (File.Exists(_ipInfoOptions.DatabasePath))
                {
                    _ipInfoReader = new DatabaseReader(_ipInfoOptions.DatabasePath);
                    _logger.LogInformation("IPInfo database loaded from: {DatabasePath}", _ipInfoOptions.DatabasePath);
                }
                else
                {
                    _logger.LogWarning("IPInfo database not found at: {DatabasePath}", _ipInfoOptions.DatabasePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize IPInfo database");
                _ipInfoReader?.Dispose();
                _ipInfoReader = null;
            }
        }

        private void InitializeMaxMindDatabase()
        {
            try
            {
                _maxMindReader?.Dispose();
                _maxMindReader = null;

                if (File.Exists(_maxMindOptions.DatabasePath))
                {
                    _maxMindReader = new DatabaseReader(_maxMindOptions.DatabasePath);
                    _logger.LogInformation("MaxMind database loaded from: {DatabasePath}", _maxMindOptions.DatabasePath);
                }
                else
                {
                    _logger.LogWarning("MaxMind database not found at: {DatabasePath}", _maxMindOptions.DatabasePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize MaxMind database");
                _maxMindReader?.Dispose();
                _maxMindReader = null;
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
                _ipInfoReader?.Dispose();
                _ipInfoReader = null;
                _maxMindReader?.Dispose();
                _maxMindReader = null;
            }
        }
    }
}
