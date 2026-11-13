<?php

declare(strict_types=1);

use GibFramework\Whmcs\Queue;
use GibFramework\Whmcs\FrameworkClient;
use WHMCS\Database\Capsule;

defined('WHMCS') || exit;

$tabs = ['all' => 'Tümü', 'pending' => 'Bekleyen', 'outbound' => 'Giden faturalar', 'draft' => 'Taslaklar', 'transmitting' => 'İmzalı / iletimde', 'issued' => 'Kesinleşen faturalar', 'errors' => 'İnceleme'];
$tab = is_string($_GET['view'] ?? null) && isset($tabs[$_GET['view']]) ? $_GET['view'] : 'all';
$search = is_string($_GET['q'] ?? null) ? trim(substr($_GET['q'], 0, 70)) : '';
$page = filter_var($_GET['page'] ?? 1, FILTER_VALIDATE_INT, ['options' => ['min_range' => 1, 'max_range' => 1000000]]) ?: 1;
$perPage = 25;
$state = Capsule::table(Queue::STATE)->where('id', 1)->first();
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

$labels = [
    'Draft' => 'Taslak', 'Validating' => 'Doğrulanıyor', 'Validated' => 'Doğrulandı', 'AwaitingApproval' => 'Onay bekliyor',
    'Approved' => 'Onaylandı', 'Signing' => 'İmza bekliyor', 'Signed' => 'İmzalandı', 'Queued' => 'İletim kuyruğunda',
    'Transmitting' => 'İletiliyor', 'Sent' => 'Gönderildi', 'Acknowledged' => 'Alındı', 'Delivered' => 'Teslim edildi',
    'Accepted' => 'Kabul edildi', 'InDoubt' => 'Sonuç belirsiz', 'Rejected' => 'Reddedildi', 'Cancelled' => 'İptal edildi',
    'Objected' => 'İtiraz edildi', 'Failed' => 'Başarısız',
];
$paymentLabels = ['Paid' => 'Ödendi', 'Unpaid' => 'Ödenmedi', 'Refunded' => 'İade edildi', 'Cancelled' => 'İptal edildi', 'Payment Pending' => 'Ödeme bekliyor'];
$escape = static fn ($value): string => htmlspecialchars((string) $value, ENT_QUOTES | ENT_SUBSTITUTE, 'UTF-8');
$date = static fn ($value): string => $value ? gmdate('d.m.Y H:i', (int) $value) : '—';
$url = static fn (string $view, int $number = 1, string $query = ''): string => 'addonmodules.php?' . http_build_query(['module' => 'gibframework', 'view' => $view, 'page' => $number, 'q' => $query]);
$enabled = ($vars['enabled'] ?? '') === 'on';
$panelUrl = rtrim(trim((string) ($vars['frameworkUrl'] ?? '')), '/');
$panelUrl = filter_var($panelUrl, FILTER_VALIDATE_URL) && str_starts_with($panelUrl, 'https://')
    && !array_intersect(['user', 'pass', 'query', 'fragment'], array_keys(parse_url($panelUrl))) ? $panelUrl : '';
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

    <section class="card gib-invoices" aria-label="Fatura listesi">
        <div class="gib-toolbar">
            <nav class="nav nav-pills gap-2" aria-label="Fatura durumları">
                <?php foreach ($tabs as $key => $label): ?>
                    <a class="nav-link<?= $key === $tab ? ' active' : '' ?>" href="<?= $escape($url($key, 1, $search)) ?>"<?= $key === $tab ? ' aria-current="page"' : '' ?>><?= $escape($label) ?></a>
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

        <div class="table-responsive" tabindex="0" aria-label="Faturalar tablosu">
            <table class="table table-hover align-middle mb-0">
                <thead><tr>
                    <th scope="col">WHMCS faturası</th><th scope="col">Ödeme</th><th scope="col">GIB Framework</th>
                    <th scope="col">Belge no</th><th scope="col">Gönderim (UTC)</th><th scope="col">Son kontrol (UTC)</th>
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
                            <?php if ($invoice->draft_id === null && $invoice->reason !== null): ?><small class="d-block text-body-secondary">Sonraki deneme <?= $escape($date($invoice->next_at)) ?> UTC</small><?php endif; ?>
                        </td>
                        <td>
                            <?php if ($panelUrl !== '' && FrameworkClient::isUuid((string) $invoice->draft_id)): ?>
                                <a href="<?= $escape($panelUrl . '/#/invoices/' . $invoice->draft_id) ?>"><?= $escape($invoice->document_number ?? 'Belgeyi aç') ?></a>
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
</div>
