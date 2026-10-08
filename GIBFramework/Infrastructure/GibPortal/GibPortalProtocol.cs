namespace GIBFramework.Infrastructure.GibPortal;

public static class GibPortalProtocol
{
    public const string LoginPath = "/earsiv-services/assos-login";
    public const string DispatchPath = "/earsiv-services/dispatch";
    public const string DownloadPath = "/earsiv-services/download";
    public const string EsignPath = "/earsiv-services/esign";
    public const string RefererPath = "/intragiris.html";

    public const string LoginCommandProduction = "anologin";
    public const string LoginCommandTest = "login";
    public const string LogoutCommand = "logout";
    public const string SuggestTestUserCommand = "kullaniciOner";
    public const string TestUserPassword = "1";

    public const string CreateDraft = "EARSIV_PORTAL_FATURA_OLUSTUR";
    public const string ListDrafts = "EARSIV_PORTAL_TASLAKLARI_GETIR";
    public const string ListIssuedToMe = "EARSIV_PORTAL_ADIMA_KESILEN_BELGELERI_GETIR";
    public const string GetDocument = "EARSIV_PORTAL_FATURA_GETIR";
    public const string DeleteDrafts = "EARSIV_PORTAL_FATURA_SIL";
    public const string ShowHtml = "EARSIV_PORTAL_FATURA_GOSTER";
    public const string RecipientInfo = "SICIL_VEYA_MERNISTEN_BILGILERI_GETIR";
    public const string UserInfo = "EARSIV_PORTAL_KULLANICI_BILGILERI_GETIR";
    public const string PhoneNumber = "EARSIV_PORTAL_TELEFONNO_SORGULA";
    public const string SendSms = "EARSIV_PORTAL_SMSSIFRE_GONDER";
    public const string VerifySmsAndSign = "0lhozfib5410mp";
    public const string SignWithHsm = "EARSIV_PORTAL_FATURA_HSM_CIHAZI_ILE_IMZALA";
    public const string CancellationRequest = "EARSIV_PORTAL_IPTAL_TALEBI_OLUSTUR";
    public const string ObjectionRequest = "EARSIV_PORTAL_ITIRAZ_TALEBI_OLUSTUR";
    public const string DownloadCommand = "EARSIV_PORTAL_BELGE_INDIR";

    public const string PageInvoice = "RG_BASITFATURA";
    public const string PageDrafts = "RG_BASITTASLAKLAR";
    public const string PageDocuments = "RG_TASLAKLAR";
    public const string PageIssuedToMe = "RG_ALICI_TASLAKLAR";
    public const string PageUser = "RG_KULLANICI";
    public const string PageSms = "RG_SMSONAY";

    public const string InvoiceKind = "5000/30000";
    public const string Approved = "Onaylandı";
    public const string NotApproved = "Onaylanmadı";
    public const string CreateSuccessFragment = "başarıyla oluşturul";
}
