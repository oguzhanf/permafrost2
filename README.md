# Permafrost Identity Management System

A two-tier identity management system for Active Directory environments.

## Architecture

### Tier A - Windows Edge Service
- Lightweight .NET 8 worker service running on domain controllers
- Exposes REST endpoints for `/api/users`, `/api/groups`, `/api/events`
- Uses `System.DirectoryServices.Protocols` for AD queries
- Publishes data to Azure Event Hubs

### Tier B - Main Application
- Cross-platform Docker-based application
- Backend: Go 1.22 with PostgreSQL 16
- Frontend: Next.js 14 with React/TypeScript
- Messaging: NATS + Azure Event Hubs
- Authentication: Azure AD via MSAL

## Project Structure

```
├── edge-service/          # Tier A - Windows Edge Service (.NET 8)
├── backend/              # Tier B - Go API Server
├── frontend/             # Tier B - Next.js Application
├── database/             # PostgreSQL schemas and migrations
├── docker/               # Docker configurations
└── docs/                 # Documentation
```

## Development

### Prerequisites
- .NET SDK 8.0.x
- Go 1.22.x
- Node.js 20.x / npm 10.x
- PostgreSQL 16.x
- Docker & Docker Compose
- NATS Server 2.10.x
- Azure Event Hubs (for production)

### Quick Start with Docker Compose

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd permafrost
   ```

2. **Start all services**
   ```bash
   docker-compose up -d
   ```

3. **Access the application**
   - Frontend: http://localhost:3000
   - Backend API: http://localhost:8080
   - API Documentation: http://localhost:8080/swagger/index.html
   - NATS Monitoring: http://localhost:8222

### Manual Development Setup

1. **Database Setup**
   ```bash
   # Start PostgreSQL
   docker run -d --name permafrost-postgres \
     -e POSTGRES_DB=permafrost \
     -e POSTGRES_USER=permafrost \
     -e POSTGRES_PASSWORD=dev_password_change_in_prod \
     -p 5432:5432 postgres:16.3-alpine

   # Run migrations
   psql -h localhost -U permafrost -d permafrost -f database/migrations/001_initial_schema.sql
   ```

2. **NATS Setup**
   ```bash
   # Start NATS with JetStream
   docker run -d --name permafrost-nats \
     -p 4222:4222 -p 8222:8222 \
     nats:2.10.14-alpine -js -m 8222
   ```

3. **Backend Setup**
   ```bash
   cd backend
   go mod download
   go run .
   ```

4. **Frontend Setup**
   ```bash
   cd frontend
   npm install
   npm run dev
   ```

5. **Edge Service Setup** (Windows only)
   ```bash
   cd edge-service
   dotnet restore
   dotnet run
   ```

## API Endpoints

### Backend API (Port 8080)
- `GET /health` - Health check
- `GET /api/v1/users` - List users with pagination and filtering
- `POST /api/v1/users` - Create new user
- `GET /api/v1/users/{id}` - Get user by ID
- `PUT /api/v1/users/{id}` - Update user
- `DELETE /api/v1/users/{id}` - Delete user (soft delete)
- `GET /api/v1/groups` - List groups with pagination and filtering
- `POST /api/v1/groups` - Create new group
- `GET /api/v1/groups/{id}` - Get group by ID
- `PUT /api/v1/groups/{id}` - Update group
- `DELETE /api/v1/groups/{id}` - Delete group
- `GET /api/v1/events` - List events with pagination and filtering
- `POST /api/v1/events` - Create new event

### Edge Service API (Port 5000/5001 - Localhost only)
- `GET /health` - Health check
- `GET /api/users` - Get users from Active Directory
- `GET /api/users/{id}` - Get specific user by Object GUID
- `GET /api/groups` - Get groups from Active Directory
- `GET /api/groups/{id}` - Get specific group by Object GUID
- `GET /api/events` - Get events from Windows Event Log

## Testing

### Backend Tests
```bash
cd backend
go test -v ./...
go test -cover ./...
```

### Frontend Tests
```bash
cd frontend
npm test
npm run test:coverage
```

### Edge Service Tests
```bash
cd edge-service
dotnet test
dotnet test --collect:"XPlat Code Coverage"
```

### Integration Tests
```bash
# Start all services
docker-compose up -d

# Run integration tests
./scripts/run-integration-tests.sh
```

## Configuration

### Environment Variables

#### Frontend (.env.local)
```bash
NEXT_PUBLIC_API_URL=http://localhost:8080
NEXT_PUBLIC_AZURE_CLIENT_ID=your-azure-client-id
NEXT_PUBLIC_AZURE_TENANT_ID=your-azure-tenant-id
NEXT_PUBLIC_AZURE_REDIRECT_URI=http://localhost:3000
```

#### Backend
```bash
DATABASE_URL=postgres://permafrost:password@localhost:5432/permafrost?sslmode=disable
NATS_URL=nats://localhost:4222
PORT=8080
JAEGER_URL=http://localhost:14268/api/traces
```

#### Edge Service (appsettings.json)
```json
{
  "ActiveDirectory": {
    "DomainController": "dc01.example.com",
    "SearchBase": "DC=example,DC=com"
  },
  "EventHub": {
    "ConnectionString": "Endpoint=sb://...",
    "HubName": "permafrost-events"
  }
}
```

## Security

### Authentication
- **Frontend**: Azure AD via MSAL React
- **Backend**: Bearer token validation
- **Edge Service**: Windows integrated authentication (gMSA)

### Network Security
- **Edge Service**: Localhost-only binding (127.0.0.1)
- **Backend**: CORS configured for frontend domain
- **Database**: Network isolation, encrypted connections

### Data Protection
- **In Transit**: HTTPS/TLS for all communications
- **At Rest**: Database encryption, secure configuration storage
- **Audit**: All operations logged with OpenTelemetry tracing

## Monitoring & Observability

### Health Checks
- All services expose `/health` endpoints
- Database connectivity checks
- External service dependency checks

### Logging
- **Structured logging** with JSON format
- **Correlation IDs** for request tracing
- **Log levels**: Debug, Info, Warning, Error

### Metrics
- **OpenTelemetry** instrumentation
- **Performance counters** for critical operations
- **Business metrics** for user/group operations

### Tracing
- **Distributed tracing** across all services
- **Jaeger** integration for trace visualization
- **Custom spans** for business operations

## Deployment

### Production Deployment
1. **Build Docker images**
   ```bash
   docker build -t permafrost-backend ./backend
   docker build -t permafrost-frontend ./frontend
   ```

2. **Deploy to Kubernetes**
   ```bash
   kubectl apply -f k8s/
   ```

3. **Install Edge Service on Domain Controllers**
   ```powershell
   # See edge-service/README.md for detailed instructions
   ```

### Scaling Considerations
- **Backend**: Stateless, can be horizontally scaled
- **Frontend**: Static assets, CDN-friendly
- **Database**: Read replicas for query scaling
- **Edge Service**: One per domain controller

## Production Deployment

### Kubernetes Deployment

1. **Prepare environment**
   ```bash
   # Update secrets with production values
   kubectl create secret generic permafrost-secrets \
     --from-literal=POSTGRES_PASSWORD=your-secure-password \
     --from-literal=DATABASE_URL=your-database-url \
     --namespace=permafrost

   kubectl create secret generic azure-ad-secrets \
     --from-literal=NEXT_PUBLIC_AZURE_CLIENT_ID=your-client-id \
     --from-literal=NEXT_PUBLIC_AZURE_TENANT_ID=your-tenant-id \
     --from-literal=NEXT_PUBLIC_AZURE_REDIRECT_URI=https://your-domain.com \
     --namespace=permafrost
   ```

2. **Deploy to Kubernetes**
   ```bash
   # Automated deployment
   ./scripts/deploy.sh --environment production --registry your-registry.com

   # Manual deployment
   kubectl apply -f k8s/namespace.yaml
   kubectl apply -f k8s/secrets.yaml
   kubectl apply -f k8s/configmap.yaml
   kubectl apply -f k8s/postgres.yaml
   kubectl apply -f k8s/nats.yaml
   kubectl apply -f k8s/backend.yaml
   kubectl apply -f k8s/frontend.yaml
   kubectl apply -f k8s/ingress.yaml
   kubectl apply -f k8s/monitoring.yaml
   ```

3. **Verify deployment**
   ```bash
   # Check all pods are running
   kubectl get pods -n permafrost

   # Check services
   kubectl get services -n permafrost

   # Run health checks
   kubectl exec -n permafrost deployment/permafrost-backend -- curl http://localhost:8080/health
   ```

### Edge Service Deployment (Windows)

1. **Build and package**
   ```powershell
   cd edge-service
   dotnet publish -c Release -r win-x64 --self-contained
   ```

2. **Install as Windows Service**
   ```powershell
   # Create service
   sc create "Permafrost Edge Service" binPath="C:\Permafrost\Permafrost.EdgeService.exe"

   # Configure service account (use gMSA)
   sc config "Permafrost Edge Service" obj="DOMAIN\gMSA$" password=""

   # Start service
   sc start "Permafrost Edge Service"
   ```

3. **Configure firewall**
   ```powershell
   # Allow localhost access only
   New-NetFirewallRule -DisplayName "Permafrost Edge Service" -Direction Inbound -Protocol TCP -LocalPort 5000,5001 -LocalAddress 127.0.0.1 -Action Allow
   ```

## Monitoring & Alerting

### Prometheus Metrics
- **Backend**: HTTP request metrics, database connection pool, NATS message rates
- **Database**: Connection count, query performance, disk usage
- **NATS**: Message throughput, connection count, JetStream metrics
- **System**: CPU, memory, disk usage per pod

### Grafana Dashboards
- **Overview**: System health, request rates, error rates
- **Performance**: Response times, throughput, resource utilization
- **Security**: Authentication events, failed login attempts
- **Business**: User/group counts, activity trends

### Alerting Rules
- **Critical**: Service down, database unavailable, high error rate (>5%)
- **Warning**: High response time (>2s), resource usage (>80%), pod restarts
- **Info**: Deployment events, configuration changes

## Backup & Recovery

### Database Backup
```bash
# Automated daily backup
kubectl create cronjob permafrost-backup \
  --image=postgres:16.3-alpine \
  --schedule="0 2 * * *" \
  --restart=OnFailure \
  -- pg_dump -h permafrost-postgres -U permafrost permafrost > /backup/permafrost-$(date +%Y%m%d).sql
```

### Configuration Backup
```bash
# Backup Kubernetes manifests
kubectl get all,secrets,configmaps,pvc -n permafrost -o yaml > permafrost-backup.yaml
```

### Disaster Recovery
1. **Database**: Restore from latest backup
2. **Configuration**: Reapply Kubernetes manifests
3. **Secrets**: Restore from secure backup location
4. **Edge Services**: Reinstall on domain controllers

## Security Hardening

### Network Security
- **Ingress**: Rate limiting, WAF protection, DDoS mitigation
- **Internal**: Network policies, service mesh (optional)
- **Edge Service**: Localhost-only binding, Windows firewall

### Authentication & Authorization
- **Frontend**: Azure AD with MFA enforcement
- **Backend**: JWT token validation, role-based access
- **Database**: Encrypted connections, limited user privileges
- **Edge Service**: Windows integrated auth with gMSA

### Data Protection
- **Encryption**: TLS 1.3 for all communications
- **Secrets**: Kubernetes secrets, Azure Key Vault integration
- **Audit**: All operations logged with correlation IDs
- **Compliance**: GDPR, SOX, HIPAA considerations

## Troubleshooting

### Common Issues

1. **Backend not starting**
   ```bash
   # Check logs
   kubectl logs -n permafrost deployment/permafrost-backend

   # Check database connectivity
   kubectl exec -n permafrost deployment/permafrost-backend -- nc -zv permafrost-postgres 5432
   ```

2. **Frontend authentication issues**
   ```bash
   # Verify Azure AD configuration
   kubectl get secret azure-ad-secrets -n permafrost -o yaml

   # Check redirect URIs in Azure AD app registration
   ```

3. **Edge Service connectivity**
   ```powershell
   # Check service status
   sc query "Permafrost Edge Service"

   # Test AD connectivity
   Test-NetConnection -ComputerName dc01.domain.com -Port 389

   # Check Event Hub connectivity
   Test-NetConnection -ComputerName your-eventhub.servicebus.windows.net -Port 443
   ```

### Performance Issues

1. **High response times**
   - Check database query performance
   - Review connection pool settings
   - Analyze slow query logs

2. **Memory leaks**
   - Monitor pod memory usage
   - Check for connection leaks
   - Review garbage collection metrics

3. **Database performance**
   - Analyze query execution plans
   - Check index usage
   - Monitor connection pool

## Reference Versions
- .NET SDK: 8.0.x (LTS)
- Go: 1.22.5
- Node/npm: 20.14.0 / 10.x
- PostgreSQL: 16.3
- Next.js: 14.2.4
- NATS: 2.10.14
- MSAL: 3.20.0 / 2.0.22
- OpenTelemetry: 1.9.0
- Kubernetes: 1.28+
- Docker: 24.0+
