# Katkı rehberi

1. Depoyu fork'layıp `main` dalından yeni bir dal açın (`feature/…`, `fix/…`).
2. `database/GIBFramework_TamKurulum.sql` ile yerel veritabanını kurun ve uygulamayı çalıştırın.
3. Değişiklikten sonra çözümü uyarısız derleyin:

   ```bash
   dotnet build GIBFramework.sln -c Release
   ```

4. Panel değişikliklerinde `GIBFramework/ClientApp` içinde `npm run build` çalıştırın.
5. Veritabanı değişikliklerini `database/0?_*.sql` dosyalarına tekrar çalıştırılabilir biçimde ekleyin, `04_BaslangicVerileri.sql`
   içindeki şema sürümünü ve `DAL/SchemaVerifier.cs` içindeki `RequiredVersion` değerini artırın.
6. Pull request açıklamasında değişikliği ve nasıl denediğinizi yazın.

## Kod stili

- C#: `.editorconfig` kuralları, `TreatWarningsAsErrors` açık.
- React: fonksiyon bileşenleri, Bootstrap 5 sınıfları, metinler Türkçe.
