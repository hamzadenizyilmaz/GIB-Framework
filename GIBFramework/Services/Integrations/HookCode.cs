namespace GIBFramework.Services.Integrations;

public static class HookCode
{
    private static string Php(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);

    private const string Sender = """
        function gibframework_send(array $payload, string $endpoint, string $secret): array
        {
            $body = json_encode($payload, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
            $ch = curl_init($endpoint);
            curl_setopt_array($ch, [
                CURLOPT_POST => true,
                CURLOPT_POSTFIELDS => $body,
                CURLOPT_RETURNTRANSFER => true,
                CURLOPT_TIMEOUT => 20,
                CURLOPT_HTTPHEADER => [
                    'Content-Type: application/json',
                    'X-GibFramework-Signature: sha256=' . hash_hmac('sha256', $body, $secret),
                ],
            ]);
            $response = curl_exec($ch);
            $status = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
            $error = curl_error($ch);
            curl_close($ch);
            return [$status, $response === false ? $error : $response];
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
            $invoice = localAPI('GetInvoice', ['invoiceid' => (int) $vars['invoiceid']]);
            if (($invoice['result'] ?? '') !== 'success') {
                return;
            }

            $details = localAPI('GetClientsDetails', ['clientid' => (int) $invoice['userid'], 'stats' => false]);
            $client = $details['client'] ?? $details;

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
            foreach (($invoice['items']['item'] ?? []) as $item) {
                $amount = (float) $item['amount'];
                if ($amount < 0) {
                    $discount += -$amount;
                    continue;
                }
                if ($amount == 0.0) {
                    continue;
                }
                $name = trim(preg_replace('/\s+/', ' ', (string) $item['description']));
                $lines[] = [
                    'name' => function_exists('mb_substr') ? mb_substr($name, 0, 250) : substr($name, 0, 250),
                    'quantity' => 1,
                    'unitPrice' => $amount,
                    'vatRate' => ((int) $item['taxed'] === 1) ? $rate : 0,
                    'discount' => 0,
                ];
            }
            if ($discount > 0 && count($lines) > 0) {
                usort($lines, fn ($a, $b) => $b['unitPrice'] <=> $a['unitPrice']);
                $lines[0]['discount'] = min($discount, $lines[0]['unitPrice']);
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
                'currency' => (string) ($client['currency_code'] ?? 'TRY'),
                'pricesIncludeTax' => $inclusive,
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

            [$status, $response] = gibframework_send($payload, GIBFRAMEWORK_ENDPOINT, GIBFRAMEWORK_SECRET);
            logActivity('GIB Framework: WHMCS fatura #' . $invoice['invoiceid'] . ' gönderildi (HTTP ' . $status . ')');
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

        Hook::add('action:invoice.status_changed', 10, function ($invoice, $status, $old_status, $options = []) {
            if ($status !== 'paid' || $old_status === 'paid' || !is_array($invoice)) {
                return;
            }

            $user = $invoice['user_data'] ?? [];
            if (is_string($user)) {
                $user = json_decode($user, true) ?: [];
            }
            $address = $user['address'] ?? [];
            if (is_string($address)) {
                $address = json_decode($address, true) ?: ['address' => $address];
            }

            $subtotal = (float) gibframework_pick($invoice, ['subtotal'], 0);
            $tax = (float) gibframework_pick($invoice, ['tax'], 0);
            $rate = (float) gibframework_pick($invoice, ['taxrate', 'tax_rate'], $subtotal > 0 ? round($tax / $subtotal * 100, 2) : 20);

            $items = [];
            if (class_exists('Invoices') && method_exists('Invoices', 'get_items')) {
                $items = Invoices::get_items((int) $invoice['id']) ?: [];
            }

            $lines = [];
            foreach ($items as $item) {
                $item = (array) $item;
                $quantity = (float) gibframework_pick($item, ['quantity'], 1);
                $amount = (float) gibframework_pick($item, ['amount', 'total_amount', 'total'], 0);
                if ($amount <= 0) {
                    continue;
                }
                $lines[] = [
                    'name' => mb_substr(trim((string) gibframework_pick($item, ['description', 'name', 'title'], 'Hizmet')), 0, 250),
                    'quantity' => $quantity > 0 ? $quantity : 1,
                    'unitPrice' => $amount,
                    'vatRate' => $rate,
                ];
            }
            if (count($lines) === 0 && $subtotal > 0) {
                $lines[] = ['name' => 'Fatura #' . gibframework_pick($invoice, ['number', 'id']), 'quantity' => 1, 'unitPrice' => $subtotal, 'vatRate' => $rate];
            }
            if (count($lines) === 0) {
                return;
            }

            $name = gibframework_pick($user, ['full_name'], trim(gibframework_pick($user, ['name']) . ' ' . gibframework_pick($user, ['surname'])));
            $payload = [
                'event' => 'invoice.paid',
                'externalId' => 'wisecp-' . $invoice['id'],
                'orderNumber' => (string) gibframework_pick($invoice, ['number', 'id']),
                'currency' => (string) gibframework_pick($invoice, ['currency_code', 'currency'], 'TRY'),
                'pricesIncludeTax' => false,
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

            gibframework_send($payload, GIBFRAMEWORK_ENDPOINT, GIBFRAMEWORK_SECRET);
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
