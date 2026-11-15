<?php

declare(strict_types=1);

/**
 * GIB Framework -> WHMCS bildirim adresi.
 *
 * Paneldeki WHMCS entegrasyonunun "Bildirim adresi (webhook URL)" alanına bu dosyanın adresi yazılır:
 *   https://WHMCS_ADRESINIZ/modules/addons/gibframework/callback.php
 *
 * Panel her fatura olayında (invoice.created, invoice.awaiting_approval, invoice.approved, invoice.signed,
 * invoice.issued, invoice.cancelled, invoice.rejected, invoice.failed, ping) imzalı JSON gönderir:
 *   X-GibFramework-Timestamp: <unix saniye>
 *   X-GibFramework-Signature: sha256=HMAC_SHA256(imza anahtarı, timestamp + "." + gövde)
**/

use GibFramework\Whmcs\Queue;
use GibFramework\Whmcs\WebhookClient;
use WHMCS\Database\Capsule;

require_once dirname(__DIR__, 3) . '/init.php';
require_once __DIR__ . '/lib/WebhookClient.php';
require_once __DIR__ . '/lib/InvoicePayload.php';
require_once __DIR__ . '/lib/Queue.php';

header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');
header('X-Robots-Tag: noindex');

$reply = static function (int $status, array $body): never {
    http_response_code($status);
    echo json_encode($body, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    exit;
};

if (($_SERVER['REQUEST_METHOD'] ?? '') !== 'POST') {
    $reply(405, ['error' => 'Yalnızca POST kabul edilir.']);
}

try {
    $settings = Capsule::table('tbladdonmodules')->where('module', 'gibframework')->pluck('value', 'setting')->all();
} catch (Throwable) {
    $reply(503, ['error' => 'Modül ayarları okunamadı.']);
}
if (($settings['enabled'] ?? '') !== 'on') {
    $reply(404, ['error' => 'Modül kapalı.']);
}

$secret = WebhookClient::secret((string) ($settings['signingSecret'] ?? ''));
$body = (string) file_get_contents('php://input', false, null, 0, 1048577);
$timestamp = (string) ($_SERVER['HTTP_X_GIBFRAMEWORK_TIMESTAMP'] ?? '');
$given = strtolower(trim((string) ($_SERVER['HTTP_X_GIBFRAMEWORK_SIGNATURE'] ?? '')));

if ($secret === '' || $body === '' || strlen($body) > 1048576 || !ctype_digit($timestamp)
    || abs(time() - (int) $timestamp) > 300
    || !hash_equals('sha256=' . hash_hmac('sha256', $timestamp . '.' . $body, $secret), $given)) {
    try {
        Queue::recordRequest('IN', '/callback.php', 401, 0, 'CALLBACK_SIGNATURE');
    } catch (Throwable) {
    }
    $reply(401, ['error' => 'İmza doğrulanamadı.']);
}

$payload = json_decode($body, true);
$event = is_array($payload) && is_string($payload['event'] ?? null) ? $payload['event'] : '';
$data = is_array($payload['data'] ?? null) ? $payload['data'] : [];
$sequence = $payload['sequence'] ?? null;
if ($event === '' || ($sequence !== null && (!is_int($sequence) || $sequence < 1))) {
    $reply(400, ['error' => 'Bildirim içeriği geçersiz.']);
}
if (!preg_match('/^(?:ping|invoice\.[a-z_]{2,40})$/D', $event)) {
    $reply(200, ['ok' => true, 'ignored' => 'Olay türü işlenmiyor.']);
}

try {
    $result = Queue::applyEvent($event, $data, $sequence);
    Queue::recordRequest('IN', substr('/callback.php ' . $event, 0, 90), 200, 0, null,
        preg_match('/^whmcs-([1-9][0-9]{0,18})$/D', (string) ($data['externalOrderId'] ?? ''), $m) ? (int) $m[1] : null);
} catch (Throwable) {
    $reply(500, ['error' => 'Olay kaydedilemedi.']);
}

$reply(200, ['ok' => true, 'result' => $result]);
