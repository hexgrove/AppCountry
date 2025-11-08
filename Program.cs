using WebCountry.Models;
using WebCountry.Services;

namespace WebCountry
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // Configure MaxMind options
            builder.Services.Configure<MaxMindOptions>(
                builder.Configuration.GetSection(MaxMindOptions.SectionName));

            // Configure IPInfo options
            builder.Services.Configure<IPInfoOptions>(
                builder.Configuration.GetSection(IPInfoOptions.SectionName));

            // Add HttpClient for downloading database
            builder.Services.AddHttpClient<GeoIPService>();

            // Register GeoIP service
            builder.Services.AddSingleton<IGeoIPService, GeoIPService>();

            // Register background service for database updates
            builder.Services.AddHostedService<DatabaseUpdateService>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            var enableSwagger = app.Environment.IsDevelopment() ||
                               builder.Configuration.GetValue<bool>("EnableSwaggerInProduction", false);

            if (enableSwagger)
            {
                app.UseSwagger(c =>
                {
                    if (!app.Environment.IsDevelopment())
                    {
                        c.RouteTemplate = "docs/{documentName}/swagger.json";
                    }
                });

                app.UseSwaggerUI(c =>
                {
                    if (!app.Environment.IsDevelopment())
                    {
                        c.RoutePrefix = "docs"; // Production: use /docs instead of /swagger
                        c.SwaggerEndpoint("/docs/v1/swagger.json", "WebCountry API V1");
                        c.DocumentTitle = "WebCountry API Documentation";
                    }
                    else
                    {
                        c.SwaggerEndpoint("/swagger/v1/swagger.json", "WebCountry API V1");
                    }
                });
            }

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
