SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.SchemaInfo', N'U') IS NULL
    THROW 51170, N'Önce GIB Framework şema 6 kurulumunu tamamlayın.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaInfo WHERE Version = 6)
    THROW 51170, N'Önce 07-Upgrade-Schema.sql betiğini çalıştırın.', 1;

BEGIN TRANSACTION;

DECLARE @result int;
EXEC @result = sys.sp_getapplock @Resource = N'GIBFramework.SchemaUpgrade', @LockMode = 'Exclusive',
    @LockOwner = 'Transaction', @LockTimeout = 15000;
IF @result < 0 THROW 51171, N'Şema yükseltme kilidi alınamadı.', 1;

IF OBJECT_ID(N'dbo.PasswordResetIntake', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordResetIntake (
        Id          uniqueidentifier NOT NULL CONSTRAINT PK_PasswordResetIntake PRIMARY KEY,
        UserCode    nvarchar(50)     NOT NULL,
        Ip          nvarchar(45)     NULL,
        RequestedAt datetimeoffset   NOT NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaInfo WHERE Version = 7)
    INSERT dbo.SchemaInfo (Version, Description) VALUES (7, N'Parola sıfırlama talep kuyruğu');

COMMIT TRANSACTION;
GO
