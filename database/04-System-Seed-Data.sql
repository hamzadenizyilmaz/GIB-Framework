SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaInfo WHERE Version = 1)
    INSERT dbo.SchemaInfo (Version, Description) VALUES (1, N'GIB Framework ilk kurulum');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Announcements WHERE Id = '6f0c2a1e-3d4b-4c5a-9e8f-1a2b3c4d5e01')
    INSERT dbo.Announcements (Id, Title, Message, Level, Audience, TenantId, StartsAt, EndsAt, IsActive, CreatedBy, CreatedAt, UpdatedAt)
    VALUES (
        '6f0c2a1e-3d4b-4c5a-9e8f-1a2b3c4d5e01',
        N'GIB Framework''e hoş geldiniz',
        N'e-Fatura ve e-Arşiv faturalarınızı tek ekrandan oluşturabilir, onaylayabilir, imzalayabilir ve takip edebilirsiniz.',
        'Info',
        'Everyone',
        NULL,
        SYSDATETIMEOFFSET(),
        NULL,
        1,
        N'system',
        SYSDATETIMEOFFSET(),
        SYSDATETIMEOFFSET());
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaInfo WHERE Version = 2)
    INSERT dbo.SchemaInfo (Version, Description) VALUES (2, N'Cari kartlar, ürünler, firma ayarları, API anahtarları, il/ilçe/mahalle, taslak numarası');
GO

MERGE dbo.Provinces AS t
USING (VALUES
    (23, 1, N'Adana'),
    (24, 2, N'Adıyaman'),
    (25, 3, N'Afyonkarahisar'),
    (26, 4, N'Ağrı'),
    (27, 5, N'Amasya'),
    (28, 6, N'Ankara'),
    (29, 7, N'Antalya'),
    (30, 8, N'Artvin'),
    (31, 9, N'Aydın'),
    (32, 10, N'Balıkesir'),
    (33, 11, N'Bilecik'),
    (34, 12, N'Bingöl'),
    (35, 13, N'Bitlis'),
    (36, 14, N'Bolu'),
    (37, 15, N'Burdur'),
    (38, 16, N'Bursa'),
    (39, 17, N'Çanakkale'),
    (40, 18, N'Çankırı'),
    (41, 19, N'Çorum'),
    (42, 20, N'Denizli'),
    (43, 21, N'Diyarbakır'),
    (44, 22, N'Edirne'),
    (45, 23, N'Elazığ'),
    (46, 24, N'Erzincan'),
    (47, 25, N'Erzurum'),
    (48, 26, N'Eskişehir'),
    (49, 27, N'Gaziantep'),
    (50, 28, N'Giresun'),
    (51, 29, N'Gümüşhane'),
    (52, 30, N'Hakkari'),
    (53, 31, N'Hatay'),
    (54, 32, N'Isparta'),
    (55, 33, N'Mersin'),
    (56, 34, N'İstanbul'),
    (57, 35, N'İzmir'),
    (58, 36, N'Kars'),
    (59, 37, N'Kastamonu'),
    (60, 38, N'Kayseri'),
    (61, 39, N'Kırklareli'),
    (62, 40, N'Kırşehir'),
    (63, 41, N'Kocaeli'),
    (64, 42, N'Konya'),
    (65, 43, N'Kütahya'),
    (66, 44, N'Malatya'),
    (67, 45, N'Manisa'),
    (68, 46, N'Kahramanmaraş'),
    (69, 47, N'Mardin'),
    (70, 48, N'Muğla'),
    (71, 49, N'Muş'),
    (72, 50, N'Nevşehir'),
    (73, 51, N'Niğde'),
    (74, 52, N'Ordu'),
    (75, 53, N'Rize'),
    (76, 54, N'Sakarya'),
    (77, 55, N'Samsun'),
    (78, 56, N'Siirt'),
    (79, 57, N'Sinop'),
    (80, 58, N'Sivas'),
    (81, 59, N'Tekirdağ'),
    (82, 60, N'Tokat'),
    (83, 61, N'Trabzon'),
    (84, 62, N'Tunceli'),
    (85, 63, N'Şanlıurfa'),
    (86, 64, N'Uşak'),
    (87, 65, N'Van'),
    (88, 66, N'Yozgat'),
    (89, 67, N'Zonguldak'),
    (90, 68, N'Aksaray'),
    (91, 69, N'Bayburt'),
    (92, 70, N'Karaman'),
    (93, 71, N'Kırıkkale'),
    (94, 72, N'Batman'),
    (95, 73, N'Şırnak'),
    (96, 74, N'Bartın'),
    (97, 75, N'Ardahan'),
    (98, 76, N'Iğdır'),
    (99, 77, N'Yalova'),
    (100, 78, N'Karabük'),
    (101, 79, N'Kilis'),
    (102, 80, N'Osmaniye'),
    (103, 81, N'Düzce')
) AS s (Id, PlateCode, Name)
ON t.Id = s.Id
WHEN MATCHED AND (t.Name <> s.Name OR t.PlateCode <> s.PlateCode) THEN UPDATE SET Name = s.Name, PlateCode = s.PlateCode
WHEN NOT MATCHED THEN INSERT (Id, PlateCode, Name) VALUES (s.Id, s.PlateCode, s.Name);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Announcements WHERE Id = '6f0c2a1e-3d4b-4c5a-9e8f-1a2b3c4d5e02')
    INSERT dbo.Announcements (Id, Title, Message, Level, Audience, TenantId, StartsAt, EndsAt, IsActive, CreatedBy, CreatedAt, UpdatedAt)
    VALUES (
        '6f0c2a1e-3d4b-4c5a-9e8f-1a2b3c4d5e02',
        N'UYARI',
        N'UYARI: BU PAKET VERGİYE TABİ OLAN MALİ VERİ OLUŞTURUR. BU PAKET NEDENİYLE OLUŞABİLECEK SORUNLARDAN BU PAKET SORUMLU TUTULAMAZ, RİSK KULLANANA AİTTİR. RİSKLİ GÖRÜYORSANIZ KULLANMAYINIZ.',
        'Danger',
        'Everyone',
        NULL,
        '2026-01-01T00:00:00+03:00',
        NULL,
        1,
        N'system',
        SYSDATETIMEOFFSET(),
        SYSDATETIMEOFFSET());
GO

EXEC sp_set_session_context @key = N'SystemContext', @value = 1;

;WITH pending AS (
    SELECT i.Id, i.TenantId, YEAR(i.IssueDate) AS FiscalYear,
           ROW_NUMBER() OVER (PARTITION BY i.TenantId, YEAR(i.IssueDate) ORDER BY i.CreatedAt, i.ClusterKey)
             + ISNULL(s.LastValue, 0) AS Seq
      FROM dbo.Invoices i
      LEFT JOIN dbo.DocumentSequences s
        ON s.TenantId = i.TenantId AND s.DocumentKind = 'Draft' AND s.Prefix = 'TSL' AND s.FiscalYear = YEAR(i.IssueDate)
     WHERE i.DraftNumber IS NULL
)
UPDATE i
   SET DraftNumber = 'TSL' + CAST(p.FiscalYear AS char(4)) + RIGHT('000000000' + CAST(p.Seq AS varchar(9)), 9)
  FROM dbo.Invoices i
  JOIN pending p ON p.Id = i.Id;

MERGE dbo.DocumentSequences AS t
USING (
    SELECT TenantId, YEAR(IssueDate) AS FiscalYear, MAX(CAST(RIGHT(DraftNumber, 9) AS bigint)) AS LastValue
      FROM dbo.Invoices
     WHERE DraftNumber IS NOT NULL
     GROUP BY TenantId, YEAR(IssueDate)
) AS s
ON t.TenantId = s.TenantId AND t.DocumentKind = 'Draft' AND t.Prefix = 'TSL' AND t.FiscalYear = s.FiscalYear
WHEN MATCHED AND t.LastValue < s.LastValue THEN UPDATE SET LastValue = s.LastValue, UpdatedAt = SYSDATETIMEOFFSET()
WHEN NOT MATCHED THEN INSERT (TenantId, DocumentKind, Prefix, FiscalYear, LastValue, UpdatedAt)
                      VALUES (s.TenantId, 'Draft', 'TSL', s.FiscalYear, s.LastValue, SYSDATETIMEOFFSET());

EXEC sp_set_session_context @key = N'SystemContext', @value = 0;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaInfo WHERE Version = 3)
    INSERT dbo.SchemaInfo (Version, Description) VALUES (3, N'Şifre sıfırlama talepleri, rol hiyerarşisi');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaInfo WHERE Version = 4)
    INSERT dbo.SchemaInfo (Version, Description) VALUES (4, N'Firma logosu, e-posta / SMS kuyruğu, olay kuyruğu, üçüncü taraf entegrasyonlar, kullanıcı telefonu');
GO
