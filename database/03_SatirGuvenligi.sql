SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF EXISTS (SELECT 1 FROM sys.security_policies WHERE name = N'TenantIsolation')
BEGIN
    DECLARE @policy nvarchar(300) = (SELECT QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) FROM sys.security_policies WHERE name = N'TenantIsolation');
    EXEC (N'DROP SECURITY POLICY ' + @policy);
END;
GO

IF OBJECT_ID(N'Security.fn_TenantAccess', N'IF') IS NOT NULL
    DROP FUNCTION Security.fn_TenantAccess;
GO

IF SCHEMA_ID(N'Security') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.objects WHERE schema_id = SCHEMA_ID(N'Security'))
    EXEC (N'DROP SCHEMA Security');
GO

CREATE OR ALTER FUNCTION dbo.fn_TenantAccess (@TenantId uniqueidentifier)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
    SELECT 1 AS Allowed
    WHERE @TenantId = CAST(SESSION_CONTEXT(N'TenantId') AS uniqueidentifier)
       OR CAST(SESSION_CONTEXT(N'SystemContext') AS bit) = 1;
GO

CREATE SECURITY POLICY dbo.TenantIsolation
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
    WITH (STATE = ON);
GO
