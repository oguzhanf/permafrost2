# Permafrost2

Permafrost2 is a comprehensive identity and permission management system that provides visibility into user access across multiple systems including Active Directory, Azure AD, and Microsoft 365.

## 🏗️ Architecture

### Components

#### 1. Customer Portal (Permafrost2.CustomerPortal)
- **Purpose**: Customer-facing web application for viewing application access reports and system status
- **Technology**: .NET 9.0 Blazor Server
- **Features**:
  - Dashboard with system overview
  - Application access reports
  - User permission management
  - Real-time status monitoring
  - Data collection run tracking

#### 2. Data Layer (Permafrost2.Data)
- **Purpose**: Entity Framework Core data access layer
- **Database**: SQL Server (permafrostdb)
- **Features**:
  - Entity models for users, groups, applications, permissions
  - Audit logging
  - Data collection run tracking
  - Migration support

#### 3. API Layer (Permafrost2.Api)
- **Purpose**: RESTful API for data access
- **Technology**: ASP.NET Core Web API
- **Features**: CRUD operations for all entities

#### 4. Core Services (Permafrost2.Core)
- **Purpose**: Business logic and shared services
- **Features**: Application services, user services, report generation

#### 5. Shared Components (Permafrost2.Shared)
- **Purpose**: Shared models and contracts
- **Features**: DTOs, interfaces, common utilities

## 🚀 Quick Start

### Prerequisites
- .NET 9.0 SDK
- SQL Server Developer Edition (or higher)
- IIS (for production deployment)
- Windows Server or Windows 10/11

### Development Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/oguzhanf/permafrost2.git
   cd permafrost2
   ```

2. **Restore packages**
   ```bash
   dotnet restore
   ```

3. **Update database connection string**
   Edit `src/Permafrost2.CustomerPortal/appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Database=permafrostdb;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true"
     }
   }
   ```

4. **Create and update database**
   ```bash
   dotnet ef database update --project src/Permafrost2.Data --startup-project src/Permafrost2.CustomerPortal
   ```

5. **Run the application**
   ```bash
   dotnet run --project src/Permafrost2.CustomerPortal
   ```

6. **Access the application**
   Open your browser and navigate to `http://localhost:5250`

## 🏢 Production Deployment

### IIS Deployment

1. **Run the deployment script as Administrator**
   ```powershell
   .\deploy-to-iis.ps1
   ```

   Or with custom parameters:
   ```powershell
   .\deploy-to-iis.ps1 -SiteName "MyPermafrost2" -Port 8080 -PhysicalPath "C:\MyApps\Permafrost2"
   ```

2. **Manual IIS Setup** (if script fails)
   - Create Application Pool with .NET CLR Version: "No Managed Code"
   - Create Website pointing to published application
   - Set Application Pool Identity permissions on database
   - Ensure IIS_IUSRS has access to application folder

### Database Setup

The application uses Entity Framework migrations to manage database schema. The database will be created automatically when you run the application for the first time.

**Database Name**: `permafrostdb`

**Required Permissions**: The IIS Application Pool identity needs:
- `db_datareader`
- `db_datawriter`
- `db_ddladmin` (for migrations)

## 📊 Features

### Dashboard
- System overview with key metrics
- Recent data collection run status
- Data source health monitoring
- Quick access to reports

### Applications Management
- View all applications in the system
- Application details and permissions
- User and group access reports
- Filter by criticality and type

### User Management
- User directory with detailed information
- Group memberships
- Application permissions (direct and inherited)
- User access reports

### Reports
- System status reports
- Application access reports
- User permission reports
- Data collection run history
- Export capabilities

### Data Collection
- Automated data collection from multiple sources
- Real-time status monitoring
- Error tracking and reporting
- Configurable collection intervals

## 🔧 Configuration

### Application Settings

Key configuration options in `appsettings.json`:

```json
{
  "ApplicationSettings": {
    "ApplicationName": "Permafrost2 Customer Portal",
    "Version": "1.0.0",
    "DataCollectionInterval": "01:00:00",
    "MaxReportRetentionDays": 90
  }
}
```

### Database Configuration

The application supports SQL Server with Windows Authentication:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=permafrostdb;Integrated Security=true;MultipleActiveResultSets=true;TrustServerCertificate=true"
  }
}
```

## 🗄️ Database Schema

### Core Entities

- **Users**: User accounts from various sources (AD, Azure, M365)
- **Groups**: Security and distribution groups
- **Applications**: Business applications and systems
- **Permissions**: Access permissions and roles
- **UserGroupMemberships**: User-to-group relationships
- **ApplicationPermissions**: Application access permissions

### Audit & Monitoring

- **AuditLogs**: Change tracking and audit trail
- **DataSources**: Configuration for data collection sources
- **DataCollectionRuns**: History and status of data collection operations

## 🔒 Security

### Authentication
- Windows Integrated Authentication for admin access
- Application Pool Identity for database access
- Secure connection strings with integrated security

### Authorization
- Role-based access control
- Local administrators access
- Domain administrators access (if domain-joined)

### Data Protection
- Audit logging for all changes
- Secure database connections
- Input validation and sanitization

## 🛠️ Development

### Project Structure
```
src/
├── Permafrost2.CustomerPortal/    # Main web application
├── Permafrost2.Data/              # Entity Framework data layer
├── Permafrost2.Api/               # Web API layer
├── Permafrost2.Core/              # Business logic
└── Permafrost2.Shared/            # Shared components
```

### Building
```bash
dotnet build
```

### Running Tests
```bash
dotnet test
```

### Database Migrations
```bash
# Add new migration
dotnet ef migrations add MigrationName --project src/Permafrost2.Data --startup-project src/Permafrost2.CustomerPortal

# Update database
dotnet ef database update --project src/Permafrost2.Data --startup-project src/Permafrost2.CustomerPortal
```

## 📝 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## 📞 Support

For support and questions, please create an issue in the GitHub repository.