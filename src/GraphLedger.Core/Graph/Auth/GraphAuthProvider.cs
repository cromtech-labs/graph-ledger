using Azure.Identity;
using GraphLedger.Core.Models;
using Microsoft.Graph.Beta;

namespace GraphLedger.Core.Graph.Auth;

public class GraphAuthProvider
{
    private readonly AzureSettings _settings;

    public GraphAuthProvider(AzureSettings settings)
    {
        _settings = settings;
    }

    public GraphServiceClient CreateClient()
    {
        if (!string.IsNullOrEmpty(_settings.ClientSecret))
        {
            var clientSecretCredential = new ClientSecretCredential(
                _settings.TenantId,
                _settings.ClientId,
                _settings.ClientSecret);

            return new GraphServiceClient(clientSecretCredential, _settings.Scopes);
        }

        if (!string.IsNullOrEmpty(_settings.CertificateThumbprint))
        {
            var certificate = LoadCertificateByThumbprint(_settings.CertificateThumbprint);
            var clientCertificateCredential = new ClientCertificateCredential(
                _settings.TenantId,
                _settings.ClientId,
                certificate);

            return new GraphServiceClient(clientCertificateCredential, _settings.Scopes);
        }

        var defaultCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            TenantId = _settings.TenantId
        });

        return new GraphServiceClient(defaultCredential, _settings.Scopes);
    }

    private static System.Security.Cryptography.X509Certificates.X509Certificate2 LoadCertificateByThumbprint(string thumbprint)
    {
        using var store = new System.Security.Cryptography.X509Certificates.X509Store(
            System.Security.Cryptography.X509Certificates.StoreName.My,
            System.Security.Cryptography.X509Certificates.StoreLocation.CurrentUser);

        store.Open(System.Security.Cryptography.X509Certificates.OpenFlags.ReadOnly);

        var certificates = store.Certificates.Find(
            System.Security.Cryptography.X509Certificates.X509FindType.FindByThumbprint,
            thumbprint,
            validOnly: false);

        if (certificates.Count == 0)
        {
            throw new InvalidOperationException($"Certificate with thumbprint '{thumbprint}' not found.");
        }

        return certificates[0];
    }
}
