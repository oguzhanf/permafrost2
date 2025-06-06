using Permafrost2.DomainControllerAgent;
using Permafrost2.DomainControllerAgent.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configure as Windows Service
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Permafrost2 Domain Controller Agent";
});

// Add HTTP client for API communication
builder.Services.AddHttpClient<IApiClient, ApiClient>(client =>
{
    var baseUrl = builder.Configuration.GetValue<string>("Permafrost2:ApiBaseUrl") ?? "https://localhost:7035";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("User-Agent", "Permafrost2-DomainControllerAgent/1.0.0");
});

// Add services
builder.Services.AddScoped<IDomainDataCollector, DomainDataCollector>();
builder.Services.AddScoped<Permafrost2.DomainControllerAgent.Services.IConfigurationManager, Permafrost2.DomainControllerAgent.Services.ConfigurationManager>();

// Add the main worker service
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
