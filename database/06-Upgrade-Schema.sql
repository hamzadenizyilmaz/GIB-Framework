SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
GO

IF ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'ALTER ANY SECURITY POLICY'), 0) <> 1
    THROW 51050, N'GIB sema 5 icin ALTER ANY SECURITY POLICY yetkili veritabani yoneticisi gerekir. Degisiklik uygulanmadi.', 1;

IF OBJECT_ID(N'dbo.SchemaInfo', N'U') IS NULL
    THROW 51051, N'Mevcut veritabani semasi bulunamadi. Tam kurulum dosyasini kullanin.', 1;

BEGIN TRANSACTION;

DECLARE @lockResult int;
EXEC @lockResult = sys.sp_getapplock @Resource = N'GIBFramework.SchemaUpgrade', @LockMode = N'Exclusive',
    @LockOwner = N'Transaction', @LockTimeout = 15000;
IF @lockResult < 0
    THROW 51052, N'Sema yukseltme kilidi alinamadi. Degisiklik uygulanmadi.', 1;

IF COL_LENGTH(N'dbo.Users', N'RowVersion') IS NULL
    ALTER TABLE dbo.Users ADD RowVersion rowversion NOT NULL;

IF COL_LENGTH(N'dbo.OutboxEvents', N'LeaseId') IS NULL
    ALTER TABLE dbo.OutboxEvents ADD LeaseId uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.OutboxEvents', N'LeaseUntil') IS NULL
    ALTER TABLE dbo.OutboxEvents ADD LeaseUntil datetimeoffset(7) NULL;
IF COL_LENGTH(N'dbo.OutboxEvents', N'NextAttemptAt') IS NULL
    ALTER TABLE dbo.OutboxEvents ADD NextAttemptAt datetimeoffset(7) NULL;

IF COL_LENGTH(N'dbo.Messages', N'LeaseId') IS NULL
    ALTER TABLE dbo.Messages ADD LeaseId uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.Messages', N'OutboxEventId') IS NULL
    ALTER TABLE dbo.Messages ADD OutboxEventId bigint NULL;

IF COL_LENGTH(N'dbo.IntegrationDeliveries', N'LeaseId') IS NULL
    ALTER TABLE dbo.IntegrationDeliveries ADD LeaseId uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.IntegrationDeliveries', N'OutboxEventId') IS NULL
    ALTER TABLE dbo.IntegrationDeliveries ADD OutboxEventId bigint NULL;

IF COL_LENGTH(N'dbo.Integrations', N'SyncCheckpointAt') IS NULL
    ALTER TABLE dbo.Integrations ADD SyncCheckpointAt datetimeoffset(7) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Messages') AND name = N'UX_Messages_Outbox')
    EXEC(N'CREATE UNIQUE INDEX UX_Messages_Outbox ON dbo.Messages (OutboxEventId, Channel, Recipient) WHERE OutboxEventId IS NOT NULL');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.IntegrationDeliveries') AND name = N'UX_IntegrationDeliveries_Outbox')
    EXEC(N'CREATE UNIQUE INDEX UX_IntegrationDeliveries_Outbox ON dbo.IntegrationDeliveries (OutboxEventId, IntegrationId, EventType, ExternalId) WHERE OutboxEventId IS NOT NULL');

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes i
    JOIN sys.index_columns c ON c.object_id = i.object_id AND c.index_id = i.index_id
    WHERE i.object_id = OBJECT_ID(N'dbo.OutboxEvents') AND i.name = N'IX_OutboxEvents_Pending'
      AND c.column_id = COLUMNPROPERTY(i.object_id, N'LeaseUntil', 'ColumnId') AND c.is_included_column = 1)
BEGIN
    DECLARE @replaceIndex nvarchar(30) = CASE WHEN EXISTS (
        SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.OutboxEvents') AND name = N'IX_OutboxEvents_Pending')
        THEN N' WITH (DROP_EXISTING = ON)' ELSE N'' END;
    DECLARE @outboxIndex nvarchar(max) = N'CREATE INDEX IX_OutboxEvents_Pending ON dbo.OutboxEvents (Id)
        INCLUDE (TenantId, ProcessedAt, LeaseUntil, NextAttemptAt) WHERE ProcessedAt IS NULL' + @replaceIndex;
    EXEC sys.sp_executesql @outboxIndex;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.IntegrationDeliveries') AND name = N'IX_IntegrationDeliveries_OutboundDue')
    EXEC(N'CREATE INDEX IX_IntegrationDeliveries_OutboundDue ON dbo.IntegrationDeliveries (NextAttemptAt, Id)
        INCLUDE (TenantId, LeaseId) WHERE Direction = ''Out'' AND Status = ''Pending'';');

IF OBJECT_ID(N'dbo.fn_TenantAccess', N'IF') IS NULL
    EXEC(N'CREATE FUNCTION dbo.fn_TenantAccess (@TenantId uniqueidentifier)
        RETURNS TABLE WITH SCHEMABINDING AS RETURN
        SELECT 1 AS Allowed
        WHERE @TenantId = CAST(SESSION_CONTEXT(N''TenantId'') AS uniqueidentifier)
            OR CAST(SESSION_CONTEXT(N''SystemContext'') AS bit) = 1;');

DECLARE @policyId int = (SELECT object_id FROM sys.security_policies
    WHERE name = N'TenantIsolation' AND schema_id = SCHEMA_ID(N'dbo'));
DECLARE @filter tinyint = 0, @afterInsert tinyint = 1, @afterUpdate tinyint = 2;
DECLARE @expected TABLE (TableName sysname NOT NULL, TargetId int NULL, Operation tinyint NOT NULL);

INSERT INTO @expected (TableName, TargetId, Operation)
    SELECT t.name, OBJECT_ID(N'dbo.' + QUOTENAME(t.name), N'U'), p.operation
    FROM (VALUES
        (N'Invoices', 1), (N'InvoiceStatusHistory', 0), (N'InvoiceActions', 0), (N'AuditEvents', 0),
        (N'IncomingInvoices', 1), (N'DocumentSequences', 0), (N'Customers', 1), (N'Products', 1),
        (N'TenantSettings', 1), (N'Users', 1), (N'ApiKeys', 1), (N'TenantLogos', 1), (N'OutboxEvents', 1),
        (N'Messages', 1), (N'Integrations', 1), (N'IntegrationOrders', 1), (N'IntegrationDeliveries', 1)
    ) AS t(name, block_updates)
    CROSS JOIN (VALUES (@filter), (@afterInsert), (@afterUpdate)) AS p(operation)
    WHERE p.operation <> @afterUpdate OR t.block_updates = 1;

IF EXISTS (SELECT 1 FROM @expected WHERE TargetId IS NULL)
    THROW 51054, N'Firma satir guvenligi icin gereken bir tablo bulunamadi. Degisiklik uygulanmadi.', 1;

IF EXISTS (
    SELECT 1 FROM @expected e
    JOIN sys.security_predicates p ON p.object_id = @policyId AND p.target_object_id = e.TargetId
        AND ((e.Operation = @filter AND p.predicate_type = 0) OR (p.predicate_type = 1 AND p.operation = e.Operation))
    WHERE p.predicate_definition <> N'([dbo].[fn_TenantAccess]([TenantId]))')
    THROW 51053, N'Mevcut firma satir guvenligi tanimi beklenen tanimla uyusmuyor. Degisiklik uygulanmadi.', 1;

DECLARE @predicates nvarchar(max);
SELECT @predicates = STRING_AGG(
    CONVERT(nvarchar(max), N'ADD ' + CASE WHEN e.Operation = @filter THEN N'FILTER' ELSE N'BLOCK' END
        + N' PREDICATE dbo.fn_TenantAccess(TenantId) ON dbo.' + QUOTENAME(e.TableName)
        + CASE e.Operation WHEN @afterInsert THEN N' AFTER INSERT' WHEN @afterUpdate THEN N' AFTER UPDATE' ELSE N'' END), N', ')
    WITHIN GROUP (ORDER BY e.TableName, e.Operation)
FROM @expected e
WHERE NOT EXISTS (
    SELECT 1 FROM sys.security_predicates p
    WHERE p.object_id = @policyId AND p.target_object_id = e.TargetId
      AND ((e.Operation = @filter AND p.predicate_type = 0) OR (p.predicate_type = 1 AND p.operation = e.Operation)));

IF @predicates IS NOT NULL
BEGIN
    DECLARE @policyDdl nvarchar(max) = CASE WHEN @policyId IS NULL
        THEN N'CREATE SECURITY POLICY dbo.TenantIsolation ' + @predicates + N' WITH (STATE = ON, SCHEMABINDING = ON);'
        ELSE N'ALTER SECURITY POLICY dbo.TenantIsolation ' + @predicates + N';' END;
    EXEC sys.sp_executesql @policyDdl;
END;

IF EXISTS (SELECT 1 FROM sys.security_policies WHERE object_id = @policyId AND is_enabled = 0)
    EXEC(N'ALTER SECURITY POLICY dbo.TenantIsolation WITH (STATE = ON);');

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaInfo WITH (UPDLOCK, HOLDLOCK) WHERE Version = 5)
    INSERT dbo.SchemaInfo (Version, Description) VALUES (5, N'Olay ve teslimat kiraları, kullanıcı eşzamanlılığı, firma satır güvenliği ve sipariş ilerlemesi');

COMMIT TRANSACTION;
GO
