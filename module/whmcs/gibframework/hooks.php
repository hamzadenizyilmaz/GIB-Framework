<?php

declare(strict_types=1);

use GibFramework\Whmcs\Queue;
use WHMCS\Billing\Invoice;
use WHMCS\Database\Capsule;

defined('WHMCS') || exit;

<<<<<<< Updated upstream
require_once __DIR__ . '/lib/FrameworkClient.php';
require_once __DIR__ . '/lib/InvoicePayload.php';
require_once __DIR__ . '/lib/Queue.php';

add_hook('InvoicePaid', 1, static function (array $vars): void {
    try {
        $id = (int) ($vars['invoiceid'] ?? 0);
        if ($id > 0 && gibframework_hook_settings() !== []) {
            $invoice = $vars['invoice'] ?? Invoice::find($id);
            if ($invoice instanceof Invoice && $invoice->status === 'Paid' && (float) $invoice->total > 0) {
                Queue::enqueue($id);
            }
        }
    } catch (Throwable) {
        logActivity('GIB Framework: Fatura kuyruğu yazılamadı. Cron taraması faturayı yeniden kontrol edecek.');
=======
require_once __DIR__ . '/lib/WebhookClient.php';
require_once __DIR__ . '/lib/InvoicePayload.php';
require_once __DIR__ . '/lib/Queue.php';

// Fatura "Ödendi" olduğunda kuyruğa alınır ve hemen GIB Framework'e gönderilmeye çalışılır.
// Gönderim başarısız olursa WHMCS cron'u (AfterCronJob) faturayı yeniden dener.
add_hook('InvoicePaid', 1, static function (array $vars): void {
    $id = (int) ($vars['invoiceid'] ?? 0);
    $settings = gibframework_hook_settings();
    if ($id <= 0 || $settings === []) {
        return;
    }
    try {
        $invoice = $vars['invoice'] ?? Invoice::find($id);
        if (!$invoice instanceof Invoice || $invoice->status !== 'Paid' || (float) $invoice->total <= 0) {
            return;
        }
        Queue::enqueue($id);
    } catch (Throwable) {
        logActivity('GIB Framework: Fatura #' . $id . ' kuyruğa yazılamadı. Modülü yeniden etkinleştirip kurulumu kontrol edin.');
        return;
    }
    if (($settings['sendImmediately'] ?? 'on') === 'on') {
        try {
            Queue::sendOne($id, $settings);
        } catch (Throwable) {
            logActivity('GIB Framework: Fatura #' . $id . ' hemen gönderilemedi; cron yeniden deneyecek.');
        }
>>>>>>> Stashed changes
    }
});

add_hook('AfterCronJob', 1, static function (): void {
    try {
        $settings = gibframework_hook_settings();
        if ($settings !== []) {
            Queue::run($settings);
        }
    } catch (Throwable) {
<<<<<<< Updated upstream
<<<<<<< Updated upstream
        logActivity('GIB Framework: Taslak kuyruğu çalıştırılamadı. WHMCS modül kurulumunu ve veritabanını kontrol edin.');
=======
        logActivity('GIB Framework: Fatura kuyruğu çalıştırılamadı. WHMCS modül kurulumunu ve veritabanını kontrol edin.');
>>>>>>> Stashed changes
=======
        logActivity('GIB Framework: Fatura kuyruğu çalıştırılamadı. WHMCS modül kurulumunu ve veritabanını kontrol edin.');
>>>>>>> Stashed changes
    }
});

function gibframework_hook_settings(): array
{
    $settings = Capsule::table('tbladdonmodules')->where('module', 'gibframework')->pluck('value', 'setting')->all();
    return ($settings['enabled'] ?? '') === 'on' ? $settings : [];
}
<<<<<<< Updated upstream
=======

/** WHMCS fatura ID → resmi fatura bağlantısı. */
function gibframework_official_links(array $invoiceIds): array
{
    $invoiceIds = array_values(array_filter(array_map('intval', $invoiceIds), static fn (int $id): bool => $id > 0));
    if ($invoiceIds === [] || !Capsule::schema()->hasColumn(Queue::INVOICES, 'public_url')) {
        return [];
    }
    return Capsule::table(Queue::INVOICES)->whereIn('invoice_id', $invoiceIds)->whereNotNull('public_url')
        ->pluck('public_url', 'invoice_id')->all();
}

function gibframework_official_script(array $links, ?int $currentInvoice): string
{
    $data = json_encode(['links' => (object) $links, 'current' => $currentInvoice], JSON_UNESCAPED_SLASHES | JSON_HEX_TAG | JSON_HEX_AMP | JSON_HEX_APOS | JSON_HEX_QUOT);
    return <<<HTML
<style>
.gibframework-official{display:inline-flex;align-items:center;gap:6px;margin:4px 0 4px 8px;padding:5px 12px;border:1px solid #1c3934;border-radius:8px;background:#1c3934;color:#fff!important;font-size:13px;font-weight:600;line-height:1.3;text-decoration:none!important;white-space:nowrap}
.gibframework-official:hover{background:#132b27}
.gibframework-official svg{width:14px;height:14px;fill:currentColor}
.gibframework-official-bar{margin:12px 0;text-align:right}
</style>
<script>
(function () {
  var data = {$data};
  var icon = '<svg viewBox="0 0 16 16" aria-hidden="true"><path d="M14 4.5V14a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V2a2 2 0 0 1 2-2h5.5L14 4.5zM9.5 3A1.5 1.5 0 0 0 11 4.5h2L9.5 1v2zM4.5 8a.5.5 0 0 0 0 1h7a.5.5 0 0 0 0-1h-7zm0 2a.5.5 0 0 0 0 1h7a.5.5 0 0 0 0-1h-7zm0 2a.5.5 0 0 0 0 1h4a.5.5 0 0 0 0-1h-4z"/></svg>';
  function button(url) {
    var a = document.createElement('a');
    a.className = 'gibframework-official';
    a.href = url; a.target = '_blank'; a.rel = 'noopener';
    a.innerHTML = icon + '<span>Resmi Fatura</span>';
    return a;
  }
  function run() {
    var done = {};
    document.querySelectorAll('a[href*="viewinvoice.php?id="]').forEach(function (link) {
      var m = /viewinvoice\.php\?id=(\d+)/.exec(link.getAttribute('href') || '');
      var url = m && data.links[m[1]];
      if (!url || link.parentNode.querySelector('.gibframework-official')) return;
      link.insertAdjacentElement('afterend', button(url));
      done[m[1]] = true;
    });
    var url = data.current && data.links[data.current];
    if (url && !done[data.current] && !document.querySelector('.gibframework-official-bar')) {
      var bar = document.createElement('div');
      bar.className = 'gibframework-official-bar';
      bar.appendChild(button(url));
      var target = document.querySelector('.invoice-container, .invoice-header, #invoice, .container') || document.body;
      target.insertBefore(bar, target.firstChild);
    }
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', run); else run();
})();
</script>
HTML;
}

// Müşteri paneli: fatura listesi ve fatura görüntüleme sayfasında "Resmi Fatura" düğmesi.
add_hook('ClientAreaFooterOutput', 1, static function (array $vars): string {
    try {
        $clientId = (int) ($vars['clientsdetails']['userid'] ?? $vars['clientsdetails']['id'] ?? ($_SESSION['uid'] ?? 0));
        if ($clientId <= 0 || gibframework_hook_settings() === []) {
            return '';
        }
        $current = (int) ($vars['invoiceid'] ?? 0);
        $ids = Capsule::table('tblinvoices')->where('userid', $clientId)->where('status', 'Paid')->orderByDesc('id')->limit(500)->pluck('id')->all();
        $links = gibframework_official_links($ids);
        return $links === [] ? '' : gibframework_official_script($links, $current > 0 && isset($links[$current]) ? $current : null);
    } catch (Throwable) {
        return '';
    }
});

// Fatura görüntüleme sayfası (viewinvoice.php) bazı temalarda alt bilgi kancasını çalıştırmaz; başlık kancasıyla da eklenir.
add_hook('ClientAreaHeadOutput', 1, static function (array $vars): string {
    try {
        if (basename((string) ($_SERVER['SCRIPT_NAME'] ?? '')) !== 'viewinvoice.php' || gibframework_hook_settings() === []) {
            return '';
        }
        $id = (int) ($_GET['id'] ?? 0);
        $clientId = (int) ($_SESSION['uid'] ?? 0);
        if ($id <= 0 || $clientId <= 0 || !Capsule::table('tblinvoices')->where('id', $id)->where('userid', $clientId)->exists()) {
            return '';
        }
        $links = gibframework_official_links([$id]);
        return $links === [] ? '' : gibframework_official_script($links, $id);
    } catch (Throwable) {
        return '';
    }
});

// Yönetici paneli: fatura düzenleme sayfasında "Resmi Fatura" düğmesi ve belge durumu.
add_hook('AdminInvoicesControlsOutput', 1, static function (array $vars): string {
    try {
        $id = (int) ($vars['invoiceid'] ?? 0);
        if ($id <= 0 || !Capsule::schema()->hasTable(Queue::INVOICES)) {
            return '';
        }
        $row = Capsule::table(Queue::INVOICES)->where('invoice_id', $id)->first();
        if ($row === null) {
            return '';
        }
        $e = static fn ($v): string => htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
        $html = '<div style="margin:10px 0;padding:10px 14px;border:1px solid #e6d8ca;border-radius:10px;background:#fffdfa;font-size:13px">'
            . '<strong>GIB Framework:</strong> ' . $e($row->document_status ?? ($row->draft_id ? 'Aktarıldı' : 'Kuyrukta'))
            . ($row->document_number ? ' · Belge no <code>' . $e($row->document_number) . '</code>' : '');
        if (!empty($row->public_url)) {
            $html .= ' <a class="btn btn-sm btn-success" style="margin-left:8px" href="' . $e($row->public_url) . '" target="_blank" rel="noopener">Resmi Fatura</a>';
        }
        $html .= ' <a class="btn btn-sm btn-default" style="margin-left:4px" href="addonmodules.php?module=gibframework&amp;q=' . $id . '">Modülde aç</a></div>';
        return $html;
    } catch (Throwable) {
        return '';
    }
});
>>>>>>> Stashed changes
