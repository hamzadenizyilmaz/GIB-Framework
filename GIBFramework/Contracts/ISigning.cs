using System.Security.Cryptography.X509Certificates;
using System.Xml;

namespace GIBFramework.Contracts;

public interface ISigningCertificateProvider
{
    SigningMode Mode { get; }

    X509Certificate2 GetSigningCertificate();
}

public sealed record SignatureVerification(bool IsPresent, bool IsValid, string? SignerSubject, string? Problem,
    bool IntegrityValid = false, bool? CertificateTrusted = null);

public interface IXmlSigner
{
    XmlDocument Sign(XmlDocument document, string signatureId);

    SignatureVerification Verify(XmlDocument document, bool requireTrustedCertificate = false);
}
