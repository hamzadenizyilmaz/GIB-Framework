<?php

declare(strict_types=1);

namespace GibFramework\Whmcs;

use WHMCS\Billing\Invoice;
use WHMCS\Database\Capsule;

defined('WHMCS') || exit;

final class Queue
{
    public const INVOICES = 'mod_gibframework_queue';
    public const STATE = 'mod_gibframework_state';
<<<<<<< Updated upstream
<<<<<<< Updated upstream
    public const STATUSES = [
        'Taslak' => ['Draft', 'Validating', 'Validated', 'AwaitingApproval', 'Approved', 'Signing'],
=======
    public const REQUESTS = 'mod_gibframework_requests';
    public const STATUSES = [
        'Taslak' => ['Draft', 'Validating', 'Validated', 'AwaitingApproval'],
>>>>>>> Stashed changes
=======
    public const REQUESTS = 'mod_gibframework_requests';
    public const STATUSES = [
        'Taslak' => ['Draft', 'Validating', 'Validated', 'AwaitingApproval'],
        'İmza bekleyen' => ['Approved', 'Signing'],
>>>>>>> Stashed changes
        'İmzalı / iletimde' => ['Signed', 'Queued', 'Transmitting'],
        'Kesinleşen' => ['Sent', 'Acknowledged', 'Delivered', 'Accepted'],
        'İnceleme' => ['InDoubt', 'Rejected', 'Cancelled', 'Objected', 'Failed'],
    ];

<<<<<<< Updated upstream
=======
    /** Eski (API anahtarlı) sürümün artık kullanılmayan ayarları. */
    private const LEGACY_SETTINGS = ['frameworkUrl', 'credentialsFile', 'portalEnvironment', 'taxFieldId', 'legacyIntegrationId', 'eInvoiceField', 'eInvoiceAliasField'];

>>>>>>> Stashed changes
    public static function install(): void
    {
        $schema = Capsule::schema();
        if (!$schema->hasTable(self::INVOICES)) {
            $schema->create(self::INVOICES, static function ($table): void {
                $table->engine = 'InnoDB';
                $table->unsignedBigInteger('invoice_id')->primary();
                $table->unsignedInteger('attempts')->default(0);
                $table->unsignedBigInteger('next_at');
                $table->string('draft_id', 36)->nullable();
                $table->string('reason', 64)->nullable();
                $table->index(['draft_id', 'next_at', 'invoice_id']);
            });
        }
        if (!$schema->hasTable(self::STATE)) {
            $schema->create(self::STATE, static function ($table): void {
                $table->engine = 'InnoDB';
                $table->unsignedInteger('id')->primary();
                $table->string('source_id', 32);
                $table->string('target_id', 36)->nullable();
                $table->unsignedBigInteger('next_at')->default(0);
                $table->string('reason', 64)->nullable();
            });
        }

        Capsule::table(self::STATE)->insertOrIgnore(['id' => 1, 'source_id' => bin2hex(random_bytes(16))]);
        self::upgrade();
    }

    public static function upgrade(): void
    {
        self::locked(self::migrate(...), true);
    }

    private static function migrate(): void
    {
        $schema = Capsule::schema();
        if (!$schema->hasColumn(self::INVOICES, 'delivery_key')) {
            $schema->table(self::INVOICES, static function ($table): void {
                $table->string('delivery_key', 100)->nullable();
                $table->string('document_status', 32)->nullable();
                $table->string('document_number', 50)->nullable();
                $table->unsignedBigInteger('submitted_at')->nullable();
                $table->unsignedBigInteger('checked_at')->nullable();
                $table->unsignedBigInteger('check_due_at')->default(0);
                $table->string('check_reason', 64)->nullable();
                $table->index(['document_status', 'invoice_id']);
                $table->index(['check_due_at', 'invoice_id']);
            });
        }
<<<<<<< Updated upstream
<<<<<<< Updated upstream

=======
=======
        if (!$schema->hasColumn(self::INVOICES, 'detail')) {
            $schema->table(self::INVOICES, static function ($table): void {
                $table->string('detail', 255)->nullable();
                $table->string('draft_number', 50)->nullable();
            });
        }
>>>>>>> Stashed changes
        if (!$schema->hasTable(self::REQUESTS)) {
            $schema->create(self::REQUESTS, static function ($table): void {
                $table->engine = 'InnoDB';
                $table->bigIncrements('id');
                $table->unsignedBigInteger('invoice_id')->nullable();
                $table->string('method', 4);
                $table->string('endpoint', 90);
                $table->unsignedSmallInteger('http_status');
                $table->unsignedInteger('duration_ms');
                $table->string('reason', 64)->nullable();
                $table->unsignedBigInteger('requested_at');
            });
        }
<<<<<<< Updated upstream
>>>>>>> Stashed changes
        $state = Capsule::table(self::STATE)->where('id', 1);
        $oldId = $schema->hasColumn(self::STATE, 'legacy_integration_id') ? (string) $state->value('legacy_integration_id') : '';
        if (FrameworkClient::isUuid($oldId)) {
            $prefix = 'int:' . str_replace('-', '', strtolower($oldId)) . ':whmcs-';
            Capsule::table(self::INVOICES)->whereNull('draft_id')->whereNull('delivery_key')->chunkById(250, static function ($rows) use ($prefix): void {
                foreach ($rows as $row) {
                    Capsule::table(self::INVOICES)->where('invoice_id', $row->invoice_id)->whereNull('delivery_key')
                        ->update(['delivery_key' => $prefix . $row->invoice_id]);
                }
            }, 'invoice_id');
            $state->update(['legacy_integration_id' => null]);
        }

        Capsule::table('tbladdonmodules')->where('module', 'gibframework')->where('setting', 'legacyIntegrationId')->delete();
<<<<<<< Updated upstream
        Capsule::table(self::STATE)->where('id', 1)->whereIn('reason', ['CONFIG_LEGACY_ID', 'LEGACY_INTEGRATION_CHANGED'])
            ->update(['reason' => null, 'next_at' => 0]);
=======
        Capsule::table(self::STATE)->where('id', 1)->whereIn('reason', ['CONFIG_LEGACY_ID', 'LEGACY_INTEGRATION_CHANGED', 'PORTAL_DISCONNECTED'])
            ->update(['reason' => null, 'next_at' => 0]);
=======
>>>>>>> Stashed changes
        if (!$schema->hasColumn(self::STATE, 'last_cron_at')) {
            $schema->table(self::STATE, static function ($table): void {
                $table->unsignedBigInteger('last_cron_at')->nullable();
            });
        }
<<<<<<< Updated upstream
>>>>>>> Stashed changes
=======
        if (!$schema->hasColumn(self::INVOICES, 'public_url')) {
            $schema->table(self::INVOICES, static function ($table): void {
                $table->string('public_url', 600)->nullable();
            });
        }
        if (!$schema->hasColumn(self::INVOICES, 'last_event_sequence')) {
            $schema->table(self::INVOICES, static function ($table): void {
                $table->unsignedBigInteger('last_event_sequence')->nullable();
            });
        }
        if (!$schema->hasColumn(self::STATE, 'last_callback_at')) {
            $schema->table(self::STATE, static function ($table): void {
                $table->unsignedBigInteger('last_callback_at')->nullable();
                $table->string('last_callback_event', 64)->nullable();
                $table->unsignedBigInteger('last_success_at')->nullable();
            });
        }

        // 2.0: API anahtarı / operatör / portal akışı kaldırıldı. Eski yapılandırma hataları ve
        // bu hatalar yüzünden bekleyen faturalar yeni webhook akışıyla hemen yeniden denenir.
        Capsule::table('tbladdonmodules')->where('module', 'gibframework')->whereIn('setting', self::LEGACY_SETTINGS)->delete();
        Capsule::table(self::STATE)->where('id', 1)->update(['reason' => null, 'next_at' => 0, 'target_id' => null]);
        Capsule::table(self::INVOICES)->whereNull('draft_id')
            ->where(static function ($query): void {
                $query->where('reason', 'like', 'CONFIG_%')->orWhere('reason', 'like', 'API_KEY_%')
                    ->orWhere('reason', 'like', 'OPERATOR_%')->orWhere('reason', 'like', 'PORTAL_%')
                    ->orWhere('reason', 'like', 'FRAMEWORK_%')->orWhere('reason', 'like', 'HTTP_%');
            })
            ->update(['attempts' => 0, 'next_at' => time(), 'reason' => null, 'detail' => null]);
        Capsule::table(self::INVOICES)->whereNotNull('check_reason')->update(['check_reason' => null]);
>>>>>>> Stashed changes
    }

    public static function enqueue(int $id): void
    {
        if ($id > 0) {
            Capsule::table(self::INVOICES)->insertOrIgnore(['invoice_id' => $id, 'next_at' => time()]);
        }
    }

<<<<<<< Updated upstream
    public static function run(array $settings): void
    {
        self::locked(static function () use ($settings): void {
<<<<<<< Updated upstream
            if (!Capsule::schema()->hasColumn(self::INVOICES, 'delivery_key')) {
=======
            if (!Capsule::schema()->hasTable(self::REQUESTS) || !Capsule::schema()->hasColumn(self::STATE, 'last_cron_at')) {
>>>>>>> Stashed changes
                self::migrate();
            }
            self::process($settings);
            self::refreshStatuses($settings);
<<<<<<< Updated upstream
        });
    }

=======
=======
    /** Hata almış veya bekleyen faturayı bir sonraki çalışmada hemen denenecek hale getirir. */
    public static function retry(int $id): bool
    {
        return Capsule::table(self::INVOICES)->where('invoice_id', $id)->whereNull('draft_id')
            ->update(['attempts' => 0, 'next_at' => time(), 'reason' => null, 'detail' => null]) > 0;
    }

    /** Panelde silinen taslağı yeniden göndermek için eşleşmeyi kaldırır. Panel aynı externalId ile mevcut taslağı döndürür. */
    public static function resend(int $id): bool
    {
        return Capsule::table(self::INVOICES)->where('invoice_id', $id)
            ->update(['draft_id' => null, 'attempts' => 0, 'next_at' => time(), 'reason' => null, 'detail' => null,
                'document_status' => null, 'document_number' => null, 'draft_number' => null, 'check_reason' => null, 'public_url' => null]) > 0;
    }

    public static function resumeQueue(): void
    {
        Capsule::table(self::STATE)->where('id', 1)->update(['reason' => null, 'next_at' => 0]);
    }

    /** WHMCS cron'u (AfterCronJob) ve paneldeki "Şimdi gönder" düğmesi. */
    public static function run(array $settings, bool $force = false): void
    {
        self::locked(static function () use ($settings, $force): void {
            if (!Capsule::schema()->hasColumn(self::INVOICES, 'last_event_sequence')) {
                self::migrate();
            }
            if ($force) {
                Capsule::table(self::STATE)->where('id', 1)->update(['next_at' => 0]);
            }
            self::backfill($settings);
            self::process($settings);
>>>>>>> Stashed changes
            $cutoff = Capsule::table(self::REQUESTS)->orderByDesc('id')->offset(999)->limit(1)->value('id');
            if ($cutoff !== null) {
                Capsule::table(self::REQUESTS)->where('id', '<', $cutoff)->delete();
            }
            Capsule::table(self::STATE)->where('id', 1)->update(['last_cron_at' => time()]);
        });
    }

<<<<<<< Updated upstream
    public static function recordRequest(string $method, string $endpoint, int $status, int $duration, ?string $reason, ?int $invoiceId = null): void
    {
        Capsule::table(self::REQUESTS)->insert([
            'invoice_id' => $invoiceId, 'method' => $method, 'endpoint' => $endpoint,
=======
    /** InvoicePaid kancası: yalnızca ödenen faturayı hemen gönderir; başarısız olursa cron yeniden dener. */
    public static function sendOne(int $id, array $settings): void
    {
        self::locked(static function () use ($id, $settings): void {
            if (!Capsule::schema()->hasColumn(self::INVOICES, 'last_event_sequence')) {
                self::migrate();
            }
            $state = Capsule::table(self::STATE)->where('id', 1)->first();
            $row = Capsule::table(self::INVOICES)->where('invoice_id', $id)->first();
            if ($row === null || $row->draft_id !== null || ($state?->next_at ?? 0) > time()) {
                return;
            }
            $invoiceId = $id;
            try {
                $client = self::client($settings, $invoiceId);
            } catch (DeliveryException $error) {
                self::pause($error);
                return;
            }
            self::deliver($client, $row, $settings);
        });
    }

    public static function recordRequest(string $method, string $endpoint, int $status, int $duration, ?string $reason, ?int $invoiceId = null): void
    {
        Capsule::table(self::REQUESTS)->insert([
            'invoice_id' => $invoiceId, 'method' => $method, 'endpoint' => substr($endpoint, 0, 90),
>>>>>>> Stashed changes
            'http_status' => $status, 'duration_ms' => $duration, 'reason' => $reason, 'requested_at' => time(),
        ]);
    }

<<<<<<< Updated upstream
>>>>>>> Stashed changes
=======
    /**
     * Paneldeki "Bildirim adresi"nden gelen olayı (invoice.created, invoice.issued, …) işler.
     * @return string İşlem sonucu (kayıt için).
     */
    public static function applyEvent(string $event, array $data, ?int $sequence = null): string
    {
        Capsule::table(self::STATE)->where('id', 1)->update(['last_callback_at' => time(), 'last_callback_event' => substr($event, 0, 64)]);
        if ($event === 'ping') {
            return 'ping';
        }

        $draftId = is_string($data['invoiceId'] ?? null) && WebhookClient::isUuid($data['invoiceId']) ? strtolower($data['invoiceId']) : null;
        $external = is_string($data['externalOrderId'] ?? null) ? $data['externalOrderId'] : '';
        $invoiceId = preg_match('/^whmcs-([1-9][0-9]{0,18})$/D', $external, $match) ? (int) $match[1] : 0;

        $query = Capsule::table(self::INVOICES);
        if ($invoiceId > 0) {
            $query->where('invoice_id', $invoiceId);
        } elseif ($draftId !== null) {
            $query->where('draft_id', $draftId);
        } else {
            return 'unmatched';
        }
        $row = $query->first();
        if ($row !== null && $draftId !== null && $row->draft_id !== null && strtolower($row->draft_id) !== $draftId) {
            return 'unmatched';
        }
        if ($row === null) {
            if ($invoiceId === 0 || !Capsule::table('tblinvoices')->where('id', $invoiceId)->exists()) {
                return 'unmatched';
            }
            // Fatura modül dışından (ör. includes/hooks kanca dosyası) gönderilmiş: kayıt altına al.
            Capsule::table(self::INVOICES)->insertOrIgnore(['invoice_id' => $invoiceId, 'next_at' => time(), 'submitted_at' => time()]);
        }

        $status = is_string($data['status'] ?? null) && preg_match('/^[A-Za-z]{2,32}$/D', $data['status']) ? $data['status'] : null;
        $update = ['checked_at' => time(), 'check_reason' => null, 'reason' => null];
        if ($draftId !== null) {
            $update['draft_id'] = $draftId;
        }
        if ($status !== null) {
            $update['document_status'] = $status;
        }
        foreach (['documentNumber' => 'document_number', 'draftNumber' => 'draft_number'] as $key => $column) {
            if (is_string($data[$key] ?? null) && trim($data[$key]) !== '') {
                $update[$column] = substr(trim($data[$key]), 0, 50);
            }
        }
        // Resmi fatura bağlantısı: uzun ömürlü arşiv bağlantısı, yoksa görüntüleme bağlantısı.
        if (Capsule::schema()->hasColumn(self::INVOICES, 'public_url')) {
            $links = is_array($data['links'] ?? null) ? $data['links'] : [];
            foreach (['archive', 'view'] as $key) {
                $link = is_string($links[$key] ?? null) ? trim($links[$key]) : '';
                if ($link !== '' && strlen($link) <= 600 && preg_match('~^https://[^\s"<>]+$~D', $link)) {
                    $update['public_url'] = $link;
                    break;
                }
            }
            if (in_array($status, ['Cancelled', 'Rejected', 'Failed'], true)) {
                $update['public_url'] = null;
            }
        }
        if (is_string($data['note'] ?? null) && trim($data['note']) !== '' && in_array($status, self::STATUSES['İnceleme'], true)) {
            $update['detail'] = InvoicePayload::text($data['note'], 250);
        }
        $target = Capsule::table(self::INVOICES)->where('invoice_id', $invoiceId > 0 ? $invoiceId : $row->invoice_id);
        if ($draftId !== null) {
            $target->where(static function ($query) use ($draftId): void {
                $query->whereNull('draft_id')->orWhere('draft_id', '=', $draftId);
            });
        }
        if (Capsule::schema()->hasColumn(self::INVOICES, 'last_event_sequence')) {
            $target->where(static function ($query) use ($sequence): void {
                $query->whereNull('last_event_sequence');
                if ($sequence !== null) {
                    $query->orWhere('last_event_sequence', '<', $sequence);
                }
            });
            if ($sequence !== null) {
                $update['last_event_sequence'] = $sequence;
            }
        }
        return $target->update($update) > 0 ? 'updated' : 'unchanged';
    }

>>>>>>> Stashed changes
    private static function locked(callable $work, bool $wait = false): void
    {
        $db = Capsule::connection();
        $lock = 'gibframework:' . substr(hash('sha256', $db->getDatabaseName()), 0, 40);
        $connection = $db->getPdo();
        $acquire = $connection->prepare('SELECT GET_LOCK(?, ?)');
        $acquire->execute([$lock, $wait ? 10 : 0]);
        $locked = (int) $acquire->fetchColumn() === 1;
        $acquire->closeCursor();
        if (!$locked) {
            if ($wait) {
<<<<<<< Updated upstream
                throw new \RuntimeException('Modül kuyruğu şu anda çalışıyor. Güncellemeyi tekrar açın.');
=======
                throw new \RuntimeException('Modül kuyruğu şu anda çalışıyor. Birkaç saniye sonra tekrar deneyin.');
>>>>>>> Stashed changes
            }
            return;
        }

        try {
            $work();
        } finally {
            $release = $connection->prepare('SELECT RELEASE_LOCK(?)');
            $release->execute([$lock]);
            $release->closeCursor();
        }
    }

<<<<<<< Updated upstream
    private static function process(array $settings): void
    {
        $queue = Capsule::table(self::INVOICES);
        $state = Capsule::table(self::STATE)->where('id', 1);
        $transfer = $state->first();
        $ids = Invoice::query()->paid()->where('total', '>', 0)
=======
    /** "Başlangıç tarihi" ayarlandıysa o tarihten sonra ödenmiş ve kuyrukta olmayan faturaları ekler. */
    private static function backfill(array $settings): void
    {
        $start = trim((string) ($settings['startDate'] ?? ''));
        if (!preg_match('/^\d{4}-\d{2}-\d{2}$/D', $start) || !checkdate((int) substr($start, 5, 2), (int) substr($start, 8, 2), (int) substr($start, 0, 4))) {
            return;
        }
        $ids = Invoice::query()->paid()->where('tblinvoices.total', '>', 0)->where('tblinvoices.datepaid', '>=', $start . ' 00:00:00')
>>>>>>> Stashed changes
            ->leftJoin(self::INVOICES, self::INVOICES . '.invoice_id', '=', 'tblinvoices.id')
            ->whereNull(self::INVOICES . '.invoice_id')->orderBy('tblinvoices.id')->limit(250)->pluck('tblinvoices.id');
        if ($ids->isNotEmpty()) {
            $now = time();
<<<<<<< Updated upstream
            $queue->insertOrIgnore($ids->map(static fn ($id): array => ['invoice_id' => $id, 'next_at' => $now])->all());
        }

        if ($transfer->next_at > time()) {
            return;
        }
        $rows = $queue->whereNull('draft_id')->where('next_at', '<=', time())->orderBy('next_at')->orderBy('invoice_id')->limit(25)->get();
=======
            Capsule::table(self::INVOICES)->insertOrIgnore($ids->map(static fn ($id): array => ['invoice_id' => $id, 'next_at' => $now])->all());
        }
    }

    private static function process(array $settings): void
    {
        $state = Capsule::table(self::STATE)->where('id', 1)->first();
        if (($state?->next_at ?? 0) > time()) {
            return;
        }
        $rows = Capsule::table(self::INVOICES)->whereNull('draft_id')->where('next_at', '<=', time())
            ->orderBy('next_at')->orderBy('invoice_id')->limit(25)->get();
>>>>>>> Stashed changes
        if ($rows->isEmpty()) {
            return;
        }

<<<<<<< Updated upstream
        $deadline = hrtime(true) + 50_000_000_000;
<<<<<<< Updated upstream
        try {
            $client = new FrameworkClient($settings['frameworkUrl'] ?? '', ($settings['portalEnvironment'] ?? '') ?: 'Production');
            $targetId = $client->connectedTenant();
=======
        $invoiceId = null;
        try {
            $client = new FrameworkClient($settings['frameworkUrl'] ?? '', ($settings['portalEnvironment'] ?? '') ?: 'Production',
                static function ($method, $path, $status, $duration, $reason) use (&$invoiceId): void {
                    self::recordRequest($method, $path, $status, $duration, $reason, $invoiceId);
                });
            $targetId = $client->authorizedTenant(true);
>>>>>>> Stashed changes
            if ($transfer->target_id !== null && $targetId !== $transfer->target_id) {
                throw new DeliveryException('TARGET_TENANT_CHANGED', true, 300);
            }
            $state->update(['target_id' => $targetId, 'next_at' => 0, 'reason' => null]);
        } catch (DeliveryException $error) {
            $state->update(['next_at' => time() + max(60, $error->retryAfter), 'reason' => $error->reason]);
            return;
        }

=======
        $invoiceId = null;
        try {
            $client = self::client($settings, $invoiceId);
        } catch (DeliveryException $error) {
            self::pause($error);
            return;
        }

        $deadline = hrtime(true) + 50_000_000_000;
>>>>>>> Stashed changes
        foreach ($rows as $row) {
            if (hrtime(true) >= $deadline) {
                break;
            }
<<<<<<< Updated upstream
            $id = (int) $row->invoice_id;
<<<<<<< Updated upstream
=======
            $invoiceId = $id;
>>>>>>> Stashed changes
            $entry = Capsule::table(self::INVOICES)->where('invoice_id', $id);
            try {
                $source = Invoice::find($id);
                if ($source === null || $source->status !== 'Paid' || (float) $source->total <= 0) {
<<<<<<< Updated upstream
=======
                    if ($row->delivery_key !== null) {
                        throw new DeliveryException('WHMCS_PAYMENT_CHANGED');
                    }
>>>>>>> Stashed changes
                    $entry->delete();
                    continue;
                }
                $invoice = self::local('GetInvoice', ['invoiceid' => $id]);
                if ((int) ($invoice['invoiceid'] ?? 0) !== $id) {
                    throw new DeliveryException('WHMCS_INVOICE_RESPONSE');
                }
                if (!InvoicePayload::isEligible($invoice)) {
<<<<<<< Updated upstream
=======
                    if ($row->delivery_key !== null) {
                        throw new DeliveryException('WHMCS_PAYMENT_CHANGED');
                    }
>>>>>>> Stashed changes
                    $entry->delete();
                    continue;
                }
                $details = self::local('GetClientsDetails', ['clientid' => (int) $invoice['userid'], 'stats' => false]);
                $customer = $details['client'] ?? $details;
                if (!is_array($customer)) {
                    throw new DeliveryException('WHMCS_GET_CUSTOMER');
                }
                $currency = strtoupper((string) $source->getCurrencyCodeAttribute());
                $rate = self::exchangeRate($settings['tryRates'] ?? '', $currency);
<<<<<<< Updated upstream
                $payload = InvoicePayload::create($invoice, $customer, $currency, $rate, self::taxId($customer, (int) ($settings['taxFieldId'] ?? 0)));
                $key = $row->delivery_key ?? 'whmcs:' . $transfer->source_id . ':' . $id;
                $response = $client->createDraft($payload, $key);
                $entry->update(self::documentData($response) + ['draft_id' => strtolower($response['id']), 'submitted_at' => time(), 'reason' => null]);
=======
                $payload = InvoicePayload::create($invoice, $customer, $currency, $rate, self::taxId($customer, (int) ($settings['taxFieldId'] ?? 0)), $settings);
                $key = $row->delivery_key ?? 'whmcs:' . $transfer->source_id . ':' . $id;
                if ($row->delivery_key === null) {
                    $entry->update(['delivery_key' => $key]);
                }
                $response = $client->createDraft($payload, $key);
                $data = self::documentData($response);
                if (in_array($response['status'], ['Draft', 'AwaitingApproval', 'Approved', 'Signing'], true)) {
                    $data['check_due_at'] = 0;
                }
                $entry->update($data + ['draft_id' => strtolower($response['id']), 'submitted_at' => time(), 'reason' => null]);
>>>>>>> Stashed changes
            } catch (DeliveryException $error) {
                if ($error->pauseQueue) {
                    $state->update(['next_at' => time() + max(60, $error->retryAfter), 'reason' => $error->reason]);
                    break;
                }
                $attempt = min(10, $row->attempts + 1);
                $entry->update([
                    'attempts' => $attempt,
                    'next_at' => time() + max(min(21600, 300 * (2 ** ($attempt - 1))), $error->retryAfter),
                    'reason' => $error->reason,
                ]);
            }
        }
    }

    private static function refreshStatuses(array $settings): void
    {
        $rows = Capsule::table(self::INVOICES)->whereNotNull('draft_id')->where('check_due_at', '<=', time())
            ->orderBy('check_due_at')->orderBy('invoice_id')->limit(20)->get();
        if ($rows->isEmpty()) {
            return;
        }

        $deadline = hrtime(true) + 20_000_000_000;
<<<<<<< Updated upstream
        try {
            $client = new FrameworkClient($settings['frameworkUrl'] ?? '', '');
=======
        $invoiceId = null;
        try {
            $client = new FrameworkClient($settings['frameworkUrl'] ?? '', ($settings['portalEnvironment'] ?? '') ?: 'Production',
                static function ($method, $path, $status, $duration, $reason) use (&$invoiceId): void {
                    self::recordRequest($method, $path, $status, $duration, $reason, $invoiceId);
                });
>>>>>>> Stashed changes
            $target = Capsule::table(self::STATE)->where('id', 1)->value('target_id');
            if ($client->authorizedTenant() !== $target) {
                throw new DeliveryException('TARGET_TENANT_CHANGED', true, 300);
            }
        } catch (DeliveryException $error) {
            self::deferStatus(null, $error);
            return;
        }

        foreach ($rows as $row) {
            if (hrtime(true) >= $deadline) {
                break;
            }
<<<<<<< Updated upstream
            try {
                $data = self::documentData($client->invoice($row->draft_id));
                Capsule::table(self::INVOICES)->where('invoice_id', $row->invoice_id)->update($data);
=======
            $invoiceId = (int) $row->invoice_id;
            $entry = Capsule::table(self::INVOICES)->where('invoice_id', $invoiceId);
            try {
                $document = $client->invoice($row->draft_id);
                $entry->update(self::documentData($document));
>>>>>>> Stashed changes
            } catch (DeliveryException $error) {
                self::deferStatus($error->pauseQueue ? null : (int) $row->invoice_id, $error);
                if ($error->pauseQueue) {
                    break;
                }
<<<<<<< Updated upstream
=======
                continue;
            }
            if (!in_array($document['status'], ['Draft', 'AwaitingApproval', 'Approved', 'Signing'], true)) {
                continue;
            }
            if ($document['status'] === 'Signing' && ($document['issuanceChannel'] ?? '') === 'GibPortal'
                && ($document['portalEnvironment'] ?? null) !== 'Test'
                && empty($document['portalReconciliationRequired'])) {
                continue;
            }
            try {
                if (!Invoice::query()->paid()->whereKey($invoiceId)->where('total', '>', 0)->exists()) {
                    throw new DeliveryException('WHMCS_PAYMENT_CHANGED');
                }
                $updated = $client->advance($document, $target);
                $entry->update(self::documentData($updated));
            } catch (DeliveryException $error) {
                self::deferStatus($invoiceId, $error);
>>>>>>> Stashed changes
=======
            $invoiceId = (int) $row->invoice_id;
            if (!self::deliver($client, $row, $settings)) {
                break;
>>>>>>> Stashed changes
            }
        }
    }

<<<<<<< Updated upstream
    private static function documentData(array $document): array
    {
        $status = substr($document['status'], 0, 32);
        $number = $document['documentNumber'] ?? null;
        $now = time();
        return [
            'document_status' => $status,
            'document_number' => is_string($number) ? substr($number, 0, 50) : null,
            'checked_at' => $now,
            'check_due_at' => $now + (in_array($status, self::STATUSES['Kesinleşen'], true) ? 21600 : 300),
            'check_reason' => null,
        ];
    }

    private static function deferStatus(?int $id, DeliveryException $error): void
    {
        $now = time();
        $query = Capsule::table(self::INVOICES)->whereNotNull('draft_id');
        $id === null ? $query->where('check_due_at', '<=', $now) : $query->where('invoice_id', $id);
        $query->update([
            'check_due_at' => $now + max(300, $error->retryAfter),
            'check_reason' => $error->reason,
        ]);
=======
    private static function client(array $settings, ?int &$invoiceId): WebhookClient
    {
        return new WebhookClient((string) ($settings['webhookUrl'] ?? ''), (string) ($settings['signingSecret'] ?? ''),
            static function ($method, $path, $status, $duration, $reason) use (&$invoiceId): void {
                self::recordRequest($method, $path, $status, $duration, $reason, $invoiceId);
            });
    }

    private static function pause(DeliveryException $error): void
    {
        Capsule::table(self::STATE)->where('id', 1)->update(['next_at' => time() + max(60, $error->retryAfter), 'reason' => $error->reason]);
    }

    /** @return bool false ise kuyruk durdu (adres / imza / sunucu sorunu). */
    private static function deliver(WebhookClient $client, object $row, array $settings): bool
    {
        $id = (int) $row->invoice_id;
        $entry = Capsule::table(self::INVOICES)->where('invoice_id', $id);
        try {
            $source = Invoice::find($id);
            if ($source === null || $source->status !== 'Paid' || (float) $source->total <= 0) {
                $entry->delete();
                return true;
            }
            $invoice = self::local('GetInvoice', ['invoiceid' => $id]);
            if ((int) ($invoice['invoiceid'] ?? 0) !== $id) {
                throw new DeliveryException('WHMCS_INVOICE_RESPONSE');
            }
            if (!InvoicePayload::isEligible($invoice)) {
                $entry->delete();
                return true;
            }
            $details = self::local('GetClientsDetails', ['clientid' => (int) $invoice['userid'], 'stats' => false]);
            $customer = $details['client'] ?? $details;
            if (!is_array($customer)) {
                throw new DeliveryException('WHMCS_GET_CUSTOMER');
            }
            $currency = strtoupper((string) ($customer['currency_code'] ?? ''));
            if ($currency === '' && method_exists($source, 'getCurrencyCodeAttribute')) {
                $currency = strtoupper((string) $source->getCurrencyCodeAttribute());
            }
            $currency = $currency === 'TL' ? 'TRY' : $currency;
            $payload = InvoicePayload::create($invoice, $customer, $currency,
                self::exchangeRate((string) ($settings['tryRates'] ?? ''), $currency),
                self::customField($customer, (string) ($settings['taxField'] ?? '')) ?? (trim((string) ($customer['tax_id'] ?? '')) ?: null),
                self::customField($customer, (string) ($settings['taxOfficeField'] ?? '')),
                $settings);

            $response = $client->send($payload);
            $now = time();
            $entry->update([
                'draft_id' => strtolower($response['invoiceId']),
                'submitted_at' => $now,
                'checked_at' => $now,
                'reason' => null,
            ]);
            $entry->whereNull('last_event_sequence')->update([
                'document_status' => is_string($response['status'] ?? null) ? substr($response['status'], 0, 32) : 'Draft',
                'draft_number' => is_string($response['draftNumber'] ?? null) ? substr($response['draftNumber'], 0, 50) : null,
                'detail' => is_string($response['warning'] ?? null) && $response['warning'] !== '' ? InvoicePayload::text($response['warning'], 250) : null,
            ]);
            Capsule::table(self::STATE)->where('id', 1)->update(['last_success_at' => $now, 'reason' => null, 'next_at' => 0]);
            return true;
        } catch (DeliveryException $error) {
            if ($error->pauseQueue) {
                self::pause($error);
                return false;
            }
            $attempt = min(10, (int) $row->attempts + 1);
            $entry->update([
                'attempts' => $attempt,
                'next_at' => time() + max(min(21600, 300 * (2 ** ($attempt - 1))), $error->retryAfter),
                'reason' => $error->reason,
                'detail' => $error->detail === null ? null : InvoicePayload::text($error->detail, 250),
            ]);
            return true;
        } catch (\JsonException) {
            $entry->update(['attempts' => min(10, (int) $row->attempts + 1), 'next_at' => time() + 3600, 'reason' => 'PAYLOAD_JSON']);
            return true;
        }
>>>>>>> Stashed changes
    }

    private static function local(string $command, array $params): array
    {
        $response = localAPI($command, $params);
        if (!is_array($response) || ($response['result'] ?? null) !== 'success') {
            throw new DeliveryException($command === 'GetInvoice' ? 'WHMCS_GET_INVOICE' : 'WHMCS_GET_CUSTOMER');
        }
        return $response;
    }

<<<<<<< Updated upstream
    private static function taxId(array $customer, int $fieldId): ?string
    {
        if ($fieldId > 0) {
            foreach ($customer['customfields'] ?? [] as $field) {
                if ((int) ($field['id'] ?? 0) === $fieldId && trim((string) ($field['value'] ?? '')) !== '') {
                    return (string) $field['value'];
                }
=======
    /** Müşteri özel alanını adına (ör. "TC Kimlik / Vergi No") veya ID'sine göre okur. */
    private static function customField(array $customer, string $field): ?string
    {
        $field = trim($field);
        if ($field === '') {
            return null;
        }
        $fieldId = ctype_digit($field) ? (int) $field
            : (int) Capsule::table('tblcustomfields')->where('type', 'client')->where('fieldname', $field)->value('id');
        if ($fieldId <= 0) {
            return null;
        }
        foreach ($customer['customfields'] ?? [] as $custom) {
            if ((int) ($custom['id'] ?? 0) === $fieldId && trim((string) ($custom['value'] ?? '')) !== '') {
                return trim((string) $custom['value']);
>>>>>>> Stashed changes
            }
        }
        return null;
    }

    private static function exchangeRate(string $rates, string $currency): ?float
    {
<<<<<<< Updated upstream
        foreach (preg_split('/[\s,;]+/', strtoupper(trim($rates)), -1, PREG_SPLIT_NO_EMPTY) as $entry) {
=======
        foreach (preg_split('/[\s,;]+/', strtoupper(trim($rates)), -1, PREG_SPLIT_NO_EMPTY) ?: [] as $entry) {
>>>>>>> Stashed changes
            $parts = explode('=', $entry, 2);
            if ($parts[0] === $currency && is_numeric($parts[1] ?? null)) {
                return (float) $parts[1];
            }
        }
        return null;
    }
}
