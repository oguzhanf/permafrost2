using Permafrost2.Data.Models;
using Permafrost2.Shared.DTOs;

namespace Permafrost2.Api.Services;

public class AgentDownloadInfo
{
    public string AgentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime LastModified { get; set; }
    public string Description { get; set; } = string.Empty;
}

public interface IAgentService
{
    Task<AgentRegistrationResponse> RegisterAgentAsync(AgentRegistrationRequest request);
    Task<AgentHeartbeatResponse> ProcessHeartbeatAsync(AgentHeartbeatRequest request);
    Task<DataSubmissionResponse> ProcessDataSubmissionAsync(DataSubmissionRequest request);
    Task<IEnumerable<AgentStatusDto>> GetAgentsAsync();
    Task<AgentStatusDto?> GetAgentAsync(Guid agentId);
    Task<bool> UpdateAgentConfigurationAsync(Guid agentId, AgentConfigurationDto configuration);
    Task<bool> DeactivateAgentAsync(Guid agentId);
    Task<IEnumerable<AgentDataSubmission>> GetAgentSubmissionsAsync(Guid agentId, int limit = 50);
    Task<AgentDownloadInfo?> GetAgentDownloadInfoAsync(string agentType);
    Task<AgentErrorReportResponse> ProcessErrorReportAsync(AgentErrorReportRequest request);

    // Certificate management methods
    Task<CertificateGenerationResponse> GenerateCertificateAsync(CertificateGenerationRequest request);
    Task<CertificateValidationResponse> ValidateCertificateAsync(CertificateValidationRequest request);
    Task<CertificateRenewalResponse> RenewCertificateAsync(CertificateRenewalRequest request);
    Task<CertificateRevocationResponse> RevokeCertificateAsync(CertificateRevocationRequest request);
    Task<CertificateListResponse> ListCertificatesAsync(Guid agentId);
}
