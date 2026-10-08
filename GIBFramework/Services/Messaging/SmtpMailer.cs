using System.Globalization;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using GIBFramework.Models.Messaging;

namespace GIBFramework.Services.Messaging;

public sealed record SmtpEndpoint(string Host, int Port, SmtpSecurity Security, string? UserName, string? Password, string FromAddress, string FromName, string? ReplyTo);

public sealed record MailAttachment(string FileName, string ContentType, byte[] Content, string? ContentId = null);

public sealed record MailMessageData(
    string ToAddress,
    string? ToName,
    string Subject,
    string Html,
    string Text,
    IReadOnlyList<MailAttachment> Attachments);

public sealed class SmtpException(string message, int? code = null, bool transient = false) : Exception(message)
{
    public int? Code { get; } = code;

    public bool Transient { get; } = transient;
}

public static class MimeBuilder
{
    public static byte[] Build(SmtpEndpoint from, MailMessageData message, DateTimeOffset now, string messageId)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(message);
        var mixed = Boundary();
        var related = Boundary();
        var alternative = Boundary();
        var inline = message.Attachments.Where(a => a.ContentId is not null).ToList();
        var files = message.Attachments.Where(a => a.ContentId is null).ToList();

        var sb = new StringBuilder();
        Header(sb, "From", Address(from.FromName, from.FromAddress));
        Header(sb, "To", Address(message.ToName, message.ToAddress));
        if (!string.IsNullOrWhiteSpace(from.ReplyTo))
        {
            Header(sb, "Reply-To", $"<{from.ReplyTo.Trim()}>");
        }

        Header(sb, "Subject", EncodeWord(message.Subject));
        Header(sb, "Date", now.ToString("ddd, dd MMM yyyy HH:mm:ss ", CultureInfo.InvariantCulture) + now.ToString("zzz", CultureInfo.InvariantCulture).Replace(":", string.Empty, StringComparison.Ordinal));
        Header(sb, "Message-ID", $"<{messageId}>");
        Header(sb, "MIME-Version", "1.0");
        Header(sb, "X-Mailer", "GIB Framework");
        Header(sb, "Content-Type", $"multipart/mixed; boundary=\"{mixed}\"");
        sb.Append("\r\n");

        sb.Append("--").Append(mixed).Append("\r\n");
        sb.Append("Content-Type: multipart/related; boundary=\"").Append(related).Append("\"\r\n\r\n");
        sb.Append("--").Append(related).Append("\r\n");
        sb.Append("Content-Type: multipart/alternative; boundary=\"").Append(alternative).Append("\"\r\n\r\n");
        Part(sb, alternative, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(message.Text), null);
        Part(sb, alternative, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(message.Html), null);
        sb.Append("--").Append(alternative).Append("--\r\n");
        foreach (var a in inline)
        {
            Part(sb, related, a.ContentType, a.Content, $"Content-ID: <{a.ContentId}>\r\nContent-Disposition: inline; filename=\"{SafeFileName(a.FileName)}\"");
        }

        sb.Append("--").Append(related).Append("--\r\n");
        foreach (var a in files)
        {
            var name = SafeFileName(a.FileName);
            Part(sb, mixed, $"{a.ContentType}; name=\"{name}\"", a.Content, $"Content-Disposition: attachment; filename=\"{name}\"");
        }

        sb.Append("--").Append(mixed).Append("--\r\n");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    public static string EncodeWord(string? value)
    {
        value ??= string.Empty;
        if (value.All(c => c is >= ' ' and <= '~'))
        {
            return value;
        }

        var words = new List<string>();
        var chunk = new StringBuilder();
        foreach (var rune in value.EnumerateRunes())
        {
            if (Encoding.UTF8.GetByteCount(chunk + rune.ToString()) > 45)
            {
                words.Add(chunk.ToString());
                chunk.Clear();
            }

            chunk.Append(rune.ToString());
        }

        if (chunk.Length > 0)
        {
            words.Add(chunk.ToString());
        }

        return string.Join("\r\n ", words.Select(w => $"=?UTF-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(w))}?="));
    }

    private static string Address(string? name, string address) =>
        string.IsNullOrWhiteSpace(name) ? $"<{address.Trim()}>" : $"{EncodeWord(name.Replace("\"", string.Empty, StringComparison.Ordinal).Trim())} <{address.Trim()}>";

    private static void Header(StringBuilder sb, string name, string value) => sb.Append(name).Append(": ").Append(value).Append("\r\n");

    private static void Part(StringBuilder sb, string boundary, string contentType, byte[] content, string? extraHeaders)
    {
        sb.Append("--").Append(boundary).Append("\r\n");
        sb.Append("Content-Type: ").Append(contentType).Append("\r\n");
        sb.Append("Content-Transfer-Encoding: base64\r\n");
        if (extraHeaders is not null)
        {
            sb.Append(extraHeaders).Append("\r\n");
        }

        sb.Append("\r\n");
        var b64 = Convert.ToBase64String(content);
        for (var i = 0; i < b64.Length; i += 76)
        {
            sb.Append(b64, i, Math.Min(76, b64.Length - i)).Append("\r\n");
        }
    }

    private static string SafeFileName(string name)
    {
        var map = new Dictionary<char, char> { ['ç'] = 'c', ['Ç'] = 'C', ['ğ'] = 'g', ['Ğ'] = 'G', ['ı'] = 'i', ['İ'] = 'I', ['ö'] = 'o', ['Ö'] = 'O', ['ş'] = 's', ['Ş'] = 'S', ['ü'] = 'u', ['Ü'] = 'U' };
        var chars = name.Select(c => map.TryGetValue(c, out var r) ? r : c).Where(c => c is >= ' ' and <= '~' and not '"' and not '\\').ToArray();
        return chars.Length == 0 ? "ek" : new string(chars);
    }

    private static string Boundary() => "=_gf_" + Guid.NewGuid().ToString("N");
}

public sealed class SmtpMailer(MessagingOptions options)
{
    public async Task<string> SendAsync(SmtpEndpoint endpoint, MailMessageData message, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(message);
        var domain = endpoint.FromAddress.Contains('@', StringComparison.Ordinal) ? endpoint.FromAddress[(endpoint.FromAddress.IndexOf('@', StringComparison.Ordinal) + 1)..] : "gibframework.local";
        var messageId = $"{Guid.NewGuid():N}@{domain}";
        var mime = MimeBuilder.Build(endpoint, message, now, messageId);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, options.SmtpTimeoutSeconds)));
        var token = timeout.Token;
        using var tcp = new TcpClient();
        try
        {
            await tcp.ConnectAsync(endpoint.Host, endpoint.Port, token);
        }
        catch (SocketException ex)
        {
            throw new SmtpException($"SMTP sunucusuna bağlanılamadı ({endpoint.Host}:{endpoint.Port}): {ex.Message}", transient: true);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new SmtpException($"SMTP sunucusuna bağlantı zaman aşımına uğradı ({endpoint.Host}:{endpoint.Port}).", transient: true);
        }

        Stream stream = tcp.GetStream();
        try
        {
            if (endpoint.Security == SmtpSecurity.SslOnConnect)
            {
                stream = await UpgradeAsync(stream, endpoint.Host, token);
            }

            var session = new SmtpSession(stream);
            await session.ExpectAsync(220, token);
            var capabilities = await session.EhloAsync(token);
            if (endpoint.Security == SmtpSecurity.StartTls)
            {
                if (!capabilities.Any(c => c.StartsWith("STARTTLS", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new SmtpException("SMTP sunucusu STARTTLS desteklemiyor; güvenlik türünü SSL (465) veya Yok olarak değiştirin.");
                }

                await session.CommandAsync("STARTTLS", 220, token);
                stream = await UpgradeAsync(stream, endpoint.Host, token);
                session = new SmtpSession(stream);
                capabilities = await session.EhloAsync(token);
            }

            if (!string.IsNullOrEmpty(endpoint.UserName))
            {
                await AuthenticateAsync(session, capabilities, endpoint.UserName, endpoint.Password ?? string.Empty, token);
            }

            await session.CommandAsync($"MAIL FROM:<{endpoint.FromAddress.Trim()}>", 250, token);
            await session.CommandAsync($"RCPT TO:<{message.ToAddress.Trim()}>", [250, 251], token);
            await session.CommandAsync("DATA", 354, token);
            await session.WriteDataAsync(mime, token);
            var accepted = await session.ExpectAsync(250, token);
            try
            {
                await session.CommandAsync("QUIT", 221, token);
            }
            catch (Exception ex) when (ex is SmtpException or IOException)
            {
            }

            return string.IsNullOrWhiteSpace(accepted) ? messageId : $"{messageId} {accepted}".Trim();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new SmtpException("SMTP sunucusu zamanında yanıt vermedi.", transient: true);
        }
        catch (IOException ex)
        {
            throw new SmtpException("SMTP bağlantısı kesildi: " + ex.Message, transient: true);
        }
        catch (AuthenticationException ex)
        {
            throw new SmtpException("TLS el sıkışması başarısız: " + ex.Message);
        }
        finally
        {
            await stream.DisposeAsync();
        }
    }

    private static async Task AuthenticateAsync(SmtpSession session, IReadOnlyList<string> capabilities, string user, string password, CancellationToken ct)
    {
        var auth = capabilities.FirstOrDefault(c => c.StartsWith("AUTH", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        try
        {
            if (auth.Contains("PLAIN", StringComparison.OrdinalIgnoreCase) || !auth.Contains("LOGIN", StringComparison.OrdinalIgnoreCase))
            {
                var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"\0{user}\0{password}"));
                await session.CommandAsync($"AUTH PLAIN {token}", 235, ct, sensitive: true);
                return;
            }

            await session.CommandAsync("AUTH LOGIN", 334, ct);
            await session.CommandAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(user)), 334, ct, sensitive: true);
            await session.CommandAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(password)), 235, ct, sensitive: true);
        }
        catch (SmtpException ex) when (ex.Code is >= 500)
        {
            throw new SmtpException("SMTP kimlik doğrulaması başarısız: kullanıcı adı veya şifre hatalı ya da hesap SMTP gönderimine kapalı. " + ex.Message, ex.Code);
        }
    }

    private static async Task<Stream> UpgradeAsync(Stream inner, string host, CancellationToken ct)
    {
        var ssl = new SslStream(inner, leaveInnerStreamOpen: false);
        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = host }, ct);
        return ssl;
    }

    private sealed class SmtpSession(Stream stream)
    {
        private readonly byte[] _buffer = new byte[4096];
        private int _start;
        private int _end;

        public async Task<IReadOnlyList<string>> EhloAsync(CancellationToken ct)
        {
            await WriteLineAsync($"EHLO {Environment.MachineName.ToLowerInvariant()}", ct);
            var (code, lines) = await ReadReplyAsync(ct);
            if (code != 250)
            {
                await WriteLineAsync($"HELO {Environment.MachineName.ToLowerInvariant()}", ct);
                (code, lines) = await ReadReplyAsync(ct);
                if (code != 250)
                {
                    throw new SmtpException($"SMTP EHLO reddedildi: {code} {string.Join(' ', lines)}", code);
                }
            }

            return [.. lines.Skip(1)];
        }

        public Task<string> CommandAsync(string command, int expected, CancellationToken ct, bool sensitive = false) =>
            CommandAsync(command, [expected], ct, sensitive);

        public async Task<string> CommandAsync(string command, int[] expected, CancellationToken ct, bool sensitive = false)
        {
            await WriteLineAsync(command, ct);
            var (code, lines) = await ReadReplyAsync(ct);
            if (!expected.Contains(code))
            {
                var shown = sensitive ? "AUTH" : command.Split(' ')[0];
                throw new SmtpException($"SMTP {shown} komutu reddedildi: {code} {string.Join(' ', lines)}", code, transient: code is >= 400 and < 500);
            }

            return string.Join(' ', lines);
        }

        public async Task<string> ExpectAsync(int expected, CancellationToken ct)
        {
            var (code, lines) = await ReadReplyAsync(ct);
            if (code != expected)
            {
                throw new SmtpException($"SMTP beklenmeyen yanıt: {code} {string.Join(' ', lines)}", code, transient: code is >= 400 and < 500);
            }

            return string.Join(' ', lines);
        }

        public async Task WriteDataAsync(byte[] mime, CancellationToken ct)
        {
            using var output = new MemoryStream(mime.Length + 1024);
            var lineStart = true;
            foreach (var b in mime)
            {
                if (lineStart && b == (byte)'.')
                {
                    output.WriteByte((byte)'.');
                }

                output.WriteByte(b);
                lineStart = b == (byte)'\n';
            }

            if (!lineStart)
            {
                output.Write("\r\n"u8);
            }

            output.Write(".\r\n"u8);
            await stream.WriteAsync(output.ToArray(), ct);
            await stream.FlushAsync(ct);
        }

        private async Task WriteLineAsync(string line, CancellationToken ct)
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes(line + "\r\n"), ct);
            await stream.FlushAsync(ct);
        }

        private async Task<(int Code, List<string> Lines)> ReadReplyAsync(CancellationToken ct)
        {
            var lines = new List<string>();
            while (true)
            {
                var line = await ReadLineAsync(ct);
                if (line.Length < 3 || !int.TryParse(line.AsSpan(0, 3), NumberStyles.None, CultureInfo.InvariantCulture, out var code))
                {
                    throw new SmtpException("SMTP sunucusundan geçersiz yanıt: " + line);
                }

                lines.Add(line.Length > 4 ? line[4..] : string.Empty);
                if (line.Length == 3 || line[3] != '-')
                {
                    return (code, lines);
                }
            }
        }

        private async Task<string> ReadLineAsync(CancellationToken ct)
        {
            var sb = new StringBuilder();
            while (true)
            {
                if (_start == _end)
                {
                    _start = 0;
                    _end = await stream.ReadAsync(_buffer, ct);
                    if (_end == 0)
                    {
                        throw new IOException("Sunucu bağlantıyı kapattı.");
                    }
                }

                while (_start < _end)
                {
                    var b = _buffer[_start++];
                    if (b == '\n')
                    {
                        return sb.ToString().TrimEnd('\r');
                    }

                    sb.Append((char)b);
                    if (sb.Length > 8192)
                    {
                        throw new SmtpException("SMTP yanıt satırı çok uzun.");
                    }
                }
            }
        }
    }
}
