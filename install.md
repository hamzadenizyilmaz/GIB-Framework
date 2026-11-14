# GIB Framework 6.0.0 kurulum kılavuzu

## Gereksinimler

- Derleme için Windows x64, [.NET 10 SDK 10.0.404](https://dotnet.microsoft.com/download/dotnet/10.0) ve Node.js 24.
- SQL Server 2019 veya üzeri. Yerel örnek SQL Server Express ve Windows kimlik doğrulaması kullanır.
- SQL betiklerini çalıştırmak için SSMS veya `sqlcmd`. Birleşik betikleri doğrulamak için PowerShell 7.
- IIS/Plesk için [.NET 10 Hosting Bundle ve ASP.NET Core Module V2](https://learn.microsoft.com/aspnet/core/host-and-deploy/iis/hosting-bundle?view=aspnetcore-10.0).

Komutları deponun kökünde PowerShell ile çalıştırın.

## Yerel kurulum

1. SQL Server hizmetini başlatın. Express örneği için `Get-Service -Name 'MSSQL$SQLEXPRESS'` ile durumu kontrol edin.
2. Yerel veritabanını kurun. Betik `GIBFramework` veritabanını oluşturur ve şema 5'e getirir.

   ```powershell
   sqlcmd -S '.\SQLEXPRESS' -E -C -b -f 65001 -i database/GIBFramework_TamKurulum.sql
   ```

3. JWT anahtarını ve ilk yönetici bilgilerini yerel kullanıcı sırlarına ekleyin. Parola kullanıcı kodunu içermemeli ve uygulamanın parola kurallarını karşılamalıdır.

   ```powershell
   $project = 'GIBFramework/GIBFramework.csproj'
   $signingKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
   dotnet user-secrets set 'Jwt:SigningKey' $signingKey --project $project
   dotnet user-secrets set 'Bootstrap:AdminUserCode' 'admin' --project $project
   $initialPassword = Read-Host 'İlk yönetici parolasını girin' -MaskInput
   dotnet user-secrets set 'Bootstrap:AdminPassword' $initialPassword --project $project
   Remove-Variable signingKey, initialPassword
   ```

   İlk yönetici boş veritabanında oluşturulur. Mevcut hesabın parolası bu ayarla değişmez. Yerel SQL örneğiniz farklıysa `ConnectionStrings:GibFramework` anahtarını da kullanıcı sırlarında ayarlayın.

4. Uygulamayı çalıştırın. MSBuild, npm kilit dosyası üzerinden paneli derler.

   ```powershell
   dotnet run --project $project --launch-profile http
   ```

5. [Yerel paneli](http://localhost:5097/app/#/login) açın. `/health` canlılığı, `/health/ready` SQL bağlantısını ve şema hazırlığını gösterir.

### İsteğe bağlı demo ve panel geliştirme

Demo için bir deneme veritabanı kullanın. Şifreyi siz belirleyin.

```powershell
$demoPassword = Read-Host 'Yerel demo parolasını girin' -MaskInput
dotnet user-secrets set 'Demo:Enabled' 'true' --project $project
dotnet user-secrets set 'Demo:Password' $demoPassword --project $project
Remove-Variable demoPassword
```

Uygulama yeniden başlatıldığında eksik demo hesapları oluşturulur. Var olan hesapların parolaları korunur. Demo hesapları yalnızca `Development` ve `Demo` ortamlarında kullanılabilir.

Paneli anlık yenilemeyle geliştirmek için API 5097'de çalışırken ikinci terminalde `GIBFramework/ClientApp` dizininde `npm ci` ve `npm run dev` çalıştırın. Vite adresi `http://localhost:5173/app/` olur.

## SQL Server ve Plesk

| Hedef | Betik |
|---|---|
| Yerelde kurulum | [GIBFramework_TamKurulum.sql](database/GIBFramework_TamKurulum.sql) |
| Plesk'te önceden oluşturulmuş boş veritabanı | [GIBFramework_Plesk.sql](database/GIBFramework_Plesk.sql) |
| Mevcut şema 4 | [06_Sema5.sql](database/06_Sema5.sql) |
| Mevcut şema 5 | Uygulama dosyalarını güncelleyin |

Plesk'te betiği çalıştırmadan önce bağlantının hedef veritabanını seçtiğini doğrulayın. Kurulum ve yükseltme hesabında `ALTER ANY SECURITY POLICY` ve betiklerin gerektirdiği DDL yetkileri bulunmalıdır. Uygulama için ayrı bir SQL hesabı veya IIS uygulama havuzu kimliği kullanın. Üretimde SQL sunucusunun TLS sertifikası doğrulanmalıdır.

Kurulumu kontrol edin.

```sql
SELECT MAX(Version) AS SchemaVersion FROM dbo.SchemaInfo;
SELECT name, is_enabled FROM sys.security_policies WHERE name = N'TenantIsolation';
```

Beklenen şema sürümü `5`, satır güvenliği durumu `is_enabled = 1`. Birleşik betiklerin numaralı kaynaklarla aynı olduğunu kontrol etmek için aşağıdaki komutu kullanın.

```powershell
pwsh -File database/Build-Kurulum.ps1 -Check
```

## IIS/Plesk için manuel yayın

1. Yeni veya boş bir hedef dizine Windows x64 yayını hazırlayın.

   ```powershell
   dotnet publish GIBFramework/GIBFramework.csproj -c Release -r win-x64 --self-contained true -o ./publish/httpdocs
   ```

2. `publish/httpdocs/` içeriğini IIS/Plesk sitesi köküne aktarın. `GIBFramework.exe`, `web.config`, `wwwroot/app/` ve `DAL/Seed/` yayın içinde yer alır. SQL betiklerini hedef veritabanına ayrı yönetici oturumundan uygulayın.
3. Uygulama havuzunu 64 bit ve tek işçi süreci ile çalıştırın. HTTPS sertifikasını siteye bağlayın ve aşağıdaki değerleri dağıtım ortamından sağlayın.

| Ortam değişkeni | Değer veya açıklama |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__GibFramework` | Üretim SQL bağlantı dizesi |
| `Jwt__Issuer` / `Jwt__SigningKey` | Ortama özel issuer ve en az 32 baytlık rastgele gizli anahtar |
| `Bootstrap__AdminUserCode` / `Bootstrap__AdminPassword` | Yalnızca ilk kurulum için yönetici bilgileri |
| `App__PublicUrl` / `AllowedHosts` | Panelin HTTPS kök adresi ve kabul edilen sunucu adı |
| `Demo__Enabled` / `Compliance__AllowUnapprovedRules` | `false` |
| `Compliance__SeedPath` | Onaylı kural ve vergi veri paketinin dizini |
| `Signing__Mode` / `Signing__TrustedRootSha256__0` | `WindowsStore` ve güvenilen kök sertifikanın SHA-256 değeri. Diğer kökler için dizi indeksini artırın. |
| `Providers__PortalOnly` / `GibPortal__Enabled` / `GibPortal__AllowProduction` | Desteklenen üretim portal akışı için `true` |
| `Ubl__SchemaDirectory`, `Ubl__SchemaVersion`, `Ubl__SchemaSha256` | Onaylı UBL-TR XSD paketi ve sürüm özeti |
| `Ubl__SchematronPath`, `Ubl__SchematronSha256` | SVRL çıktısı üreten XSLT 1.0 şematronu ve dosya özeti |
| `EvidenceVault__RootPath` / `Security__DataProtectionKeysPath` | Kalıcı, yalnızca servis kimliğinin ve yetkili operatörlerin eriştiği dizinler |
| `ReverseProxy__KnownProxies__0` | Varsa güvenilen ters vekilin IP adresi |

Ayar ayrıntıları ve XSD dizin özetinin hesaplanması [README.md](README.md#yapılandırma) bölümündedir. Depodaki örnek belge yönlendirme kuralları uzman incelemesi bekler. Üretime geçmeden önce kurumunuzca onaylanmış ve yürürlükte olan e-Fatura ve e-Arşiv kurallarını kullanın. Portal bağlantısı, imza, arşivlenen XML ve müşteri teslimatını kendi ortamınızda doğrulayın.

### Yükseltme ve geri dönüş

1. SQL veritabanını, kanıt kasasını, anahtar dizinini ve `App_Data` altındaki IP kurallarını yedekleyin. Mevcut JWT anahtarını, Data Protection anahtarlarını ve uygulama adını koruyun.
2. IIS uygulamasını durdurun. Şema 4 ise yükseltme betiğini uygulayın. Yeni uygulama dosyalarını aktarın ve siteyi başlatın.
3. HTTPS üzerinden `/health/ready`, yönetici girişi, IP Erişimi, fatura okuma ve örnek PDF indirmeyi kontrol edin. HTTP 503 alırsanız yetkili hesapla **Ayarlar → Sistem durumu** sayfasındaki nedeni inceleyin.
4. Geri dönüş gerekiyorsa önceki uygulama dosyalarını ve o sürümle uyumlu veri yedeğini birlikte kullanın.

## GitHub'a kaynak yükleme

Mevcut GitHub deposunun temiz bir klonunda çalışma dalı açın ve gözden geçirilmiş kaynak dosyalarını kopyalayın. `git status --short` ile değişiklikleri, `git diff --cached --name-only` ile eklenecek dosyaları doğrulayın. `database/` altına şema ve başlangıç verisi betiklerini alın. Üretim SQL yedeği, uygulama anahtarları, kanıtlar, günlükler, kişisel dosyalar ve `publish/` çalışma ortamına aittir.

Kaynağı doğrulamak için aşağıdaki komutları çalıştırın. İnceleme tamamlandıktan sonra sürüm etiketi olarak `v6.0.0` kullanın.

```powershell
pwsh -File database/Build-Kurulum.ps1 -Check
dotnet build GIBFramework.sln -c Release
```
