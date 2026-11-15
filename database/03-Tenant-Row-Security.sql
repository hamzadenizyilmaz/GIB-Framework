SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

IF ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'ALTER ANY SECURITY POLICY'), 0) <> 1
    THROW 51050, N'Firma satir guvenligi kurulumu icin ALTER ANY SECURITY POLICY yetkisi gerekir.', 1;

BEGIN TRANSACTION;

DECLARE @lockResult int;
EXEC @lockResult = sys.sp_getapplock @Resource = N'GIBFramework.SchemaUpgrade', @LockMode = N'Exclusive',
    @LockOwner = N'Transaction', @LockTimeout = 15000;
IF @lockResult < 0
    THROW 51052, N'Sema yukseltme kilidi alinamadi. Degisiklik uygulanmadi.', 1;

IF OBJECT_ID(N'dbo.fn_TenantAccess', N'IF') IS NULL
    EXEC(N'CREATE FUNCTION dbo.fn_TenantAccess (@TenantId uniqueidentifier)
        RETURNS TABLE WITH SCHEMABINDING AS RETURN
        SELECT 1 AS Allowed
        WHERE @TenantId = CAST(SESSION_CONTEXT(N''TenantId'') AS uniqueidentifier)
           OR CAST(SESSION_CONTEXT(N''SystemContext'') AS bit) = 1;');

IF NOT EXISTS (SELECT 1 FROM sys.security_policies WHERE schema_id = SCHEMA_ID(N'dbo') AND name = N'TenantIsolation')
    EXEC(N'CREATE SECURITY POLICY dbo.TenantIsolation
    ADD FILTER PREDICATE dbo.fn_TenantAccess(TenantId) ON dbo.Invoices,
    ADD BLOCK PREDICATE  dbo.fn_TenantAccess(TenantId) ON dbo.Invoices AFTER INSERT,
    ADD BLOCK PREDICATE  dbo.fn_TenantAccess(TenantId) ON dbo.Invoices AFTER UPDATE,
    ADD FILTER PREDICATE dbo.fn_TenantAccess(TenantId) ON dbo.InvoiceStatusHistory,
    ADD BLOCK PREDICATE  dbo.fn_TenantAccess(TenantId) ON dbo.InvoiceStatusHistory AFTER INSERT,
    ADD FILTER PREDICATE dbo.fn_TenantAccess(TenantId) ON dbo.InvoiceActions,
    ADD BLOCK PREDICATE  dbo.fn_TenantAccess(TenantId) ON dbo.InvoiceActions AFTER INSERT,
    ADD FILTER PREDICATE dbo.fn_TenantAccess(TenantId) ON dbo.AuditEvents,
    ADD BLOCK PREDICATE  dbo.fn_TenantAccess(TenantId) ON dbo.AuditEvents AFTER INSERT,
    ADD FILTER PREDICATE dbo.fn_TenantAccess(TenantId) ON dbo.IncomingInvoices,
    ADD BLOCK PREDICATE  dbo.fn_TenantAccess(TenantId) ON dbo.IncomingInvoices AFTER INSERT,
    ADD BLOCK PREDICATE  dbo.fn_TenantAccess(TenantId) ON dbo.IncomingInvoices AFTER UPDATE,
    ADD FILTER PREDICATE dbo.fn_TenantAccess(TenantId) ON dbo.DocumentSequences,
    ADD BLOCK PREDICATE  dbo.fn_TenantAccess(TenantId) ON dbo.DocumentSequences AFTER INSERT,
    ADD FILTER PREDICATE dbo.fn_TenantAccess(TenantId) ON dbo.Customers,
    ADD BLOCK PREDICATE  dbo.fn_TenantAccess(TenantId) ON dbo.Customers AFTER INSERT,
    ADD BLOCK PREDICATE  dbo.fn_TenantAccess(TenantId) ON dbo.Customers AFTER UPDATE,
    ADD FILTER PREDICATE dbo.fn_TenantAccess(TenantId) ON dbo.Products,
    ADD BLOCK PREDICATE  dbo.fn_TenantAccess(TenantId) ON dbo.Products AFTER INSERT,
    ADD BLOCK PREDICATE  dbo.fn_TenantAccess(TenantId) ON dbo.Products AFTER UPDATE,
    ADD FILTER PREDICATE dbo.fn_TenantAccess(TenantId) ON dbo.TenantSettings,
    ADD BLOCK PREDICATE  dbo.fn_TenantAccess(TenantId) ON dbo.TenantSettings AFTER INSERT,
    ADD BLOCK PREDICATE  dbo.fn_TenantAccess(TenantId) ON dbo.TenantSettings AFTER UPDATE
    WITH (STATE = ON, SCHEMABINDING = ON);');

COMMIT TRANSACTION;
GO
