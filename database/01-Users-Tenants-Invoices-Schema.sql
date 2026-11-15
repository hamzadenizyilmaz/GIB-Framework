SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.SchemaInfo', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaInfo (
        Version     int               NOT NULL CONSTRAINT PK_SchemaInfo PRIMARY KEY,
        Description nvarchar(200)     NOT NULL,
        InstalledAt datetimeoffset(7) NOT NULL CONSTRAINT DF_SchemaInfo_InstalledAt DEFAULT (SYSDATETIMEOFFSET())
    );
END
GO

IF OBJECT_ID(N'dbo.Tenants', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Tenants (
        Id            uniqueidentifier  NOT NULL CONSTRAINT PK_Tenants PRIMARY KEY,
        Name          nvarchar(250)     NOT NULL,
        TaxId         varchar(11)       NOT NULL,
        ProfileJson   nvarchar(max)     NOT NULL,
        EFaturaPrefix char(3)           NOT NULL,
        EArsivPrefix  char(3)           NOT NULL,
        IsActive      bit               NOT NULL CONSTRAINT DF_Tenants_IsActive DEFAULT (1),
        CreatedAt     datetimeoffset(7) NOT NULL,
        CONSTRAINT UQ_Tenants_TaxId UNIQUE (TaxId)
    );
END
GO

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users (
        Id                  uniqueidentifier  NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
        TenantId            uniqueidentifier  NULL CONSTRAINT FK_Users_Tenants REFERENCES dbo.Tenants (Id),
        UserCode            varchar(50)       NOT NULL,
        DisplayName         nvarchar(200)     NOT NULL,
        Email               nvarchar(200)     NULL,
        PasswordHash        nvarchar(500)     NOT NULL,
        RolesCsv            varchar(1000)     NOT NULL,
        IsActive            bit               NOT NULL,
        MustChangePassword  bit               NOT NULL,
        FailedLoginCount    int               NOT NULL CONSTRAINT DF_Users_Failed DEFAULT (0),
        LockoutUntil        datetimeoffset(7) NULL,
        TotpSecretProtected nvarchar(1000)    NULL,
        TotpEnabled         bit               NOT NULL CONSTRAINT DF_Users_Totp DEFAULT (0),
        TotpLastStep        bigint            NULL,
        SecurityStamp       uniqueidentifier  NOT NULL,
        CreatedAt           datetimeoffset(7) NOT NULL,
        UpdatedAt           datetimeoffset(7) NOT NULL,
        LastLoginAt         datetimeoffset(7) NULL,
        CONSTRAINT UQ_Users_UserCode UNIQUE (UserCode)
    );
    CREATE INDEX IX_Users_Tenant ON dbo.Users (TenantId);
END
GO

IF OBJECT_ID(N'dbo.DocumentSequences', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentSequences (
        TenantId     uniqueidentifier  NOT NULL,
        DocumentKind varchar(20)       NOT NULL,
        Prefix       char(3)           NOT NULL,
        FiscalYear   int               NOT NULL,
        LastValue    bigint            NOT NULL,
        UpdatedAt    datetimeoffset(7) NOT NULL,
        CONSTRAINT PK_DocumentSequences PRIMARY KEY (TenantId, DocumentKind, Prefix, FiscalYear),
        CONSTRAINT CK_DocumentSequences_Range CHECK (LastValue BETWEEN 0 AND 999999999)
    );
END
GO

IF OBJECT_ID(N'dbo.Invoices', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Invoices (
        Id                uniqueidentifier  NOT NULL CONSTRAINT PK_Invoices PRIMARY KEY NONCLUSTERED,
        ClusterKey        bigint IDENTITY(1,1) NOT NULL,
        TenantId          uniqueidentifier  NOT NULL CONSTRAINT FK_Invoices_Tenants REFERENCES dbo.Tenants (Id),
        Ettn              uniqueidentifier  NOT NULL,
        DocumentNumber    varchar(16)       NULL,
        DocumentType      varchar(20)       NOT NULL,
        Profile           varchar(20)       NOT NULL,
        TypeCode          varchar(20)       NOT NULL,
        Status            varchar(30)       NOT NULL,
        IssueDate         date              NOT NULL,
        CustomerTaxId     varchar(11)       NOT NULL,
        Currency          char(3)           NOT NULL,
        PayableAmount     decimal(19,4)     NOT NULL,
        IdempotencyKey    nvarchar(100)     NOT NULL,
        ContentJson       nvarchar(max)     NOT NULL,
        DecisionJson      nvarchar(max)     NULL,
        SignedXmlSha256   char(64)          NULL,
        ProviderReference nvarchar(100)     NULL,
        LastError         nvarchar(2000)    NULL,
        CreatedBy         nvarchar(200)     NOT NULL,
        CreatedAt         datetimeoffset(7) NOT NULL,
        ApprovedBy        nvarchar(200)     NULL,
        ApprovedAt        datetimeoffset(7) NULL,
        SignedBy          nvarchar(200)     NULL,
        SignedAt          datetimeoffset(7) NULL,
        TransmittedAt     datetimeoffset(7) NULL,
        UpdatedAt         datetimeoffset(7) NOT NULL,
        RowVersion        rowversion        NOT NULL,
        CONSTRAINT CK_Invoices_Status CHECK (Status IN ('Draft','Validating','Validated','AwaitingApproval','Approved','Signing','Signed',
            'Queued','Transmitting','InDoubt','Sent','Acknowledged','Delivered','Accepted','Rejected','Cancelled','Objected','Failed'))
    );
    CREATE UNIQUE CLUSTERED INDEX CX_Invoices ON dbo.Invoices (ClusterKey);
    CREATE UNIQUE INDEX UX_Invoices_Ettn ON dbo.Invoices (TenantId, Ettn);
    CREATE UNIQUE INDEX UX_Invoices_Idempotency ON dbo.Invoices (TenantId, IdempotencyKey);
    CREATE UNIQUE INDEX UX_Invoices_Number ON dbo.Invoices (TenantId, DocumentType, DocumentNumber) WHERE DocumentNumber IS NOT NULL;
    CREATE INDEX IX_Invoices_Status ON dbo.Invoices (Status, UpdatedAt) INCLUDE (TenantId);
    CREATE INDEX IX_Invoices_TenantDate ON dbo.Invoices (TenantId, IssueDate DESC);
END
GO

IF OBJECT_ID(N'dbo.InvoiceStatusHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InvoiceStatusHistory (
        Id         bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_InvoiceStatusHistory PRIMARY KEY,
        TenantId   uniqueidentifier  NOT NULL,
        InvoiceId  uniqueidentifier  NOT NULL CONSTRAINT FK_InvoiceStatusHistory_Invoices REFERENCES dbo.Invoices (Id),
        FromStatus varchar(30)       NULL,
        ToStatus   varchar(30)       NOT NULL,
        Actor      nvarchar(200)     NOT NULL,
        Note       nvarchar(2000)    NULL,
        At         datetimeoffset(7) NOT NULL
    );
    CREATE INDEX IX_InvoiceStatusHistory_Invoice ON dbo.InvoiceStatusHistory (InvoiceId, Id);
END
GO

IF OBJECT_ID(N'dbo.InvoiceActions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InvoiceActions (
        Id                uniqueidentifier  NOT NULL CONSTRAINT PK_InvoiceActions PRIMARY KEY,
        TenantId          uniqueidentifier  NOT NULL,
        InvoiceId         uniqueidentifier  NOT NULL CONSTRAINT FK_InvoiceActions_Invoices REFERENCES dbo.Invoices (Id),
        Kind              varchar(20)       NOT NULL,
        Method            nvarchar(50)      NULL,
        ReferenceNumber   nvarchar(100)     NULL,
        NotificationDate  date              NULL,
        Reason            nvarchar(2000)    NOT NULL,
        Status            varchar(20)       NOT NULL,
        RequestedBy       nvarchar(200)     NOT NULL,
        CreatedAt         datetimeoffset(7) NOT NULL,
        ProviderReference nvarchar(100)     NULL
    );
    CREATE INDEX IX_InvoiceActions_Invoice ON dbo.InvoiceActions (InvoiceId);
END
GO

IF OBJECT_ID(N'dbo.AuditEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditEvents (
        Sequence      bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditEvents PRIMARY KEY,
        EventId       uniqueidentifier  NOT NULL CONSTRAINT UQ_AuditEvents_EventId UNIQUE,
        TenantId      uniqueidentifier  NOT NULL,
        UserId        nvarchar(200)     NULL,
        ActorType     varchar(20)       NOT NULL,
        Action        varchar(100)      NOT NULL,
        EntityType    varchar(100)      NOT NULL,
        EntityId      nvarchar(100)     NULL,
        TimestampUtc  datetimeoffset(7) NOT NULL,
        Ip            varchar(64)       NULL,
        UserAgent     nvarchar(400)     NULL,
        CorrelationId varchar(100)      NULL,
        TraceId       varchar(100)      NULL,
        Result        varchar(20)       NOT NULL,
        FailureReason nvarchar(2000)    NULL,
        DataJson      nvarchar(max)     NULL,
        PreviousHash  varchar(64)       NOT NULL,
        Hash          char(64)          NOT NULL
    );
    CREATE INDEX IX_AuditEvents_Tenant ON dbo.AuditEvents (TenantId, Sequence);
    CREATE INDEX IX_AuditEvents_Entity ON dbo.AuditEvents (TenantId, EntityId, Sequence);
END
GO

IF OBJECT_ID(N'dbo.IncomingInvoices', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IncomingInvoices (
        Id               uniqueidentifier  NOT NULL CONSTRAINT PK_IncomingInvoices PRIMARY KEY,
        TenantId         uniqueidentifier  NOT NULL,
        Ettn             uniqueidentifier  NOT NULL,
        DocumentNumber   varchar(16)       NOT NULL,
        Profile          varchar(20)       NOT NULL,
        SupplierTaxId    varchar(11)       NOT NULL,
        SupplierTitle    nvarchar(250)     NOT NULL,
        CustomerTaxId    varchar(11)       NOT NULL,
        IssueDate        date              NOT NULL,
        PayableAmount    decimal(19,4)     NOT NULL,
        Currency         char(3)           NOT NULL,
        Status           varchar(20)       NOT NULL,
        SignaturePresent bit               NOT NULL,
        SignatureValid   bit               NOT NULL,
        IssuesJson       nvarchar(max)     NOT NULL,
        XmlSha256        char(64)          NOT NULL,
        ReceivedVia      varchar(30)       NOT NULL,
        ReceivedAt       datetimeoffset(7) NOT NULL,
        DecidedBy        nvarchar(200)     NULL,
        DecidedAt        datetimeoffset(7) NULL,
        CONSTRAINT UQ_IncomingInvoices_Ettn UNIQUE (TenantId, Ettn)
    );
END
GO

IF OBJECT_ID(N'dbo.TaxOfficeSnapshots', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TaxOfficeSnapshots (
        Id                   uniqueidentifier  NOT NULL CONSTRAINT PK_TaxOfficeSnapshots PRIMARY KEY,
        SourceType           varchar(30)       NOT NULL,
        SourceUrl            nvarchar(1000)    NOT NULL,
        PublishedAt          date              NULL,
        DownloadedAt         datetimeoffset(7) NOT NULL,
        Sha256               char(64)          NOT NULL,
        ParserVersion        varchar(50)       NOT NULL,
        RecordCount          int               NOT NULL,
        PreviousSnapshotId   uniqueidentifier  NULL,
        ImportedBy           nvarchar(200)     NOT NULL,
        ValidationStatus     varchar(20)       NOT NULL,
        ValidationErrorsJson nvarchar(max)     NOT NULL,
        RecordsJson          nvarchar(max)     NOT NULL,
        DiffJson             nvarchar(max)     NOT NULL,
        ApprovedBy           nvarchar(200)     NULL,
        ApprovedAt           datetimeoffset(7) NULL,
        EffectiveFrom        date              NULL
    );
END
GO

IF OBJECT_ID(N'dbo.TaxOffices', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TaxOffices (
        Id               uniqueidentifier NOT NULL CONSTRAINT PK_TaxOffices PRIMARY KEY,
        GibCode          varchar(10)      NOT NULL,
        Name             nvarchar(250)    NOT NULL,
        NormalizedName   nvarchar(250)    NOT NULL,
        SearchKey        nvarchar(250)    NOT NULL,
        OfficeType       varchar(40)      NOT NULL,
        ProvinceCode     varchar(5)       NOT NULL,
        ProvinceName     nvarchar(100)    NOT NULL,
        DistrictName     nvarchar(100)    NULL,
        ParentGibCode    varchar(10)      NULL,
        IsBranch         bit              NOT NULL,
        EffectiveFrom    date             NOT NULL,
        EffectiveTo      date             NULL,
        SourceSnapshotId uniqueidentifier NOT NULL CONSTRAINT FK_TaxOffices_Snapshots REFERENCES dbo.TaxOfficeSnapshots (Id)
    );
    CREATE UNIQUE INDEX UX_TaxOffices_Active ON dbo.TaxOffices (GibCode) WHERE EffectiveTo IS NULL;
    CREATE INDEX IX_TaxOffices_Search ON dbo.TaxOffices (SearchKey);
END
GO

IF OBJECT_ID(N'dbo.LegalSourceChecks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LegalSourceChecks (
        Id             bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_LegalSourceChecks PRIMARY KEY,
        SourceCode     varchar(50)       NOT NULL,
        Url            nvarchar(1000)    NOT NULL,
        CheckedAt      datetimeoffset(7) NOT NULL,
        HttpStatus     int               NULL,
        Sha256         char(64)          NULL,
        PreviousSha256 char(64)          NULL,
        Changed        bit               NOT NULL,
        Error          nvarchar(2000)    NULL
    );
    CREATE INDEX IX_LegalSourceChecks_Source ON dbo.LegalSourceChecks (SourceCode, Id DESC);
END
GO

IF OBJECT_ID(N'dbo.Announcements', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Announcements (
        Id        uniqueidentifier  NOT NULL CONSTRAINT PK_Announcements PRIMARY KEY,
        Title     nvarchar(200)     NOT NULL,
        Message   nvarchar(4000)    NOT NULL,
        Level     varchar(20)       NOT NULL,
        Audience  varchar(20)       NOT NULL,
        TenantId  uniqueidentifier  NULL CONSTRAINT FK_Announcements_Tenants REFERENCES dbo.Tenants (Id),
        StartsAt  datetimeoffset(7) NOT NULL,
        EndsAt    datetimeoffset(7) NULL,
        IsActive  bit               NOT NULL,
        CreatedBy nvarchar(200)     NOT NULL,
        CreatedAt datetimeoffset(7) NOT NULL,
        UpdatedAt datetimeoffset(7) NOT NULL,
        CONSTRAINT CK_Announcements_Level CHECK (Level IN ('Info','Success','Warning','Danger')),
        CONSTRAINT CK_Announcements_Audience CHECK (Audience IN ('Everyone','Authenticated','Tenant')),
        CONSTRAINT CK_Announcements_Tenant CHECK ((Audience = 'Tenant' AND TenantId IS NOT NULL) OR (Audience <> 'Tenant' AND TenantId IS NULL)),
        CONSTRAINT CK_Announcements_Period CHECK (EndsAt IS NULL OR EndsAt > StartsAt)
    );
    CREATE INDEX IX_Announcements_Active ON dbo.Announcements (IsActive, StartsAt) INCLUDE (EndsAt, Audience, TenantId);
END
GO

IF COL_LENGTH(N'dbo.Invoices', N'DraftNumber') IS NULL
    ALTER TABLE dbo.Invoices ADD DraftNumber varchar(16) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Invoices_DraftNumber' AND object_id = OBJECT_ID(N'dbo.Invoices'))
    CREATE UNIQUE INDEX UX_Invoices_DraftNumber ON dbo.Invoices (TenantId, DraftNumber) WHERE DraftNumber IS NOT NULL;
GO

IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Customers (
        Id                  uniqueidentifier  NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
        TenantId            uniqueidentifier  NOT NULL CONSTRAINT FK_Customers_Tenants REFERENCES dbo.Tenants (Id),
        TaxId               varchar(11)       NOT NULL,
        Title               nvarchar(250)     NOT NULL,
        FirstName           nvarchar(100)     NULL,
        FamilyName          nvarchar(100)     NULL,
        Regime              varchar(20)       NOT NULL,
        TaxOffice           nvarchar(100)     NULL,
        ProvinceName        nvarchar(100)     NULL,
        DistrictName        nvarchar(100)     NULL,
        NeighborhoodName    nvarchar(150)     NULL,
        Street              nvarchar(250)     NULL,
        BuildingNumber      nvarchar(20)      NULL,
        PostalCode          nvarchar(10)      NULL,
        Country             nvarchar(100)     NOT NULL,
        Email               nvarchar(200)     NULL,
        Phone               nvarchar(30)      NULL,
        IsEFaturaRegistered bit               NOT NULL,
        EFaturaAlias        nvarchar(200)     NULL,
        Notes               nvarchar(1000)    NULL,
        IsActive            bit               NOT NULL,
        CreatedBy           nvarchar(200)     NOT NULL,
        CreatedAt           datetimeoffset(7) NOT NULL,
        UpdatedAt           datetimeoffset(7) NOT NULL,
        CONSTRAINT CK_Customers_TaxId CHECK (LEN(TaxId) IN (10, 11) AND TaxId NOT LIKE '%[^0-9]%')
    );
    CREATE UNIQUE INDEX UX_Customers_TaxId ON dbo.Customers (TenantId, TaxId);
    CREATE INDEX IX_Customers_Title ON dbo.Customers (TenantId, Title);
END
GO

IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Products (
        Id               uniqueidentifier  NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
        TenantId         uniqueidentifier  NOT NULL CONSTRAINT FK_Products_Tenants REFERENCES dbo.Tenants (Id),
        Code             nvarchar(50)      NULL,
        Name             nvarchar(250)     NOT NULL,
        Description      nvarchar(500)     NULL,
        UnitCode         varchar(10)       NOT NULL,
        UnitPrice        decimal(19,4)     NOT NULL,
        Currency         char(3)           NOT NULL,
        VatRate          decimal(5,2)      NOT NULL,
        VatExemptionCode varchar(10)       NULL,
        WithholdingCode  varchar(10)       NULL,
        IsActive         bit               NOT NULL,
        CreatedAt        datetimeoffset(7) NOT NULL,
        UpdatedAt        datetimeoffset(7) NOT NULL,
        CONSTRAINT CK_Products_Price CHECK (UnitPrice >= 0)
    );
    CREATE INDEX IX_Products_Name ON dbo.Products (TenantId, Name);
END
GO

IF OBJECT_ID(N'dbo.TenantSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenantSettings (
        TenantId     uniqueidentifier  NOT NULL CONSTRAINT PK_TenantSettings PRIMARY KEY
                     CONSTRAINT FK_TenantSettings_Tenants REFERENCES dbo.Tenants (Id),
        SettingsJson nvarchar(max)     NOT NULL,
        UpdatedBy    nvarchar(200)     NOT NULL,
        UpdatedAt    datetimeoffset(7) NOT NULL
    );
END
GO

IF OBJECT_ID(N'dbo.ApiKeys', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApiKeys (
        Id         uniqueidentifier  NOT NULL CONSTRAINT PK_ApiKeys PRIMARY KEY,
        TenantId   uniqueidentifier  NOT NULL CONSTRAINT FK_ApiKeys_Tenants REFERENCES dbo.Tenants (Id),
        Name       nvarchar(100)     NOT NULL,
        Prefix     varchar(16)       NOT NULL,
        KeyHash    char(64)          NOT NULL,
        RolesJson  nvarchar(1000)    NOT NULL,
        CreatedBy  nvarchar(200)     NOT NULL,
        CreatedAt  datetimeoffset(7) NOT NULL,
        ExpiresAt  datetimeoffset(7) NULL,
        LastUsedAt datetimeoffset(7) NULL,
        RevokedAt  datetimeoffset(7) NULL,
        RevokedBy  nvarchar(200)     NULL
    );
    CREATE UNIQUE INDEX UX_ApiKeys_Prefix ON dbo.ApiKeys (Prefix);
    CREATE INDEX IX_ApiKeys_Tenant ON dbo.ApiKeys (TenantId);
END
GO

IF OBJECT_ID(N'dbo.Provinces', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Provinces (
        Id                int               NOT NULL CONSTRAINT PK_Provinces PRIMARY KEY,
        PlateCode         tinyint           NOT NULL,
        Name              nvarchar(100)     NOT NULL,
        DistrictsLoadedAt datetimeoffset(7) NULL
    );
    CREATE UNIQUE INDEX UX_Provinces_Plate ON dbo.Provinces (PlateCode);
END
GO

IF OBJECT_ID(N'dbo.Districts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Districts (
        Id                    int               NOT NULL CONSTRAINT PK_Districts PRIMARY KEY,
        ProvinceId            int               NOT NULL CONSTRAINT FK_Districts_Provinces REFERENCES dbo.Provinces (Id),
        Name                  nvarchar(100)     NOT NULL,
        NeighborhoodsLoadedAt datetimeoffset(7) NULL
    );
    CREATE INDEX IX_Districts_Province ON dbo.Districts (ProvinceId, Name);
END
GO

IF OBJECT_ID(N'dbo.Neighborhoods', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Neighborhoods (
        Id         int           NOT NULL CONSTRAINT PK_Neighborhoods PRIMARY KEY,
        DistrictId int           NOT NULL CONSTRAINT FK_Neighborhoods_Districts REFERENCES dbo.Districts (Id),
        Name       nvarchar(150) NOT NULL
    );
    CREATE INDEX IX_Neighborhoods_District ON dbo.Neighborhoods (DistrictId, Name);
END
GO

IF OBJECT_ID(N'dbo.PasswordResetRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordResetRequests (
        Id          uniqueidentifier  NOT NULL CONSTRAINT PK_PasswordResetRequests PRIMARY KEY,
        UserId      uniqueidentifier  NOT NULL CONSTRAINT FK_PasswordResetRequests_Users REFERENCES dbo.Users (Id),
        TenantId    uniqueidentifier  NULL,
        UserCode    nvarchar(50)      NOT NULL,
        Ip          nvarchar(64)      NULL,
        RequestedAt datetimeoffset(7) NOT NULL,
        Status      varchar(20)       NOT NULL,
        HandledBy   nvarchar(200)     NULL,
        HandledAt   datetimeoffset(7) NULL,
        CONSTRAINT CK_PasswordResetRequests_Status CHECK (Status IN ('Open','Done','Dismissed'))
    );
    CREATE INDEX IX_PasswordResetRequests_Open ON dbo.PasswordResetRequests (Status, RequestedAt) INCLUDE (TenantId, UserId);
END
GO

IF COL_LENGTH(N'dbo.Users', N'Phone') IS NULL
    ALTER TABLE dbo.Users ADD Phone varchar(20) NULL;
GO

IF OBJECT_ID(N'dbo.TenantLogos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenantLogos (
        TenantId    uniqueidentifier  NOT NULL CONSTRAINT PK_TenantLogos PRIMARY KEY
                    CONSTRAINT FK_TenantLogos_Tenants REFERENCES dbo.Tenants (Id),
        ContentType varchar(50)       NOT NULL,
        Data        varbinary(max)    NOT NULL,
        Sha256      char(64)          NOT NULL,
        UpdatedBy   nvarchar(200)     NOT NULL,
        UpdatedAt   datetimeoffset(7) NOT NULL
    );
END
GO

IF OBJECT_ID(N'dbo.OutboxEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OutboxEvents (
        Id          bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_OutboxEvents PRIMARY KEY,
        TenantId    uniqueidentifier  NULL,
        EventType   varchar(60)       NOT NULL,
        EntityType  varchar(40)       NOT NULL,
        EntityId    varchar(64)       NOT NULL,
        PayloadJson nvarchar(max)     NOT NULL,
        CreatedAt   datetimeoffset(7) NOT NULL,
        ProcessedAt datetimeoffset(7) NULL,
        Attempts    int               NOT NULL CONSTRAINT DF_OutboxEvents_Attempts DEFAULT (0),
        LastError   nvarchar(2000)    NULL
    );
    CREATE INDEX IX_OutboxEvents_Pending ON dbo.OutboxEvents (Id) WHERE ProcessedAt IS NULL;
END
GO

IF OBJECT_ID(N'dbo.Messages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Messages (
        Id                uniqueidentifier  NOT NULL CONSTRAINT PK_Messages PRIMARY KEY,
        TenantId          uniqueidentifier  NULL,
        Channel           varchar(10)       NOT NULL,
        Recipient         nvarchar(320)     NOT NULL,
        RecipientName     nvarchar(200)     NULL,
        Subject           nvarchar(400)     NULL,
        Body              nvarchar(max)     NOT NULL,
        TemplateKey       varchar(60)       NULL,
        EntityType        varchar(40)       NULL,
        EntityId          varchar(64)       NULL,
        AttachmentsJson   nvarchar(max)     NULL,
        Status            varchar(12)       NOT NULL,
        Attempts          int               NOT NULL CONSTRAINT DF_Messages_Attempts DEFAULT (0),
        NextAttemptAt     datetimeoffset(7) NOT NULL,
        LastError         nvarchar(2000)    NULL,
        ProviderMessageId nvarchar(200)     NULL,
        CreatedBy         nvarchar(200)     NOT NULL,
        CreatedAt         datetimeoffset(7) NOT NULL,
        SentAt            datetimeoffset(7) NULL,
        CONSTRAINT CK_Messages_Channel CHECK (Channel IN ('Email','Sms')),
        CONSTRAINT CK_Messages_Status CHECK (Status IN ('Queued','Sending','Sent','Failed','Cancelled'))
    );
    CREATE INDEX IX_Messages_Due ON dbo.Messages (Status, NextAttemptAt);
    CREATE INDEX IX_Messages_Tenant ON dbo.Messages (TenantId, CreatedAt DESC);
END
GO

IF OBJECT_ID(N'dbo.Integrations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Integrations (
        Id               uniqueidentifier  NOT NULL CONSTRAINT PK_Integrations PRIMARY KEY,
        TenantId         uniqueidentifier  NOT NULL CONSTRAINT FK_Integrations_Tenants REFERENCES dbo.Tenants (Id),
        Kind             varchar(30)       NOT NULL,
        Name             nvarchar(100)     NOT NULL,
        IsActive         bit               NOT NULL,
        SettingsJson     nvarchar(max)     NOT NULL,
        SecretsProtected nvarchar(max)     NULL,
        CreatedBy        nvarchar(200)     NOT NULL,
        CreatedAt        datetimeoffset(7) NOT NULL,
        UpdatedAt        datetimeoffset(7) NOT NULL,
        LastInboundAt    datetimeoffset(7) NULL,
        LastOutboundAt   datetimeoffset(7) NULL
    );
    CREATE INDEX IX_Integrations_Tenant ON dbo.Integrations (TenantId);
END
GO

IF OBJECT_ID(N'dbo.IntegrationDeliveries', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IntegrationDeliveries (
        Id            uniqueidentifier  NOT NULL CONSTRAINT PK_IntegrationDeliveries PRIMARY KEY,
        TenantId      uniqueidentifier  NOT NULL,
        IntegrationId uniqueidentifier  NOT NULL CONSTRAINT FK_IntegrationDeliveries_Integrations REFERENCES dbo.Integrations (Id) ON DELETE CASCADE,
        Direction     varchar(3)        NOT NULL,
        EventType     varchar(60)       NOT NULL,
        Status        varchar(12)       NOT NULL,
        HttpStatus    int               NULL,
        Attempts      int               NOT NULL CONSTRAINT DF_IntegrationDeliveries_Attempts DEFAULT (0),
        NextAttemptAt datetimeoffset(7) NULL,
        RequestBody   nvarchar(max)     NULL,
        ResponseBody  nvarchar(max)     NULL,
        Error         nvarchar(2000)    NULL,
        InvoiceId     uniqueidentifier  NULL,
        ExternalId    nvarchar(100)     NULL,
        CreatedAt     datetimeoffset(7) NOT NULL,
        CompletedAt   datetimeoffset(7) NULL,
        CONSTRAINT CK_IntegrationDeliveries_Direction CHECK (Direction IN ('In','Out')),
        CONSTRAINT CK_IntegrationDeliveries_Status CHECK (Status IN ('Pending','Succeeded','Failed','Ignored'))
    );
    CREATE INDEX IX_IntegrationDeliveries_Due ON dbo.IntegrationDeliveries (Status, NextAttemptAt);
    CREATE INDEX IX_IntegrationDeliveries_Integration ON dbo.IntegrationDeliveries (IntegrationId, CreatedAt DESC);
END
GO

IF OBJECT_ID(N'dbo.IntegrationOrders', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IntegrationOrders (
        IntegrationId uniqueidentifier  NOT NULL CONSTRAINT FK_IntegrationOrders_Integrations REFERENCES dbo.Integrations (Id) ON DELETE CASCADE,
        ExternalId    nvarchar(100)     NOT NULL,
        TenantId      uniqueidentifier  NOT NULL,
        InvoiceId     uniqueidentifier  NOT NULL,
        CreatedAt     datetimeoffset(7) NOT NULL,
        CONSTRAINT PK_IntegrationOrders PRIMARY KEY (IntegrationId, ExternalId)
    );
    CREATE INDEX IX_IntegrationOrders_Invoice ON dbo.IntegrationOrders (InvoiceId);
END
GO
