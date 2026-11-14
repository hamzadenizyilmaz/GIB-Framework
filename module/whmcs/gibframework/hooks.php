<?php

declare(strict_types=1);

use GibFramework\Whmcs\Queue;
use WHMCS\Billing\Invoice;
use WHMCS\Database\Capsule;

defined('WHMCS') || exit;

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
        logActivity('GIB Framework: Taslak kuyruğu çalıştırılamadı. WHMCS modül kurulumunu ve veritabanını kontrol edin.');
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
