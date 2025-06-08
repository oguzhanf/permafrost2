using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Permafrost.EdgeService.Services;
using System.Diagnostics;

namespace Permafrost.EdgeService;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure logging
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddEventLog();

        // Add services to the container
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() 
            { 
                Title = "Permafrost Edge Service API", 
                Version = "v1",
                Description = "Windows Edge Service for Active Directory data collection"
            });
        });

        // Add health checks
        builder.Services.AddHealthChecks()
            .AddCheck<ActiveDirectoryHealthCheck>("active_directory")
            .AddCheck<EventHubHealthCheck>("event_hub");

        // Add HTTP client
        builder.Services.AddHttpClient();

        // Configure options
        builder.Services.Configure<ActiveDirectoryConfiguration>(
            builder.Configuration.GetSection("ActiveDirectory"));
        builder.Services.Configure<EventHubConfiguration>(
            builder.Configuration.GetSection("EventHub"));
        builder.Services.Configure<ServiceConfiguration>(
            builder.Configuration.GetSection("Service"));
        builder.Services.Configure<ApiConfiguration>(
            builder.Configuration.GetSection("Api"));

        // Add custom services
        builder.Services.AddSingleton<IActiveDirectoryService, ActiveDirectoryService>();
        builder.Services.AddSingleton<IEventHubService, EventHubService>();
        builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();

        // Add OpenTelemetry
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracerProviderBuilder =>
            {
                tracerProviderBuilder
                    .AddSource("Permafrost.EdgeService")
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService("permafrost-edge-service", "1.0.0"))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddConsoleExporter();
            });

        // Configure Windows Service
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "Permafrost Edge Service";
        });

        // Add hosted service for background work
        builder.Services.AddHostedService<DataCollectionWorker>();

        var app = builder.Build();

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Permafrost Edge Service API v1");
                c.RoutePrefix = string.Empty; // Serve Swagger UI at root
            });
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHealthChecks("/health");

        // Only listen on localhost for security
        app.Urls.Clear();
        app.Urls.Add("https://localhost:5001");
        app.Urls.Add("http://localhost:5000");

        app.Run();
    }
}

public static class ActivitySourceProvider
{
    public static readonly ActivitySource ActivitySource = new("Permafrost.EdgeService");
}
