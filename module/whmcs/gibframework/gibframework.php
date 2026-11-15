<?php

declare(strict_types=1);

use GibFramework\Whmcs\Queue;
use WHMCS\Config\Setting;

defined('WHMCS') || exit;

<<<<<<< Updated upstream
<<<<<<< Updated upstream
=======
const GIBFRAMEWORK_MODULE_VERSION = '1.3.0';

>>>>>>> Stashed changes
require_once __DIR__ . '/lib/FrameworkClient.php';
=======
const GIBFRAMEWORK_MODULE_VERSION = '2.3.2';

require_once __DIR__ . '/lib/WebhookClient.php';
require_once __DIR__ . '/lib/InvoicePayload.php';
>>>>>>> Stashed changes
require_once __DIR__ . '/lib/Queue.php';

function gibframework_config(): array
{
    return [
        'name' => 'GIB Framework',
<<<<<<< Updated upstream
<<<<<<< Updated upstream
        'description' => 'Ödenmiş WHMCS faturalarını GIB Framework taslak kuyruğuna aktarır.',
        'version' => '1.1.0',
=======
        'description' => 'Ödenmiş WHMCS faturalarını GIB Framework’e aktarır, onaylar ve belge durumlarını takip eder.',
        'version' => GIBFRAMEWORK_MODULE_VERSION,
>>>>>>> Stashed changes
        'author' => 'GIB Framework',
        'fields' => [
            'enabled' => ['FriendlyName' => 'Otomatik aktarım', 'Type' => 'yesno', 'Description' => 'Geçmiş ve yeni ödenmiş faturaları işle.'],
            'frameworkUrl' => ['FriendlyName' => 'GIB Framework adresi', 'Type' => 'text', 'Size' => '60', 'Description' => 'Panelin HTTPS kök adresi.'],
            'portalEnvironment' => ['FriendlyName' => 'GİB Portal ortamı', 'Type' => 'dropdown', 'Options' => 'Production,Test', 'Default' => 'Production'],
            'taxFieldId' => ['FriendlyName' => 'VKN/TCKN özel alan ID', 'Type' => 'text', 'Size' => '8', 'Description' => 'Boş bırakılırsa WHMCS Tax ID kullanılır.'],
<<<<<<< Updated upstream
=======
            'storeName' => ['FriendlyName' => 'Satış sitesi adı', 'Type' => 'text', 'Size' => '45', 'Description' => 'Fatura notunda görünür. Boşsa WHMCS firma adı kullanılır.'],
            'storeUrl' => ['FriendlyName' => 'Satış sitesi adresi', 'Type' => 'text', 'Size' => '60', 'Description' => 'Fatura notunda görünür. Boşsa WHMCS sistem adresi kullanılır.'],
>>>>>>> Stashed changes
            'tryRates' => ['FriendlyName' => 'Döviz → TL kurları', 'Type' => 'text', 'Size' => '50', 'Description' => 'Kullanılacak TL kurlarını para birimi kodu ve eşittir işaretiyle girin. Birden fazla girişi boşlukla ayırın.'],
=======
        'description' => 'Ödenen WHMCS faturalarını GIB Framework paneline imzalı webhook ile aktarır ve belge durumlarını panelden geri alır.',
        'version' => GIBFRAMEWORK_MODULE_VERSION,
        'author' => 'GIB Framework',
        'fields' => [
            'enabled' => [
                'FriendlyName' => 'Otomatik aktarım',
                'Type' => 'yesno',
                'Description' => 'Ödenen faturaları GIB Framework\'e gönder.',
            ],
            'webhookUrl' => [
                'FriendlyName' => 'Gelen adres (webhook URL)',
                'Type' => 'text',
                'Size' => '80',
                'Description' => 'Panel › Ayarlar › Entegrasyonlar › WHMCS › Kurulum bölümündeki "Gelen adres". Örn. https://panel.ornek.com/api/v1/hooks/…',
            ],
            'signingSecret' => [
                'FriendlyName' => 'İmza anahtarı',
                'Type' => 'password',
                'Size' => '60',
                'Description' => 'Aynı bölümdeki "İmza anahtarı · X-GibFramework-Signature" (gfs_ ile başlar). Panelde anahtarı yenilerseniz buraya da girin.',
            ],
            'taxField' => [
                'FriendlyName' => 'TCKN / VKN müşteri özel alanı',
                'Type' => 'text',
                'Size' => '40',
                'Default' => 'TC Kimlik / Vergi No',
                'Description' => 'WHMCS müşteri özel alanının adı veya ID\'si. Boşsa / alan yoksa WHMCS Tax ID kullanılır; o da boşsa 11111111111 (nihai tüketici).',
            ],
            'taxOfficeField' => [
                'FriendlyName' => 'Vergi dairesi özel alanı',
                'Type' => 'text',
                'Size' => '40',
                'Description' => 'İsteğe bağlı. Kurumsal müşteriler için vergi dairesi özel alanının adı veya ID\'si.',
            ],
            'sendImmediately' => [
                'FriendlyName' => 'Ödeme anında gönder',
                'Type' => 'yesno',
                'Default' => 'yes',
                'Description' => 'Açıksa fatura ödendiği anda gönderilir; kapalıysa yalnızca WHMCS cron\'unda gönderilir.',
            ],
            'startDate' => [
                'FriendlyName' => 'Geçmiş faturalar için başlangıç tarihi',
                'Type' => 'text',
                'Size' => '12',
                'Description' => 'İsteğe bağlı, YYYY-AA-GG. Doluysa bu tarihten sonra ödenmiş eski faturalar da cron ile kuyruğa alınır. Boşsa yalnızca yeni ödemeler gönderilir.',
            ],
            'storeName' => [
                'FriendlyName' => 'Satış sitesi adı',
                'Type' => 'text',
                'Size' => '45',
                'Description' => 'Fatura notunda görünür. Boşsa WHMCS firma adı kullanılır.',
            ],
            'storeUrl' => [
                'FriendlyName' => 'Satış sitesi adresi',
                'Type' => 'text',
                'Size' => '60',
                'Description' => 'Fatura notunda görünür. Boşsa WHMCS sistem adresi kullanılır.',
            ],
            'tryRates' => [
                'FriendlyName' => 'Döviz → TL kurları',
                'Type' => 'text',
                'Size' => '50',
                'Description' => 'Döviz faturaları TL\'ye çevrilerek gönderilir. Örn. USD=41.25 EUR=48.10 (ondalık ayırıcı nokta, girişler boşlukla ayrılır).',
            ],
>>>>>>> Stashed changes
        ],
    ];
}

function gibframework_activate(): array
{
    try {
        $version = (string) Setting::getValue('Version');
<<<<<<< Updated upstream
        if (!preg_match('/^(\d+\.\d+\.\d+)/', $version, $match) || version_compare($match[1], '9.0.0', '<')
            || PHP_VERSION_ID < 80200 || PHP_INT_SIZE < 8 || !extension_loaded('curl')) {
            return ['status' => 'error', 'description' => 'WHMCS 9.0.0+, 64 bit PHP 8.2+ ve cURL gerekli.'];
        }
        Queue::install();
<<<<<<< Updated upstream
        return ['status' => 'success', 'description' => 'Taslak kuyruğu hazır. Otomatik aktarımı modül ayarlarından açın.'];
=======
        return ['status' => 'success', 'description' => 'Fatura kuyruğu hazır. Otomatik aktarımı modül ayarlarından açın.'];
>>>>>>> Stashed changes
    } catch (Throwable) {
        return ['status' => 'error', 'description' => 'WHMCS veritabanında modül kuyruğu oluşturulamadı.'];
=======
        if (!preg_match('/^(\d+\.\d+\.\d+)/', $version, $match) || version_compare($match[1], '8.0.0', '<')
            || PHP_VERSION_ID < 80100 || !extension_loaded('curl')) {
            return ['status' => 'error', 'description' => 'WHMCS 8.0+, PHP 8.1+ ve cURL eklentisi gerekli.'];
        }
        Queue::install();
        return ['status' => 'success', 'description' => 'Modül hazır. Ayarlara paneldeki gelen adresi ve imza anahtarını girip otomatik aktarımı açın.'];
    } catch (Throwable) {
        return ['status' => 'error', 'description' => 'WHMCS veritabanında modül tabloları oluşturulamadı.'];
>>>>>>> Stashed changes
    }
}

function gibframework_upgrade(array $vars): void
{
<<<<<<< Updated upstream
<<<<<<< Updated upstream
    if (version_compare((string) $vars['version'], '1.1.0', '<')) {
=======
    if (version_compare((string) ($vars['version'] ?? '0'), GIBFRAMEWORK_MODULE_VERSION, '<')) {
>>>>>>> Stashed changes
=======
    if (version_compare((string) ($vars['version'] ?? '0'), GIBFRAMEWORK_MODULE_VERSION, '<')) {
>>>>>>> Stashed changes
        Queue::upgrade();
    }
}

function gibframework_deactivate(): array
{
    return ['status' => 'success', 'description' => 'Aktarım durduruldu. Kuyruk ve fatura eşleştirmeleri saklandı.'];
}

function gibframework_output(array $vars): void
{
    require __DIR__ . '/lib/dashboard.php';
}
