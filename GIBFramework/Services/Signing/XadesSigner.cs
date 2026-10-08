using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using GIBFramework.Services.Ubl;

namespace GIBFramework.Services.Signing;

public sealed class XadesSigner(ISigningCertificateProvider certificates, IClock clock) : IXmlSigner
{
    private const string XadesNs = "http://uri.etsi.org/01903/v1.3.2#";
    private const string SignedPropertiesType = "http://uri.etsi.org/01903#SignedProperties";

    public XmlDocument Sign(XmlDocument document, string signatureId)
    {
        ArgumentNullException.ThrowIfNull(document);
        var cert = certificates.GetSigningCertificate();
        using var key = cert.GetRSAPrivateKey()
            ?? throw new DomainException("SIGN_NO_KEY", "İmza sertifikasının RSA özel anahtarına erişilemiyor.");

        var ns = new XmlNamespaceManager(document.NameTable);
        ns.AddNamespace("inv", UblNs.Invoice.NamespaceName);
        ns.AddNamespace("ext", UblNs.Ext.NamespaceName);
        var content = document.SelectSingleNode("/inv:Invoice/ext:UBLExtensions/ext:UBLExtension/ext:ExtensionContent", ns) as XmlElement
            ?? throw new DomainException("SIGN_NO_EXTENSION", "UBL belgesinde imza için ext:ExtensionContent bulunamadı.");
        if (content.HasChildNodes)
        {
            throw new DomainException("SIGN_ALREADY_SIGNED", "Belge zaten imzalanmış.");
        }

        var signedPropertiesId = "SignedProperties_" + signatureId;
        var qualifying = BuildQualifyingProperties(document, signatureId, signedPropertiesId, cert, clock.TurkeyNow);
        var holder = document.CreateElement("holder");
        holder.AppendChild(qualifying);

        var signed = new XadesSignedXml(document) { SigningKey = key, Detached = qualifying };
        signed.Signature.Id = signatureId;
        signed.SignedInfo!.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;
        signed.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA256Url;

        var documentReference = new Reference(string.Empty) { DigestMethod = SignedXml.XmlDsigSHA256Url };
        documentReference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        signed.AddReference(documentReference);

        var propertiesReference = new Reference("#" + signedPropertiesId) { Type = SignedPropertiesType, DigestMethod = SignedXml.XmlDsigSHA256Url };
        propertiesReference.AddTransform(new XmlDsigExcC14NTransform());
        signed.AddReference(propertiesReference);

        var keyInfo = new KeyInfo();
        var x509Data = new KeyInfoX509Data(cert);
        x509Data.AddSubjectName(cert.Subject);
        keyInfo.AddClause(x509Data);
        keyInfo.AddClause(new RSAKeyValue(key));
        signed.KeyInfo = keyInfo;

        signed.AddObject(new DataObject { Data = holder.ChildNodes });
        signed.ComputeSignature();

        content.AppendChild(document.ImportNode(signed.GetXml(), deep: true));
        return document;
    }

    public SignatureVerification Verify(XmlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var nodes = document.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl);
        if (nodes.Count == 0)
        {
            return new SignatureVerification(false, false, null, "İmza yok.");
        }

        if (nodes.Count > 1)
        {
            return new SignatureVerification(true, false, null, "Birden fazla imza bulundu; imza sarmalama (wrapping) riskine karşı reddedildi.");
        }

        try
        {
            var signed = new XadesSignedXml(document);
            signed.LoadXml((XmlElement)nodes[0]!);

            if (!signed.SignedInfo!.References.OfType<Reference>().Any(r => r.Uri == string.Empty))
            {
                return new SignatureVerification(true, false, null, "İmza belgenin tamamını kapsamıyor (URI=\"\" referansı yok).");
            }

            var cert = signed.KeyInfo.OfType<KeyInfoX509Data>()
                .SelectMany(d => d.Certificates?.OfType<X509Certificate2>() ?? [])
                .FirstOrDefault();
            if (cert is null)
            {
                return new SignatureVerification(true, false, null, "İmzada X509 sertifikası yok.");
            }

            var valid = signed.CheckSignature(cert, verifySignatureOnly: true);
            return new SignatureVerification(true, valid, cert.Subject, valid
                ? "Kriptografik imza geçerli. Sertifika zinciri (Kamu SM) ve iptal durumu ayrıca doğrulanmalıdır."
                : "İmza doğrulanamadı: belge imzadan sonra değiştirilmiş olabilir.");
        }
        catch (CryptographicException ex)
        {
            return new SignatureVerification(true, false, null, "İmza işlenemedi: " + ex.Message);
        }
    }

    private static XmlElement BuildQualifyingProperties(XmlDocument doc, string signatureId, string signedPropertiesId, X509Certificate2 cert, DateTimeOffset signingTime)
    {
        XmlElement X(string name) => doc.CreateElement("xades", name, XadesNs);
        XmlElement D(string name) => doc.CreateElement("ds", name, SignedXml.XmlDsigNamespaceUrl);

        var qp = X("QualifyingProperties");
        qp.SetAttribute("xmlns:xades", XadesNs);
        qp.SetAttribute("xmlns:ds", SignedXml.XmlDsigNamespaceUrl);
        qp.SetAttribute("Target", "#" + signatureId);

        var sp = X("SignedProperties");
        sp.SetAttribute("Id", signedPropertiesId);
        var ssp = X("SignedSignatureProperties");

        var time = X("SigningTime");
        time.InnerText = signingTime.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

        var digestMethod = D("DigestMethod");
        digestMethod.SetAttribute("Algorithm", SignedXml.XmlDsigSHA256Url);
        var digestValue = D("DigestValue");
        digestValue.InnerText = Convert.ToBase64String(SHA256.HashData(cert.RawData));
        var certDigest = X("CertDigest");
        certDigest.AppendChild(digestMethod);
        certDigest.AppendChild(digestValue);

        var issuerName = D("X509IssuerName");
        issuerName.InnerText = cert.Issuer;
        var serial = D("X509SerialNumber");
        serial.InnerText = BigInteger.Parse("0" + cert.SerialNumber, NumberStyles.HexNumber, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        var issuerSerial = X("IssuerSerial");
        issuerSerial.AppendChild(issuerName);
        issuerSerial.AppendChild(serial);

        var certElement = X("Cert");
        certElement.AppendChild(certDigest);
        certElement.AppendChild(issuerSerial);
        var signingCertificate = X("SigningCertificate");
        signingCertificate.AppendChild(certElement);

        ssp.AppendChild(time);
        ssp.AppendChild(signingCertificate);

        var claimedRole = X("ClaimedRole");
        claimedRole.InnerText = "Supplier";
        var claimedRoles = X("ClaimedRoles");
        claimedRoles.AppendChild(claimedRole);
        var signerRole = X("SignerRole");
        signerRole.AppendChild(claimedRoles);
        ssp.AppendChild(signerRole);
        sp.AppendChild(ssp);
        qp.AppendChild(sp);
        return qp;
    }

    private sealed class XadesSignedXml(XmlDocument document) : SignedXml(document)
    {
        public XmlElement? Detached { get; init; }

        public override XmlElement? GetIdElement(XmlDocument? document, string idValue)
        {
            var found = base.GetIdElement(document, idValue);
            if (found is not null || Detached is null)
            {
                return found;
            }

            return Detached.GetElementsByTagName("*").OfType<XmlElement>()
                .FirstOrDefault(e => e.GetAttribute("Id") == idValue);
        }
    }
}
