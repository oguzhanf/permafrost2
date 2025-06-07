using Microsoft.AspNetCore.Mvc;
using Permafrost2.Api.Services;
using Permafrost2.Api.Middleware;
using Permafrost2.Shared.DTOs;

namespace Permafrost2.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly IAgentService _agentService;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(IAgentService agentService, ILogger<AgentsController> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AgentRegistrationResponse>> RegisterAgent([FromBody] AgentRegistrationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var response = await _agentService.RegisterAgentAsync(request);
        
        if (response.Success)
        {
            return Ok(response);
        }
        
        return BadRequest(response);
    }

    [HttpPost("heartbeat")]
    public async Task<ActionResult<AgentHeartbeatResponse>> Heartbeat([FromBody] AgentHeartbeatRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var response = await _agentService.ProcessHeartbeatAsync(request);
        
        if (response.Success)
        {
            return Ok(response);
        }
        
        return BadRequest(response);
    }

    [HttpPost("submit-data")]
    public async Task<ActionResult<DataSubmissionResponse>> SubmitData([FromBody] DataSubmissionRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var response = await _agentService.ProcessDataSubmissionAsync(request);
        
        if (response.Success)
        {
            return Ok(response);
        }
        
        return BadRequest(response);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AgentStatusDto>>> GetAgents()
    {
        var agents = await _agentService.GetAgentsAsync();
        return Ok(agents);
    }

    [HttpGet("{agentId}")]
    public async Task<ActionResult<AgentStatusDto>> GetAgent(Guid agentId)
    {
        var agent = await _agentService.GetAgentAsync(agentId);
        
        if (agent == null)
        {
            return NotFound();
        }
        
        return Ok(agent);
    }

    [HttpPut("{agentId}/configuration")]
    public async Task<ActionResult> UpdateConfiguration(Guid agentId, [FromBody] AgentConfigurationDto configuration)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var success = await _agentService.UpdateAgentConfigurationAsync(agentId, configuration);
        
        if (success)
        {
            return Ok();
        }
        
        return NotFound();
    }

    [HttpDelete("{agentId}")]
    public async Task<ActionResult> DeactivateAgent(Guid agentId)
    {
        var success = await _agentService.DeactivateAgentAsync(agentId);
        
        if (success)
        {
            return Ok();
        }
        
        return NotFound();
    }

    [HttpGet("{agentId}/submissions")]
    public async Task<ActionResult> GetAgentSubmissions(Guid agentId, [FromQuery] int limit = 50)
    {
        var submissions = await _agentService.GetAgentSubmissionsAsync(agentId, limit);
        return Ok(submissions);
    }

    [HttpGet("version")]
    public ActionResult<object> GetVersion()
    {
        return Ok(new {
            Version = "1.0.0",
            ApiVersion = "v1",
            Timestamp = DateTime.UtcNow
        });
    }

    [HttpGet("download/{agentType}")]
    public async Task<ActionResult> DownloadAgent(string agentType)
    {
        try
        {
            var agentInfo = await _agentService.GetAgentDownloadInfoAsync(agentType);

            if (agentInfo == null)
            {
                return NotFound($"Agent type '{agentType}' not found or not available for download.");
            }

            if (!System.IO.File.Exists(agentInfo.FilePath))
            {
                _logger.LogWarning("Agent installer file not found: {FilePath}", agentInfo.FilePath);
                return NotFound("Agent installer file not found. Please contact support.");
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(agentInfo.FilePath);

            // Get user information from authentication middleware for logging
            var userId = "Unknown";
            if (HttpContext.Items["DownloadUser"] is Permafrost2.Api.Middleware.DownloadUser user)
            {
                userId = user.Id;
            }

            _logger.LogInformation("Serving agent download: {AgentType}, File: {FileName}, Size: {Size} bytes, User: {UserId}",
                agentType, agentInfo.FileName, fileBytes.Length, userId);

            return File(fileBytes, "application/octet-stream", agentInfo.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving agent download for type: {AgentType}", agentType);
            return StatusCode(500, "An error occurred while downloading the agent.");
        }
    }

    [HttpGet("download/{agentType}/info")]
    public async Task<ActionResult<object>> GetAgentDownloadInfo(string agentType)
    {
        try
        {
            var agentInfo = await _agentService.GetAgentDownloadInfoAsync(agentType);

            if (agentInfo == null)
            {
                return NotFound($"Agent type '{agentType}' not found.");
            }

            return Ok(new
            {
                AgentType = agentType,
                agentInfo.FileName,
                agentInfo.Version,
                agentInfo.FileSize,
                agentInfo.LastModified,
                agentInfo.Description,
                Available = System.IO.File.Exists(agentInfo.FilePath)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting agent download info for type: {AgentType}", agentType);
            return StatusCode(500, "An error occurred while retrieving agent information.");
        }
    }

    [HttpPost("check-updates")]
    public async Task<ActionResult<UpdateCheckResponse>> CheckForUpdates([FromBody] UpdateCheckRequest request)
    {
        try
        {
            _logger.LogInformation("Checking for updates for agent type {AgentType}, current version {Version}",
                request.AgentType, request.CurrentVersion);

            // For now, we'll implement a simple version comparison
            // In a real implementation, this would check against a database or update service
            var latestVersion = GetLatestVersionForAgentType(request.AgentType);
            var updateAvailable = IsUpdateAvailable(request.CurrentVersion, latestVersion);

            if (updateAvailable)
            {
                var updateInfo = await GetUpdateInfoAsync(request.AgentType, latestVersion);
                return Ok(new UpdateCheckResponse
                {
                    Success = true,
                    UpdateAvailable = true,
                    UpdateInfo = updateInfo
                });
            }
            else
            {
                return Ok(new UpdateCheckResponse
                {
                    Success = true,
                    UpdateAvailable = false,
                    Message = "Agent is up to date"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for updates");
            return Ok(new UpdateCheckResponse
            {
                Success = false,
                Message = "An error occurred while checking for updates"
            });
        }
    }

    private string GetLatestVersionForAgentType(string agentType)
    {
        // In a real implementation, this would query a database or configuration
        return agentType.ToLower() switch
        {
            "domain-controller" => "1.0.1",
            "server" => "1.0.0",
            "workstation" => "1.0.0",
            _ => "1.0.0"
        };
    }

    private bool IsUpdateAvailable(string currentVersion, string latestVersion)
    {
        try
        {
            var current = new Version(currentVersion);
            var latest = new Version(latestVersion);
            return latest > current;
        }
        catch
        {
            return false;
        }
    }

    private async Task<UpdateInfo> GetUpdateInfoAsync(string agentType, string version)
    {
        await Task.CompletedTask; // For async consistency

        var agentInfo = await _agentService.GetAgentDownloadInfoAsync(agentType);
        if (agentInfo == null)
        {
            throw new InvalidOperationException($"No download info available for agent type: {agentType}");
        }

        return new UpdateInfo
        {
            Version = version,
            DownloadUrl = $"/api/agents/download/{agentType}",
            FileName = agentInfo.FileName,
            FileSize = agentInfo.FileSize,
            Checksum = await ComputeFileChecksumAsync(agentInfo.FilePath),
            ChecksumAlgorithm = "SHA256",
            ReleasedAt = agentInfo.LastModified,
            ReleaseNotes = $"Update to version {version} with bug fixes and improvements",
            IsCritical = false,
            RequiresRestart = true
        };
    }

    private async Task<string> ComputeFileChecksumAsync(string filePath)
    {
        try
        {
            if (!System.IO.File.Exists(filePath))
                return string.Empty;

            using var sha256 = System.Security.Cryptography.SHA256.Create();
            await using var stream = System.IO.File.OpenRead(filePath);
            var hash = await sha256.ComputeHashAsync(stream);
            return Convert.ToHexString(hash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing file checksum for {FilePath}", filePath);
            return string.Empty;
        }
    }

    [HttpPost("report-errors")]
    public async Task<ActionResult<AgentErrorReportResponse>> ReportErrors([FromBody] AgentErrorReportRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var response = await _agentService.ProcessErrorReportAsync(request);

            if (response.Success)
            {
                return Ok(response);
            }

            return BadRequest(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing error report from agent {AgentId}", request.AgentId);
            return StatusCode(500, new AgentErrorReportResponse
            {
                Success = false,
                Message = "An error occurred while processing the error report."
            });
        }
    }

    [HttpPost("certificates/generate")]
    public async Task<ActionResult<CertificateGenerationResponse>> GenerateCertificate([FromBody] CertificateGenerationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var response = await _agentService.GenerateCertificateAsync(request);

            if (response.Success)
            {
                return Ok(response);
            }

            return BadRequest(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating certificate for agent {AgentId}", request.AgentId);
            return StatusCode(500, new CertificateGenerationResponse
            {
                Success = false,
                Message = "An error occurred while generating the certificate."
            });
        }
    }

    [HttpPost("certificates/validate")]
    public async Task<ActionResult<CertificateValidationResponse>> ValidateCertificate([FromBody] CertificateValidationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var response = await _agentService.ValidateCertificateAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating certificate");
            return StatusCode(500, new CertificateValidationResponse
            {
                IsValid = false,
                ValidationErrors = new List<string> { "An error occurred while validating the certificate." }
            });
        }
    }

    [HttpPost("certificates/renew")]
    public async Task<ActionResult<CertificateRenewalResponse>> RenewCertificate([FromBody] CertificateRenewalRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var response = await _agentService.RenewCertificateAsync(request);

            if (response.Success)
            {
                return Ok(response);
            }

            return BadRequest(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renewing certificate for agent {AgentId}", request.AgentId);
            return StatusCode(500, new CertificateRenewalResponse
            {
                Success = false,
                Message = "An error occurred while renewing the certificate."
            });
        }
    }

    [HttpPost("certificates/revoke")]
    public async Task<ActionResult<CertificateRevocationResponse>> RevokeCertificate([FromBody] CertificateRevocationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var response = await _agentService.RevokeCertificateAsync(request);

            if (response.Success)
            {
                return Ok(response);
            }

            return BadRequest(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking certificate for agent {AgentId}", request.AgentId);
            return StatusCode(500, new CertificateRevocationResponse
            {
                Success = false,
                Message = "An error occurred while revoking the certificate."
            });
        }
    }

    [HttpGet("certificates/{agentId}")]
    public async Task<ActionResult<CertificateListResponse>> ListCertificates(Guid agentId)
    {
        try
        {
            var response = await _agentService.ListCertificatesAsync(agentId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing certificates for agent {AgentId}", agentId);
            return StatusCode(500, new CertificateListResponse
            {
                Success = false,
                Message = "An error occurred while listing certificates."
            });
        }
    }
}
