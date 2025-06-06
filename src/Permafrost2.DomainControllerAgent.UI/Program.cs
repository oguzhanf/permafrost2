using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Permafrost2.DomainControllerAgent.UI.Services;

namespace Permafrost2.DomainControllerAgent.UI;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        var host = CreateHostBuilder().Build();

        using var scope = host.Services.CreateScope();
        var mainForm = scope.ServiceProvider.GetRequiredService<MainForm>();

        Application.Run(mainForm);
    }

    private static IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Add logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.AddDebug();
                });

                // Add HTTP client for API communication
                services.AddHttpClient<IApiClient, ApiClient>(client =>
                {
                    client.BaseAddress = new Uri("https://localhost:7035");
                    client.DefaultRequestHeaders.Add("User-Agent", "Permafrost2-DomainControllerAgent-UI/1.0.0");
                });

                // Add services
                services.AddScoped<IAgentServiceManager, AgentServiceManager>();
                services.AddScoped<MainForm>();
            });
    }
}