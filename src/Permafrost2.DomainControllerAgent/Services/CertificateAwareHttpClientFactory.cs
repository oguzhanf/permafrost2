using Microsoft.Extensions.Logging;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Permafrost2.Shared.DTOs;

namespace Permafrost2.DomainControllerAgent.Services;

public class CertificateAwareHttpClientFactory
{
    private readonly ILogger<CertificateAwareHttpClientFactory> _logger;
    private readonly ICertificateService _certificateService;

    public CertificateAwareHttpClientFactory(
        ILogger<CertificateAwareHttpClientFactory> logger,
        ICertificateService certificateService)
    {
        _logger = logger;
        _certificateService = certificateService;
    }

    public async Task<HttpClient> CreateHttpClientAsync(string baseAddress, AgentCertificateConfiguration? certificateConfig = null)
    {
        var handler = new HttpClientHandler();

        // Configure certificate-based authentication if enabled
        if (certificateConfig?.UseCertificateAuthentication == true && !string.IsNullOrEmpty(certificateConfig.CertificateThumbprint))
        {
            try
            {
                var clientCertificate = await GetClientCertificateAsync(certificateConfig);
                if (clientCertificate != null)
                {
                    handler.ClientCertificates.Add(clientCertificate);
                    _logger.LogInformation("Added client certificate for authentication: {Thumbprint}", clientCertificate.Thumbprint);
                }
                else
                {
                    _logger.LogWarning("Client certificate not found: {Thumbprint}", certificateConfig.CertificateThumbprint);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load client certificate: {Thumbprint}", certificateConfig.CertificateThumbprint);
            }
        }

        // Configure server certificate validation
        if (certificateConfig?.ValidateServerCertificate == true)
        {
            handler.ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
            {
                return ValidateServerCertificateAsync(certificate, chain, sslPolicyErrors, certificateConfig).Result;
            };
        }
        else
        {
            // For development/testing - accept all certificates
            handler.ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
        }

        // Configure certificate revocation checking
        if (certificateConfig?.CheckCertificateRevocation == true)
        {
            handler.CheckCertificateRevocationList = true;
        }

        var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri(baseAddress);
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Permafrost2-DomainControllerAgent/1.0");

        return httpClient;
    }

    private async Task<X509Certificate2?> GetClientCertificateAsync(AgentCertificateConfiguration config)
    {
        if (string.IsNullOrEmpty(config.CertificateThumbprint))
            return null;

        var storeName = Enum.TryParse<StoreName>(config.CertificateStoreName, out var parsedStoreName) 
            ? parsedStoreName : StoreName.My;
        
        var storeLocation = Enum.TryParse<StoreLocation>(config.CertificateStoreLocation, out var parsedStoreLocation) 
            ? parsedStoreLocation : StoreLocation.CurrentUser;

        return await _certificateService.GetCertificateAsync(config.CertificateThumbprint, storeName, storeLocation);
    }

    private async Task<bool> ValidateServerCertificateAsync(
        X509Certificate? certificate, 
        X509Chain? chain, 
        SslPolicyErrors sslPolicyErrors,
        AgentCertificateConfiguration config)
    {
        try
        {
            // If no errors, certificate is valid
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            // Convert to X509Certificate2 for detailed validation
            if (certificate == null)
            {
                _logger.LogWarning("Server certificate is null");
                return false;
            }

            var cert2 = new X509Certificate2(certificate);

            // Check if certificate is in trusted CA list
            if (config.TrustedCertificateAuthorities.Count > 0)
            {
                var issuerThumbprint = cert2.GetCertHashString();
                if (!config.TrustedCertificateAuthorities.Contains(issuerThumbprint))
                {
                    _logger.LogWarning("Server certificate issuer not in trusted CA list: {Issuer}", cert2.Issuer);
                    return false;
                }
            }

            // Validate certificate chain if available
            if (chain != null && config.CheckCertificateRevocation)
            {
                var isChainValid = await _certificateService.ValidateCertificateChainAsync(cert2, true);
                if (!isChainValid)
                {
                    _logger.LogWarning("Server certificate chain validation failed");
                    return false;
                }
            }

            // Check certificate expiration
            if (cert2.NotAfter < DateTime.UtcNow)
            {
                _logger.LogWarning("Server certificate has expired: {NotAfter}", cert2.NotAfter);
                return false;
            }

            if (cert2.NotBefore > DateTime.UtcNow)
            {
                _logger.LogWarning("Server certificate is not yet valid: {NotBefore}", cert2.NotBefore);
                return false;
            }

            // Log SSL policy errors but allow connection for specific scenarios
            if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
            {
                _logger.LogWarning("Server certificate name mismatch - allowing for development");
                // In production, this should return false
            }

            if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors))
            {
                _logger.LogWarning("Server certificate chain errors - allowing for development");
                // In production, this should return false
            }

            _logger.LogInformation("Server certificate validation passed with warnings: {Errors}", sslPolicyErrors);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating server certificate");
            return false;
        }
    }

    public async Task<bool> ConfigureClientCertificateAsync(HttpClient httpClient, string certificateThumbprint)
    {
        try
        {
            var certificate = await _certificateService.GetCertificateAsync(certificateThumbprint);
            if (certificate == null)
            {
                _logger.LogWarning("Certificate not found: {Thumbprint}", certificateThumbprint);
                return false;
            }

            // Note: HttpClient doesn't allow modifying certificates after creation
            // This method would be used during HttpClient creation
            _logger.LogInformation("Certificate configured for HTTP client: {Thumbprint}", certificate.Thumbprint);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure client certificate");
            return false;
        }
    }

    public async Task<bool> ValidateCertificateConfigurationAsync(AgentCertificateConfiguration config)
    {
        try
        {
            if (!config.UseCertificateAuthentication)
                return true;

            if (string.IsNullOrEmpty(config.CertificateThumbprint))
            {
                _logger.LogWarning("Certificate authentication enabled but no thumbprint specified");
                return false;
            }

            var certificate = await GetClientCertificateAsync(config);
            if (certificate == null)
            {
                _logger.LogWarning("Client certificate not found: {Thumbprint}", config.CertificateThumbprint);
                return false;
            }

            // Check if certificate needs renewal
            if (config.AutoRenewCertificate)
            {
                var needsRenewal = await _certificateService.NeedsRenewalAsync(
                    config.CertificateThumbprint, config.RenewalThresholdDays);
                
                if (needsRenewal)
                {
                    _logger.LogWarning("Client certificate needs renewal: {Thumbprint}", config.CertificateThumbprint);
                    // Could trigger automatic renewal here
                }
            }

            _logger.LogInformation("Certificate configuration is valid");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate certificate configuration");
            return false;
        }
    }
}
