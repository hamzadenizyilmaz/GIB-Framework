namespace GIBFramework.Services.Integrations;

public static class HookCode
{
    private static string Php(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);

    private const string Sender = """
        function gibframework_send(array $payload, string $endpoint, string $secret): array
        {
            $body = json_encode($payload, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_INVALID_UTF8_SUBSTITUTE | JSON_THROW_ON_ERROR);
            $ch = curl_init($endpoint);
            $response = '';
            curl_setopt_array($ch, [
                CURLOPT_POST => true,
                CURLOPT_POSTFIELDS => $body,
                CURLOPT_CONNECTTIMEOUT => 5,
                CURLOPT_TIMEOUT => 20,
                CURLOPT_PROTOCOLS => CURLPROTO_HTTPS,
                CURLOPT_FOLLOWLOCATION => false,
                CURLOPT_SSL_VERIFYPEER => true,
                CURLOPT_SSL_VERIFYHOST => 2,
                CURLOPT_WRITEFUNCTION => static function ($handle, string $chunk) use (&$response): int {
                    if (strlen($response) + strlen($chunk) > 65536) {
                        return 0;
                    }
                    $response .= $chunk;
                    return strlen($chunk);
                },
                CURLOPT_HTTPHEADER => [
                    'Content-Type: application/json',
                    'X-GibFramework-Signature: sha256=' . hash_hmac('sha256', $body, $secret),
                ],
            ]);
            $received = curl_exec($ch);
            $status = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
            $result = $received === true ? json_decode($response, true) : null;
            return [$status, is_array($result) && ($result['ok'] ?? null) === true && is_string($result['invoiceId'] ?? null)];
        }
        """;

    public static string Whmcs(string endpoint, string secret, string taxField) => $$"""
        <?php

        if (!defined('WHMCS')) {
            die('This file cannot be accessed directly');
        }

        use WHMCS\Database\Capsule;

        const GIBFRAMEWORK_ENDPOINT = '{{Php(endpoint)}}';
        const GIBFRAMEWORK_SECRET = '{{Php(secret)}}';
        const GIBFRAMEWORK_TAX_FIELD = '{{Php(taxField)}}';

        {{Sender}}

        add_hook('InvoicePaid', 1, function ($vars) {
            $id = (int) ($vars['invoiceid'] ?? 0);
            if ($id <= 0) {
                return;
            }
            $invoice = localAPI('GetInvoice', ['invoiceid' => $id]);
            if (($invoice['result'] ?? '') !== 'success' || ($invoice['status'] ?? '') !== 'Paid' || (float) ($invoice['total'] ?? 0) <= 0) {
                return;
            }

            $details = localAPI('GetClientsDetails', ['clientid' => (int) $invoice['userid'], 'stats' => false]);
            if (($details['result'] ?? '') !== 'success') {
                logActivity('GIB Framework: WHMCS fatura #' . $id . ' için müşteri bilgisi alınamadı.');
                return;
            }
            $client = $details['client'] ?? $details;
            $currency = strtoupper((string) ($client['currency_code'] ?? ''));
            if ($currency !== 'TRY' || (float) ($invoice['tax2'] ?? 0) != 0.0) {
                logActivity('GIB Framework: WHMCS fatura #' . $id . ' için kur veya ikinci vergi kontrolü gerekli.');
                return;
            }

            $taxId = (string) ($client['tax_id'] ?? '');
            $fieldId = Capsule::table('tblcustomfields')->where('type', 'client')->where('fieldname', GIBFRAMEWORK_TAX_FIELD)->value('id');
            foreach (($client['customfields'] ?? []) as $field) {
                if ($fieldId && (int) $field['id'] === (int) $fieldId && trim((string) $field['value']) !== '') {
                    $taxId = trim((string) $field['value']);
                }
            }

            $rate = (float) ($invoice['taxrate'] ?? 0);
            $inclusive = (float) $invoice['tax'] > 0 && abs((float) $invoice['subtotal'] - ((float) $invoice['total'] + (float) $invoice['credit'])) < 0.01;
            $lines = [];
            $discount = 0.0;
            $gross = 0.0;
            $items = $invoice['items']['item'] ?? [];
            foreach (isset($items['amount']) ? [$items] : $items as $item) {
                $amount = (float) $item['amount'];
                if ($amount < 0) {
                    $discount += -$amount;
                    continue;
                }
                if ($amount == 0.0) {
                    continue;
                }
                $name = trim(preg_replace('/\s+/', ' ', (string) $item['description']));
                $vat = ((int) $item['taxed'] === 1) ? $rate : 0.0;
                if (!in_array($vat, [0.0, 1.0, 8.0, 10.0, 18.0, 20.0], true)) {
                    logActivity('GIB Framework: WHMCS fatura #' . $id . ' için KDV oranı desteklenmiyor.');
                    return;
                }
                $gross += $inclusive ? $amount : $amount + round($amount * $vat / 100, 2);
                $lines[] = [
                    'name' => function_exists('mb_substr') ? mb_substr($name, 0, 250) : substr($name, 0, 250),
                    'quantity' => 1,
                    'unitPrice' => $amount,
                    'vatRate' => $vat,
                    'discount' => 0,
                ];
            }
            if ($discount > 0 || abs($gross - ((float) $invoice['total'] + (float) $invoice['credit'])) >= 0.005) {
                logActivity('GIB Framework: WHMCS fatura #' . $id . ' indirim veya tutar mutabakatı için inceleme gerekli.');
                return;
            }
            if (count($lines) === 0) {
                return;
            }

            $state = trim((string) ($client['state'] ?? ''));
            $city = trim((string) ($client['city'] ?? ''));
            $payload = [
                'event' => 'invoice.paid',
                'externalId' => 'whmcs-' . $invoice['invoiceid'],
                'orderNumber' => (string) (($invoice['invoicenum'] ?? '') !== '' ? $invoice['invoicenum'] : $invoice['invoiceid']),
                'currency' => $currency,
                'pricesIncludeTax' => $inclusive,
                'expectedTotal' => round((float) $invoice['total'] + (float) $invoice['credit'], 2),
                'customer' => [
                    'taxId' => $taxId,
                    'name' => trim(($client['firstname'] ?? '') . ' ' . ($client['lastname'] ?? '')),
                    'company' => (string) ($client['companyname'] ?? ''),
                    'email' => (string) ($client['email'] ?? ''),
                    'phone' => (string) ($client['phonenumber'] ?? ''),
                    'address' => trim(($client['address1'] ?? '') . ' ' . ($client['address2'] ?? '')),
                    'district' => $state !== '' ? $city : '',
                    'city' => $state !== '' ? $state : $city,
                    'postalCode' => (string) ($client['postcode'] ?? ''),
                    'country' => (string) ($client['countrycode'] ?? 'TR'),
                ],
                'lines' => $lines,
                'notes' => [],
            ];

            [$status, $accepted] = gibframework_send($payload, GIBFRAMEWORK_ENDPOINT, GIBFRAMEWORK_SECRET);
            logActivity('GIB Framework: WHMCS fatura #' . $id . ($accepted && $status >= 200 && $status < 300 ? ' aktarıldı' : ' aktarılamadı') . ' (HTTP ' . $status . ')');
        });
        """;

    public static string WiseCp(string endpoint, string secret) => $$"""
        <?php

        const GIBFRAMEWORK_ENDPOINT = '{{Php(endpoint)}}';
        const GIBFRAMEWORK_SECRET = '{{Php(secret)}}';

        {{Sender}}

        function gibframework_pick(array $source, array $keys, $default = '')
        {
            foreach ($keys as $key) {
                if (isset($source[$key]) && $source[$key] !== '' && !is_array($source[$key])) {
                    return $source[$key];
                }
            }
            return $default;
        }

        \Hook::add('action:invoice.status_changed', 10, function ($snapshot, $status) {
            $id = (int) ($snapshot['id'] ?? 0);
            if ($status !== 'paid' || $id <= 0) {
                return;
            }

            try {
                $result = \WISECP\Api\Kernel::internal('Invoices/GetInvoice', ['id' => $id]);
                $invoice = $result['data'] ?? [];
                if (($invoice['status'] ?? '') !== 'paid' || !is_numeric($invoice['total'] ?? null)) {
                    throw new \RuntimeException('WISECP_INVOICE_RESPONSE');
                }
                $currencies = \WISECP\Api\Kernel::internal('Reference/GetCurrencies');
                $codes = array_column($currencies['data'] ?? [], 'code', 'id');
                if (($codes[$invoice['currency_id'] ?? 0] ?? null) !== 'TRY') {
                    throw new \RuntimeException('WISECP_INVOICE_CURRENCY');
                }
                $user = $invoice['user_data'] ?? [];
                if (is_string($user)) {
                    $user = json_decode($user, true) ?: [];
                }
                $address = $user['address'] ?? [];
                if (is_string($address)) {
                    $address = json_decode($address, true) ?: ['address' => $address];
                }

                $inclusive = ($invoice['taxation_type'] ?? '') === 'inclusive';
                $invoiceRate = (float) ($invoice['tax_rate'] ?? 0);
                $lines = [];
                $gross = 0.0;
                foreach ($invoice['items'] ?? [] as $item) {
                    $quantity = (int) ($item['quantity'] ?? 0);
                    $amount = (float) ($item['amount'] ?? 0);
                    $total = $item['total_amount'] ?? null;
                    if ($quantity < 1 || $amount < 0 || !is_numeric($total) || (float) $total < 0
                        || (float) $total > $quantity * $amount + 0.005 || (float) ($item['additional_tax_total'] ?? 0) != 0.0) {
                        throw new \RuntimeException('WISECP_INVOICE_LINE');
                    }
                    $rate = (float) ($item['tax_rate'] ?? -1);
                    $rate = !empty($item['tax_exempt']) || !empty($invoice['taxfree']) ? 0.0 : ($rate === -1.0 ? $invoiceRate : $rate);
                    if (!in_array($rate, [0.0, 1.0, 8.0, 10.0, 18.0, 20.0], true)) {
                        throw new \RuntimeException('WISECP_INVOICE_TAX_RATE');
                    }
                    $name = trim((string) ($item['description'] ?? ''));
                    preg_match('/^.{0,250}/us', $name, $match);
                    $gross += $inclusive ? (float) $total : (float) $total + round((float) $total * $rate / 100, 2);
                    $lines[] = [
                        'name' => ($match[0] ?? '') ?: 'Hizmet',
                        'quantity' => $quantity,
                        'unitPrice' => $amount,
                        'discount' => max(0, round($quantity * $amount - (float) $total, 2)),
                        'vatRate' => $rate,
                    ];
                }
                if ($lines === [] || $gross <= 0 || abs($gross - (float) $invoice['total']) >= 0.005) {
                    throw new \RuntimeException('WISECP_INVOICE_TOTAL_MISMATCH');
                }

                $name = gibframework_pick($user, ['full_name'], trim(gibframework_pick($user, ['name']) . ' ' . gibframework_pick($user, ['surname'])));
                $payload = [
                    'event' => 'invoice.paid',
                    'externalId' => 'wisecp-' . $id,
                    'orderNumber' => (string) gibframework_pick($invoice, ['number', 'id']),
                    'currency' => 'TRY',
                    'pricesIncludeTax' => $inclusive,
                    'expectedTotal' => round((float) $invoice['total'], 2),
                    'customer' => [
                        'taxId' => (string) gibframework_pick($user, ['company_tax_number', 'tax_number', 'identity', 'identity_number']),
                        'name' => $name,
                        'company' => (string) gibframework_pick($user, ['company_name']),
                        'taxOffice' => (string) gibframework_pick($user, ['company_tax_office', 'tax_office']),
                        'email' => (string) gibframework_pick($user, ['email']),
                        'phone' => (string) gibframework_pick($user, ['gsm', 'phone', 'landline_phone']),
                        'address' => (string) gibframework_pick((array) $address, ['address']),
                        'district' => (string) gibframework_pick((array) $address, ['counti', 'district']),
                        'city' => (string) gibframework_pick((array) $address, ['city']),
                        'postalCode' => (string) gibframework_pick((array) $address, ['zipcode', 'postcode']),
                        'country' => (string) gibframework_pick((array) $address, ['country_code', 'country'], 'TR'),
                    ],
                    'lines' => $lines,
                    'notes' => [],
                ];

                [$http, $accepted] = gibframework_send($payload, GIBFRAMEWORK_ENDPOINT, GIBFRAMEWORK_SECRET);
                if (!$accepted || $http < 200 || $http >= 300) {
                    error_log('GIB Framework: WISECP fatura #' . $id . ' aktarılamadı (HTTP ' . $http . ').');
                }
            } catch (\Throwable $error) {
                error_log('GIB Framework: WISECP fatura #' . $id . ' aktarımı tamamlanamadı (' . get_class($error) . ').');
            }
        });
        """;

    public static string Curl(string endpoint, string secret) => $$"""
        #!/usr/bin/env bash
        BODY='{"externalId":"SIPARIS-1001","orderNumber":"1001","currency":"TRY","pricesIncludeTax":false,"customer":{"taxId":"11111111111","name":"Ayşe Yılmaz","email":"ayse@example.com","phone":"05321234567","address":"Atatürk Cad. No:1","district":"Kadıköy","city":"İstanbul"},"lines":[{"name":"Web hosting (1 yıl)","quantity":1,"unitPrice":1000,"vatRate":20}]}'
        SECRET='{{secret}}'
        SIGNATURE="sha256=$(printf '%s' "$BODY" | openssl dgst -sha256 -hmac "$SECRET" | sed 's/^.* //')"
        curl -X POST '{{endpoint}}' \
          -H 'Content-Type: application/json' \
          -H "X-GibFramework-Signature: $SIGNATURE" \
          --data "$BODY"
        """;
}
