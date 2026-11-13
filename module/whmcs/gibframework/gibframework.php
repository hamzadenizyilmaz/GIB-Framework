<?php

declare(strict_types=1);

use GibFramework\Whmcs\Queue;
use WHMCS\Config\Setting;

defined('WHMCS') || exit;

require_once __DIR__ . '/lib/FrameworkClient.php';
require_once __DIR__ . '/lib/Queue.php';

function gibframework_config(): array
{
    return [
        'name' => 'GIB Framework',
        'description' => 'Ödenmiş WHMCS faturalarını GIB Framework taslak kuyruğuna aktarır.',
        'version' => '1.1.0',
        'author' => 'GIB Framework',
        'fields' => [
            'enabled' => ['FriendlyName' => 'Otomatik aktarım', 'Type' => 'yesno', 'Description' => 'Geçmiş ve yeni ödenmiş faturaları işle.'],
            'frameworkUrl' => ['FriendlyName' => 'GIB Framework adresi', 'Type' => 'text', 'Size' => '60', 'Description' => 'Panelin HTTPS kök adresi.'],
            'portalEnvironment' => ['FriendlyName' => 'GİB Portal ortamı', 'Type' => 'dropdown', 'Options' => 'Production,Test', 'Default' => 'Production'],
            'taxFieldId' => ['FriendlyName' => 'VKN/TCKN özel alan ID', 'Type' => 'text', 'Size' => '8', 'Description' => 'Boş bırakılırsa WHMCS Tax ID kullanılır.'],
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
        return ['status' => 'success', 'description' => 'Taslak kuyruğu hazır. Otomatik aktarımı modül ayarlarından açın.'];
    } catch (Throwable) {
        return ['status' => 'error', 'description' => 'WHMCS veritabanında modül kuyruğu oluşturulamadı.'];
    }
}

function gibframework_upgrade(array $vars): void
{
    if (version_compare((string) $vars['version'], '1.1.0', '<')) {
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
