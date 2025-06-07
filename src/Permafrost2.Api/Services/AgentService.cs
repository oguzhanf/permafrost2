using Microsoft.EntityFrameworkCore;
using Permafrost2.Data;
using Permafrost2.Data.Models;
using Permafrost2.Shared.DTOs;
using System.Text.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Permafrost2.Api.Services;

public class AgentService : IAgentService
{
    private readonly PermafrostDbContext _context;
    private readonly ILogger<AgentService> _logger;

    public AgentService(PermafrostDbContext context, ILogger<AgentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AgentRegistrationResponse> RegisterAgentAsync(AgentRegistrationRequest request)
    {
        try
        {
            // Check if agent already exists
            var existingAgent = await _context.Agents
                .FirstOrDefaultAsync(a => a.MachineName == request.MachineName && a.Type == request.Type);

            Agent agent;
            if (existingAgent != null)
            {
                // Update existing agent
                existingAgent.Name = request.Name;
                existingAgent.Version = request.Version;
                existingAgent.IpAddress = request.IpAddress;
                existingAgent.Domain = request.Domain;
                existingAgent.OperatingSystem = request.OperatingSystem;
                existingAgent.Configuration = request.Configuration != null ? JsonSerializer.Serialize(request.Configuration) : null;
                existingAgent.IsActive = true;
                existingAgent.LastUpdated = DateTime.UtcNow;
                agent = existingAgent;
            }
            else
            {
                // Create new agent
                agent = new Agent
                {
                    Name = request.Name,
                    Type = request.Type,
                    Version = request.Version,
                    MachineName = request.MachineName,
                    IpAddress = request.IpAddress,
                    Domain = request.Domain,
                    OperatingSystem = request.OperatingSystem,
                    Configuration = request.Configuration != null ? JsonSerializer.Serialize(request.Configuration) : null,
                    IsActive = true,
                    Status = "Registered"
                };
                _context.Agents.Add(agent);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Agent {AgentName} ({Type}) registered successfully from {MachineName}", 
                request.Name, request.Type, request.MachineName);

            return new AgentRegistrationResponse
            {
                AgentId = agent.Id,
                Success = true,
                Message = "Agent registered successfully",
                ApiKey = GenerateApiKey(agent.Id),
                Configuration = GetDefaultConfiguration(request.Type)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register agent {AgentName} from {MachineName}", 
                request.Name, request.MachineName);
            
            return new AgentRegistrationResponse
            {
                Success = false,
                Message = "Registration failed: " + ex.Message
            };
        }
    }

    public async Task<AgentHeartbeatResponse> ProcessHeartbeatAsync(AgentHeartbeatRequest request)
    {
        try
        {
            var agent = await _context.Agents.FindAsync(request.AgentId);
            if (agent == null)
            {
                return new AgentHeartbeatResponse
                {
                    Success = false,
                    Message = "Agent not found"
                };
            }

            agent.LastHeartbeat = DateTime.UtcNow;
            agent.Status = request.Status;
            agent.StatusMessage = request.StatusMessage;
            agent.IsOnline = true;

            await _context.SaveChangesAsync();

            return new AgentHeartbeatResponse
            {
                Success = true,
                Message = "Heartbeat processed",
                UpdateAvailable = false // TODO: Implement update checking
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process heartbeat for agent {AgentId}", request.AgentId);
            
            return new AgentHeartbeatResponse
            {
                Success = false,
                Message = "Heartbeat processing failed"
            };
        }
    }

    public async Task<DataSubmissionResponse> ProcessDataSubmissionAsync(DataSubmissionRequest request)
    {
        try
        {
            var agent = await _context.Agents.FindAsync(request.AgentId);
            if (agent == null)
            {
                return new DataSubmissionResponse
                {
                    Success = false,
                    Message = "Agent not found"
                };
            }

            var submission = new AgentDataSubmission
            {
                AgentId = request.AgentId,
                DataType = request.DataType,
                RecordCount = request.RecordCount,
                DataSizeBytes = request.Data.Length,
                FileHash = request.DataHash,
                Metadata = request.Metadata,
                Status = "Pending"
            };

            _context.AgentDataSubmissions.Add(submission);

            // Process the data based on type
            await ProcessSubmittedData(request, submission);

            agent.LastDataCollection = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Data submission {SubmissionId} processed for agent {AgentId}, type: {DataType}, records: {RecordCount}",
                submission.Id, request.AgentId, request.DataType, request.RecordCount);

            return new DataSubmissionResponse
            {
                SubmissionId = submission.Id,
                Success = true,
                Message = "Data submitted successfully",
                ProcessedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process data submission for agent {AgentId}", request.AgentId);
            
            return new DataSubmissionResponse
            {
                Success = false,
                Message = "Data submission failed: " + ex.Message
            };
        }
    }

    public async Task<IEnumerable<AgentStatusDto>> GetAgentsAsync()
    {
        var agents = await _context.Agents
            .Where(a => a.IsActive)
            .OrderBy(a => a.Name)
            .ToListAsync();

        return agents.Select(a => new AgentStatusDto
        {
            Id = a.Id,
            Name = a.Name,
            Type = a.Type,
            Version = a.Version,
            MachineName = a.MachineName,
            IpAddress = a.IpAddress,
            Domain = a.Domain,
            IsActive = a.IsActive,
            IsOnline = a.IsOnline,
            Status = a.Status,
            StatusMessage = a.StatusMessage,
            RegisteredAt = a.RegisteredAt,
            LastHeartbeat = a.LastHeartbeat,
            LastDataCollection = a.LastDataCollection
        });
    }

    public async Task<AgentStatusDto?> GetAgentAsync(Guid agentId)
    {
        var agent = await _context.Agents.FindAsync(agentId);
        if (agent == null) return null;

        return new AgentStatusDto
        {
            Id = agent.Id,
            Name = agent.Name,
            Type = agent.Type,
            Version = agent.Version,
            MachineName = agent.MachineName,
            IpAddress = agent.IpAddress,
            Domain = agent.Domain,
            IsActive = agent.IsActive,
            IsOnline = agent.IsOnline,
            Status = agent.Status,
            StatusMessage = agent.StatusMessage,
            RegisteredAt = agent.RegisteredAt,
            LastHeartbeat = agent.LastHeartbeat,
            LastDataCollection = agent.LastDataCollection
        };
    }

    public async Task<bool> UpdateAgentConfigurationAsync(Guid agentId, AgentConfigurationDto configuration)
    {
        var agent = await _context.Agents.FindAsync(agentId);
        if (agent == null) return false;

        agent.Configuration = JsonSerializer.Serialize(configuration);
        agent.LastUpdated = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeactivateAgentAsync(Guid agentId)
    {
        var agent = await _context.Agents.FindAsync(agentId);
        if (agent == null) return false;

        agent.IsActive = false;
        agent.IsOnline = false;
        agent.Status = "Deactivated";
        agent.LastUpdated = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<AgentDataSubmission>> GetAgentSubmissionsAsync(Guid agentId, int limit = 50)
    {
        return await _context.AgentDataSubmissions
            .Where(s => s.AgentId == agentId)
            .OrderByDescending(s => s.SubmittedAt)
            .Take(limit)
            .ToListAsync();
    }

    private string GenerateApiKey(Guid agentId)
    {
        // Simple API key generation - in production, use proper cryptographic methods
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"agent_{agentId}_{DateTime.UtcNow.Ticks}"));
    }

    private AgentConfigurationDto GetDefaultConfiguration(string agentType)
    {
        var config = new AgentConfigurationDto();
        
        switch (agentType.ToLower())
        {
            case "domaincontroller":
                config.EnabledDataTypes = new List<string> { "Users", "Groups", "Policies" };
                config.DataCollectionIntervalMinutes = 60;
                break;
            case "server":
                config.EnabledDataTypes = new List<string> { "Events", "LocalUsers", "LocalGroups" };
                config.DataCollectionIntervalMinutes = 30;
                break;
            case "workstation":
                config.EnabledDataTypes = new List<string> { "Events", "LocalUsers" };
                config.DataCollectionIntervalMinutes = 120;
                break;
        }

        return config;
    }

    private async Task ProcessSubmittedData(DataSubmissionRequest request, AgentDataSubmission submission)
    {
        try
        {
            switch (request.DataType.ToLower())
            {
                case "users":
                    await ProcessUserData(request.Data, submission);
                    break;
                case "groups":
                    await ProcessGroupData(request.Data, submission);
                    break;
                default:
                    _logger.LogWarning("Unknown data type: {DataType}", request.DataType);
                    break;
            }

            submission.Status = "Completed";
            submission.ProcessedAt = DateTime.UtcNow;
            submission.ProcessedCount = submission.RecordCount;
        }
        catch (Exception ex)
        {
            submission.Status = "Failed";
            submission.ErrorDetails = ex.Message;
            submission.ErrorCount = submission.RecordCount;
            _logger.LogError(ex, "Failed to process {DataType} data for submission {SubmissionId}", 
                request.DataType, submission.Id);
        }
    }

    private async Task ProcessUserData(byte[] data, AgentDataSubmission submission)
    {
        // Deserialize user data and save to database
        var json = System.Text.Encoding.UTF8.GetString(data);
        var users = JsonSerializer.Deserialize<List<DomainUserDto>>(json);
        
        if (users == null) return;

        foreach (var userDto in users)
        {
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == userDto.Username);

            if (existingUser != null)
            {
                // Update existing user
                existingUser.Email = userDto.Email;
                existingUser.DisplayName = userDto.DisplayName;
                existingUser.FirstName = userDto.FirstName;
                existingUser.LastName = userDto.LastName;
                existingUser.Department = userDto.Department;
                existingUser.JobTitle = userDto.JobTitle;
                existingUser.Manager = userDto.Manager;
                existingUser.IsActive = userDto.IsActive;
                existingUser.LastUpdated = DateTime.UtcNow;
                existingUser.Source = "DomainController";
            }
            else
            {
                // Create new user
                var user = new User
                {
                    Username = userDto.Username,
                    Email = userDto.Email,
                    DisplayName = userDto.DisplayName,
                    FirstName = userDto.FirstName,
                    LastName = userDto.LastName,
                    Department = userDto.Department,
                    JobTitle = userDto.JobTitle,
                    Manager = userDto.Manager,
                    IsActive = userDto.IsActive,
                    Source = "DomainController",
                    SourceId = userDto.ObjectSid
                };
                _context.Users.Add(user);
            }
        }
    }

    private async Task ProcessGroupData(byte[] data, AgentDataSubmission submission)
    {
        // TODO: Implement group data processing
        await Task.CompletedTask;
    }

    public async Task<AgentDownloadInfo?> GetAgentDownloadInfoAsync(string agentType)
    {
        await Task.CompletedTask; // For async consistency

        var agentTypeNormalized = agentType.ToLower();

        return agentTypeNormalized switch
        {
            "domain-controller" => GetDomainControllerAgentInfo(),
            "server" => GetServerAgentInfo(),
            "workstation" => GetWorkstationAgentInfo(),
            _ => null
        };
    }

    private AgentDownloadInfo GetDomainControllerAgentInfo()
    {
        // Get the installer path from the dist/installers directory
        var installerPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "dist",
            "installers",
            "PermafrostDCAgent-v1.0.1.msi"
        );

        var fileName = "PermafrostDCAgent-v1.0.1.msi";
        var fileInfo = new FileInfo(installerPath);

        return new AgentDownloadInfo
        {
            AgentType = "domain-controller",
            FileName = fileName,
            FilePath = installerPath,
            Version = "1.0.1",
            FileSize = fileInfo.Exists ? fileInfo.Length : 0,
            LastModified = fileInfo.Exists ? fileInfo.LastWriteTime : DateTime.MinValue,
            Description = "Permafrost2 Domain Controller Agent - Collects domain data including users, groups, policies, and organizational units."
        };
    }

    private AgentDownloadInfo GetServerAgentInfo()
    {
        // Placeholder for server agent - not yet implemented
        return new AgentDownloadInfo
        {
            AgentType = "server",
            FileName = "PermafrostServerAgent-v1.0.0.msi",
            FilePath = "",
            Version = "1.0.0",
            FileSize = 0,
            LastModified = DateTime.MinValue,
            Description = "Permafrost2 Server Agent - Collects security event logs, audit events, and local group memberships from Windows servers."
        };
    }

    private AgentDownloadInfo GetWorkstationAgentInfo()
    {
        // Placeholder for workstation agent - not yet implemented
        return new AgentDownloadInfo
        {
            AgentType = "workstation",
            FileName = "PermafrostWorkstationAgent-v1.0.0.msi",
            FilePath = "",
            Version = "1.0.0",
            FileSize = 0,
            LastModified = DateTime.MinValue,
            Description = "Permafrost2 Workstation Agent - Collects security event logs and local user information from Windows workstations."
        };
    }

    public async Task<AgentErrorReportResponse> ProcessErrorReportAsync(AgentErrorReportRequest request)
    {
        try
        {
            var agent = await _context.Agents.FindAsync(request.AgentId);
            if (agent == null)
            {
                return new AgentErrorReportResponse
                {
                    Success = false,
                    Message = "Agent not found"
                };
            }

            var processedCount = 0;
            var duplicateCount = 0;
            var newErrorCount = 0;

            // Create error report record
            var errorReport = new AgentErrorReport
            {
                AgentId = request.AgentId,
                ReportedAt = request.ReportedAt,
                TotalErrorCount = request.Errors.Count
            };

            foreach (var errorDto in request.Errors)
            {
                // Check for existing error with same ErrorId
                var existingError = await _context.AgentErrors
                    .FirstOrDefaultAsync(e => e.AgentId == request.AgentId && e.ErrorId == errorDto.ErrorId);

                if (existingError != null)
                {
                    // Update existing error
                    existingError.OccurrenceCount += errorDto.OccurrenceCount;
                    existingError.LastOccurrence = errorDto.LastOccurrence;
                    existingError.ReportedAt = DateTime.UtcNow;
                    duplicateCount++;
                }
                else
                {
                    // Create new error record
                    var agentError = new AgentError
                    {
                        AgentId = request.AgentId,
                        ErrorId = errorDto.ErrorId,
                        Severity = (int)errorDto.Severity,
                        Category = (int)errorDto.Category,
                        Source = errorDto.Source,
                        Message = errorDto.Message,
                        StackTrace = errorDto.StackTrace,
                        AdditionalData = errorDto.AdditionalData,
                        OccurredAt = errorDto.OccurredAt,
                        OccurrenceCount = errorDto.OccurrenceCount,
                        FirstOccurrence = errorDto.FirstOccurrence,
                        LastOccurrence = errorDto.LastOccurrence,
                        ReportedAt = DateTime.UtcNow,
                        Status = "New"
                    };

                    _context.AgentErrors.Add(agentError);
                    newErrorCount++;
                }
                processedCount++;
            }

            // Update error report with processing results
            errorReport.ProcessedErrorCount = processedCount;
            errorReport.DuplicateErrorCount = duplicateCount;
            errorReport.NewErrorCount = newErrorCount;
            errorReport.Status = "Processed";

            _context.AgentErrorReports.Add(errorReport);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Processed error report from agent {AgentId}: {ProcessedCount} errors processed, {NewCount} new, {DuplicateCount} duplicates",
                request.AgentId, processedCount, newErrorCount, duplicateCount);

            return new AgentErrorReportResponse
            {
                Success = true,
                Message = "Error report processed successfully",
                ProcessedErrorCount = processedCount,
                DuplicateErrorCount = duplicateCount,
                ProcessedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process error report from agent {AgentId}", request.AgentId);

            return new AgentErrorReportResponse
            {
                Success = false,
                Message = "Error report processing failed"
            };
        }
    }

    public async Task<CertificateGenerationResponse> GenerateCertificateAsync(CertificateGenerationRequest request)
    {
        try
        {
            _logger.LogInformation("Generating certificate for agent {AgentId}", request.AgentId);

            // Verify agent exists
            var agent = await _context.Agents.FindAsync(request.AgentId);
            if (agent == null)
            {
                return new CertificateGenerationResponse
                {
                    Success = false,
                    Message = "Agent not found"
                };
            }

            // Create a self-signed certificate for now
            // In production, this would call a Certificate Authority
            using var rsa = RSA.Create(2048);
            var certificateRequest = new CertificateRequest(
                $"CN={request.CommonName}, O={request.Organization}, OU={request.OrganizationalUnit}, C={request.Country}",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // Add key usage extensions
            certificateRequest.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                    critical: true));

            // Add enhanced key usage for client authentication
            certificateRequest.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection { new Oid("1.3.6.1.5.5.7.3.2") }, // Client Authentication
                    critical: true));

            // Add subject alternative name
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName(request.CommonName);
            certificateRequest.CertificateExtensions.Add(sanBuilder.Build());

            // Create the certificate
            var notBefore = DateTime.UtcNow.AddDays(-1);
            var notAfter = notBefore.AddDays(request.ValidityDays);

            using var certificate = certificateRequest.CreateSelfSigned(notBefore, notAfter);

            // Export certificate and private key
            var certificateData = Convert.ToBase64String(certificate.Export(X509ContentType.Cert));
            var privateKeyData = Convert.ToBase64String(certificate.Export(X509ContentType.Pfx));

            // Store certificate information in database
            var agentCertificate = new AgentCertificate
            {
                AgentId = request.AgentId,
                Thumbprint = certificate.Thumbprint,
                SerialNumber = certificate.SerialNumber,
                Subject = certificate.Subject,
                Issuer = certificate.Issuer,
                NotBefore = notBefore,
                NotAfter = notAfter,
                IssuedAt = DateTime.UtcNow,
                Status = "Active",
                Usage = "ClientAuthentication"
            };

            _context.AgentCertificates.Add(agentCertificate);
            await _context.SaveChangesAsync();

            var response = new CertificateGenerationResponse
            {
                Success = true,
                Message = "Certificate generated successfully",
                CertificateData = certificateData,
                PrivateKeyData = privateKeyData,
                IssuedAt = notBefore,
                ExpiresAt = notAfter,
                Thumbprint = certificate.Thumbprint,
                SerialNumber = certificate.SerialNumber
            };

            _logger.LogInformation("Certificate generated successfully for agent {AgentId}, thumbprint: {Thumbprint}",
                request.AgentId, certificate.Thumbprint);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate certificate for agent {AgentId}", request.AgentId);
            return new CertificateGenerationResponse
            {
                Success = false,
                Message = "Certificate generation failed: " + ex.Message
            };
        }
    }

    public async Task<CertificateValidationResponse> ValidateCertificateAsync(CertificateValidationRequest request)
    {
        try
        {
            var certificateBytes = Convert.FromBase64String(request.CertificateData);
            using var certificate = X509CertificateLoader.LoadCertificate(certificateBytes);

            var response = new CertificateValidationResponse
            {
                CertificateInfo = CreateCertificateInfo(certificate)
            };

            var validationErrors = new List<string>();

            // Check expiration
            var now = request.ValidateAtTime ?? DateTime.UtcNow;
            if (certificate.NotAfter < now)
                validationErrors.Add("Certificate has expired");
            if (certificate.NotBefore > now)
                validationErrors.Add("Certificate is not yet valid");

            // Check if certificate exists in database
            var dbCertificate = await _context.AgentCertificates
                .FirstOrDefaultAsync(c => c.Thumbprint == certificate.Thumbprint);

            if (dbCertificate == null)
                validationErrors.Add("Certificate not found in database");
            else if (dbCertificate.Status == "Revoked")
                validationErrors.Add("Certificate has been revoked");

            // Check certificate chain if requested
            if (request.CheckChain)
            {
                var chainValid = ValidateCertificateChain(certificate, request.CheckRevocation);
                if (!chainValid)
                    validationErrors.Add("Certificate chain validation failed");
            }

            response.IsValid = validationErrors.Count == 0;
            response.ValidationErrors = validationErrors;

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate certificate");
            return new CertificateValidationResponse
            {
                IsValid = false,
                ValidationErrors = new List<string> { "Certificate validation failed: " + ex.Message }
            };
        }
    }

    public async Task<CertificateRenewalResponse> RenewCertificateAsync(CertificateRenewalRequest request)
    {
        try
        {
            _logger.LogInformation("Renewing certificate for agent {AgentId}", request.AgentId);

            // Get current certificate from database
            var currentCert = await _context.AgentCertificates
                .FirstOrDefaultAsync(c => c.AgentId == request.AgentId && c.Thumbprint == request.CurrentThumbprint);

            if (currentCert == null)
            {
                return new CertificateRenewalResponse
                {
                    Success = false,
                    Message = "Current certificate not found"
                };
            }

            // Create renewal request based on current certificate
            var renewalRequest = new CertificateGenerationRequest
            {
                AgentId = request.AgentId,
                CommonName = GetCommonNameFromSubject(currentCert.Subject),
                ValidityDays = request.ValidityDays
            };

            // Generate new certificate
            var newCertResponse = await GenerateCertificateAsync(renewalRequest);
            if (!newCertResponse.Success)
            {
                return new CertificateRenewalResponse
                {
                    Success = false,
                    Message = $"Failed to generate new certificate: {newCertResponse.Message}"
                };
            }

            // Mark old certificate as superseded if requested
            if (request.RevokeOldCertificate)
            {
                currentCert.Status = "Superseded";
                currentCert.RevokedAt = DateTime.UtcNow;
                currentCert.RevocationReason = "Superseded";
            }

            await _context.SaveChangesAsync();

            return new CertificateRenewalResponse
            {
                Success = true,
                Message = "Certificate renewed successfully",
                NewCertificateData = newCertResponse.CertificateData,
                NewPrivateKeyData = newCertResponse.PrivateKeyData,
                NewThumbprint = newCertResponse.Thumbprint,
                NewSerialNumber = newCertResponse.SerialNumber,
                IssuedAt = newCertResponse.IssuedAt,
                ExpiresAt = newCertResponse.ExpiresAt,
                OldCertificateRevoked = request.RevokeOldCertificate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to renew certificate for agent {AgentId}", request.AgentId);
            return new CertificateRenewalResponse
            {
                Success = false,
                Message = "Certificate renewal failed: " + ex.Message
            };
        }
    }

    public async Task<CertificateRevocationResponse> RevokeCertificateAsync(CertificateRevocationRequest request)
    {
        try
        {
            _logger.LogInformation("Revoking certificate for agent {AgentId}, thumbprint: {Thumbprint}",
                request.AgentId, request.CertificateThumbprint);

            var certificate = await _context.AgentCertificates
                .FirstOrDefaultAsync(c => c.AgentId == request.AgentId && c.Thumbprint == request.CertificateThumbprint);

            if (certificate == null)
            {
                return new CertificateRevocationResponse
                {
                    Success = false,
                    Message = "Certificate not found"
                };
            }

            if (certificate.Status == "Revoked")
            {
                return new CertificateRevocationResponse
                {
                    Success = false,
                    Message = "Certificate is already revoked"
                };
            }

            certificate.Status = "Revoked";
            certificate.RevokedAt = DateTime.UtcNow;
            certificate.RevocationReason = request.Reason.ToString();

            await _context.SaveChangesAsync();

            return new CertificateRevocationResponse
            {
                Success = true,
                Message = "Certificate revoked successfully",
                RevokedAt = certificate.RevokedAt.Value
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke certificate for agent {AgentId}", request.AgentId);
            return new CertificateRevocationResponse
            {
                Success = false,
                Message = "Certificate revocation failed: " + ex.Message
            };
        }
    }

    public async Task<CertificateListResponse> ListCertificatesAsync(Guid agentId)
    {
        try
        {
            var certificates = await _context.AgentCertificates
                .Where(c => c.AgentId == agentId)
                .OrderByDescending(c => c.IssuedAt)
                .ToListAsync();

            var certificateInfos = certificates.Select(c => new CertificateInfo
            {
                Subject = c.Subject,
                Issuer = c.Issuer,
                Thumbprint = c.Thumbprint,
                SerialNumber = c.SerialNumber,
                NotBefore = c.NotBefore,
                NotAfter = c.NotAfter,
                KeyUsage = CertificateKeyUsage.ClientAuthentication,
                Status = Enum.Parse<CertificateStatus>(c.Status),
                IssuedAt = c.IssuedAt,
                RevokedAt = c.RevokedAt,
                RevocationReason = c.RevocationReason != null ? Enum.Parse<CertificateRevocationReason>(c.RevocationReason) : null
            }).ToList();

            return new CertificateListResponse
            {
                Success = true,
                Certificates = certificateInfos
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list certificates for agent {AgentId}", agentId);
            return new CertificateListResponse
            {
                Success = false,
                Message = "Failed to list certificates: " + ex.Message
            };
        }
    }

    private CertificateInfo CreateCertificateInfo(X509Certificate2 certificate)
    {
        return new CertificateInfo
        {
            Subject = certificate.Subject,
            Issuer = certificate.Issuer,
            Thumbprint = certificate.Thumbprint,
            SerialNumber = certificate.SerialNumber,
            NotBefore = certificate.NotBefore,
            NotAfter = certificate.NotAfter,
            KeyUsage = CertificateKeyUsage.ClientAuthentication // Default, could be parsed from extensions
        };
    }

    private bool ValidateCertificateChain(X509Certificate2 certificate, bool checkRevocation)
    {
        try
        {
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = checkRevocation ? X509RevocationMode.Online : X509RevocationMode.NoCheck;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreWrongUsage;

            return chain.Build(certificate);
        }
        catch
        {
            return false;
        }
    }

    private string GetCommonNameFromSubject(string subject)
    {
        // Simple CN extraction - in production, use proper X.500 name parsing
        var cnStart = subject.IndexOf("CN=");
        if (cnStart == -1) return "Unknown";

        cnStart += 3; // Skip "CN="
        var cnEnd = subject.IndexOf(',', cnStart);
        if (cnEnd == -1) cnEnd = subject.Length;

        return subject.Substring(cnStart, cnEnd - cnStart).Trim();
    }
}
