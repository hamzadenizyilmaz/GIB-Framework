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
    public const STATUSES = [
        'Taslak' => ['Draft', 'Validating', 'Validated', 'AwaitingApproval', 'Approved', 'Signing'],
        'İmzalı / iletimde' => ['Signed', 'Queued', 'Transmitting'],
        'Kesinleşen' => ['Sent', 'Acknowledged', 'Delivered', 'Accepted'],
        'İnceleme' => ['InDoubt', 'Rejected', 'Cancelled', 'Objected', 'Failed'],
    ];

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
        Capsule::table(self::STATE)->where('id', 1)->whereIn('reason', ['CONFIG_LEGACY_ID', 'LEGACY_INTEGRATION_CHANGED'])
            ->update(['reason' => null, 'next_at' => 0]);
    }

    public static function enqueue(int $id): void
    {
        if ($id > 0) {
            Capsule::table(self::INVOICES)->insertOrIgnore(['invoice_id' => $id, 'next_at' => time()]);
        }
    }

    public static function run(array $settings): void
    {
        self::locked(static function () use ($settings): void {
            if (!Capsule::schema()->hasColumn(self::INVOICES, 'delivery_key')) {
                self::migrate();
            }
            self::process($settings);
            self::refreshStatuses($settings);
        });
    }

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
                throw new \RuntimeException('Modül kuyruğu şu anda çalışıyor. Güncellemeyi tekrar açın.');
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

    private static function process(array $settings): void
    {
        $queue = Capsule::table(self::INVOICES);
        $state = Capsule::table(self::STATE)->where('id', 1);
        $transfer = $state->first();
        $ids = Invoice::query()->paid()->where('total', '>', 0)
            ->leftJoin(self::INVOICES, self::INVOICES . '.invoice_id', '=', 'tblinvoices.id')
            ->whereNull(self::INVOICES . '.invoice_id')->orderBy('tblinvoices.id')->limit(250)->pluck('tblinvoices.id');
        if ($ids->isNotEmpty()) {
            $now = time();
            $queue->insertOrIgnore($ids->map(static fn ($id): array => ['invoice_id' => $id, 'next_at' => $now])->all());
        }

        if ($transfer->next_at > time()) {
            return;
        }
        $rows = $queue->whereNull('draft_id')->where('next_at', '<=', time())->orderBy('next_at')->orderBy('invoice_id')->limit(25)->get();
        if ($rows->isEmpty()) {
            return;
        }

        $deadline = hrtime(true) + 50_000_000_000;
        try {
            $client = new FrameworkClient($settings['frameworkUrl'] ?? '', ($settings['portalEnvironment'] ?? '') ?: 'Production');
            $targetId = $client->connectedTenant();
            if ($transfer->target_id !== null && $targetId !== $transfer->target_id) {
                throw new DeliveryException('TARGET_TENANT_CHANGED', true, 300);
            }
            $state->update(['target_id' => $targetId, 'next_at' => 0, 'reason' => null]);
        } catch (DeliveryException $error) {
            $state->update(['next_at' => time() + max(60, $error->retryAfter), 'reason' => $error->reason]);
            return;
        }

        foreach ($rows as $row) {
            if (hrtime(true) >= $deadline) {
                break;
            }
            $id = (int) $row->invoice_id;
            $entry = Capsule::table(self::INVOICES)->where('invoice_id', $id);
            try {
                $source = Invoice::find($id);
                if ($source === null || $source->status !== 'Paid' || (float) $source->total <= 0) {
                    $entry->delete();
                    continue;
                }
                $invoice = self::local('GetInvoice', ['invoiceid' => $id]);
                if ((int) ($invoice['invoiceid'] ?? 0) !== $id) {
                    throw new DeliveryException('WHMCS_INVOICE_RESPONSE');
                }
                if (!InvoicePayload::isEligible($invoice)) {
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
                $payload = InvoicePayload::create($invoice, $customer, $currency, $rate, self::taxId($customer, (int) ($settings['taxFieldId'] ?? 0)));
                $key = $row->delivery_key ?? 'whmcs:' . $transfer->source_id . ':' . $id;
                $response = $client->createDraft($payload, $key);
                $entry->update(self::documentData($response) + ['draft_id' => strtolower($response['id']), 'submitted_at' => time(), 'reason' => null]);
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
        try {
            $client = new FrameworkClient($settings['frameworkUrl'] ?? '', '');
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
            try {
                $data = self::documentData($client->invoice($row->draft_id));
                Capsule::table(self::INVOICES)->where('invoice_id', $row->invoice_id)->update($data);
            } catch (DeliveryException $error) {
                self::deferStatus($error->pauseQueue ? null : (int) $row->invoice_id, $error);
                if ($error->pauseQueue) {
                    break;
                }
            }
        }
    }

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
    }

    private static function local(string $command, array $params): array
    {
        $response = localAPI($command, $params);
        if (!is_array($response) || ($response['result'] ?? null) !== 'success') {
            throw new DeliveryException($command === 'GetInvoice' ? 'WHMCS_GET_INVOICE' : 'WHMCS_GET_CUSTOMER');
        }
        return $response;
    }

    private static function taxId(array $customer, int $fieldId): ?string
    {
        if ($fieldId > 0) {
            foreach ($customer['customfields'] ?? [] as $field) {
                if ((int) ($field['id'] ?? 0) === $fieldId && trim((string) ($field['value'] ?? '')) !== '') {
                    return (string) $field['value'];
                }
            }
        }
        return null;
    }

    private static function exchangeRate(string $rates, string $currency): ?float
    {
        foreach (preg_split('/[\s,;]+/', strtoupper(trim($rates)), -1, PREG_SPLIT_NO_EMPTY) as $entry) {
            $parts = explode('=', $entry, 2);
            if ($parts[0] === $currency && is_numeric($parts[1] ?? null)) {
                return (float) $parts[1];
            }
        }
        return null;
    }
}
