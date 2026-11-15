<?php

declare(strict_types=1);

namespace GibFramework\Whmcs;

use RuntimeException;

defined('WHMCS') || exit;

/**
 * Bir faturanın gönderilemediğini anlatan hata.
 * pauseQueue=true ise sorun tüm kuyruğu etkiler (adres, imza, sunucu); false ise yalnızca o faturayı.
 */
final class DeliveryException extends RuntimeException
{
    public function __construct(
        public readonly string $reason,
        public readonly bool $pauseQueue = false,
        public readonly int $retryAfter = 0,
        public readonly ?string $detail = null,
        public readonly ?string $observedIp = null,
    ) {
        parent::__construct($reason);
    }
}

/**
 * GIB Framework panelindeki "WHMCS" entegrasyonunun gelen adresine (POST /api/v1/hooks/{id})
 * X-GibFramework-Signature: sha256=HMAC_SHA256(imza anahtarı, gövde) başlığıyla imzalı JSON gönderir.
 */
final class WebhookClient
{
    public const SIGNATURE_HEADER = 'X-GibFramework-Signature';
    private const URL_PATTERN = '~^https://[^/?#@\s]+/api/v1/hooks/[a-f0-9]{8}-(?:[a-f0-9]{4}-){3}[a-f0-9]{12}$~iD';
    private const SECRET_PATTERN = '/^gfs_[a-f0-9]{48}$/D';

    private readonly string $url;
    private readonly string $secret;

    public function __construct(string $url, string $secret, private readonly ?\Closure $observe = null)
    {
        $url = rtrim(trim($url), '/');
        if ($url === '') {
            throw new DeliveryException('CONFIG_WEBHOOK_URL_MISSING', true, 300);
        }
        if (!self::isWebhookUrl($url)) {
            throw new DeliveryException('CONFIG_WEBHOOK_URL', true, 300);
        }
        $secret = self::secret($secret);
        if ($secret === '') {
            throw new DeliveryException('CONFIG_SIGNING_SECRET_MISSING', true, 300);
        }
        if (!preg_match(self::SECRET_PATTERN, $secret)) {
            throw new DeliveryException('CONFIG_SIGNING_SECRET', true, 300);
        }
        $this->url = $url;
        $this->secret = $secret;
    }

    public static function isWebhookUrl(string $url): bool
    {
        return preg_match(self::URL_PATTERN, rtrim(trim($url), '/')) === 1 && filter_var(rtrim(trim($url), '/'), FILTER_VALIDATE_URL) !== false;
    }

    /** Panelin kök adresi (ör. https://sandbox.example.com). Fatura bağlantıları için kullanılır. */
    public static function panelOrigin(string $url): string
    {
        if (!self::isWebhookUrl($url)) {
            return '';
        }
        $parts = parse_url(trim($url));
        return 'https://' . $parts['host'] . (isset($parts['port']) ? ':' . $parts['port'] : '');
    }

    /**
     * WHMCS "password" alanlarını şifreli saklayabilir. Değer düz gfs_ anahtarı değilse WHMCS decrypt() ile açılır.
     */
    public static function secret(string $value): string
    {
        $value = trim($value);
        if ($value === '' || preg_match(self::SECRET_PATTERN, $value)) {
            return $value;
        }
        if (function_exists('decrypt')) {
            $plain = trim((string) decrypt($value));
            if (preg_match(self::SECRET_PATTERN, $plain)) {
                return $plain;
            }
        }
        return $value;
    }

    public function sign(string $body): string
    {
        return 'sha256=' . hash_hmac('sha256', $body, $this->secret);
    }

    /**
     * Faturayı gönderir. Başarılı yanıt: {ok, created, invoiceId, draftNumber, status, documentType, warning}.
     */
    /**
     * Bağlantı testi: panel imzayı doğrular, "ping" olayını faturalamadan yok sayar (HTTP 200, ignored).
     * Fatura oluşturmaz.
     */
    public function ping(): void
    {
        try {
            $this->send(['event' => 'ping', 'externalId' => 'whmcs-ping', 'lines' => []]);
        } catch (DeliveryException $error) {
            if ($error->reason !== 'FRAMEWORK_IGNORED') {
                throw $error;
            }
            return;
        }
        throw new DeliveryException('FRAMEWORK_RESPONSE');
    }

    public function send(array $payload): array
    {
        $body = json_encode($payload, JSON_THROW_ON_ERROR | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_INVALID_UTF8_SUBSTITUTE);
        $content = '';
        $retryAfter = 0;
        $cloudflare = false;
        $curl = curl_init($this->url);
        curl_setopt_array($curl, [
            CURLOPT_POST => true,
            CURLOPT_POSTFIELDS => $body,
            CURLOPT_HTTPHEADER => [
                'Content-Type: application/json',
                'Accept: application/json',
                'User-Agent: GIBFramework-WHMCS/' . (defined('GIBFRAMEWORK_MODULE_VERSION') ? GIBFRAMEWORK_MODULE_VERSION : '2'),
                self::SIGNATURE_HEADER . ': ' . $this->sign($body),
            ],
            CURLOPT_CONNECTTIMEOUT => 5,
            CURLOPT_TIMEOUT => 25,
            CURLOPT_PROTOCOLS => CURLPROTO_HTTPS,
            CURLOPT_IPRESOLVE => CURL_IPRESOLVE_V4,
            CURLOPT_SSL_VERIFYPEER => true,
            CURLOPT_SSL_VERIFYHOST => 2,
            CURLOPT_FOLLOWLOCATION => false,
            CURLOPT_WRITEFUNCTION => static function ($curl, string $chunk) use (&$content): int {
                if (strlen($content) + strlen($chunk) > 262144) {
                    return 0;
                }
                $content .= $chunk;
                return strlen($chunk);
            },
            CURLOPT_HEADERFUNCTION => static function ($curl, string $line) use (&$retryAfter, &$cloudflare): int {
                if (strncasecmp($line, 'cf-mitigated:', 13) === 0 || preg_match('/^server:\s*cloudflare/i', $line)) {
                    $cloudflare = true;
                }
                if (strncasecmp($line, 'Retry-After:', 12) === 0) {
                    $value = trim(substr($line, 12));
                    $retryAfter = min(86400, max(0, ctype_digit($value) ? (int) $value : (int) strtotime($value) - time()));
                }
                return strlen($line);
            },
        ]);
        $sent = curl_exec($curl);
        $status = (int) curl_getinfo($curl, CURLINFO_HTTP_CODE);
        $duration = (int) round(1000 * (float) curl_getinfo($curl, CURLINFO_TOTAL_TIME));
        $endpoint = '/api/v1/hooks/{id}';

        if ($sent === false) {
            ($this->observe)?->__invoke('POST', $endpoint, $status, $duration, 'FRAMEWORK_CONNECTION');
            throw new DeliveryException('FRAMEWORK_CONNECTION', true, 120);
        }

        $data = json_decode($content, true);
        $data = is_array($data) ? $data : [];
        $message = self::message($data);

        if ($status === 401) {
            ($this->observe)?->__invoke('POST', $endpoint, $status, $duration, 'SIGNATURE_REJECTED');
            throw new DeliveryException('SIGNATURE_REJECTED', true, 600, $message);
        }
        if ($status === 403) {
            // 403 webhook imzasından gelmez: istek panelin IP erişim listesine veya Cloudflare WAF'ına takılmıştır.
            $code = is_string($data['code'] ?? null) ? $data['code'] : '';
            $reason = match (true) {
                $code === 'IP_ACCESS_DENIED' => 'PANEL_IP_DENIED',
                $code === 'TENANT_IP_ACCESS_DENIED' => 'PANEL_TENANT_IP_DENIED',
                $data === [] && ($cloudflare || stripos($content, 'cloudflare') !== false) => 'CLOUDFLARE_BLOCKED',
                default => 'HTTP_403',
            };
            $details = array_filter([$message]);
            $rawIp = in_array($reason, ['PANEL_IP_DENIED', 'PANEL_TENANT_IP_DENIED'], true) ? ($data['clientIp'] ?? null) : null;
            if ($reason === 'CLOUDFLARE_BLOCKED' && preg_match('~id="cf-footer-ip"[^>]*>\s*([0-9a-f:.]{3,45})\s*<~i', $content, $found)) {
                $rawIp = $found[1];
            }
            $ip = is_string($rawIp) && filter_var($rawIp, FILTER_VALIDATE_IP) !== false ? $rawIp : null;
            if ($ip !== null) {
                $details[] = ($reason === 'CLOUDFLARE_BLOCKED' ? 'Cloudflare’in gördüğü IP: ' : 'Panelin kontrol ettiği IP: ') . $ip;
            }
            if ($reason === 'PANEL_IP_DENIED') {
                $rule = match ($data['reason'] ?? null) {
                    'allowList' => 'IP, site geneli izin listesiyle eşleşmedi.',
                    'denyList' => 'IP, site geneli engel listesiyle eşleşti.',
                    'missingAddress' => 'Panel istemci adresini belirleyemedi.',
                    default => null,
                };
                if ($rule !== null) {
                    $details[] = $rule;
                }
                if (is_string($data['correlationId'] ?? null) && preg_match('/^[a-zA-Z0-9_-]{1,64}$/D', $data['correlationId'])) {
                    $details[] = 'İstek kimliği: ' . $data['correlationId'];
                }
            }
            ($this->observe)?->__invoke('POST', $endpoint, $status, $duration, $reason);
            throw new DeliveryException($reason, true, 900, $details === [] ? null : implode(' · ', $details), $ip);
        }
        if ($status === 404) {
            ($this->observe)?->__invoke('POST', $endpoint, $status, $duration, 'INTEGRATION_DISABLED');
            throw new DeliveryException('INTEGRATION_DISABLED', true, 600, $message);
        }
        if (in_array($status, [400, 413, 422], true)) {
            ($this->observe)?->__invoke('POST', $endpoint, $status, $duration, 'FRAMEWORK_REJECTED');
            throw new DeliveryException('FRAMEWORK_REJECTED', false, 0, $message);
        }
        if ($status === 429) {
            ($this->observe)?->__invoke('POST', $endpoint, $status, $duration, 'RATE_LIMITED');
            throw new DeliveryException('RATE_LIMITED', true, max(60, $retryAfter));
        }
        if ($status < 200 || $status >= 300) {
            ($this->observe)?->__invoke('POST', $endpoint, $status, $duration, 'HTTP_' . $status);
            throw new DeliveryException('HTTP_' . $status, true, max(120, $retryAfter), $message);
        }
        if (isset($data['ignored'])) {
            ($this->observe)?->__invoke('POST', $endpoint, $status, $duration, 'FRAMEWORK_IGNORED');
            throw new DeliveryException('FRAMEWORK_IGNORED', false, 0, is_string($data['ignored']) ? $data['ignored'] : null);
        }
        $id = $data['invoiceId'] ?? null;
        if (($data['ok'] ?? false) !== true || !is_string($id) || !self::isUuid($id)) {
            ($this->observe)?->__invoke('POST', $endpoint, $status, $duration, 'FRAMEWORK_RESPONSE');
            throw new DeliveryException('FRAMEWORK_RESPONSE', true, 300);
        }

        ($this->observe)?->__invoke('POST', $endpoint, $status, $duration, null);
        $data['_httpStatus'] = $status;
        return $data;
    }

    /**
     * WHMCS sunucusunun internete çıktığı IP. Panel Cloudflare arkasındaysa panelin kendi
     * /cdn-cgi/trace adresinden okunur (üçüncü taraf servise istek gönderilmez).
     */
    public static function outboundIp(string $webhookUrl): ?string
    {
        $origin = self::panelOrigin($webhookUrl);
        if ($origin === '') {
            return null;
        }
        $body = '';
        $curl = curl_init($origin . '/cdn-cgi/trace');
        curl_setopt_array($curl, [
            CURLOPT_CONNECTTIMEOUT => 4, CURLOPT_TIMEOUT => 6,
            CURLOPT_PROTOCOLS => CURLPROTO_HTTPS, CURLOPT_IPRESOLVE => CURL_IPRESOLVE_V4, CURLOPT_FOLLOWLOCATION => false, CURLOPT_SSL_VERIFYPEER => true,
            CURLOPT_WRITEFUNCTION => static function ($curl, string $chunk) use (&$body): int {
                if (strlen($body) + strlen($chunk) > 8192) {
                    return 0;
                }
                $body .= $chunk;
                return strlen($chunk);
            },
        ]);
        return curl_exec($curl) === true && (int) curl_getinfo($curl, CURLINFO_HTTP_CODE) === 200
            && preg_match('/^ip=([0-9a-f:.]{3,45})\r?$/mi', $body, $match)
            && filter_var($match[1], FILTER_VALIDATE_IP) !== false ? $match[1] : null;
    }

    public static function isUuid(string $value): bool
    {
        return preg_match('/^[a-f0-9]{8}-(?:[a-f0-9]{4}-){3}[a-f0-9]{12}$/iD', $value) === 1;
    }

    private static function message(array $data): ?string
    {
        foreach (['details', 'error', 'title', 'warning'] as $key) {
            if (is_string($data[$key] ?? null) && trim($data[$key]) !== '') {
                return preg_match('/^.{0,250}/us', trim($data[$key]), $match) ? $match[0] : null;
            }
        }
        return null;
    }
}
