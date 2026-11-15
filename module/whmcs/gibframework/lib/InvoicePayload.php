<?php

declare(strict_types=1);

namespace GibFramework\Whmcs;

defined('WHMCS') || exit;

<<<<<<< Updated upstream
final class InvoicePayload
{
=======
/**
 * WHMCS faturasını GIB Framework gelen adresinin beklediği JSON'a çevirir:
 * {event, externalId, orderNumber, currency, pricesIncludeTax, customer{...}, lines[{name, quantity, unitPrice, vatRate, discount}], notes[]}
 */
final class InvoicePayload
{
    /** GIB Framework'ün kabul ettiği KDV oranları (yüzde × 100). */
    private const VAT_RATES = [0, 100, 800, 1000, 1800, 2000];

    public static function externalId(int $invoiceId): string
    {
        // Paneldeki hazır kanca dosyasıyla aynı kimlik: iki yoldan gelen fatura panelde tek taslak olur.
        return 'whmcs-' . $invoiceId;
    }

>>>>>>> Stashed changes
    public static function isEligible(array $invoice): bool
    {
        return ($invoice['status'] ?? null) === 'Paid' && self::cents($invoice['total'] ?? null) > 0;
    }

<<<<<<< Updated upstream
<<<<<<< Updated upstream
    public static function create(array $invoice, array $client, string $currency, ?float $rate, ?string $taxId): array
=======
    public static function create(array $invoice, array $client, string $currency, ?float $rate, ?string $taxId, array $settings = []): array
>>>>>>> Stashed changes
    {
        if (!preg_match('/^[A-Z]{3}$/D', $currency) || ($currency !== 'TRY' && ($rate === null || !is_finite($rate) || $rate <= 0))) {
            throw new DeliveryException('INVOICE_EXCHANGE_RATE');
=======
    public static function create(array $invoice, array $client, string $currency, ?float $rate, ?string $taxId, ?string $taxOffice, array $settings = []): array
    {
        if (!preg_match('/^[A-Z]{3}$/D', $currency)) {
            throw new DeliveryException('INVOICE_CURRENCY');
        }
        if ($currency !== 'TRY' && ($rate === null || !is_finite($rate) || $rate <= 0)) {
            throw new DeliveryException('INVOICE_EXCHANGE_RATE', false, 0, $currency . ' için modül ayarlarında TL kuru tanımlı değil.');
>>>>>>> Stashed changes
        }

        $first = self::text($client['firstname'] ?? '', 100);
        $last = self::text($client['lastname'] ?? '', 100);
        $company = self::text($client['companyname'] ?? '', 250);
        $city = self::text($client['city'] ?? '', 100);
        $state = self::text($client['state'] ?? '', 100);
        $country = self::text($client['countrycode'] ?? $client['country'] ?? '', 100);
<<<<<<< Updated upstream
        $taxId = preg_replace('/\s+/', '', $taxId ?? (string) ($client['tax_id'] ?? ''));
        if ($taxId !== '' && !preg_match('/^[0-9]{10,11}$/D', $taxId)) {
            throw new DeliveryException('CUSTOMER_TAX_ID');
        }
        if ($taxId === '' && $company !== '') {
            throw new DeliveryException('COMPANY_TAX_ID_REQUIRED');
        }

        $person = strlen($taxId) !== 10;
        $fullName = trim($first . ' ' . $last);
        $title = $person ? $fullName : ($company !== '' ? $company : $fullName);
        if ($title === '' || ($city === '' && $state === '') || $country === '') {
            throw new DeliveryException('CUSTOMER_BILLING_DETAILS');
        }

        $number = self::text($invoice['invoicenum'] ?? '', 250);
        $number = $number === '' ? (string) $invoice['invoiceid'] : $number;
        $email = self::text($client['email'] ?? '', 200);
        $phone = self::text($client['phonenumber'] ?? '', 30);
        $taxId = $taxId !== '' ? $taxId : '11111111111';
<<<<<<< Updated upstream
=======
        $store = self::text($settings['storeName'] ?? '', 100) ?: self::text(\WHMCS\Config\Setting::getValue('CompanyName'), 100);
        $website = self::text($settings['storeUrl'] ?? '', 180) ?: self::text(\WHMCS\Config\Setting::getValue('SystemURL'), 180);
        $notes = ['Fatura referans numarası: ' . $number];
=======
        $taxId = preg_replace('/\D+/', '', $taxId ?? (string) ($client['tax_id'] ?? '')) ?? '';
        if ($taxId !== '' && !preg_match('/^[0-9]{10,11}$/D', $taxId)) {
            throw new DeliveryException('CUSTOMER_TAX_ID', false, 0, 'TCKN 11, VKN 10 haneli olmalı.');
        }
        $name = trim($first . ' ' . $last);
        if ($name === '' && $company === '') {
            throw new DeliveryException('CUSTOMER_BILLING_DETAILS', false, 0, 'Müşteri adı veya firma adı boş.');
        }

        // Hesap türü yalnızca kimlik numarasından: VKN (10 hane) → şirket, TCKN (11 hane) veya boş → şahıs.
        $corporate = strlen($taxId) === 10;

        $number = self::text($invoice['invoicenum'] ?? '', 50);
        $number = $number === '' ? (string) $invoice['invoiceid'] : $number;
        [$lines, $inclusive] = self::lines($invoice);

        $notes = ['Hesap türü: ' . ($corporate ? 'Şirket (VKN)' : 'Şahıs (TCKN)')];
        $store = self::text($settings['storeName'] ?? '', 100) ?: self::text(\WHMCS\Config\Setting::getValue('CompanyName'), 100);
        $website = self::text($settings['storeUrl'] ?? '', 180) ?: self::text(\WHMCS\Config\Setting::getValue('SystemURL'), 180);
>>>>>>> Stashed changes
        if ($store !== '') {
            $notes[] = 'Satış sitesi: ' . $store;
        }
        if (filter_var($website, FILTER_VALIDATE_URL) && preg_match('~^https?://~i', $website)
<<<<<<< Updated upstream
            && !array_intersect(['user', 'pass', 'query', 'fragment'], array_keys(parse_url($website)))) {
=======
            && !array_intersect(['user', 'pass', 'query', 'fragment'], array_keys(parse_url($website) ?: []))) {
>>>>>>> Stashed changes
            $notes[] = 'Site adresi: ' . $website;
        }
        if (preg_match('/^\d{4}-\d{2}-\d{2}/', (string) ($invoice['datepaid'] ?? ''), $paid) && $paid[0] !== '0000-00-00') {
            $notes[] = 'Ödeme tarihi: ' . $paid[0];
        }
<<<<<<< Updated upstream
        if (preg_match('/^\d{4}-\d{2}-\d{2}$/D', (string) ($invoice['date'] ?? '')) && $invoice['date'] !== '0000-00-00') {
            $notes[] = 'Sipariş fatura tarihi: ' . $invoice['date'];
        }
>>>>>>> Stashed changes

        return [
            'orderNumber' => self::text($number, 50),
            'currency' => $currency,
            'exchangeRate' => $currency === 'TRY' ? null : $rate,
            'customer' => [
                'taxId' => $taxId,
                'kind' => $person ? 'NaturalPerson' : 'LegalEntity',
                'regime' => $person ? 'NotATaxpayer' : 'Unknown',
                'title' => $title,
                'firstName' => $person && $first !== '' ? $first : null,
                'familyName' => $person && $last !== '' ? $last : null,
                'street' => self::text(($client['address1'] ?? '') . ' ' . ($client['address2'] ?? ''), 250),
                'district' => $state !== '' && $state !== $city ? $city : null,
                'city' => $state !== '' ? $state : $city,
                'country' => in_array(strtoupper($country), ['TR', 'TURKEY'], true) ? 'Türkiye' : $country,
                'postalCode' => self::text($client['postcode'] ?? '', 10),
                'email' => filter_var($email, FILTER_VALIDATE_EMAIL) ? $email : null,
                'phone' => $phone === '' ? null : $phone,
            ],
            'lines' => self::lines($invoice),
<<<<<<< Updated upstream
            'notes' => [sprintf('WHMCS fatura no: %s (ID: %d)', $number, $invoice['invoiceid'])],
=======
            'notes' => $notes,
>>>>>>> Stashed changes
            'saveCustomer' => false,
        ];
    }

=======
        if (self::text($invoice['paymentmethod'] ?? '', 60) !== '') {
            $notes[] = 'Ödeme yöntemi: ' . self::text($invoice['paymentmethod'], 60);
        }

        if ($currency !== 'TRY') {
            $total = self::cents($invoice['total']) / 100;
            $notes[] = sprintf('Döviz: %s · Kur: %s TL · WHMCS tutarı: %s %s',
                $currency, rtrim(rtrim(number_format($rate, 6, ',', ''), '0'), ','), number_format($total, 2, ',', '.'), $currency);
            foreach ($lines as &$line) {
                $line['unitPrice'] = round($line['unitPrice'] * $rate, 2);
                $line['discount'] = round($line['discount'] * $rate, 2);
            }
            unset($line);
        }

        return [
            'event' => 'invoice.paid',
            'externalId' => self::externalId((int) $invoice['invoiceid']),
            'orderNumber' => $number,
            'currency' => 'TRY',
            'pricesIncludeTax' => $inclusive,
            'expectedTotal' => round((self::cents($invoice['total']) + self::cents($invoice['credit'] ?? 0)) * ($currency === 'TRY' ? 1 : $rate) / 100, 2),
            'customer' => [
                'taxId' => $taxId,
                'name' => $name !== '' ? $name : $company,
                'company' => $company,
                'taxOffice' => self::text($taxOffice ?? '', 100),
                'email' => self::text($client['email'] ?? '', 200),
                'phone' => self::text($client['phonenumber'] ?? '', 30),
                'address' => self::text(($client['address1'] ?? '') . ' ' . ($client['address2'] ?? ''), 250),
                'district' => $state !== '' && $state !== $city ? $city : '',
                'city' => $state !== '' ? $state : $city,
                'postalCode' => self::text($client['postcode'] ?? '', 10),
                'country' => in_array(strtoupper($country), ['', 'TR', 'TURKEY', 'TÜRKIYE', 'TÜRKİYE'], true) ? 'TR' : $country,
                'accountType' => $corporate ? 'Corporate' : 'Individual',
            ],
            'lines' => $lines,
            'notes' => $notes,
        ];
    }

    /** @return array{0: list<array>, 1: bool} Satırlar ve fiyatların KDV dahil olup olmadığı. */
>>>>>>> Stashed changes
    private static function lines(array $invoice): array
    {
        $tax = self::cents($invoice['tax'] ?? 0);
        $credit = self::cents($invoice['credit'] ?? 0);
        $subtotal = self::cents($invoice['subtotal'] ?? null);
        $total = self::cents($invoice['total']);
        $rate = self::cents($invoice['taxrate'] ?? 0);
        if (self::cents($invoice['tax2'] ?? 0) !== 0) {
<<<<<<< Updated upstream
            throw new DeliveryException('INVOICE_SECOND_TAX');
=======
            throw new DeliveryException('INVOICE_SECOND_TAX', false, 0, 'İkinci vergi (tax2) içeren faturalar desteklenmiyor.');
>>>>>>> Stashed changes
        }
        if ($tax < 0 || $credit < 0 || $rate < 0 || $rate > 10000) {
            throw new DeliveryException('INVOICE_TAX_DATA');
        }

        $items = $invoice['items']['item'] ?? [];
        if (isset($items['amount'])) {
            $items = [$items];
        }
        $buckets = [];
        $sum = 0;
        foreach ($items as $item) {
            $amount = self::cents($item['amount'] ?? null);
            $sum += $amount;
            if ($amount === 0) {
                continue;
            }
            $vat = filter_var($item['taxed'] ?? false, FILTER_VALIDATE_BOOLEAN) ? $rate : 0;
<<<<<<< Updated upstream
=======
            if (!in_array($vat, self::VAT_RATES, true)) {
                throw new DeliveryException('INVOICE_VAT_RATE', false, 0, 'KDV oranı %' . ($vat / 100) . ' desteklenmiyor (0, 1, 8, 10, 18, 20).');
            }
>>>>>>> Stashed changes
            $buckets[$vat] ??= ['lines' => [], 'discount' => 0];
            if ($amount < 0) {
                $buckets[$vat]['discount'] -= $amount;
                continue;
            }
            $name = self::text($item['description'] ?? '', 250);
            $buckets[$vat]['lines'][] = ['name' => $name === '' ? 'Hizmet' : $name, 'amount' => $amount];
        }

        $gross = $total + $credit;
        $net = $gross - $tax;
        if ($net <= 0 || !in_array($subtotal, [$net, $gross], true) || !in_array($sum, [$net, $gross], true)) {
<<<<<<< Updated upstream
            throw new DeliveryException('INVOICE_TOTAL_MISMATCH');
        }
        $inclusive = $tax > 0 && $sum === $gross;
        $lines = [];
        $draftNet = $draftTax = 0;
=======
            throw new DeliveryException('INVOICE_TOTAL_MISMATCH', false, 0, 'Fatura satırları ile ara toplam / toplam uyuşmuyor.');
        }
        $inclusive = $tax > 0 && $sum === $gross;

        $lines = [];
        $expectedTax = 0;
>>>>>>> Stashed changes
        foreach ($buckets as $vat => $bucket) {
            $discount = $bucket['discount'];
            $factor = $inclusive ? 1 + $vat / 10000 : 1;
            foreach ($bucket['lines'] as $line) {
                $applied = min($line['amount'], $discount);
                $discount -= $applied;
<<<<<<< Updated upstream
                $unit = (int) round($line['amount'] / $factor);
                $lineNet = (int) round(($line['amount'] - $applied) / $factor);
                $draftNet += $lineNet;
                $draftTax += (int) round($lineNet * $vat / 10000);
                $lines[] = [
                    'name' => $line['name'],
                    'quantity' => 1,
                    'unitPrice' => $unit / 100,
                    'discountAmount' => ($unit - $lineNet) / 100,
                    'vatRate' => $vat / 100,
                    'vatExemptionCode' => $vat === 0 ? '351' : null,
                ];
            }
            if ($discount !== 0) {
                throw new DeliveryException('INVOICE_DISCOUNT_ALLOCATION');
            }
        }

        if ($lines === [] || $draftNet !== $net || $draftTax !== $tax) {
            throw new DeliveryException('INVOICE_TAX_RECONCILIATION');
        }

        return $lines;
=======
                $expectedTax += (int) round(($line['amount'] - $applied) / $factor * $vat / 10000);
                $lines[] = [
                    'name' => $line['name'],
                    'quantity' => 1,
                    'unitPrice' => $line['amount'] / 100,
                    'discount' => $applied / 100,
                    'vatRate' => $vat / 100,
                ];
            }
            if ($discount !== 0) {
                throw new DeliveryException('INVOICE_DISCOUNT_ALLOCATION', false, 0, 'İndirim satırı aynı KDV oranındaki hizmetlere dağıtılamadı.');
            }
        }

        if ($lines === [] || abs($expectedTax - $tax) > max(1, count($lines))) {
            throw new DeliveryException('INVOICE_TAX_RECONCILIATION', false, 0, 'Hesaplanan KDV ile WHMCS KDV tutarı uyuşmuyor.');
        }

        return [$lines, $inclusive];
>>>>>>> Stashed changes
    }

    private static function cents(mixed $value): int
    {
        if (!is_numeric($value) || abs((float) $value) > 999999999999) {
            throw new DeliveryException('INVOICE_AMOUNT_INVALID');
        }
        return (int) round((float) $value * 100);
    }

<<<<<<< Updated upstream
    private static function text(mixed $value, int $length): string
=======
    public static function text(mixed $value, int $length): string
>>>>>>> Stashed changes
    {
        $text = trim(preg_replace('/\s+/u', ' ', html_entity_decode((string) $value, ENT_QUOTES | ENT_HTML5, 'UTF-8')) ?? '');
        return preg_match('/^.{0,' . $length . '}/us', $text, $match) ? $match[0] : '';
    }
}
