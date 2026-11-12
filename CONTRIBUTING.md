# Katkı rehberi

1. Depoyu fork'layıp `main` dalından yeni bir dal açın (`feature/…`, `fix/…`).
2. .NET 10.0.404 SDK ve Node.js 24 kullanın. `database/GIBFramework_TamKurulum.sql` ile ayrı bir yerel SQL Server veritabanı kurun.
3. Çözümü Release yapılandırmasında derleyin. Derleme panel bağımlılıklarını kilit dosyasından yükler ve Vite çıktısını üretir.

   ```bash
   dotnet build GIBFramework.sln -c Release
   ```

4. Paneli ayrı doğrulamak için `GIBFramework/ClientApp` dizininde `npm ci` ve `npm run build` çalıştırın. Bağımlılık değişikliklerinde kilit dosyasını da güncelleyin.
5. Pull request açıklamasına kullanıcıya yansıyan değişikliği ve doğrulama sonuçlarını yazın. Sürüm notuna uygun maddeleri [CHANGELOG.md](CHANGELOG.md) dosyasına ekleyin.

## SQL değişiklikleri

- Numaralı SQL betiklerini kaynak olarak kullanın. Yeni bir şema sürümü gerektiğinde yükseltme betiğini, `SchemaVerifier.RequiredVersion` değerini ve üretim komutunun girdi listesini birlikte güncelleyin.
- Birleşik dosyaları `pwsh -File database/Build-Kurulum.ps1` ile üretin. `-Check` seçeneği birleşik betiklerin güncel olduğunu doğrular.
- Temiz kurulum, önceki sürümden yükseltme ve aynı betiğin ikinci çalıştırmasını ayrı yerel veritabanlarında deneyin. Firma kapsamlı değişiklikleri iki firma ve yeniden kullanılan SQL bağlantılarıyla sınayın.
- Vergi ve mevzuat verilerini tarihli sürümlerle yönetin. Kaldırılan kuralın bitiş tarihini kapatıp yeni sürüm ekleyin. `PendingExpertReview` durumundaki kurallar için üretim onayı gerekir.

## Kod stili

- C# için `.editorconfig` kurallarına uyun. Uyarılar Release derlemesini durdurur.
- React'te fonksiyon bileşenlerini ve mevcut Bootstrap bileşenlerini kullanın. Panel metinlerini Türkçe yazın.
- Niyeti isimlerle anlatın. Yorumları koddan çıkarılamayan gerekçeler, güvenlik sınırları ve lisans bildirimleri için kullanın.
- Paylaşım paketini temiz bir yayın dizininden üretin. Üretim ayarlarını, bağlantı parolalarını, JWT ve Data Protection anahtarlarını, kanıtları ve günlükleri ayrı tutun.
