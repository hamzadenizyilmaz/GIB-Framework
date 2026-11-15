# GIB Framework 6.1.1 kurulum kılavuzu

## Gereksinimler

- Derleme için Windows x64, [.NET 10 SDK 10.0.302 veya daha yeni bir .NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) ve Node.js 24.
- SQL Server 2019 veya üzeri. Yerel örnek SQL Server Express ve Windows kimlik doğrulaması kullanır.
- SQL betiklerini çalıştırmak için SSMS veya `sqlcmd`. Birleşik betikleri doğrulamak için PowerShell 7.
- IIS/Plesk için [.NET 10 Hosting Bundle ve ASP.NET Core Module V2](https://learn.microsoft.com/aspnet/core/host-and-deploy/iis/hosting-bundle?view=aspnetcore-10.0).

Komutları deponun kökünde PowerShell ile çalıştırın.

## Yerel kurulum

1. SQL Server hizmetini başlatın. Express örneği için `Get-Service -Name 'MSSQL$SQLEXPRESS'` ile durumu kontrol edin.
2. Yerel veritabanını kurun. Betik `GIBFramework` veritabanını oluşturur ve şema 7'ye getirir.

   ```powershell
   sqlcmd -S '.\SQLEXPRESS' -E -C -b -f 65001 -i database/GIBFramework-Local-Install.sql
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
   dotnet dev-certs https --trust
   dotnet run --project $project --launch-profile https
   ```

5. [Yerel paneli](https://localhost:7188/app/#/login) açın. `/health` canlılığı, `/health/ready` SQL bağlantısını ve şema hazırlığını gösterir.

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
| Yerelde kurulum | [GIBFramework-Local-Install.sql](database/GIBFramework-Local-Install.sql) |
| Plesk'te önceden oluşturulmuş boş veritabanı | [GIBFramework-Plesk-Install.sql](database/GIBFramework-Plesk-Install.sql) |
| Mevcut şema 4 | Sırayla [06-Upgrade-Schema.sql](database/06-Upgrade-Schema.sql), [07-Upgrade-Schema.sql](database/07-Upgrade-Schema.sql) ve [08-Upgrade-Schema.sql](database/08-Upgrade-Schema.sql) |
| Mevcut şema 5 | Sırayla [07-Upgrade-Schema.sql](database/07-Upgrade-Schema.sql) ve [08-Upgrade-Schema.sql](database/08-Upgrade-Schema.sql) |
| Mevcut şema 6 | [08-Upgrade-Schema.sql](database/08-Upgrade-Schema.sql) |
| Mevcut şema 7 | Uygulama dosyalarını güncelleyin |

### Tarayıcıdan ilk yönetici kurulumu

SQL şemasını kurup bağlantı dizesini ayarladıktan sonra kurulum için rastgele bir anahtar üretin. `Install__TokenSha256` ortam değişkenine veya `appsettings.json` dosyasındaki `Install.TokenSha256` alanına anahtarın SHA-256 özetini girin. Kurulum ekranında anahtarın kendisini kullanın.

```powershell
$installKey = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$installHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($installKey)))
```

Uygulamayı yeniden başlatın ve siteyi HTTPS üzerinden açın. SQL hazır ve kurulum anahtarı tanımlıysa ana adres `/install` sayfasına yönlendirir. Firma bilgilerini, platform yöneticisi hesabını ve firma sahibi hesabını girin. Web kurulumu boş bir uygulama veritabanı gerektirir. Bu yöntem için otomatik yönetici ve demo hesabı ayarlarını boş veya kapalı bırakın.

Firma, iki hesap ve kurulum kaydı tek SQL işleminde oluşturulur. İşlem tamamlandığında `/install` ve `/api/v1/install` kapanır. Kurulum kaydı veritabanında kalıcıdır ve uygulama yeniden başlatıldığında korunur. Ardından kurulum özeti ayarını kaldırın.

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
| `App__PublicUrl` / `AllowedHosts` | Panelin HTTPS kök adresi ve noktalı virgülle ayrılmış izinli sunucu adları. Liste boşsa HTTPS kök adresinin alan adı kullanılır. |
| `Demo__Enabled` / `Compliance__AllowUnapprovedRules` | `false` |
| `Compliance__SeedPath` | Onaylı kural ve vergi veri paketinin dizini |
| `Signing__Mode` / `Signing__TrustedRootSha256__0` | `WindowsStore` ve güvenilen kök sertifikanın SHA-256 değeri. Diğer kökler için dizi indeksini artırın. |
| `Providers__PortalOnly` / `GibPortal__Enabled` / `GibPortal__AllowProduction` | Desteklenen üretim portal akışı için `true` |
| `Ubl__SchemaDirectory`, `Ubl__SchemaVersion`, `Ubl__SchemaSha256` | Onaylı UBL-TR XSD paketi ve sürüm özeti |
| `Ubl__SchematronPath`, `Ubl__SchematronSha256` | SVRL çıktısı üreten XSLT 1.0 şematronu ve dosya özeti |
| `EvidenceVault__RootPath` / `Security__DataProtectionKeysPath` | Kalıcı, yalnızca servis kimliğinin ve yetkili operatörlerin eriştiği dizinler |
| `ReverseProxy__KnownProxies__0` | TLS sonlandıran veya istemci IP’si aktaran güvenilen ters vekilin doğrudan bağlantı IP adresi. Vekil gelen `X-Forwarded-For` ve `X-Forwarded-Proto` değerlerini kendisi yazmalıdır. |
| `ReverseProxy__KnownNetworks__0` | Güvenilen ters vekillerin CIDR ağı. Diğer ağları ayrı dizi indekslerine ekleyin. |
| `ReverseProxy__ForwardLimit` | Güvenilen zincirde işlenecek vekil sayısı. Varsayılan 1. Birden fazla vekil varsa zincirdeki her güvenilir vekili ayrıca tanımlayın. |
| `ReverseProxy__ForwardedForHeaderName` | Varsayılan `X-Forwarded-For`. Cloudflare doğrudan uygulamaya bağlanıyorsa ve yalnızca Cloudflare vekilleri güvenilir olarak tanımlıysa `CF-Connecting-IP` ve `ForwardLimit=1` kullanılabilir. |

Ayar ayrıntıları ve XSD dizin özetinin hesaplanması [README.md](README.md#yapılandırma) bölümündedir. Depodaki örnek belge yönlendirme kuralları uzman incelemesi bekler. Üretime geçmeden önce kurumunuzca onaylanmış ve yürürlükte olan e-Fatura ve e-Arşiv kurallarını kullanın: ya onaylı kural paketini `Compliance__SeedPath` ile verin ya da kuralları **Mevzuat & uyum › Kurallar** ekranında iki farklı yetkiliye (Platform yöneticisi / Uyum sorumlusu) inceletip onaylatın. Panel onayları `App_Data/rule-reviews.json` dosyasında kuralın içerik parmak iziyle saklanır; kural içeriği değişirse onay geçersiz olur ve yeniden onay gerekir. Portal bağlantısı, imza, arşivlenen XML ve müşteri teslimatını kendi ortamınızda doğrulayın.

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
