# Permafrost Edge Service

A lightweight .NET 8 Windows Service that runs on domain controllers to collect Active Directory data and publish it to Azure Event Hubs.

## Overview

The Edge Service is **Tier A** of the Permafrost Identity Management System. It provides a secure bridge between Active Directory and the main cross-platform application.

### Key Features

- **Lightweight**: ≤ 500 lines of core logic
- **Secure**: Runs under gMSA, localhost-only endpoints
- **Reliable**: Windows Service with health checks
- **Observable**: OpenTelemetry instrumentation
- **RESTful**: Simple API endpoints for AD data

## Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Active        │    │  Edge Service    │    │  Azure Event    │
│   Directory     │───▶│  (.NET 8)        │───▶│  Hubs           │
│                 │    │  - REST API      │    │                 │
│                 │    │  - Background    │    │                 │
│                 │    │    Worker        │    │                 │
└─────────────────┘    └──────────────────┘    └─────────────────┘
```

## API Endpoints

### Users
- `GET /api/users` - Get paginated list of users
- `GET /api/users/{id}` - Get specific user by Object GUID

### Groups  
- `GET /api/groups` - Get paginated list of groups
- `GET /api/groups/{id}` - Get specific group by Object GUID

### Events
- `GET /api/events` - Get paginated list of events

### Health
- `GET /health` - Health check endpoint

### Documentation
- `GET /` - Swagger UI (development only)

## Configuration

### appsettings.json

```json
{
  "ActiveDirectory": {
    "DomainController": "dc01.example.com",
    "Port": 389,
    "UseSsl": false,
    "Username": "",
    "Password": "",
    "SearchBase": "DC=example,DC=com",
    "QueryIntervalMinutes": 5,
    "MaxResults": 1000
  },
  "EventHub": {
    "ConnectionString": "Endpoint=sb://...",
    "HubName": "permafrost-events",
    "BatchSize": 100,
    "MaxWaitTimeSeconds": 30
  },
  "Service": {
    "InstanceId": "",
    "DomainControllerName": "",
    "CollectionIntervalMinutes": 5,
    "EnableEventCollection": true,
    "EnableUserCollection": true,
    "EnableGroupCollection": true
  }
}
```

## Installation

### Prerequisites
- .NET 8.0 Runtime
- Windows Server 2019+ or Windows 10+
- Domain Controller access
- Azure Event Hubs connection

### Install as Windows Service

1. **Build the application**:
   ```powershell
   dotnet publish -c Release -r win-x64 --self-contained
   ```

2. **Install the service**:
   ```powershell
   sc create "Permafrost Edge Service" binPath="C:\Path\To\Permafrost.EdgeService.exe"
   sc config "Permafrost Edge Service" obj="DOMAIN\gMSA$" password=""
   ```

3. **Start the service**:
   ```powershell
   sc start "Permafrost Edge Service"
   ```

### Development Setup

1. **Clone and build**:
   ```bash
   git clone <repository>
   cd edge-service
   dotnet restore
   dotnet build
   ```

2. **Configure settings**:
   - Copy `appsettings.json` to `appsettings.Development.json`
   - Update connection strings and AD settings

3. **Run locally**:
   ```bash
   dotnet run
   ```

4. **Access Swagger UI**:
   - Navigate to `https://localhost:5001`

## Security Considerations

### Network Security
- **Localhost only**: Service only listens on localhost interfaces
- **No external access**: Cannot be reached from outside the domain controller
- **HTTPS**: Uses HTTPS for all communications

### Authentication
- **gMSA**: Runs under Group Managed Service Account
- **Integrated Auth**: Uses Windows integrated authentication for AD
- **Least Privilege**: Only requires read access to AD

### Data Protection
- **No caching**: No sensitive data stored locally
- **Encrypted transport**: All data sent over HTTPS/TLS
- **Audit logging**: All operations logged for compliance

## Monitoring

### Health Checks
- **Active Directory**: Tests LDAP connection
- **Event Hub**: Tests Azure connection
- **Overall**: Combined health status

### Logging
- **Structured logging**: JSON format with correlation IDs
- **Event Log**: Windows Event Log integration
- **OpenTelemetry**: Distributed tracing support

### Metrics
- **Performance counters**: AD query performance
- **Event Hub metrics**: Batch sizes and success rates
- **Health status**: Service availability

## Troubleshooting

### Common Issues

1. **AD Connection Failed**
   - Check domain controller connectivity
   - Verify service account permissions
   - Test LDAP port (389/636)

2. **Event Hub Connection Failed**
   - Verify connection string
   - Check Azure service status
   - Validate network connectivity

3. **Service Won't Start**
   - Check Windows Event Log
   - Verify .NET 8 runtime installed
   - Confirm service account permissions

### Logs Location
- **Application logs**: Windows Event Log > Application
- **Service logs**: Windows Event Log > System
- **Debug logs**: Console output (development mode)

## Development

### Running Tests
```bash
dotnet test
```

### Code Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Building for Production
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

## Dependencies

- **.NET 8.0**: Base framework
- **System.DirectoryServices.Protocols**: AD integration
- **Azure.Messaging.EventHubs**: Event Hub client
- **OpenTelemetry**: Observability
- **Swashbuckle**: API documentation

## License

MIT License - see LICENSE file for details.
