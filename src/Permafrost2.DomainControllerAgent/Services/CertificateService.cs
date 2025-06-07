using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Permafrost2.Shared.DTOs;

namespace Permafrost2.DomainControllerAgent.Services;

public class CertificateService : ICertificateService
{
    private readonly ILogger<CertificateService> _logger;
    private readonly IApiClient _apiClient;
    private readonly IConfigurationManager _configurationManager;
    private readonly IErrorReportingService _errorReportingService;

    public CertificateService(
        ILogger<CertificateService> logger,
        IApiClient apiClient,
        IConfigurationManager configurationManager,
        IErrorReportingService errorReportingService)
    {
        _logger = logger;
        _apiClient = apiClient;
        _configurationManager = configurationManager;
        _errorReportingService = errorReportingService;
    }

    public async Task<CertificateGenerationResponse> GenerateCertificateAsync(CertificateGenerationRequest request)
    {
        try
        {
            _logger.LogInformation("Generating certificate for agent {AgentId}", request.AgentId);

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

            // Add subject alternative names if provided
            if (request.SubjectAlternativeNames.Count > 0)
            {
                var sanBuilder = new SubjectAlternativeNameBuilder();
                foreach (var san in request.SubjectAlternativeNames)
                {
                    if (san.Contains("@"))
                        sanBuilder.AddEmailAddress(san);
                    else if (System.Net.IPAddress.TryParse(san, out _))
                        sanBuilder.AddIpAddress(System.Net.IPAddress.Parse(san));
                    else
                        sanBuilder.AddDnsName(san);
                }
                certificateRequest.CertificateExtensions.Add(sanBuilder.Build());
            }

            // Create the certificate
            var notBefore = DateTime.UtcNow.AddDays(-1);
            var notAfter = notBefore.AddDays(request.ValidityDays);

            using var certificate = certificateRequest.CreateSelfSigned(notBefore, notAfter);

            // Export certificate and private key
            var certificateData = Convert.ToBase64String(certificate.Export(X509ContentType.Cert));
            var privateKeyData = Convert.ToBase64String(certificate.Export(X509ContentType.Pfx));

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
            
            await _errorReportingService.ReportErrorAsync(
                AgentErrorSeverity.High,
                AgentErrorCategory.Service,
                "CertificateService.GenerateCertificateAsync",
                "Failed to generate certificate",
                ex);

            return new CertificateGenerationResponse
            {
                Success = false,
                Message = $"Certificate generation failed: {ex.Message}"
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
                CertificateInfo = await CreateCertificateInfoAsync(certificate)
            };

            var validationErrors = new List<string>();

            // Check expiration
            var now = request.ValidateAtTime ?? DateTime.UtcNow;
            if (certificate.NotAfter < now)
                validationErrors.Add("Certificate has expired");
            if (certificate.NotBefore > now)
                validationErrors.Add("Certificate is not yet valid");

            // Validate certificate chain if requested
            if (request.CheckChain)
            {
                var isChainValid = await ValidateCertificateChainAsync(certificate, request.CheckRevocation);
                if (!isChainValid)
                    validationErrors.Add("Certificate chain validation failed");
            }

            response.ValidationErrors = validationErrors;
            response.IsValid = validationErrors.Count == 0;

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate certificate");
            
            return new CertificateValidationResponse
            {
                IsValid = false,
                ValidationErrors = new List<string> { $"Validation failed: {ex.Message}" }
            };
        }
    }

    public async Task<CertificateRenewalResponse> RenewCertificateAsync(CertificateRenewalRequest request)
    {
        try
        {
            _logger.LogInformation("Renewing certificate for agent {AgentId}", request.AgentId);

            // Get current certificate info
            var currentCert = await GetCertificateAsync(request.CurrentThumbprint);
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

            // Install new certificate
            var installed = await InstallCertificateAsync(
                newCertResponse.CertificateData!,
                newCertResponse.PrivateKeyData!);

            if (!installed)
            {
                return new CertificateRenewalResponse
                {
                    Success = false,
                    Message = "Failed to install new certificate"
                };
            }

            // Remove old certificate if requested
            bool oldCertRevoked = false;
            if (request.RevokeOldCertificate)
            {
                oldCertRevoked = await RemoveCertificateAsync(request.CurrentThumbprint);
            }

            return new CertificateRenewalResponse
            {
                Success = true,
                Message = "Certificate renewed successfully",
                NewCertificate = newCertResponse,
                OldCertificateRevoked = oldCertRevoked
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to renew certificate for agent {AgentId}", request.AgentId);
            
            await _errorReportingService.ReportErrorAsync(
                AgentErrorSeverity.High,
                AgentErrorCategory.Service,
                "CertificateService.RenewCertificateAsync",
                "Failed to renew certificate",
                ex);

            return new CertificateRenewalResponse
            {
                Success = false,
                Message = $"Certificate renewal failed: {ex.Message}"
            };
        }
    }

    public async Task<bool> InstallCertificateAsync(string certificateData, string privateKeyData,
        StoreName storeName = StoreName.My, StoreLocation storeLocation = StoreLocation.CurrentUser)
    {
        try
        {
            var certificateBytes = Convert.FromBase64String(certificateData);
            var privateKeyBytes = Convert.FromBase64String(privateKeyData);

            // Create certificate with private key
            using var certificate = X509CertificateLoader.LoadPkcs12(privateKeyBytes, null,
                X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

            // Install to certificate store
            using var store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadWrite);
            store.Add(certificate);
            store.Close();

            _logger.LogInformation("Certificate installed successfully, thumbprint: {Thumbprint}", certificate.Thumbprint);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install certificate");
            
            await _errorReportingService.ReportErrorAsync(
                AgentErrorSeverity.Medium,
                AgentErrorCategory.Configuration,
                "CertificateService.InstallCertificateAsync",
                "Failed to install certificate",
                ex);

            return false;
        }
    }

    public async Task<bool> RemoveCertificateAsync(string thumbprint,
        StoreName storeName = StoreName.My, StoreLocation storeLocation = StoreLocation.CurrentUser)
    {
        try
        {
            using var store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadWrite);

            var certificates = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
            if (certificates.Count > 0)
            {
                store.Remove(certificates[0]);
                _logger.LogInformation("Certificate removed successfully, thumbprint: {Thumbprint}", thumbprint);
                return true;
            }

            _logger.LogWarning("Certificate not found for removal, thumbprint: {Thumbprint}", thumbprint);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove certificate with thumbprint: {Thumbprint}", thumbprint);
            return false;
        }
        finally
        {
            await Task.CompletedTask;
        }
    }

    public async Task<X509Certificate2?> GetCertificateAsync(string thumbprint,
        StoreName storeName = StoreName.My, StoreLocation storeLocation = StoreLocation.CurrentUser)
    {
        try
        {
            using var store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadOnly);

            var certificates = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
            return certificates.Count > 0 ? certificates[0] : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get certificate with thumbprint: {Thumbprint}", thumbprint);
            return null;
        }
        finally
        {
            await Task.CompletedTask;
        }
    }

    public async Task<List<CertificateInfo>> ListCertificatesAsync(
        StoreName storeName = StoreName.My, StoreLocation storeLocation = StoreLocation.CurrentUser)
    {
        try
        {
            var certificateInfos = new List<CertificateInfo>();

            using var store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadOnly);

            foreach (var certificate in store.Certificates)
            {
                var info = await CreateCertificateInfoAsync(certificate);
                certificateInfos.Add(info);
            }

            return certificateInfos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list certificates");
            return new List<CertificateInfo>();
        }
    }

    public async Task<bool> NeedsRenewalAsync(string thumbprint, int thresholdDays = 30)
    {
        try
        {
            var certificate = await GetCertificateAsync(thumbprint);
            if (certificate == null)
                return false;

            var daysUntilExpiry = (certificate.NotAfter - DateTime.UtcNow).TotalDays;
            return daysUntilExpiry <= thresholdDays;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check certificate renewal status");
            return false;
        }
    }

    public async Task<AgentCertificateConfiguration> GetCertificateConfigurationAsync()
    {
        try
        {
            var configJson = await _configurationManager.GetSettingAsync("CertificateConfiguration");
            if (string.IsNullOrEmpty(configJson))
            {
                return new AgentCertificateConfiguration();
            }

            var config = JsonSerializer.Deserialize<AgentCertificateConfiguration>(configJson);
            return config ?? new AgentCertificateConfiguration();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get certificate configuration");
            return new AgentCertificateConfiguration();
        }
    }

    public async Task<bool> UpdateCertificateConfigurationAsync(AgentCertificateConfiguration configuration)
    {
        try
        {
            var configJson = JsonSerializer.Serialize(configuration);
            await _configurationManager.SetSettingAsync("CertificateConfiguration", configJson);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update certificate configuration");
            return false;
        }
    }

    public async Task<string> ExportCertificateToPemAsync(string thumbprint)
    {
        try
        {
            var certificate = await GetCertificateAsync(thumbprint);
            if (certificate == null)
                throw new InvalidOperationException("Certificate not found");

            var certBytes = certificate.Export(X509ContentType.Cert);
            var base64 = Convert.ToBase64String(certBytes);

            var pem = new StringBuilder();
            pem.AppendLine("-----BEGIN CERTIFICATE-----");

            for (int i = 0; i < base64.Length; i += 64)
            {
                var length = Math.Min(64, base64.Length - i);
                pem.AppendLine(base64.Substring(i, length));
            }

            pem.AppendLine("-----END CERTIFICATE-----");

            return pem.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export certificate to PEM format");
            throw;
        }
    }

    public async Task<bool> ImportCertificateFromPemAsync(string pemData, string? privateKeyPem = null)
    {
        try
        {
            // Extract certificate data from PEM
            var certStart = pemData.IndexOf("-----BEGIN CERTIFICATE-----");
            var certEnd = pemData.IndexOf("-----END CERTIFICATE-----");

            if (certStart == -1 || certEnd == -1)
                throw new ArgumentException("Invalid PEM format");

            var certData = pemData.Substring(certStart + 27, certEnd - certStart - 27)
                .Replace("\r", "").Replace("\n", "").Replace(" ", "");

            var certificateBytes = Convert.FromBase64String(certData);

            X509Certificate2 certificate;
            if (!string.IsNullOrEmpty(privateKeyPem))
            {
                // Handle private key if provided
                certificate = X509CertificateLoader.LoadCertificate(certificateBytes);
                // Note: Private key handling would need additional implementation
            }
            else
            {
                certificate = X509CertificateLoader.LoadCertificate(certificateBytes);
            }

            using (certificate)
            {
                using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadWrite);
                store.Add(certificate);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import certificate from PEM");
            return false;
        }
        finally
        {
            await Task.CompletedTask;
        }
    }

    public async Task<string> CreateCertificateSigningRequestAsync(CertificateGenerationRequest request)
    {
        try
        {
            using var rsa = RSA.Create(2048);
            var certificateRequest = new CertificateRequest(
                $"CN={request.CommonName}, O={request.Organization}, OU={request.OrganizationalUnit}, C={request.Country}",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // Add extensions
            certificateRequest.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                    critical: true));

            certificateRequest.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection { new Oid("1.3.6.1.5.5.7.3.2") },
                    critical: true));

            var csr = certificateRequest.CreateSigningRequest();
            return Convert.ToBase64String(csr);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create certificate signing request");
            throw;
        }
        finally
        {
            await Task.CompletedTask;
        }
    }

    public async Task<bool> InstallCertificateFromResponseAsync(string certificateResponse, string privateKeyData)
    {
        try
        {
            return await InstallCertificateAsync(certificateResponse, privateKeyData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install certificate from response");
            return false;
        }
    }

    public async Task<bool> ValidateCertificateChainAsync(X509Certificate2 certificate, bool checkRevocation = true)
    {
        try
        {
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = checkRevocation ? X509RevocationMode.Online : X509RevocationMode.NoCheck;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

            return chain.Build(certificate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate certificate chain");
            return false;
        }
        finally
        {
            await Task.CompletedTask;
        }
    }

    public async Task<CertificateInfo?> GetCertificateInfoAsync(string thumbprint)
    {
        try
        {
            var certificate = await GetCertificateAsync(thumbprint);
            return certificate != null ? await CreateCertificateInfoAsync(certificate) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get certificate info");
            return null;
        }
    }

    public async Task<bool> IsCertificateTrustedAsync(X509Certificate2 certificate)
    {
        try
        {
            return await ValidateCertificateChainAsync(certificate, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check certificate trust");
            return false;
        }
    }

    private async Task<CertificateInfo> CreateCertificateInfoAsync(X509Certificate2 certificate)
    {
        if (certificate == null)
            throw new ArgumentNullException(nameof(certificate));

        var info = new CertificateInfo
        {
            Subject = certificate.Subject,
            Issuer = certificate.Issuer,
            Thumbprint = certificate.Thumbprint,
            SerialNumber = certificate.SerialNumber,
            NotBefore = certificate.NotBefore,
            NotAfter = certificate.NotAfter,
            KeyUsage = CertificateKeyUsage.ClientAuthentication // Default, could be parsed from extensions
        };

        // Extract Subject Alternative Names
        foreach (var extension in certificate.Extensions)
        {
            if (extension.Oid?.Value == "2.5.29.17") // Subject Alternative Name
            {
                // Parse SAN extension (simplified)
                var sanExtension = extension as X509SubjectAlternativeNameExtension;
                if (sanExtension != null)
                {
                    // Note: This would need proper SAN parsing implementation
                    info.SubjectAlternativeNames.Add(sanExtension.Format(false));
                }
            }
        }

        await Task.CompletedTask;
        return info;
    }

    private string GetCommonNameFromSubject(string subject)
    {
        var parts = subject.Split(',');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("CN="))
            {
                return trimmed.Substring(3);
            }
        }
        return "Unknown";
    }
}
