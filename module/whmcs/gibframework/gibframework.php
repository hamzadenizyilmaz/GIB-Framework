<?php

declare(strict_types=1);

use GibFramework\Whmcs\Queue;
use WHMCS\Config\Setting;

defined('WHMCS') || exit;

<<<<<<< Updated upstream
=======
const GIBFRAMEWORK_MODULE_VERSION = '1.3.0';

>>>>>>> Stashed changes
require_once __DIR__ . '/lib/FrameworkClient.php';
require_once __DIR__ . '/lib/Queue.php';

function gibframework_config(): array
{
    return [
        'name' => 'GIB Framework',
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
        ],
    ];
}

function gibframework_activate(): array
{
    try {
        $version = (string) Setting::getValue('Version');
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
    }
}

function gibframework_upgrade(array $vars): void
{
<<<<<<< Updated upstream
    if (version_compare((string) $vars['version'], '1.1.0', '<')) {
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
