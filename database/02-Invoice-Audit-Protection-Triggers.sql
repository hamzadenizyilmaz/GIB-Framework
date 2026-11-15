SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER TRIGGER dbo.TR_Invoices_NoDelete ON dbo.Invoices INSTEAD OF DELETE AS
BEGIN
    THROW 51001, N'Mali belgeler fiziksel olarak silinemez (VUK 253).', 1;
END
GO

CREATE OR ALTER TRIGGER dbo.TR_Invoices_Immutable ON dbo.Invoices AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted d ON d.Id = i.Id
        WHERE i.TenantId <> d.TenantId
           OR (i.Ettn <> d.Ettn
               AND (d.SignedXmlSha256 IS NOT NULL OR d.DocumentNumber IS NOT NULL
                    OR d.Status NOT IN ('Draft','Validating','Validated','AwaitingApproval','Approved','Signing')))
           OR (d.DocumentNumber IS NOT NULL AND (i.DocumentNumber IS NULL OR i.DocumentNumber <> d.DocumentNumber))
           OR (d.DraftNumber IS NOT NULL AND (i.DraftNumber IS NULL OR i.DraftNumber <> d.DraftNumber))
           OR (d.SignedXmlSha256 IS NOT NULL AND (i.SignedXmlSha256 IS NULL OR i.SignedXmlSha256 <> d.SignedXmlSha256))
           OR (d.Status NOT IN ('Draft','Validating','Validated','AwaitingApproval','Approved','Signing')
               AND (i.ContentJson <> d.ContentJson OR ISNULL(i.DecisionJson, N'') <> ISNULL(d.DecisionJson, N''))))
        THROW 51002, N'İmzalanmış mali belgenin içeriği, ETTN''si veya belge numarası değiştirilemez.', 1;
END
GO

CREATE OR ALTER TRIGGER dbo.TR_InvoiceStatusHistory_AppendOnly ON dbo.InvoiceStatusHistory INSTEAD OF UPDATE, DELETE AS
BEGIN
    THROW 51003, N'Durum geçmişi yalnızca eklenebilir.', 1;
END
GO

CREATE OR ALTER TRIGGER dbo.TR_InvoiceActions_NoDelete ON dbo.InvoiceActions INSTEAD OF DELETE AS
BEGIN
    THROW 51004, N'İptal/itiraz kayıtları silinemez.', 1;
END
GO

CREATE OR ALTER TRIGGER dbo.TR_AuditEvents_AppendOnly ON dbo.AuditEvents INSTEAD OF UPDATE, DELETE AS
BEGIN
    THROW 51005, N'Denetim kayıtları değiştirilemez ve silinemez.', 1;
END
GO

CREATE OR ALTER TRIGGER dbo.TR_IncomingInvoices_NoDelete ON dbo.IncomingInvoices INSTEAD OF DELETE AS
BEGIN
    THROW 51006, N'Gelen mali belgeler silinemez.', 1;
END
GO

CREATE OR ALTER TRIGGER dbo.TR_TaxOffices_History ON dbo.TaxOffices INSTEAD OF DELETE AS
BEGIN
    THROW 51007, N'Vergi dairesi kayıtları silinmez; EffectiveTo ile kapatılır.', 1;
END
GO

CREATE OR ALTER TRIGGER dbo.TR_TaxOffices_CloseOnly ON dbo.TaxOffices AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
        WHERE d.EffectiveTo IS NOT NULL
           OR i.GibCode <> d.GibCode OR i.Name <> d.Name OR i.ProvinceCode <> d.ProvinceCode
           OR i.EffectiveFrom <> d.EffectiveFrom OR i.SourceSnapshotId <> d.SourceSnapshotId)
        THROW 51008, N'Vergi dairesi versiyonu yalnızca EffectiveTo ile kapatılabilir.', 1;
END
GO

CREATE OR ALTER TRIGGER dbo.TR_Users_NoDelete ON dbo.Users INSTEAD OF DELETE AS
BEGIN
    THROW 51009, N'Kullanıcılar silinmez; pasifleştirilir.', 1;
END
GO
