using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace GIBFramework.Infrastructure.Signing;

public sealed class DevSelfSignedCertificateProvider(IClock clock) : ISigningCertificateProvider, IDisposable
{
    private readonly Lazy<X509Certificate2> _certificate = new(() => Create(clock.UtcNow));

    public SigningMode Mode => SigningMode.DevSelfSigned;

    public X509Certificate2 GetSigningCertificate() => _certificate.Value;

    public void Dispose()
    {
        if (_certificate.IsValueCreated)
        {
            _certificate.Value.Dispose();
        }
    }

    private static X509Certificate2 Create(DateTimeOffset now)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=GIB Framework Test, O=GIB Framework, C=TR",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, critical: true));
        using var ephemeral = request.CreateSelfSigned(now.AddMinutes(-5), now.AddYears(1));
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), password: null, X509KeyStorageFlags.EphemeralKeySet);
    }
}

public sealed class WindowsStoreCertificateProvider(SigningOptions options) : ISigningCertificateProvider
{
    private readonly Lazy<X509Certificate2> _certificate = new(() => Find(options));

    public SigningMode Mode => SigningMode.WindowsStore;

    public X509Certificate2 GetSigningCertificate() => _certificate.Value;

    private static X509Certificate2 Find(SigningOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Thumbprint))
        {
            throw new InvalidOperationException("Signing:Thumbprint yapılandırılmamış.");
        }

        var location = Enum.Parse<StoreLocation>(options.StoreLocation, ignoreCase: true);
        using var store = new X509Store(StoreName.My, location);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        var thumbprint = options.Thumbprint.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        var cert = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false).OfType<X509Certificate2>().FirstOrDefault()
            ?? throw new InvalidOperationException($"Sertifika bulunamadı: {location}\\My\\{thumbprint}");
        return cert.HasPrivateKey
            ? cert
            : throw new InvalidOperationException("Sertifikanın özel anahtarına erişim yok (token takılı mı, sürücü kurulu mu, servis hesabına yetki verildi mi?).");
    }
}

public sealed class Pkcs11CertificateProvider : ISigningCertificateProvider
{
    public SigningMode Mode => SigningMode.Pkcs11;

    public X509Certificate2 GetSigningCertificate() =>
        throw new NotSupportedException("PKCS#11 imza sağlayıcısı bu sürümde etkin değil. HSM üreticisinin KSP/CNG sağlayıcısı ile Signing:Mode=WindowsStore kullanın.");
}
