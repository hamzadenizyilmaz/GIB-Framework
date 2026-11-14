<?php

declare(strict_types=1);

use GibFramework\Whmcs\Queue;
use GibFramework\Whmcs\FrameworkClient;
<<<<<<< Updated upstream
=======
use GibFramework\Whmcs\DeliveryException;
>>>>>>> Stashed changes
use WHMCS\Database\Capsule;

defined('WHMCS') || exit;

<<<<<<< Updated upstream
$tabs = ['all' => 'Tümü', 'pending' => 'Bekleyen', 'outbound' => 'Giden faturalar', 'draft' => 'Taslaklar', 'transmitting' => 'İmzalı / iletimde', 'issued' => 'Kesinleşen faturalar', 'errors' => 'İnceleme'];
=======
$tabs = ['all' => 'Tümü', 'pending' => 'Kuyrukta', 'outbound' => 'Aktarılan', 'draft' => 'Taslaklar', 'signature' => 'İmza bekleyen', 'transmitting' => 'İmzalı / iletimde', 'issued' => 'Kesinleşen', 'errors' => 'İnceleme'];
$apiView = ($_GET['section'] ?? null) === 'api';
>>>>>>> Stashed changes
$tab = is_string($_GET['view'] ?? null) && isset($tabs[$_GET['view']]) ? $_GET['view'] : 'all';
$search = is_string($_GET['q'] ?? null) ? trim(substr($_GET['q'], 0, 70)) : '';
$page = filter_var($_GET['page'] ?? 1, FILTER_VALIDATE_INT, ['options' => ['min_range' => 1, 'max_range' => 1000000]]) ?: 1;
$perPage = 25;
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

$labels = [
    'Draft' => 'Taslak', 'Validating' => 'Doğrulanıyor', 'Validated' => 'Doğrulandı', 'AwaitingApproval' => 'Onay bekliyor',
    'Approved' => 'Onaylandı', 'Signing' => 'İmza bekliyor', 'Signed' => 'İmzalandı', 'Queued' => 'İletim kuyruğunda',
    'Transmitting' => 'İletiliyor', 'Sent' => 'Gönderildi', 'Acknowledged' => 'Alındı', 'Delivered' => 'Teslim edildi',
    'Accepted' => 'Kabul edildi', 'InDoubt' => 'Sonuç belirsiz', 'Rejected' => 'Reddedildi', 'Cancelled' => 'İptal edildi',
    'Objected' => 'İtiraz edildi', 'Failed' => 'Başarısız',
];
$paymentLabels = ['Paid' => 'Ödendi', 'Unpaid' => 'Ödenmedi', 'Refunded' => 'İade edildi', 'Cancelled' => 'İptal edildi', 'Payment Pending' => 'Ödeme bekliyor'];
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
    $requests = Capsule::table(Queue::REQUESTS);
    if ($requestErrors) {
        $requests->whereNotNull('reason');
    }
    $requestPages = max(1, (int) ceil((clone $requests)->count() / $perPage));
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
</div>
