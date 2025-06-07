using System.ComponentModel.DataAnnotations;

namespace Permafrost2.Shared.DTOs;

// Certificate management DTOs
public class CertificateGenerationRequest
{
    public Guid AgentId { get; set; }
    public string AgentType { get; set; } = string.Empty;
    public string CommonName { get; set; } = string.Empty;
    public string Organization { get; set; } = "Permafrost2";
    public string OrganizationalUnit { get; set; } = "Domain Controller Agent";
    public string Country { get; set; } = "US";
    public string State { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public int ValidityDays { get; set; } = 365;
    public List<string> SubjectAlternativeNames { get; set; } = new();
    public CertificateKeyUsage KeyUsage { get; set; } = CertificateKeyUsage.ClientAuthentication;
}

public class CertificateGenerationResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? CertificateData { get; set; } // Base64 encoded certificate
    public string? PrivateKeyData { get; set; } // Base64 encoded private key
    public string? CertificateChain { get; set; } // Base64 encoded certificate chain
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public string? Thumbprint { get; set; }
    public string? SerialNumber { get; set; }
}

public class CertificateValidationRequest
{
    public string CertificateData { get; set; } = string.Empty; // Base64 encoded certificate
    public bool CheckRevocation { get; set; } = true;
    public bool CheckChain { get; set; } = true;
    public DateTime? ValidateAtTime { get; set; }
}

public class CertificateValidationResponse
{
    public bool IsValid { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public CertificateInfo? CertificateInfo { get; set; }
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
}

public class CertificateRenewalRequest
{
    public Guid AgentId { get; set; }
    public string CurrentThumbprint { get; set; } = string.Empty;
    public int ValidityDays { get; set; } = 365;
    public bool RevokeOldCertificate { get; set; } = true;
}

public class CertificateRenewalResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public CertificateGenerationResponse? NewCertificate { get; set; }
    public string? NewCertificateData { get; set; }
    public string? NewPrivateKeyData { get; set; }
    public string? NewThumbprint { get; set; }
    public string? NewSerialNumber { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool OldCertificateRevoked { get; set; }
    public DateTime RenewedAt { get; set; } = DateTime.UtcNow;
}

public class CertificateRevocationRequest
{
    public Guid AgentId { get; set; }
    public string CertificateThumbprint { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public CertificateRevocationReason Reason { get; set; } = CertificateRevocationReason.Unspecified;
    public string? ReasonText { get; set; }
}

public class CertificateRevocationResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public DateTime RevokedAt { get; set; } = DateTime.UtcNow;
}

public class CertificateInfo
{
    public string Subject { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Thumbprint { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public DateTime NotBefore { get; set; }
    public DateTime NotAfter { get; set; }
    public List<string> SubjectAlternativeNames { get; set; } = new();
    public CertificateKeyUsage KeyUsage { get; set; }
    public CertificateStatus Status { get; set; } = CertificateStatus.Valid;
    public DateTime IssuedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public CertificateRevocationReason? RevocationReason { get; set; }
    public bool IsExpired => DateTime.UtcNow > NotAfter;
    public bool IsNotYetValid => DateTime.UtcNow < NotBefore;
    public TimeSpan TimeUntilExpiry => NotAfter - DateTime.UtcNow;
    public bool IsNearExpiry => TimeUntilExpiry.TotalDays <= 30;
}

public class CertificateListRequest
{
    public Guid? AgentId { get; set; }
    public string? AgentType { get; set; }
    public CertificateStatus? Status { get; set; }
    public bool IncludeExpired { get; set; } = false;
    public bool IncludeRevoked { get; set; } = false;
    public int PageSize { get; set; } = 50;
    public int PageNumber { get; set; } = 1;
}

public class CertificateListResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<CertificateInfo> Certificates { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageSize { get; set; }
    public int PageNumber { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public enum CertificateKeyUsage
{
    ClientAuthentication = 1,
    ServerAuthentication = 2,
    CodeSigning = 4,
    EmailProtection = 8,
    TimeStamping = 16
}

public enum CertificateRevocationReason
{
    Unspecified = 0,
    KeyCompromise = 1,
    CertificateAuthorityCompromise = 2,
    AffiliationChanged = 3,
    Superseded = 4,
    CessationOfOperation = 5,
    CertificateHold = 6,
    RemoveFromCRL = 8,
    PrivilegeWithdrawn = 9,
    AttributeAuthorityCompromise = 10
}

public enum CertificateStatus
{
    Valid = 0,
    Expired = 1,
    Revoked = 2,
    NotYetValid = 3,
    Unknown = 4
}

// Agent certificate configuration
public class AgentCertificateConfiguration
{
    public bool UseCertificateAuthentication { get; set; } = true;
    public string? CertificateThumbprint { get; set; }
    public string? CertificateStoreName { get; set; } = "My";
    public string? CertificateStoreLocation { get; set; } = "CurrentUser";
    public bool AutoRenewCertificate { get; set; } = true;
    public int RenewalThresholdDays { get; set; } = 30;
    public bool ValidateServerCertificate { get; set; } = true;
    public bool CheckCertificateRevocation { get; set; } = true;
    public List<string> TrustedCertificateAuthorities { get; set; } = new();
}

// Server certificate configuration
public class ServerCertificateConfiguration
{
    public bool RequireClientCertificate { get; set; } = true;
    public bool ValidateClientCertificate { get; set; } = true;
    public bool CheckCertificateRevocation { get; set; } = true;
    public List<string> TrustedCertificateAuthorities { get; set; } = new();
    public List<string> AllowedCertificateThumbprints { get; set; } = new();
    public string? ServerCertificateThumbprint { get; set; }
    public string? ServerCertificateStoreName { get; set; } = "My";
    public string? ServerCertificateStoreLocation { get; set; } = "LocalMachine";
}
