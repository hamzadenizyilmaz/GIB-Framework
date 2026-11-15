SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.SchemaInfo', N'U') IS NULL OR OBJECT_ID(N'dbo.AuditEvents', N'U') IS NULL
    THROW 51160, N'Önce GIB Framework şema 5 kurulumunu tamamlayın.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaInfo WHERE Version = 5)
    THROW 51161, N'Önce 06-Upgrade-Schema.sql betiğini çalıştırın.', 1;

IF (SELECT compatibility_level FROM sys.databases WHERE database_id = DB_ID()) < 130
    THROW 51162, N'Denetim rol filtresi için SQL veritabanı uyumluluk seviyesi en az 130 olmalıdır.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.security_policies WHERE object_id = OBJECT_ID(N'dbo.TenantIsolation', N'SP') AND is_enabled = 1)
    THROW 51164, N'Etkin TenantIsolation politikası bulunamadı. Yetkili bir hesapla önce 06-Upgrade-Schema.sql betiğini yeniden çalıştırın.', 1;

BEGIN TRANSACTION;

DECLARE @result int;
EXEC @result = sys.sp_getapplock @Resource = N'GIBFramework.SchemaUpgrade', @LockMode = 'Exclusive',
    @LockOwner = 'Transaction', @LockTimeout = 15000;
IF @result < 0 THROW 51163, N'Şema yükseltme kilidi alınamadı.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AuditEvents') AND name = N'IX_AuditEvents_Tenant_User_Sequence')
    CREATE INDEX IX_AuditEvents_Tenant_User_Sequence ON dbo.AuditEvents (TenantId, UserId, Sequence DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AuditEvents') AND name = N'IX_AuditEvents_Tenant_Action_Sequence')
    CREATE INDEX IX_AuditEvents_Tenant_Action_Sequence ON dbo.AuditEvents (TenantId, Action, Sequence DESC);

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaInfo WHERE Version = 6)
    INSERT dbo.SchemaInfo (Version, Description) VALUES (6, N'Denetim izinde kullanıcı ve işlem sorgusu indeksleri');

COMMIT TRANSACTION;
GO
