using Microsoft.AspNetCore.Mvc;
using WebCountry.Models;
using WebCountry.Services;

namespace WebCountry.Controllers
{
    [ApiController]
    [Route("api/ip")]
    public class IPLocationController : ControllerBase
    {
        private readonly IGeoIPService _geoIPService;
        private readonly ILogger<IPLocationController> _logger;

        public IPLocationController(IGeoIPService geoIPService, ILogger<IPLocationController> logger)
        {
            _geoIPService = geoIPService;
            _logger = logger;
        }

        /// <summary>
        /// Get country information for an IP address, client IP if empty, or database status if ipAddress is "status"
        /// </summary>
        /// <param name="ipAddress">The IP address to lookup, empty for client IP, or "status" for database status</param>
        /// <returns>Country information for the IP address or database status</returns>
        [HttpGet("{ipAddress?}")]
        public async Task<ActionResult> GetCountryByIPOrStatus([FromRoute] string? ipAddress = null)
        {
            // Special case: return database status
            if (string.Equals(ipAddress, "status", StringComparison.OrdinalIgnoreCase))
            {
                var status = _geoIPService.GetDatabaseStatus();
                return Ok(status);
            }

            // If no IP address provided, use client IP
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                ipAddress = GetClientIPAddress();

                if (string.IsNullOrEmpty(ipAddress))
                {
                    return BadRequest(new IPLocationResponse
                    {
                        Ip = "unknown",
                        IsSuccess = false,
                        Message = "Unable to determine client IP address"
                    });
                }

                _logger.LogInformation("Auto-detected client IP for lookup: {Ip}", ipAddress);
            }
            else
            {
                _logger.LogInformation("Looking up country for IP: {Ip}", ipAddress);
            }

            var result = await _geoIPService.GetCountryByIPAsync(ipAddress);

            // Success when IsSuccess is null (indicating success without error fields)
            if (result.IsSuccess == null)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }

        /// <summary>
        /// Update the MaxMind database manually
        /// </summary>
        /// <returns>Update status</returns>
        [HttpPost("update")]
        public async Task<ActionResult> UpdateDatabase()
        {
            _logger.LogInformation("Manual database update requested");

            var ip = GetClientIPAddress();

            if (ip != "127.0.0.1")
            {
                return StatusCode(400, new { message = "Unauthorized" });
            }

            try
            {
                var success = await _geoIPService.UpdateDatabaseAsync();

                if (success)
                {
                    return Ok(new { message = "Database updated successfully" });
                }
                else
                {
                    return StatusCode(500, new { message = "Failed to update database" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during manual database update");
                return StatusCode(500, new { message = "An error occurred while updating the database" });
            }
        }

        private string? GetClientIPAddress()
        {
            // Try to get IP from X-Forwarded-For header (if behind proxy)
            var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                var ips = forwardedFor.Split(',');
                if (ips.Length > 0)
                {
                    return ips[0].Trim();
                }
            }

            // Try X-Real-IP header
            var realIP = Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIP))
            {
                return realIP;
            }

            // Fall back to remote IP address
            return HttpContext.Connection.RemoteIpAddress?.ToString();
        }
    }
}
