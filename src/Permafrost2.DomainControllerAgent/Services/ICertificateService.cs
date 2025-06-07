using System.Security.Cryptography.X509Certificates;
using Permafrost2.Shared.DTOs;

namespace Permafrost2.DomainControllerAgent.Services;

public interface ICertificateService
{
    /// <summary>
    /// Generates a new client certificate for the agent
    /// </summary>
    Task<CertificateGenerationResponse> GenerateCertificateAsync(CertificateGenerationRequest request);

    /// <summary>
    /// Validates a certificate
    /// </summary>
    Task<CertificateValidationResponse> ValidateCertificateAsync(CertificateValidationRequest request);

    /// <summary>
    /// Renews an existing certificate
    /// </summary>
    Task<CertificateRenewalResponse> RenewCertificateAsync(CertificateRenewalRequest request);

    /// <summary>
    /// Installs a certificate to the local certificate store
    /// </summary>
    Task<bool> InstallCertificateAsync(string certificateData, string privateKeyData, 
        StoreName storeName = StoreName.My, StoreLocation storeLocation = StoreLocation.CurrentUser);

    /// <summary>
    /// Removes a certificate from the local certificate store
    /// </summary>
    Task<bool> RemoveCertificateAsync(string thumbprint, 
        StoreName storeName = StoreName.My, StoreLocation storeLocation = StoreLocation.CurrentUser);

    /// <summary>
    /// Gets a certificate from the local certificate store
    /// </summary>
    Task<X509Certificate2?> GetCertificateAsync(string thumbprint, 
        StoreName storeName = StoreName.My, StoreLocation storeLocation = StoreLocation.CurrentUser);

    /// <summary>
    /// Lists certificates in the local certificate store
    /// </summary>
    Task<List<CertificateInfo>> ListCertificatesAsync(
        StoreName storeName = StoreName.My, StoreLocation storeLocation = StoreLocation.CurrentUser);

    /// <summary>
    /// Checks if a certificate needs renewal
    /// </summary>
    Task<bool> NeedsRenewalAsync(string thumbprint, int thresholdDays = 30);

    /// <summary>
    /// Gets the current agent certificate configuration
    /// </summary>
    Task<AgentCertificateConfiguration> GetCertificateConfigurationAsync();

    /// <summary>
    /// Updates the agent certificate configuration
    /// </summary>
    Task<bool> UpdateCertificateConfigurationAsync(AgentCertificateConfiguration configuration);

    /// <summary>
    /// Exports a certificate to PEM format
    /// </summary>
    Task<string> ExportCertificateToPemAsync(string thumbprint);

    /// <summary>
    /// Imports a certificate from PEM format
    /// </summary>
    Task<bool> ImportCertificateFromPemAsync(string pemData, string? privateKeyPem = null);

    /// <summary>
    /// Creates a certificate signing request (CSR)
    /// </summary>
    Task<string> CreateCertificateSigningRequestAsync(CertificateGenerationRequest request);

    /// <summary>
    /// Installs a certificate from a CSR response
    /// </summary>
    Task<bool> InstallCertificateFromResponseAsync(string certificateResponse, string privateKeyData);

    /// <summary>
    /// Validates certificate chain
    /// </summary>
    Task<bool> ValidateCertificateChainAsync(X509Certificate2 certificate, bool checkRevocation = true);

    /// <summary>
    /// Gets certificate information
    /// </summary>
    Task<CertificateInfo?> GetCertificateInfoAsync(string thumbprint);

    /// <summary>
    /// Checks if certificate is trusted
    /// </summary>
    Task<bool> IsCertificateTrustedAsync(X509Certificate2 certificate);
}
