# WebCountry - IP to Country Lookup API

A .NET 6 Web API service that provides IP address to country lookup functionality using MaxMind GeoLite2 database.

## Features

- RESTful API for IP geolocation lookup
- Automatic MaxMind database updates (configurable interval)
- Support for both IPv4 and IPv6 addresses
- Client IP detection for "my location" queries
- Manual database update endpoint
- Database status monitoring
- Comprehensive error handling and logging

## Configuration

1. Register for a free MaxMind account at https://www.maxmind.com/en/geolite2/signup
2. Obtain your Account ID and License Key from MaxMind dashboard
3. Update `appsettings.json` with your MaxMind credentials:

```json
{
  "MaxMind": {
    "AccountId": "YOUR_ACCOUNT_ID",
    "LicenseKey": "YOUR_LICENSE_KEY",
    "DatabasePath": "Data/GeoLite2-Country.mmdb",
    "DownloadUrl": "https://download.maxmind.com/app/geoip_download?edition_id=GeoLite2-Country&license_key={0}&suffix=tar.gz",
    "UpdateIntervalHours": 168,
    "DatabaseEdition": "GeoLite2-Country"
  }
}
```

## Installation

1. Clone the repository
2. Install dependencies:
   ```
   dotnet restore
   ```
3. Configure your MaxMind credentials in `appsettings.json`
4. Run the application:
   ```
   dotnet run
   ```

## API Endpoints

The API provides 2 simple endpoints:

### 1. Get Location Information (GET)

#### Get Client's Location
```
GET /api/ip
```
Returns the geolocation of the requesting client's IP address.

#### Get Specific IP Location
```
GET /api/ip/{ipAddress}
```
Returns the geolocation of the specified IP address.

Examples:
```
GET /api/ip                    # Get client's location
GET /api/ip/8.8.8.8           # Lookup Google DNS
GET /api/ip/1.1.1.1           # Lookup Cloudflare DNS
```

#### Get Database Status
```
GET /api/ip/status
```
Returns detailed information about the MaxMind database status.

**Success Response** (clean, data-only):
```json
{
  "ip": "8.8.8.8",
  "country": "US",
  "countryName": "United States"
}
```

**Error Response** (includes error details):
```json
{
  "ip": "192.168.1.1",
  "country": null,
  "countryName": null,
  "isSuccess": false,
  "message": "Private IP addresses do not have geolocation data"
}
```

**Database Status Response**:
```json
{
  "isAvailable": true,
  "message": "Database is available and loaded",
  "lastModified": "2023-12-15T10:30:45.123Z"
}
```

### 2. Update Database (POST)

```
POST /api/ip/update
```

Manually triggers a database update from MaxMind.

**Security**: Only accessible from localhost (127.0.0.1) for security reasons.

**Response**:
```json
{
  "message": "Database updated successfully"
}
```

## Configuration Options

### MaxMind Settings
- `AccountId`: Your MaxMind account ID
- `LicenseKey`: Your MaxMind license key
- `DatabasePath`: Local path where the database file will be stored
- `DownloadUrl`: MaxMind download URL template
- `UpdateIntervalHours`: How often to check for database updates (default: 168 hours = 1 week)
- `DatabaseEdition`: MaxMind database edition (GeoLite2-Country)

### Swagger Documentation Settings
- `EnableSwaggerInProduction`: Set to `true` to enable Swagger in production environment (default: `false`)

**Production Swagger Access:**
- Development: `https://yourapp.com/swagger`
- Production: `https://yourapp.com/docs` (when enabled)

## Error Handling

The API provides comprehensive error handling:

- Invalid IP address format
- IP address not found in database
- Database unavailable
- Network errors during database updates

All errors are returned with appropriate HTTP status codes and descriptive error messages.

## Logging

The application uses ASP.NET Core built-in logging. Log levels can be configured in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "WebCountry.Services": "Information"
    }
  }
}
```

## Swagger/OpenAPI Documentation

### Development Environment
Swagger is automatically available at: `http://localhost:5000/swagger`

### Production Environment
By default, Swagger is disabled in production for security reasons. To enable:

1. **Method 1**: Configuration file
   ```json
   {
     "EnableSwaggerInProduction": true
   }
   ```

2. **Method 2**: Environment variable
   ```bash
   export EnableSwaggerInProduction=true
   ```

3. **Method 3**: Command line
   ```bash
   dotnet run --EnableSwaggerInProduction=true
   ```

When enabled in production, Swagger will be available at: `https://yourapp.com/docs`

### Common Swagger Issues

**Issue 1**: "ipAddress parameter required in Swagger UI"
- **Solution**: Leave the field empty for client IP detection, or enter a space character
- The API correctly handles empty/null values despite Swagger UI display

**Issue 2**: "Can't access Swagger in production"
- **Cause**: Swagger is disabled by default in production environments
- **Solution**: Enable using one of the methods above

**Issue 3**: "Fetch error - Not Found swagger.json" in production
- **Error**: `Not Found http://yourserver.com:3001/docs/v1/swagger.json`
- **Cause**: Swagger JSON path mismatch in production environment
- **Solution**: This is automatically fixed in the latest configuration
- **Verification**: After deployment, check that both URLs work:
  - Swagger UI: `http://yourserver.com:3001/docs`
  - Swagger JSON: `http://yourserver.com:3001/docs/v1/swagger.json`

## Security Considerations

- Store MaxMind credentials securely (consider using environment variables or Azure Key Vault)
- Implement rate limiting for production use
- Consider adding authentication for sensitive endpoints like manual database updates
- Validate and sanitize all input IP addresses
- **Swagger in Production**: Only enable if necessary, consider adding authentication

## License

This project uses MaxMind GeoLite2 data. Please review MaxMind's license terms at:
https://dev.maxmind.com/geoip/geolite2-free-geolocation-data

## Support

For MaxMind-related issues, visit: https://support.maxmind.com/
For API-specific issues, please create an issue in this repository.
