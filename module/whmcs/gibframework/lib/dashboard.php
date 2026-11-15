<?php

declare(strict_types=1);

<<<<<<< Updated upstream
use GibFramework\Whmcs\Queue;
use GibFramework\Whmcs\FrameworkClient;
<<<<<<< Updated upstream
=======
use GibFramework\Whmcs\DeliveryException;
>>>>>>> Stashed changes
=======
use GibFramework\Whmcs\DeliveryException;
use GibFramework\Whmcs\Queue;
use GibFramework\Whmcs\WebhookClient;
>>>>>>> Stashed changes
use WHMCS\Database\Capsule;

defined('WHMCS') || exit;

<<<<<<< Updated upstream
<<<<<<< Updated upstream
$tabs = ['all' => 'Tümü', 'pending' => 'Bekleyen', 'outbound' => 'Giden faturalar', 'draft' => 'Taslaklar', 'transmitting' => 'İmzalı / iletimde', 'issued' => 'Kesinleşen faturalar', 'errors' => 'İnceleme'];
=======
$tabs = ['all' => 'Tümü', 'pending' => 'Kuyrukta', 'outbound' => 'Aktarılan', 'draft' => 'Taslaklar', 'signature' => 'İmza bekleyen', 'transmitting' => 'İmzalı / iletimde', 'issued' => 'Kesinleşen', 'errors' => 'İnceleme'];
$apiView = ($_GET['section'] ?? null) === 'api';
>>>>>>> Stashed changes
=======
$escape = static fn ($value): string => htmlspecialchars((string) $value, ENT_QUOTES | ENT_SUBSTITUTE, 'UTF-8');
$timeZone = new DateTimeZone('Europe/Istanbul');
$date = static fn ($value): string => $value ? (new DateTimeImmutable('@' . (int) $value))->setTimezone($timeZone)->format('d.m.Y H:i') : '—';
$asset = static fn (string $name): string => '../modules/addons/gibframework/assets/' . $name . '?v=' . GIBFRAMEWORK_MODULE_VERSION;
$icon = static fn (string $name): string => '<i class="bi bi-' . $name . '" aria-hidden="true"></i>';
$base = 'addonmodules.php?module=gibframework';

$sections = ['invoices' => 'Faturalar', 'setup' => 'Kurulum', 'logs' => 'İstek geçmişi'];
$section = is_string($_GET['section'] ?? null) && isset($sections[$_GET['section']]) ? $_GET['section'] : 'invoices';
$tabs = ['all' => 'Tümü', 'pending' => 'Kuyrukta', 'outbound' => 'Aktarılan', 'draft' => 'Taslak / onay', 'signature' => 'İmza bekleyen', 'transmitting' => 'İmzalı / iletimde', 'issued' => 'Kesinleşen', 'errors' => 'İnceleme'];
>>>>>>> Stashed changes
$tab = is_string($_GET['view'] ?? null) && isset($tabs[$_GET['view']]) ? $_GET['view'] : 'all';
$search = is_string($_GET['q'] ?? null) ? trim(substr($_GET['q'], 0, 70)) : '';
$page = filter_var($_GET['page'] ?? 1, FILTER_VALIDATE_INT, ['options' => ['min_range' => 1, 'max_range' => 1000000]]) ?: 1;
$perPage = 25;
<<<<<<< Updated upstream
$state = Capsule::table(Queue::STATE)->where('id', 1)->first();
<<<<<<< Updated upstream
$records = Capsule::table(Queue::INVOICES . ' as transfer');
$counts = (clone $records)->select(['transfer.document_status'])
    ->selectRaw('CASE WHEN transfer.draft_id IS NULL THEN 0 ELSE 1 END AS submitted')
    ->selectRaw('COUNT(*) AS total')->groupBy('transfer.document_status')->groupByRaw('CASE WHEN transfer.draft_id IS NULL THEN 0 ELSE 1 END')->get();
$summary = ['pending' => 0, 'outbound' => 0, 'draft' => 0, 'issued' => 0];
foreach ($counts as $group) {
    if (!$group->submitted) {
        $summary['pending'] += (int) $group->total;
        continue;
    }
    $summary['outbound'] += (int) $group->total;
    if (in_array($group->document_status, Queue::STATUSES['Taslak'], true)) {
        $summary['draft'] += (int) $group->total;
    }
    if (in_array($group->document_status, Queue::STATUSES['Kesinleşen'], true)) {
        $summary['issued'] += (int) $group->total;
    }
}

$records->leftJoin('tblinvoices as invoice', 'invoice.id', '=', 'transfer.invoice_id');
match ($tab) {
    'pending' => $records->whereNull('transfer.draft_id'),
    'outbound' => $records->whereNotNull('transfer.draft_id'),
    'draft' => $records->whereNotNull('transfer.draft_id')->whereIn('transfer.document_status', Queue::STATUSES['Taslak']),
    'transmitting' => $records->whereNotNull('transfer.draft_id')->whereIn('transfer.document_status', Queue::STATUSES['İmzalı / iletimde']),
    'issued' => $records->whereNotNull('transfer.draft_id')->whereIn('transfer.document_status', Queue::STATUSES['Kesinleşen']),
    'errors' => $records->where(static function ($query): void {
        $query->whereNotNull('transfer.reason')->orWhereNotNull('transfer.check_reason')->orWhereIn('transfer.document_status', Queue::STATUSES['İnceleme']);
    }),
    default => null,
};
if ($search !== '') {
    $records->where(static function ($query) use ($search): void {
        $query->where('invoice.invoicenum', $search)->orWhere('transfer.document_number', $search);
        if (ctype_digit($search) && strlen($search) < 19) {
            $query->orWhere('transfer.invoice_id', (int) $search);
        }
    });
}
$pages = max(1, (int) ceil((clone $records)->count() / $perPage));
$page = min($page, $pages);
$invoices = $records->orderByDesc('transfer.invoice_id')->forPage($page, $perPage)
    ->get(['transfer.*', 'invoice.invoicenum', 'invoice.status as payment_status', 'invoice.total as whmcs_total']);
=======
$requestErrors = ($_GET['result'] ?? '') === 'error';
$summary = ['pending' => 0, 'outbound' => 0, 'signature' => 0, 'issued' => 0];
if (!$apiView) {
    $records = Capsule::table(Queue::INVOICES . ' as transfer');
    $counts = (clone $records)->select(['transfer.document_status'])
        ->selectRaw('CASE WHEN transfer.draft_id IS NULL THEN 0 ELSE 1 END AS submitted')
        ->selectRaw('COUNT(*) AS total')->groupBy('transfer.document_status')->groupByRaw('CASE WHEN transfer.draft_id IS NULL THEN 0 ELSE 1 END')->get();
    foreach ($counts as $group) {
        if (!$group->submitted) {
            $summary['pending'] += (int) $group->total;
            continue;
        }
        $summary['outbound'] += (int) $group->total;
        if (in_array($group->document_status, ['Approved', 'Signing'], true)) {
            $summary['signature'] += (int) $group->total;
        }
        if (in_array($group->document_status, Queue::STATUSES['Kesinleşen'], true)) {
            $summary['issued'] += (int) $group->total;
        }
    }

    $records->leftJoin('tblinvoices as invoice', 'invoice.id', '=', 'transfer.invoice_id');
    match ($tab) {
        'pending' => $records->whereNull('transfer.draft_id'),
        'outbound' => $records->whereNotNull('transfer.draft_id'),
        'draft' => $records->whereNotNull('transfer.draft_id')->whereIn('transfer.document_status', Queue::STATUSES['Taslak']),
        'signature' => $records->whereIn('transfer.document_status', ['Approved', 'Signing']),
        'transmitting' => $records->whereNotNull('transfer.draft_id')->whereIn('transfer.document_status', Queue::STATUSES['İmzalı / iletimde']),
        'issued' => $records->whereNotNull('transfer.draft_id')->whereIn('transfer.document_status', Queue::STATUSES['Kesinleşen']),
        'errors' => $records->where(static function ($query): void {
            $query->whereNotNull('transfer.reason')->orWhereNotNull('transfer.check_reason')->orWhereIn('transfer.document_status', Queue::STATUSES['İnceleme']);
        }),
        default => null,
    };
    if ($search !== '') {
        $records->where(static function ($query) use ($search): void {
            $query->where('invoice.invoicenum', $search)->orWhere('transfer.document_number', $search);
            if (ctype_digit($search) && strlen($search) < 19) {
                $query->orWhere('transfer.invoice_id', (int) $search);
            }
        });
    }
    $pages = max(1, (int) ceil((clone $records)->count() / $perPage));
    $page = min($page, $pages);
    $invoices = $records->orderByDesc('transfer.invoice_id')->forPage($page, $perPage)
        ->get(['transfer.*', 'invoice.invoicenum', 'invoice.status as payment_status', 'invoice.total as whmcs_total']);
}
>>>>>>> Stashed changes
=======
$url = static fn (string $view, int $number = 1, string $query = ''): string => 'addonmodules.php?' . http_build_query(['module' => 'gibframework', 'view' => $view, 'page' => $number, 'q' => $query]);
$sectionUrl = static fn (string $name): string => $name === 'invoices' ? $base : $base . '&section=' . $name;
>>>>>>> Stashed changes

$labels = [
    'Draft' => 'Taslak', 'Validating' => 'Doğrulanıyor', 'Validated' => 'Doğrulandı', 'AwaitingApproval' => 'Onay bekliyor',
    'Approved' => 'Onaylandı', 'Signing' => 'İmza bekliyor', 'Signed' => 'İmzalandı', 'Queued' => 'İletim kuyruğunda',
    'Transmitting' => 'İletiliyor', 'Sent' => 'Gönderildi', 'Acknowledged' => 'Alındı', 'Delivered' => 'Teslim edildi',
    'Accepted' => 'Kabul edildi', 'InDoubt' => 'Sonuç belirsiz', 'Rejected' => 'Reddedildi', 'Cancelled' => 'İptal edildi',
    'Objected' => 'İtiraz edildi', 'Failed' => 'Başarısız',
];
$paymentLabels = ['Paid' => 'Ödendi', 'Unpaid' => 'Ödenmedi', 'Refunded' => 'İade edildi', 'Cancelled' => 'İptal edildi', 'Payment Pending' => 'Ödeme bekliyor'];
<<<<<<< Updated upstream
$escape = static fn ($value): string => htmlspecialchars((string) $value, ENT_QUOTES | ENT_SUBSTITUTE, 'UTF-8');
<<<<<<< Updated upstream
$date = static fn ($value): string => $value ? gmdate('d.m.Y H:i', (int) $value) : '—';
=======
$timeZone = new DateTimeZone('Europe/Istanbul');
$date = static fn ($value): string => $value ? (new DateTimeImmutable('@' . (int) $value))->setTimezone($timeZone)->format('d.m.Y H:i') : '—';
>>>>>>> Stashed changes
$url = static fn (string $view, int $number = 1, string $query = ''): string => 'addonmodules.php?' . http_build_query(['module' => 'gibframework', 'view' => $view, 'page' => $number, 'q' => $query]);
$enabled = ($vars['enabled'] ?? '') === 'on';
$panelUrl = rtrim(trim((string) ($vars['frameworkUrl'] ?? '')), '/');
$panelUrl = filter_var($panelUrl, FILTER_VALIDATE_URL) && str_starts_with($panelUrl, 'https://')
    && !array_intersect(['user', 'pass', 'query', 'fragment'], array_keys(parse_url($panelUrl))) ? $panelUrl : '';
<<<<<<< Updated upstream
?>
<link rel="stylesheet" href="../modules/addons/gibframework/assets/bootstrap.min.css">
<link rel="stylesheet" href="../modules/addons/gibframework/assets/dashboard.css">
<div class="gibframework-dashboard" data-bs-theme="light" lang="tr">
    <header class="gib-heading d-flex flex-wrap justify-content-between align-items-center gap-3">
        <h1>Fatura aktarımı</h1>
        <span class="badge rounded-pill <?= $enabled ? 'text-bg-success' : 'text-bg-secondary' ?>">Otomatik aktarım <?= $enabled ? 'açık' : 'kapalı' ?></span>
    </header>

    <?php if ($state?->reason !== null): ?>
        <div class="alert alert-warning mb-3" role="status">
            <strong>Aktarım bekliyor</strong> · <code><?= $escape($state->reason) ?></code> · Sonraki deneme <?= $escape($date($state->next_at)) ?> UTC
=======
$panelApp = $panelUrl === '' ? '' : $panelUrl . '/app/#';
$apiUrl = 'addonmodules.php?module=gibframework&section=api';
$asset = static fn (string $name): string => '../modules/addons/gibframework/assets/' . $name . '?v=' . GIBFRAMEWORK_MODULE_VERSION;
$environment = ($vars['portalEnvironment'] ?? '') ?: 'Production';
$token = '';
$connection = null;
if ($apiView) {
    $_SESSION['gibframework_diagnostics'] ??= bin2hex(random_bytes(32));
    $token = $_SESSION['gibframework_diagnostics'];
    if (($_SERVER['REQUEST_METHOD'] ?? '') === 'POST' && ($_POST['gib_action'] ?? '') === 'probe') {
        if (!is_string($_POST['gib_token'] ?? null) || !hash_equals($token, $_POST['gib_token'])) {
            $connection = ['error' => 'INVALID_REQUEST'];
        } else {
            $connection = [];
            try {
                $probe = new FrameworkClient($vars['frameworkUrl'] ?? '', $environment,
                    static function ($method, $path, $status, $duration, $reason): void {
                        Queue::recordRequest($method, $path, $status, $duration, $reason);
                    });
                $tenant = $probe->authorizedTenant(true);
                if ($state?->target_id !== null && $state->target_id !== $tenant) {
                    throw new DeliveryException('TARGET_TENANT_CHANGED');
                }
                $connection['api'] = true;
                $probe->verifyOperator($tenant);
                if (!$probe->hasPolicy('InvoiceApprove')) {
                    throw new DeliveryException('OPERATOR_APPROVAL_PERMISSION');
                }
                $connection['operator'] = true;
                $connection['integrator'] = $probe->integratorAvailable() && $probe->hasPolicy('InvoiceSign');
                $probe->verifyPortal($tenant);
                $connection['portal'] = true;
                if ($environment === 'Test' && !$probe->hasPolicy('InvoiceSign')) {
                    throw new DeliveryException('OPERATOR_SIGN_PERMISSION');
                }
            } catch (DeliveryException $error) {
                $connection['error'] = $error->reason;
            }
        }
    }
=======
$reasons = [
    'CONFIG_WEBHOOK_URL_MISSING' => 'Modül ayarlarında "Gelen adres (webhook URL)" boş.',
    'CONFIG_WEBHOOK_URL' => 'Gelen adres https://…/api/v1/hooks/{entegrasyon-id} biçiminde olmalı.',
    'CONFIG_SIGNING_SECRET_MISSING' => 'Modül ayarlarında "İmza anahtarı" boş.',
    'CONFIG_SIGNING_SECRET' => 'İmza anahtarı gfs_ ile başlayan 52 karakterlik değer olmalı.',
    'SIGNATURE_REJECTED' => 'Panel imzayı reddetti: imza anahtarı paneldekiyle aynı değil (anahtar yenilendiyse modüle de girin).',
    'PANEL_IP_DENIED' => 'Panelin site geneli IP kuralları isteği durdurdu. Bağlantıyı test edin ve panelin kontrol ettiği adresi Ayarlar › Güvenlik › IP erişimi › Site genelinde geçerli kurallar bölümündeki izin ve engel listeleriyle karşılaştırın.',
    'PANEL_TENANT_IP_DENIED' => 'Panelin firma IP listesi isteği engelledi. Ayarlar › Güvenlik › IP erişimi listesine aşağıdaki IP\'yi ekleyin.',
    'CLOUDFLARE_BLOCKED' => 'Panelin önündeki Cloudflare isteği engelledi (Bot Fight Mode / WAF). Cloudflare\'de /api/v1/hooks/ yolu veya WHMCS sunucusunun IP adresi için atlama (Skip) kuralı ekleyin.',
    'HTTP_403' => 'Panel erişimi reddetti (403). Panelin IP erişim listesini ve varsa Cloudflare / güvenlik duvarı kurallarını kontrol edin.',
    'INTEGRATION_DISABLED' => 'Panelde WHMCS entegrasyonu kapalı ya da silinmiş. Entegrasyonu "Etkin" yapıp kaydedin.',
    'FRAMEWORK_CONNECTION' => 'Panele bağlanılamadı (DNS, SSL veya güvenlik duvarı).',
    'FRAMEWORK_RESPONSE' => 'Panelden beklenmeyen yanıt geldi.',
    'FRAMEWORK_REJECTED' => 'Panel faturayı kabul etmedi; ayrıntıya bakıp faturayı / müşteri bilgisini düzeltin ve yeniden deneyin.',
    'FRAMEWORK_IGNORED' => 'Panel isteği yok saydı.',
    'RATE_LIMITED' => 'Panel istek sınırına ulaşıldı; kısa süre sonra devam edilecek.',
    'CALLBACK_SIGNATURE' => 'Panelden gelen bildirimin imzası doğrulanamadı (anahtar farklı veya saat farkı 5 dakikadan fazla).',
    'INVOICE_EXCHANGE_RATE' => 'Döviz faturası: modül ayarlarına TL kuru girin.',
    'INVOICE_CURRENCY' => 'Fatura para birimi okunamadı.',
    'INVOICE_SECOND_TAX' => 'İkinci vergi (tax2) içeren fatura desteklenmiyor; panelde elle düzenleyin.',
    'INVOICE_VAT_RATE' => 'KDV oranı desteklenmiyor (0, 1, 8, 10, 18, 20).',
    'INVOICE_TOTAL_MISMATCH' => 'Satırlar ile fatura toplamı uyuşmuyor.',
    'INVOICE_DISCOUNT_ALLOCATION' => 'İndirim satırı hizmetlere dağıtılamadı.',
    'INVOICE_TAX_RECONCILIATION' => 'Hesaplanan KDV ile WHMCS KDV tutarı uyuşmuyor.',
    'INVOICE_TAX_DATA' => 'Faturadaki vergi bilgisi geçersiz.',
    'INVOICE_AMOUNT_INVALID' => 'Faturada geçersiz tutar var.',
    'CUSTOMER_TAX_ID' => 'Müşterinin TCKN / VKN değeri geçersiz.',
    'CUSTOMER_BILLING_DETAILS' => 'Müşteri adı / firma adı boş.',
    'WHMCS_GET_INVOICE' => 'WHMCS faturası okunamadı.',
    'WHMCS_GET_CUSTOMER' => 'WHMCS müşteri bilgisi okunamadı.',
    'WHMCS_INVOICE_RESPONSE' => 'WHMCS fatura yanıtı beklenmeyen biçimde.',
    'PAYLOAD_JSON' => 'Fatura verisi JSON\'a çevrilemedi.',
];
$reasonText = static fn (?string $code): string => $code === null ? '' : ($reasons[$code] ?? (str_starts_with($code, 'HTTP_') ? 'Panel ' . substr($code, 5) . ' hata kodu döndürdü.' : $code));

$settings = $vars;
$enabled = ($settings['enabled'] ?? '') === 'on';
$webhookUrl = trim((string) ($settings['webhookUrl'] ?? ''));
$secretSet = WebhookClient::secret((string) ($settings['signingSecret'] ?? '')) !== '';
$panelOrigin = WebhookClient::panelOrigin($webhookUrl);
$panelApp = $panelOrigin === '' ? '' : $panelOrigin . '/app/#';
$systemUrl = rtrim((string) \WHMCS\Config\Setting::getValue('SystemURL'), '/');
$callbackUrl = $systemUrl . '/modules/addons/gibframework/callback.php';
$hookFileExists = is_file((defined('ROOTDIR') ? ROOTDIR : dirname(__DIR__, 4)) . '/includes/hooks/gibframework.php');
$configured = $webhookUrl !== '' && WebhookClient::isWebhookUrl($webhookUrl) && $secretSet;

$_SESSION['gibframework_token'] ??= bin2hex(random_bytes(32));
$token = $_SESSION['gibframework_token'];
$flash = null;
if (($_SERVER['REQUEST_METHOD'] ?? '') === 'POST' && is_string($_POST['gib_action'] ?? null)) {
    if (!is_string($_POST['gib_token'] ?? null) || !hash_equals($token, $_POST['gib_token'])) {
        $flash = ['warning', 'İstek doğrulanamadı. Sayfayı yenileyip tekrar deneyin.', null];
    } else {
        $target = (int) ($_POST['invoice_id'] ?? 0);
        try {
            switch ($_POST['gib_action']) {
                case 'probe':
                    $probe = new WebhookClient($webhookUrl, (string) ($settings['signingSecret'] ?? ''),
                        static function ($method, $path, $status, $duration, $reason): void {
                            Queue::recordRequest($method, $path . ' (test)', $status, $duration, $reason === 'FRAMEWORK_IGNORED' ? null : $reason);
                        });
                    $probe->ping();
                    Queue::resumeQueue();
                    $flash = ['success', 'Bağlantı başarılı: gelen adres erişilebilir ve imza anahtarı panelle eşleşiyor. Fatura oluşturulmadı.', null];
                    break;
                case 'run':
                    if (!$enabled) {
                        $flash = ['warning', 'Önce modül ayarlarından "Otomatik aktarım"ı açın.', null];
                        break;
                    }
                    Queue::run($settings, true);
                    $flash = ['success', 'Kuyruk çalıştırıldı. Sonuçlar aşağıdaki tabloda.', null];
                    break;
                case 'enqueue':
                    $paid = $target > 0 && Capsule::table('tblinvoices')->where('id', $target)->where('status', 'Paid')->where('total', '>', 0)->exists();
                    if (!$paid) {
                        $flash = ['warning', '#' . $target . ' numaralı ödenmiş (tutarı sıfırdan büyük) WHMCS faturası bulunamadı.', null];
                        break;
                    }
                    Queue::enqueue($target);
                    Queue::retry($target);
                    $flash = ['success', 'Fatura #' . $target . ' kuyruğa eklendi. "Şimdi gönder" ile hemen aktarabilirsiniz.', null];
                    break;
                case 'retry':
                    $flash = Queue::retry($target) ? ['success', 'Fatura #' . $target . ' yeniden denenecek.', null] : ['warning', 'Fatura bulunamadı veya zaten aktarılmış.', null];
                    break;
                case 'resend':
                    $flash = Queue::resend($target) ? ['success', 'Fatura #' . $target . ' eşleşmesi sıfırlandı; bir sonraki çalışmada yeniden gönderilecek.', null] : ['warning', 'Fatura bulunamadı.', null];
                    break;
                case 'resume':
                    Queue::resumeQueue();
                    $flash = ['success', 'Kuyruk beklemesi kaldırıldı. "Şimdi gönder" ile hemen deneyebilirsiniz.', null];
                    break;
            }
        } catch (DeliveryException $error) {
            $detail = $error->detail;
            if ($error->observedIp === null && in_array($error->reason, ['PANEL_IP_DENIED', 'PANEL_TENANT_IP_DENIED', 'CLOUDFLARE_BLOCKED', 'HTTP_403'], true)
                && ($ip = WebhookClient::outboundIp($webhookUrl)) !== null) {
                $detail = trim(($detail ?? '') . ' · Cloudflare’in gördüğü WHMCS çıkış IP’si: ' . $ip, ' ·');
            }
            $flash = ['warning', $reasonText($error->reason), trim(($detail ?? '') . ' · ' . $error->reason, ' ·')];
        } catch (Throwable $error) {
            $flash = ['warning', $error instanceof RuntimeException ? $error->getMessage() : 'İşlem tamamlanamadı.', null];
        }
    }
}

$state = Capsule::table(Queue::STATE)->where('id', 1)->first();
$summary = ['pending' => 0, 'outbound' => 0, 'signature' => 0, 'issued' => 0, 'errors' => 0];
$summaryIcons = ['pending' => 'hourglass-split', 'outbound' => 'send-check', 'signature' => 'pen', 'issued' => 'patch-check', 'errors' => 'exclamation-triangle'];
if ($section === 'invoices') {
    $records = Capsule::table(Queue::INVOICES . ' as transfer');
    $counts = (clone $records)->select(['transfer.document_status'])
        ->selectRaw('CASE WHEN transfer.draft_id IS NULL THEN 0 ELSE 1 END AS submitted')
        ->selectRaw('SUM(CASE WHEN transfer.reason IS NULL THEN 0 ELSE 1 END) AS failing')
        ->selectRaw('COUNT(*) AS total')->groupBy('transfer.document_status')->groupByRaw('CASE WHEN transfer.draft_id IS NULL THEN 0 ELSE 1 END')->get();
    foreach ($counts as $group) {
        $summary['errors'] += (int) $group->failing;
        if (!$group->submitted) {
            $summary['pending'] += (int) $group->total;
            continue;
        }
        $summary['outbound'] += (int) $group->total;
        if (in_array($group->document_status, Queue::STATUSES['İmza bekleyen'], true)) {
            $summary['signature'] += (int) $group->total;
        }
        if (in_array($group->document_status, Queue::STATUSES['Kesinleşen'], true)) {
            $summary['issued'] += (int) $group->total;
        }
        if (in_array($group->document_status, Queue::STATUSES['İnceleme'], true)) {
            $summary['errors'] += (int) $group->total;
        }
    }

    $records->leftJoin('tblinvoices as invoice', 'invoice.id', '=', 'transfer.invoice_id');
    match ($tab) {
        'pending' => $records->whereNull('transfer.draft_id'),
        'outbound' => $records->whereNotNull('transfer.draft_id'),
        'draft' => $records->whereNotNull('transfer.draft_id')->whereIn('transfer.document_status', Queue::STATUSES['Taslak']),
        'signature' => $records->whereIn('transfer.document_status', Queue::STATUSES['İmza bekleyen']),
        'transmitting' => $records->whereNotNull('transfer.draft_id')->whereIn('transfer.document_status', Queue::STATUSES['İmzalı / iletimde']),
        'issued' => $records->whereNotNull('transfer.draft_id')->whereIn('transfer.document_status', Queue::STATUSES['Kesinleşen']),
        'errors' => $records->where(static function ($query): void {
            $query->whereNotNull('transfer.reason')->orWhereIn('transfer.document_status', Queue::STATUSES['İnceleme']);
        }),
        default => null,
    };
    if ($search !== '') {
        $records->where(static function ($query) use ($search): void {
            $query->where('invoice.invoicenum', $search)->orWhere('transfer.document_number', $search)->orWhere('transfer.draft_number', $search);
            if (ctype_digit($search) && strlen($search) < 19) {
                $query->orWhere('transfer.invoice_id', (int) $search);
            }
        });
    }
    $pages = max(1, (int) ceil((clone $records)->count() / $perPage));
    $page = min($page, $pages);
    $invoices = $records->orderByDesc('transfer.invoice_id')->forPage($page, $perPage)
        ->get(['transfer.*', 'invoice.invoicenum', 'invoice.status as payment_status', 'invoice.total as whmcs_total']);
} elseif ($section === 'logs') {
    $requestErrors = ($_GET['result'] ?? '') === 'error';
>>>>>>> Stashed changes
    $requests = Capsule::table(Queue::REQUESTS);
    if ($requestErrors) {
        $requests->whereNotNull('reason');
    }
    $requestPages = max(1, (int) ceil((clone $requests)->count() / $perPage));
<<<<<<< Updated upstream
    $requestPage = min(filter_var($_GET['page'] ?? 1, FILTER_VALIDATE_INT, ['options' => ['min_range' => 1]]) ?: 1, $requestPages);
    $logs = $requests->orderByDesc('id')->forPage($requestPage, $perPage)->get();
}
?>
<link rel="stylesheet" href="<?= $escape($asset('bootstrap.min.css')) ?>">
<link rel="stylesheet" href="<?= $escape($asset('fonts.css')) ?>">
<link rel="stylesheet" href="<?= $escape($asset('dashboard.css')) ?>">
<script defer src="<?= $escape($asset('dashboard.js')) ?>"></script>
<div class="gibframework-dashboard" lang="tr">
    <div class="gib-navbar">
        <div class="gib-brand">
            <img src="<?= $escape($asset('mark.svg')) ?>" width="43" height="43" alt="">
            <span><strong>GIB</strong> Framework</span>
        </div>
        <nav class="gib-sections" aria-label="Modül bölümleri">
            <a href="<?= $escape($url('all')) ?>"<?= !$apiView ? ' aria-current="page"' : '' ?>>Faturalar</a>
            <a href="<?= $escape($apiUrl) ?>"<?= $apiView ? ' aria-current="page"' : '' ?>>API ve istekler</a>
        </nav>
        <?php if ($panelApp !== ''): ?><a class="btn btn-outline-secondary gib-panel-link" href="<?= $escape($panelApp . '/invoices') ?>">GIB panelini aç</a><?php endif; ?>
    </div>

    <main class="gib-content">
    <header class="gib-heading mb-4">
        <div class="d-flex flex-wrap justify-content-between align-items-center gap-3">
            <div><h1><?= $apiView ? 'API ve istekler' : 'Fatura aktarımı' ?></h1>
                <p><?= $apiView ? 'Bağlantı durumu ve istek geçmişi' : 'Son cron çalışması · ' . $escape(isset($state->last_cron_at) ? $date($state->last_cron_at) : 'Kayıt yok') ?></p></div>
            <span class="gib-live <?= $enabled ? 'is-active' : '' ?>">Otomatik aktarım <?= $enabled ? 'açık' : 'kapalı' ?></span>
        </div>
    </header>

    <?php if ($apiView): ?>
        <section class="card gib-invoices gib-diagnostics mb-4" aria-labelledby="gib-connection-heading">
            <div class="d-flex flex-wrap align-items-center justify-content-between gap-3">
                <div><h2 id="gib-connection-heading">Bağlantı kontrolü</h2>
                    <p>GİB Portal · <?= $environment === 'Test' ? 'Test' : 'Canlı' ?></p></div>
                <form action="<?= $escape($apiUrl) ?>" method="post" data-gib-probe>
                    <input type="hidden" name="gib_action" value="probe">
                    <input type="hidden" name="gib_token" value="<?= $escape($token) ?>">
                    <button class="btn btn-primary" type="submit" data-busy-label="Kontrol ediliyor…">Bağlantıyı test et</button>
                </form>
            </div>
            <?php if ($connection !== null): ?>
                <div class="alert <?= isset($connection['error']) ? 'alert-warning' : 'alert-success' ?> mb-0 mt-3" role="status">
                    API <?= empty($connection['api']) ? 'doğrulanamadı' : 'hazır' ?> · Onay yetkisi <?= empty($connection['operator']) ? 'doğrulanamadı' : 'hazır' ?> · Portal <?= empty($connection['portal']) ? 'doğrulanamadı' : 'bağlı' ?>
                    <?php if (isset($connection['integrator'])): ?> · e-Fatura imzası <?= $connection['integrator'] ? 'hazır' : 'kullanılamıyor' ?><?php endif; ?>
                    <?php if (isset($connection['error'])): ?> · <code><?= $escape($connection['error']) ?></code><?php endif; ?>
                </div>
            <?php endif; ?>
        </section>
        <section class="card gib-invoices" aria-label="API istek geçmişi">
            <div class="gib-toolbar d-flex flex-wrap justify-content-between align-items-center gap-3">
                <h2>İstek geçmişi</h2>
                <div class="btn-group" aria-label="İstek filtresi">
                    <a class="btn <?= $requestErrors ? 'btn-outline-secondary' : 'btn-primary' ?>" href="<?= $escape($apiUrl) ?>"<?= !$requestErrors ? ' aria-current="page"' : '' ?>>Tümü</a>
                    <a class="btn <?= $requestErrors ? 'btn-primary' : 'btn-outline-secondary' ?>" href="<?= $escape($apiUrl . '&result=error') ?>"<?= $requestErrors ? ' aria-current="page"' : '' ?>>Hatalar</a>
                </div>
            </div>
            <div class="table-responsive" tabindex="0" aria-label="API istekleri tablosu">
                <table class="table table-hover align-middle mb-0 gib-request-table">
                    <thead><tr><th scope="col">İstek</th><th scope="col">Sonuç</th><th scope="col">Süre</th><th scope="col">Fatura</th><th scope="col">Zaman (TSİ)</th></tr></thead>
                    <tbody>
                    <?php foreach ($logs as $log): ?>
                        <tr><th scope="row"><span class="gib-method"><?= $escape($log->method) ?></span> <code><?= $escape($log->endpoint) ?></code></th>
                            <td><span class="badge <?= $log->reason === null ? 'text-bg-success' : 'text-bg-warning' ?>"><?= $escape($log->http_status ?: 'Bağlantı hatası') ?></span>
                                <?php if ($log->reason !== null): ?><small class="d-block mt-1"><?= $escape($log->reason) ?></small><?php endif; ?></td>
                            <td><?= (int) $log->duration_ms ?> ms</td><td><?= $log->invoice_id ? '#' . (int) $log->invoice_id : '—' ?></td>
                            <td><?= $escape($date($log->requested_at)) ?></td></tr>
                    <?php endforeach; ?>
                    <?php if ($logs->isEmpty()): ?><tr><td colspan="5" class="text-center py-5">Bu görünümde istek bulunamadı.</td></tr><?php endif; ?>
                    </tbody>
                </table>
            </div>
            <?php if ($requestPages > 1): ?>
                <nav class="gib-pagination d-flex flex-wrap justify-content-between align-items-center gap-2" aria-label="İstek sayfaları">
                    <span>Sayfa <?= $requestPage ?> / <?= $requestPages ?></span>
                    <div class="d-flex gap-2">
                        <?php $logPage = $apiUrl . ($requestErrors ? '&result=error' : '') . '&page='; ?>
                        <?php if ($requestPage > 1): ?><a class="btn btn-outline-secondary" href="<?= $escape($logPage . ($requestPage - 1)) ?>">Önceki</a><?php endif; ?>
                        <?php if ($requestPage < $requestPages): ?><a class="btn btn-outline-secondary" href="<?= $escape($logPage . ($requestPage + 1)) ?>">Sonraki</a><?php endif; ?>
                    </div>
                </nav>
            <?php endif; ?>
        </section>
    <?php else: ?>
    <?php if ($state?->reason !== null): ?>
        <div class="alert alert-warning mb-3" role="status">
            <strong>Aktarım bekliyor</strong> · <code><?= $escape($state->reason) ?></code> · Sonraki deneme <?= $escape($date($state->next_at)) ?>
>>>>>>> Stashed changes
        </div>
    <?php endif; ?>

    <div class="row row-cols-2 row-cols-xl-4 g-3 mb-4">
        <?php foreach ($summary as $key => $total): ?>
            <div class="col">
                <a class="card gib-metric h-100 text-decoration-none" href="<?= $escape($url($key)) ?>">
                    <span><?= $escape($tabs[$key]) ?></span>
                    <strong><?= $escape(number_format($total, 0, ',', '.')) ?></strong>
                </a>
            </div>
        <?php endforeach; ?>
    </div>

<<<<<<< Updated upstream
    <section class="card gib-invoices" aria-label="Fatura listesi">
        <div class="gib-toolbar">
            <nav class="nav nav-pills gap-2" aria-label="Fatura durumları">
                <?php foreach ($tabs as $key => $label): ?>
                    <a class="nav-link<?= $key === $tab ? ' active' : '' ?>" href="<?= $escape($url($key, 1, $search)) ?>"<?= $key === $tab ? ' aria-current="page"' : '' ?>><?= $escape($label) ?></a>
=======
    <section class="card gib-invoices mb-4" aria-labelledby="gib-filters-heading">
        <div class="gib-toolbar">
            <h2 id="gib-filters-heading" class="mb-3">Arama ve filtreler</h2>
            <nav class="nav nav-pills gap-2" aria-label="Fatura durumları">
                <?php foreach ($tabs as $key => $label): ?>
                    <a class="nav-link<?= $key === $tab ? ' active' : '' ?>" href="<?= $escape($url($key, 1, $search)) ?>"<?= $key === $tab ? ' aria-current="true"' : '' ?>><?= $escape($label) ?></a>
>>>>>>> Stashed changes
                <?php endforeach; ?>
            </nav>
            <form class="d-flex flex-wrap gap-2 mt-3" action="addonmodules.php" method="get" role="search">
                <input type="hidden" name="module" value="gibframework">
                <input type="hidden" name="view" value="<?= $escape($tab) ?>">
                <label class="visually-hidden" for="gib-invoice-search">WHMCS fatura ID, fatura no veya belge no</label>
                <input id="gib-invoice-search" class="form-control" name="q" maxlength="70" value="<?= $escape($search) ?>" placeholder="Fatura ID veya belge no">
                <button type="submit" class="btn btn-primary">Ara</button>
                <?php if ($search !== ''): ?><a class="btn btn-outline-secondary" href="<?= $escape($url($tab)) ?>">Temizle</a><?php endif; ?>
            </form>
        </div>
<<<<<<< Updated upstream

=======
    </section>

    <section class="card gib-invoices" aria-labelledby="gib-invoices-heading">
        <div class="gib-toolbar"><h2 id="gib-invoices-heading">Belge kayıtları</h2></div>
>>>>>>> Stashed changes
        <div class="table-responsive" tabindex="0" aria-label="Faturalar tablosu">
            <table class="table table-hover align-middle mb-0">
                <thead><tr>
                    <th scope="col">WHMCS faturası</th><th scope="col">Ödeme</th><th scope="col">GIB Framework</th>
<<<<<<< Updated upstream
                    <th scope="col">Belge no</th><th scope="col">Gönderim (UTC)</th><th scope="col">Son kontrol (UTC)</th>
=======
                    <th scope="col">Belge no</th><th scope="col">Aktarım (TSİ)</th><th scope="col">Son kontrol (TSİ)</th>
>>>>>>> Stashed changes
                </tr></thead>
                <tbody>
                <?php foreach ($invoices as $invoice):
                    $status = $invoice->document_status;
                    $reason = $invoice->reason ?? $invoice->check_reason;
                    $label = $invoice->draft_id === null ? ($invoice->reason === null ? 'Kuyrukta' : 'Yeniden denenecek') : ($labels[$status] ?? 'Durum bekleniyor');
                    $tone = match (true) {
                        $reason !== null, in_array($status, Queue::STATUSES['İnceleme'], true) => 'warning',
                        in_array($status, Queue::STATUSES['Kesinleşen'], true) => 'success',
                        $invoice->draft_id !== null => 'info',
                        default => 'secondary',
                    };
                ?>
                    <tr>
                        <th scope="row"><a href="invoices.php?action=edit&amp;id=<?= (int) $invoice->invoice_id ?>"><?= $escape($invoice->invoicenum ?: '#' . $invoice->invoice_id) ?></a>
                            <?php if ($invoice->invoicenum): ?><small class="d-block text-body-secondary">#<?= (int) $invoice->invoice_id ?></small><?php endif; ?>
                        </th>
                        <td><?= $escape($paymentLabels[$invoice->payment_status] ?? $invoice->payment_status ?? '—') ?>
                            <?php if (is_numeric($invoice->whmcs_total)): ?><small class="d-block text-body-secondary"><?= $escape(number_format((float) $invoice->whmcs_total, 2, ',', '.')) ?></small><?php endif; ?>
                        </td>
                        <td><span class="badge rounded-pill text-bg-<?= $tone ?>"><?= $escape($label) ?></span>
                            <?php if ($reason !== null): ?><small class="d-block mt-1"><code><?= $escape($reason) ?></code></small><?php endif; ?>
<<<<<<< Updated upstream
                            <?php if ($invoice->draft_id === null && $invoice->reason !== null): ?><small class="d-block text-body-secondary">Sonraki deneme <?= $escape($date($invoice->next_at)) ?> UTC</small><?php endif; ?>
                        </td>
                        <td>
                            <?php if ($panelUrl !== '' && FrameworkClient::isUuid((string) $invoice->draft_id)): ?>
                                <a href="<?= $escape($panelUrl . '/#/invoices/' . $invoice->draft_id) ?>"><?= $escape($invoice->document_number ?? 'Belgeyi aç') ?></a>
=======
                            <?php if ($reason !== null): ?><small class="d-block text-body-secondary">Sonraki kontrol <?= $escape($date($invoice->draft_id === null ? $invoice->next_at : $invoice->check_due_at)) ?></small><?php endif; ?>
                        </td>
                        <td>
                            <?php if ($panelApp !== '' && FrameworkClient::isUuid((string) $invoice->draft_id)): ?>
                                <a href="<?= $escape($panelApp . '/invoices/' . $invoice->draft_id) ?>"><?= $escape($invoice->document_number ?? 'Belgeyi aç') ?></a>
>>>>>>> Stashed changes
                            <?php else: ?><?= $escape($invoice->document_number ?? '—') ?><?php endif; ?>
                        </td>
                        <td class="text-nowrap"><?= $escape($date($invoice->submitted_at)) ?></td>
                        <td class="text-nowrap"><?= $escape($date($invoice->checked_at)) ?></td>
                    </tr>
                <?php endforeach; ?>
                <?php if ($invoices->isEmpty()): ?><tr><td class="text-center text-body-secondary py-5" colspan="6">Bu görünümde fatura bulunamadı.</td></tr><?php endif; ?>
                </tbody>
            </table>
        </div>

        <?php if ($pages > 1): ?>
            <nav class="gib-pagination d-flex flex-wrap align-items-center justify-content-between gap-2" aria-label="Fatura sayfaları">
                <span>Sayfa <?= $page ?> / <?= $pages ?></span>
                <div class="d-flex gap-2">
                    <?php if ($page > 1): ?><a class="btn btn-sm btn-outline-secondary" href="<?= $escape($url($tab, $page - 1, $search)) ?>">Önceki</a><?php endif; ?>
                    <?php if ($page < $pages): ?><a class="btn btn-sm btn-outline-secondary" href="<?= $escape($url($tab, $page + 1, $search)) ?>">Sonraki</a><?php endif; ?>
                </div>
            </nav>
        <?php endif; ?>
    </section>
<<<<<<< Updated upstream
=======
    <?php endif; ?>
    </main>
>>>>>>> Stashed changes
=======
    $requestPage = min($page, $requestPages);
    $logs = $requests->orderByDesc('id')->forPage($requestPage, $perPage)->get();
}

$hidden = static fn (string $action, int $invoiceId = 0): string => '<input type="hidden" name="gib_action" value="' . $escape($action) . '">'
    . '<input type="hidden" name="gib_token" value="' . $escape($token) . '">'
    . ($invoiceId > 0 ? '<input type="hidden" name="invoice_id" value="' . $invoiceId . '">' : '');
$form = static function (string $action, string $label, string $class = 'gf-btn gf-btn-primary', int $invoiceId = 0, string $busy = 'İşleniyor…', string $bi = '') use ($escape, $hidden, $icon): string {
    return '<form method="post" class="gf-form-inline" data-gib-busy>' . $hidden($action, $invoiceId)
        . '<button class="' . $escape($class) . '" type="submit" data-busy-label="' . $escape($busy) . '">'
        . ($bi !== '' ? $icon($bi) : '') . '<span>' . $escape($label) . '</span></button></form>';
};
$badge = static fn (string $tone, string $text, string $bi = ''): string => '<span class="gf-badge gf-badge-' . $tone . '">' . ($bi !== '' ? '<i class="bi bi-' . $bi . '" aria-hidden="true"></i>' : '') . htmlspecialchars($text, ENT_QUOTES, 'UTF-8') . '</span>';
$titles = ['invoices' => 'Fatura aktarımı', 'setup' => 'Kurulum ve bağlantı', 'logs' => 'İstek geçmişi'];
$subtitles = [
    'invoices' => 'Ödenen WHMCS faturaları GIB Framework paneline imzalı webhook ile aktarılır.',
    'setup' => 'Paneldeki WHMCS entegrasyonunu bu modüle bağlayın.',
    'logs' => 'Panele gönderilen ve panelden gelen son 1000 istek.',
];
?>
<link rel="stylesheet" href="<?= $escape($asset('fonts.css')) ?>">
<link rel="stylesheet" href="<?= $escape($asset('bootstrap-icons.min.css')) ?>">
<link rel="stylesheet" href="<?= $escape($asset('dashboard.css')) ?>">
<script defer src="<?= $escape($asset('dashboard.js')) ?>"></script>
<div class="gibframework-app" id="gibframework-app" lang="tr">
    <header class="gf-navbar">
        <a class="gf-brand" href="<?= $escape($base) ?>">
            <img src="<?= $escape($asset('mark.svg')) ?>" width="43" height="43" alt="">
            <span class="gf-wordmark"><strong>GIB</strong> Framework</span>
        </a>
        <nav class="gf-primary-nav" aria-label="Modül bölümleri">
            <a href="<?= $escape($sectionUrl('invoices')) ?>"<?= $section === 'invoices' ? ' class="active" aria-current="page"' : '' ?>>
                <img src="<?= $escape($asset('icons/invoices.svg')) ?>" width="18" height="18" alt="">Faturalar</a>
            <a href="<?= $escape($sectionUrl('setup')) ?>"<?= $section === 'setup' ? ' class="active" aria-current="page"' : '' ?>>
                <img src="<?= $escape($asset('icons/settings.svg')) ?>" width="18" height="18" alt="">Kurulum</a>
            <a href="<?= $escape($sectionUrl('logs')) ?>"<?= $section === 'logs' ? ' class="active" aria-current="page"' : '' ?>>
                <?= $icon('activity') ?>İstek geçmişi</a>
        </nav>
        <div class="gf-nav-tools">
            <a class="gf-chip" href="configaddonmods.php#gibframework"><?= $icon('sliders') ?><span>Modül ayarları</span></a>
            <?php if ($panelApp !== ''): ?>
                <a class="gf-btn gf-btn-primary" href="<?= $escape($panelApp . '/invoices') ?>" target="_blank" rel="noopener"><?= $icon('box-arrow-up-right') ?><span>GIB paneli</span></a>
            <?php endif; ?>
            <a class="gf-chip" href="index.php" title="WHMCS yönetim paneline dön"><?= $icon('arrow-left') ?><span>WHMCS</span></a>
        </div>
    </header>

    <main class="gf-content">
        <div class="gf-page-header">
            <div class="gf-page-title">
                <h1><?= $escape($titles[$section]) ?></h1>
                <p><?= $escape($subtitles[$section]) ?></p>
            </div>
            <div class="gf-page-actions">
                <?= $enabled ? $badge('success', 'Otomatik aktarım açık', 'circle-fill') : $badge('muted', 'Otomatik aktarım kapalı', 'pause-circle') ?>
                <?php if ($section === 'invoices'): ?><?= $form('run', 'Şimdi gönder', 'gf-btn gf-btn-primary', 0, 'Gönderiliyor…', 'send') ?><?php endif; ?>
                <?php if ($section === 'setup'): ?><?= $form('probe', 'Bağlantıyı test et', 'gf-btn gf-btn-primary', 0, 'Test ediliyor…', 'plug') ?><?php endif; ?>
            </div>
        </div>

        <div class="gf-status-strip">
            <div><?= $icon('clock-history') ?><span>Son çalışma</span><strong><?= $escape(isset($state->last_cron_at) ? $date($state->last_cron_at) : 'Henüz yok') ?></strong></div>
            <div><?= $icon('cloud-check') ?><span>Son başarılı aktarım</span><strong><?= $escape(isset($state->last_success_at) ? $date($state->last_success_at) : 'Henüz yok') ?></strong></div>
            <div><?= $icon('bell') ?><span>Son panel bildirimi</span><strong><?= $escape(isset($state->last_callback_at) ? $date($state->last_callback_at) : 'Henüz yok') ?></strong></div>
        </div>

        <?php if ($flash !== null): ?>
            <div class="gf-alert gf-alert-<?= $flash[0] ?>" role="status">
                <?= $icon($flash[0] === 'success' ? 'check-circle-fill' : 'exclamation-triangle-fill') ?>
                <div class="gf-alert-body"><strong><?= $escape($flash[1]) ?></strong><?php if ($flash[2]): ?><span><?= $escape($flash[2]) ?></span><?php endif; ?></div>
            </div>
        <?php endif; ?>

        <?php if (!$configured): ?>
            <div class="gf-alert gf-alert-warning" role="status">
                <?= $icon('exclamation-triangle-fill') ?>
                <div class="gf-alert-body"><strong>Kurulum tamamlanmadı.</strong>
                    <span>Modül ayarlarına paneldeki <em>Gelen adres (webhook URL)</em> ve <em>İmza anahtarı</em> değerlerini girin.</span></div>
                <a class="gf-btn gf-btn-light" href="<?= $escape($sectionUrl('setup')) ?>"><?= $icon('list-check') ?><span>Kurulum adımları</span></a>
            </div>
        <?php elseif ($state?->reason !== null): ?>
            <div class="gf-alert gf-alert-warning" role="status">
                <?= $icon('pause-circle-fill') ?>
                <div class="gf-alert-body"><strong>Aktarım duraklatıldı.</strong>
                    <span><?= $escape($reasonText($state->reason)) ?></span>
                    <small><code><?= $escape($state->reason) ?></code> · Sonraki otomatik deneme <?= $escape($date($state->next_at)) ?></small></div>
                <?= $form('resume', 'Şimdi yeniden dene', 'gf-btn gf-btn-light', 0, 'İşleniyor…', 'arrow-clockwise') ?>
            </div>
        <?php endif; ?>
        <?php if ($hookFileExists): ?>
            <div class="gf-alert gf-alert-info" role="status">
                <?= $icon('info-circle-fill') ?>
                <div class="gf-alert-body"><strong>includes/hooks/gibframework.php bulundu.</strong>
                    <span>Panelden indirilen kanca dosyası bu modülle aynı işi yapar. Aynı fatura panelde tek taslak olur, ancak çift istek olmaması için dosyayı silmeniz önerilir.</span></div>
            </div>
        <?php endif; ?>

        <?php if ($section === 'setup'): ?>
            <div class="gf-grid-2">
                <section class="gf-card">
                    <div class="gf-card-header"><h2><?= $icon('plug') ?>Bağlantı</h2>
                        <?= $configured ? $badge('success', 'Yapılandırıldı', 'check2') : $badge('warning', 'Eksik', 'exclamation') ?></div>
                    <dl class="gf-facts">
                        <div><dt>Gelen adres (webhook URL)</dt><dd><?= $webhookUrl === '' ? $badge('warning', 'Girilmedi') : '<code>' . $escape($webhookUrl) . '</code>' . (WebhookClient::isWebhookUrl($webhookUrl) ? '' : ' ' . $badge('warning', 'Biçim hatalı')) ?></dd></div>
                        <div><dt>İmza anahtarı</dt><dd><?= $secretSet ? $badge('success', 'Girildi', 'key') : $badge('warning', 'Girilmedi') ?></dd></div>
                        <div><dt>TCKN / VKN özel alanı</dt><dd><?= $escape(($settings['taxField'] ?? '') !== '' ? $settings['taxField'] : 'WHMCS Tax ID') ?></dd></div>
                        <div><dt>Belge türü</dt><dd>VKN (10 hane) → şirket, TCKN (11 hane) → şahıs · belge türünü panel belirler (e-Fatura kayıtlı değilse e-Arşiv)</dd></div>
                        <div><dt>Vergi dairesi özel alanı</dt><dd><?= $escape(($settings['taxOfficeField'] ?? '') !== '' ? $settings['taxOfficeField'] : '—') ?></dd></div>
                        <div><dt>Ödeme anında gönder</dt><dd><?= ($settings['sendImmediately'] ?? 'on') === 'on' ? $badge('success', 'Açık') : $badge('muted', 'Kapalı (yalnızca cron)') ?></dd></div>
                        <div><dt>Geçmiş faturalar</dt><dd><?= $escape(($settings['startDate'] ?? '') !== '' ? $settings['startDate'] . ' tarihinden itibaren' : 'Gönderilmez (yalnızca yeni ödemeler)') ?></dd></div>
                    </dl>
                    <div class="gf-card-actions">
                        <a class="gf-btn gf-btn-light" href="configaddonmods.php#gibframework"><?= $icon('sliders') ?><span>Modül ayarlarını aç</span></a>
                        <?= $form('probe', 'Bağlantıyı test et', 'gf-btn gf-btn-primary', 0, 'Test ediliyor…', 'plug') ?>
                    </div>
                    <p class="gf-hint"><?= $icon('info-circle') ?>Test imzalı bir <code>ping</code> gönderir; panel imzayı doğrular ve fatura oluşturmaz.</p>
                </section>

                <section class="gf-card">
                    <div class="gf-card-header"><h2><?= $icon('tools') ?>Kurulum adımları</h2></div>
                    <ol class="gf-steps">
                        <li><span class="gf-step-no">1</span><div><strong>Panelde entegrasyonu açın</strong><p>GIB Framework › Ayarlar › Entegrasyonlar › WHMCS sayfasında <b>Etkin</b>'i açıp kaydedin.</p></div></li>
                        <li><span class="gf-step-no">2</span><div><strong>Gelen adresi ve imza anahtarını kopyalayın</strong><p>Kurulum kutusundaki <b>Gelen adres (webhook URL)</b> ve <b>İmza anahtarı · X-GibFramework-Signature</b> değerleri.</p></div></li>
                        <li><span class="gf-step-no">3</span><div><strong>Modül ayarlarına yapıştırın</strong><p>WHMCS › Sistem Ayarları › Eklenti Modülleri › GIB Framework › Yapılandır. <b>Otomatik aktarım</b>'ı açın.</p></div></li>
                        <li><span class="gf-step-no">4</span><div><strong>Bağlantıyı test edin</strong><p>Bu sayfadaki <b>Bağlantıyı test et</b> düğmesi. Kanca dosyasını (gibframework.php) indirmenize gerek yok.</p></div></li>
                    </ol>
                </section>
            </div>

            <section class="gf-card">
                <div class="gf-card-header"><h2><?= $icon('bell') ?>Durum bildirimleri (önerilir)</h2>
                    <?= isset($state->last_callback_at) ? $badge('success', 'Bildirim alınıyor', 'check2') : $badge('muted', 'Henüz bildirim yok') ?></div>
                <p>Paneldeki WHMCS entegrasyonunun <b>Bildirim adresi (webhook URL)</b> alanına aşağıdaki adresi yazın ve fatura olaylarını seçin.
                    Onay, imza, belge numarası ve iptal bilgileri bu modüle otomatik düşer.</p>
                <div class="gf-input-group">
                    <input class="gf-input gf-mono" id="gib-callback" readonly value="<?= $escape($callbackUrl) ?>" aria-label="Bildirim adresi">
                    <button class="gf-btn gf-btn-light" type="button" data-gib-copy="#gib-callback"><?= $icon('clipboard') ?><span>Kopyala</span></button>
                </div>
                <p class="gf-hint"><?= $icon('info-circle') ?>Kaydettikten sonra paneldeki <b>Giden bildirimi dene</b> düğmesine basın; yukarıdaki "Son panel bildirimi" saati güncellenmelidir.</p>
            </section>

            <section class="gf-card">
                <div class="gf-card-header"><h2><?= $icon('ui-checks') ?>Paneldeki diğer seçenekler</h2></div>
                <ul class="gf-list">
                    <li><?= $icon('check2-circle') ?><div><b>Gelen siparişi otomatik onaya gönder:</b> açıksa taslak doğrulanıp onay kuyruğuna düşer; kapalıysa taslak olarak bekler.</div></li>
                    <li><?= $icon('person-vcard') ?><div><b>Alıcıyı cari kartlara kaydet:</b> WHMCS müşterisi panelde cari kart olarak saklanır (TCKN/VKN'siz müşteriler hariç).</div></li>
                    <li><?= $icon('percent') ?><div><b>Vergisi belirtilmeyen satırlarda KDV oranı:</b> modül her satırın KDV oranını gönderdiği için WHMCS faturalarında kullanılmaz.</div></li>
                    <li><?= $icon('fingerprint') ?><div><b>TCKN / VKN müşteri özel alanı adı:</b> paneldeki değer yalnızca hazır kanca dosyası içindir; modül bu bilgiyi kendi ayarından okur.</div></li>
                    <li><?= $icon('shield-lock') ?><div><b>403 hatası alırsanız:</b> panelin IP erişim listesine WHMCS sunucusunun IP adresini ekleyin; panel Cloudflare arkasındaysa <code>/api/v1/hooks/</code> için WAF atlama kuralı tanımlayın.</div></li>
                </ul>
            </section>

        <?php elseif ($section === 'logs'): ?>
            <section class="gf-card gf-card-flush">
                <div class="gf-card-header gf-card-header-bar">
                    <h2><?= $icon('activity') ?>İstekler</h2>
                    <div class="gf-segmented" role="group" aria-label="İstek filtresi">
                        <a href="<?= $escape($sectionUrl('logs')) ?>"<?= !$requestErrors ? ' class="active" aria-current="page"' : '' ?>>Tümü</a>
                        <a href="<?= $escape($sectionUrl('logs') . '&result=error') ?>"<?= $requestErrors ? ' class="active" aria-current="page"' : '' ?>>Hatalar</a>
                    </div>
                </div>
                <div class="gf-table-wrap" tabindex="0" aria-label="İstekler tablosu">
                    <table class="gf-table">
                        <thead><tr><th>İstek</th><th>Sonuç</th><th>Süre</th><th>Fatura</th><th>Zaman (TSİ)</th></tr></thead>
                        <tbody>
                        <?php foreach ($logs as $log): ?>
                            <tr>
                                <td><span class="gf-method gf-method-<?= $log->method === 'IN' ? 'in' : 'out' ?>"><?= $log->method === 'IN' ? 'GELEN' : $escape($log->method) ?></span> <code><?= $escape($log->endpoint) ?></code></td>
                                <td><?= $log->reason === null ? $badge('success', (string) ($log->http_status ?: 'OK')) : $badge('warning', (string) ($log->http_status ?: 'Bağlantı hatası')) ?>
                                    <?php if ($log->reason !== null): ?><span class="gf-cell-note"><?= $escape($reasonText($log->reason)) ?></span><?php endif; ?></td>
                                <td class="gf-nowrap"><?= (int) $log->duration_ms ?> ms</td>
                                <td><?= $log->invoice_id ? '<a href="invoices.php?action=edit&amp;id=' . (int) $log->invoice_id . '">#' . (int) $log->invoice_id . '</a>' : '—' ?></td>
                                <td class="gf-nowrap"><?= $escape($date($log->requested_at)) ?></td>
                            </tr>
                        <?php endforeach; ?>
                        <?php if ($logs->isEmpty()): ?><tr><td colspan="5" class="gf-empty"><?= $icon('inbox') ?>Bu görünümde istek yok.</td></tr><?php endif; ?>
                        </tbody>
                    </table>
                </div>
                <?php if ($requestPages > 1): ?>
                    <nav class="gf-pagination" aria-label="İstek sayfaları">
                        <span>Sayfa <?= $requestPage ?> / <?= $requestPages ?></span>
                        <div>
                            <?php $logPage = $sectionUrl('logs') . ($requestErrors ? '&result=error' : '') . '&page='; ?>
                            <?php if ($requestPage > 1): ?><a class="gf-btn gf-btn-light" href="<?= $escape($logPage . ($requestPage - 1)) ?>"><?= $icon('chevron-left') ?><span>Önceki</span></a><?php endif; ?>
                            <?php if ($requestPage < $requestPages): ?><a class="gf-btn gf-btn-light" href="<?= $escape($logPage . ($requestPage + 1)) ?>"><span>Sonraki</span><?= $icon('chevron-right') ?></a><?php endif; ?>
                        </div>
                    </nav>
                <?php endif; ?>
            </section>

        <?php else: ?>
            <div class="gf-kpis">
                <?php foreach ($summary as $key => $total): ?>
                    <a class="gf-kpi<?= $key === 'errors' && $total > 0 ? ' is-warning' : '' ?><?= $key === $tab ? ' is-active' : '' ?>" href="<?= $escape($url($key)) ?>">
                        <span class="gf-kpi-icon"><?= $icon($summaryIcons[$key]) ?></span>
                        <span class="gf-kpi-label"><?= $escape($tabs[$key]) ?></span>
                        <strong><?= $escape(number_format($total, 0, ',', '.')) ?></strong>
                    </a>
                <?php endforeach; ?>
            </div>

            <section class="gf-card gf-card-flush">
                <nav class="gf-tabs" aria-label="Fatura durumları">
                    <?php foreach ($tabs as $key => $label): ?>
                        <a href="<?= $escape($url($key, 1, $search)) ?>"<?= $key === $tab ? ' class="active" aria-current="true"' : '' ?>><?= $escape($label) ?></a>
                    <?php endforeach; ?>
                </nav>
                <div class="gf-toolbar">
                    <form class="gf-search" action="addonmodules.php" method="get" role="search">
                        <input type="hidden" name="module" value="gibframework">
                        <input type="hidden" name="view" value="<?= $escape($tab) ?>">
                        <label class="gf-sr-only" for="gib-invoice-search">WHMCS fatura ID, fatura no veya belge no</label>
                        <span class="gf-input-icon"><img src="<?= $escape($asset('icons/search.svg')) ?>" width="17" height="17" alt="">
                            <input id="gib-invoice-search" class="gf-input" name="q" maxlength="70" value="<?= $escape($search) ?>" placeholder="Fatura ID, fatura no veya belge no"></span>
                        <button type="submit" class="gf-btn gf-btn-primary"><?= $icon('search') ?><span>Ara</span></button>
                        <?php if ($search !== ''): ?><a class="gf-btn gf-btn-light" href="<?= $escape($url($tab)) ?>"><?= $icon('x-lg') ?><span>Temizle</span></a><?php endif; ?>
                    </form>
                    <form class="gf-search" method="post" data-gib-busy>
                        <?= $hidden('enqueue') ?>
                        <label class="gf-sr-only" for="gib-enqueue">Kuyruğa eklenecek WHMCS fatura ID</label>
                        <input id="gib-enqueue" class="gf-input gf-input-narrow" name="invoice_id" inputmode="numeric" pattern="[0-9]+" required placeholder="Fatura ID">
                        <button type="submit" class="gf-btn gf-btn-light" data-busy-label="Ekleniyor…"><?= $icon('plus-lg') ?><span>Kuyruğa ekle</span></button>
                    </form>
                </div>
                <div class="gf-table-wrap" tabindex="0" aria-label="Faturalar tablosu">
                    <table class="gf-table">
                        <thead><tr>
                            <th>WHMCS faturası</th><th>Ödeme</th><th>GIB Framework durumu</th>
                            <th>Belge no</th><th>Aktarım (TSİ)</th><th class="gf-right">İşlem</th>
                        </tr></thead>
                        <tbody>
                        <?php foreach ($invoices as $invoice):
                            $status = $invoice->document_status;
                            $reason = $invoice->reason;
                            $label = $invoice->draft_id === null ? ($reason === null ? 'Kuyrukta' : 'Yeniden denenecek') : ($labels[$status] ?? 'Aktarıldı');
                            [$tone, $toneIcon] = match (true) {
                                $reason !== null, in_array($status, Queue::STATUSES['İnceleme'], true) => ['warning', 'exclamation-circle'],
                                in_array($status, Queue::STATUSES['Kesinleşen'], true) => ['success', 'patch-check'],
                                $invoice->draft_id !== null => ['info', 'send-check'],
                                default => ['muted', 'hourglass-split'],
                            };
                            $documentLabel = $invoice->document_number ?? ($invoice->draft_number ? 'Taslak ' . $invoice->draft_number : null);
                        ?>
                            <tr>
                                <td><a class="gf-strong-link" href="invoices.php?action=edit&amp;id=<?= (int) $invoice->invoice_id ?>"><?= $escape($invoice->invoicenum ?: '#' . $invoice->invoice_id) ?></a>
                                    <?php if ($invoice->invoicenum): ?><span class="gf-cell-sub">#<?= (int) $invoice->invoice_id ?></span><?php endif; ?></td>
                                <td><?= $escape($paymentLabels[$invoice->payment_status] ?? $invoice->payment_status ?? '—') ?>
                                    <?php if (is_numeric($invoice->whmcs_total)): ?><span class="gf-cell-sub"><?= $escape(number_format((float) $invoice->whmcs_total, 2, ',', '.')) ?></span><?php endif; ?></td>
                                <td class="gf-status-cell"><?= $badge($tone, $label, $toneIcon) ?>
                                    <?php if ($reason !== null): ?>
                                        <span class="gf-cell-note"><?= $escape($reasonText($reason)) ?></span>
                                        <span class="gf-cell-sub">Sonraki deneme <?= $escape($date($invoice->next_at)) ?> · <code><?= $escape($reason) ?></code></span>
                                    <?php endif; ?>
                                    <?php if ($invoice->detail): ?><span class="gf-detail"><?= $escape($invoice->detail) ?></span><?php endif; ?></td>
                                <td><?php if ($panelApp !== '' && WebhookClient::isUuid((string) $invoice->draft_id)): ?>
                                        <a class="gf-strong-link" href="<?= $escape($panelApp . '/invoices/' . $invoice->draft_id) ?>" target="_blank" rel="noopener"><?= $escape($documentLabel ?? 'Panelde aç') ?> <?= $icon('box-arrow-up-right') ?></a>
                                    <?php else: ?><?= $escape($documentLabel ?? '—') ?><?php endif; ?>
                                    <?php if (!empty($invoice->public_url)): ?><a class="gf-btn gf-btn-primary gf-btn-sm gf-official" href="<?= $escape($invoice->public_url) ?>" target="_blank" rel="noopener"><?= $icon('file-earmark-text') ?><span>Resmi Fatura</span></a><?php endif; ?></td>
                                <td class="gf-nowrap"><?= $escape($date($invoice->submitted_at)) ?>
                                    <?php if ($invoice->checked_at && $invoice->checked_at !== $invoice->submitted_at): ?><span class="gf-cell-sub">Güncellendi <?= $escape($date($invoice->checked_at)) ?></span><?php endif; ?></td>
                                <td class="gf-right gf-nowrap">
                                    <?php if ($invoice->draft_id === null): ?>
                                        <?= $form('retry', 'Şimdi dene', 'gf-btn gf-btn-light gf-btn-sm', (int) $invoice->invoice_id, 'İşleniyor…', 'arrow-clockwise') ?>
                                    <?php elseif (in_array($status, Queue::STATUSES['İnceleme'], true)): ?>
                                        <?= $form('resend', 'Yeniden gönder', 'gf-btn gf-btn-light gf-btn-sm', (int) $invoice->invoice_id, 'İşleniyor…', 'arrow-repeat') ?>
                                    <?php else: ?><span class="gf-muted">—</span><?php endif; ?>
                                </td>
                            </tr>
                        <?php endforeach; ?>
                        <?php if ($invoices->isEmpty()): ?><tr><td class="gf-empty" colspan="6"><?= $icon('inbox') ?>Bu görünümde fatura yok.</td></tr><?php endif; ?>
                        </tbody>
                    </table>
                </div>
                <?php if ($pages > 1): ?>
                    <nav class="gf-pagination" aria-label="Fatura sayfaları">
                        <span>Sayfa <?= $page ?> / <?= $pages ?></span>
                        <div>
                            <?php if ($page > 1): ?><a class="gf-btn gf-btn-light" href="<?= $escape($url($tab, $page - 1, $search)) ?>"><?= $icon('chevron-left') ?><span>Önceki</span></a><?php endif; ?>
                            <?php if ($page < $pages): ?><a class="gf-btn gf-btn-light" href="<?= $escape($url($tab, $page + 1, $search)) ?>"><span>Sonraki</span><?= $icon('chevron-right') ?></a><?php endif; ?>
                        </div>
                    </nav>
                <?php endif; ?>
            </section>
        <?php endif; ?>
    </main>
    <footer class="gf-footer">GIB Framework WHMCS modülü v<?= $escape(GIBFRAMEWORK_MODULE_VERSION) ?></footer>
>>>>>>> Stashed changes
</div>
