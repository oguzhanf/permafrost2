# Permafrost2 Customer Portal - IIS Deployment Script
# This script deploys the Permafrost2 Customer Portal to IIS

param(
    [string]$SiteName = "Permafrost2CustomerPortal",
    [string]$AppPoolName = "Permafrost2CustomerPortalAppPool",
    [string]$PhysicalPath = "C:\inetpub\wwwroot\Permafrost2CustomerPortal",
    [int]$Port = 8080,
    [string]$Configuration = "Release"
)

Write-Host "Starting deployment of Permafrost2 Customer Portal to IIS..." -ForegroundColor Green

# Check if running as administrator
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "This script must be run as Administrator. Please run PowerShell as Administrator and try again."
    exit 1
}

# Import WebAdministration module
Import-Module WebAdministration -ErrorAction SilentlyContinue
if (-not (Get-Module WebAdministration)) {
    Write-Error "IIS WebAdministration module is not available. Please ensure IIS is installed."
    exit 1
}

try {
    # Step 1: Build and publish the application
    Write-Host "Building and publishing application..." -ForegroundColor Yellow
    dotnet publish src/Permafrost2.CustomerPortal/Permafrost2.CustomerPortal.csproj -c $Configuration -o $PhysicalPath --self-contained false
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to publish application"
    }

    # Step 2: Create Application Pool
    Write-Host "Creating Application Pool: $AppPoolName" -ForegroundColor Yellow
    if (Get-IISAppPool -Name $AppPoolName -ErrorAction SilentlyContinue) {
        Write-Host "Application Pool already exists. Removing..." -ForegroundColor Yellow
        Remove-WebAppPool -Name $AppPoolName
    }
    
    New-WebAppPool -Name $AppPoolName
    Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name "managedRuntimeVersion" -Value ""
    Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name "enable32BitAppOnWin64" -Value $false
    Set-ItemProperty -Path "IIS:\AppPools\$AppPoolName" -Name "processModel.identityType" -Value "ApplicationPoolIdentity"
    
    # Step 3: Create Website
    Write-Host "Creating Website: $SiteName" -ForegroundColor Yellow
    if (Get-Website -Name $SiteName -ErrorAction SilentlyContinue) {
        Write-Host "Website already exists. Removing..." -ForegroundColor Yellow
        Remove-Website -Name $SiteName
    }
    
    New-Website -Name $SiteName -Port $Port -PhysicalPath $PhysicalPath -ApplicationPool $AppPoolName
    
    # Step 4: Set permissions for IIS identity
    Write-Host "Setting permissions for IIS identity..." -ForegroundColor Yellow
    $acl = Get-Acl $PhysicalPath
    $accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule("IIS_IUSRS", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")
    $acl.SetAccessRule($accessRule)
    $accessRule2 = New-Object System.Security.AccessControl.FileSystemAccessRule("IIS AppPool\$AppPoolName", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")
    $acl.SetAccessRule($accessRule2)
    Set-Acl -Path $PhysicalPath -AclObject $acl
    
    # Step 5: Create logs directory
    $logsPath = Join-Path $PhysicalPath "logs"
    if (-not (Test-Path $logsPath)) {
        New-Item -ItemType Directory -Path $logsPath -Force
    }
    
    # Step 6: Update database
    Write-Host "Updating database..." -ForegroundColor Yellow
    Set-Location (Join-Path $PSScriptRoot "src\Permafrost2.CustomerPortal")
    dotnet ef database update --project ..\Permafrost2.Data
    Set-Location $PSScriptRoot
    
    # Step 7: Start Application Pool and Website
    Write-Host "Starting Application Pool and Website..." -ForegroundColor Yellow
    Start-WebAppPool -Name $AppPoolName
    Start-Website -Name $SiteName
    
    Write-Host "Deployment completed successfully!" -ForegroundColor Green
    Write-Host "Website URL: http://localhost:$Port" -ForegroundColor Cyan
    Write-Host "Physical Path: $PhysicalPath" -ForegroundColor Cyan
    Write-Host "Application Pool: $AppPoolName" -ForegroundColor Cyan
    
} catch {
    Write-Error "Deployment failed: $($_.Exception.Message)"
    exit 1
}

Write-Host "Deployment script completed." -ForegroundColor Green
