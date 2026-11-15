<?php

declare(strict_types=1);

namespace GibFramework\Whmcs;

use RuntimeException;

defined('WHMCS') || exit;

final class DeliveryException extends RuntimeException
{
    public function __construct(
        public readonly string $reason,
        public readonly bool $pauseQueue = false,
        public readonly int $retryAfter = 0,
    ) {
        parent::__construct($reason);
    }
}

final class FrameworkClient
{
    private readonly string $baseUrl;
    private readonly string $apiKey;
<<<<<<< Updated upstream

    public function __construct(string $baseUrl, private readonly string $environment)
=======
    private ?string $operatorAuthorization = null;
    private array $operatorPermissions = [];
    private bool $portalReady = false;
    private ?DeliveryException $operatorError = null;
    private ?DeliveryException $portalError = null;

    public function __construct(string $baseUrl, private readonly string $environment, private readonly ?\Closure $observe = null)
>>>>>>> Stashed changes
    {
        $baseUrl = rtrim(trim($baseUrl), '/');
        $url = parse_url($baseUrl);
        $apiKey = trim((string) getenv('GIBFRAMEWORK_API_KEY'));

        if (!filter_var($baseUrl, FILTER_VALIDATE_URL) || ($url['scheme'] ?? '') !== 'https'
            || array_intersect(['user', 'pass', 'query', 'fragment'], array_keys($url ?: []))
<<<<<<< Updated upstream
            || !preg_match('/^gfk_[a-f0-9]{12}_[a-f0-9]{48}$/D', $apiKey)) {
=======
            || !preg_match('/^gfk_[a-f0-9]{12}_[a-f0-9]{48}$/D', $apiKey)
            || !in_array($environment, ['Production', 'Test'], true)) {
>>>>>>> Stashed changes
            throw new DeliveryException('CONFIG_FRAMEWORK', true, 300);
        }

        $this->baseUrl = $baseUrl;
        $this->apiKey = $apiKey;
    }

    public function authorizedTenant(bool $write = false): string
    {
        $keyHeader = 'X-Api-Key: ' . $this->apiKey;
        $apiPermissions = $this->request('/api/v1/auth/permissions', $keyHeader);
        $tenantId = $apiPermissions['tenantId'] ?? null;
        $policies = $apiPermissions['policies'] ?? null;
        if (!is_string($tenantId) || !self::isUuid($tenantId)
            || !is_array($policies) || array_diff($write ? ['InvoiceRead', 'InvoiceCreate'] : ['InvoiceRead'], $policies)
            || ($apiPermissions['mfaRequired'] ?? false) || ($apiPermissions['mustChangePassword'] ?? false)) {
            throw new DeliveryException('API_KEY_PERMISSIONS', true, 300);
        }

        return strtolower($tenantId);
    }

<<<<<<< Updated upstream
    public function connectedTenant(): string
    {
        $tenantId = $this->authorizedTenant(true);
        $token = $this->portalToken();
        $bearerHeader = 'Authorization: Bearer ' . $token;
        $portalPermissions = $this->request('/api/v1/auth/permissions', $bearerHeader);
        $portalPolicies = $portalPermissions['policies'] ?? null;
        if (strcasecmp((string) ($portalPermissions['tenantId'] ?? ''), $tenantId) !== 0
            || !is_array($portalPolicies) || !in_array('GibPortal', $portalPolicies, true)
            || ($portalPermissions['mfaRequired'] ?? false) || ($portalPermissions['mustChangePassword'] ?? false)) {
            throw new DeliveryException('PORTAL_PERMISSIONS', true, 300);
        }

        $status = $this->request('/api/v1/gib-portal/status', $bearerHeader);
        if (($status['connected'] ?? false) !== true || ($status['environment'] ?? null) !== $this->environment) {
            throw new DeliveryException('PORTAL_DISCONNECTED', true, 60);
        }

        $portal = $this->request('/api/v1/gib-portal/me', $bearerHeader);
        if (($portal['taxId'] ?? '') === '' || $portal['taxId'] !== ($status['portalTaxId'] ?? null)) {
            throw new DeliveryException('PORTAL_IDENTITY', true, 300);
        }

        return $tenantId;
=======
    public function verifyOperator(string $tenantId): void
    {
        if ($this->operatorAuthorization !== null) {
            return;
        }
        if ($this->operatorError !== null) {
            throw $this->operatorError;
        }
        try {
            $auth = 'Authorization: Bearer ' . $this->operatorToken();
            $permissions = $this->request('/api/v1/auth/permissions', $auth);
            if (strcasecmp((string) ($permissions['tenantId'] ?? ''), $tenantId) !== 0
                || !is_array($permissions['policies'] ?? null)
                || ($permissions['mfaRequired'] ?? false) || ($permissions['mustChangePassword'] ?? false)) {
                throw new DeliveryException('OPERATOR_PERMISSIONS', true, 300);
            }
            $this->operatorPermissions = $permissions;
            $this->operatorAuthorization = $auth;
        } catch (DeliveryException $error) {
            throw $this->operatorError = $error;
        }
    }

    public function verifyPortal(string $tenantId): void
    {
        if ($this->portalReady) {
            return;
        }
        if ($this->portalError !== null) {
            throw $this->portalError;
        }
        $this->verifyOperator($tenantId);
        if (!$this->hasPolicy('GibPortal')) {
            throw new DeliveryException('PORTAL_PERMISSIONS', true, 300);
        }

        try {
            $status = $this->request('/api/v1/gib-portal/status', $this->operatorAuthorization);
            if (($status['connected'] ?? false) === true && ($status['environment'] ?? null) !== $this->environment) {
                throw new DeliveryException('PORTAL_ENVIRONMENT_MISMATCH', true, 300);
            }
            if (($status['connected'] ?? false) !== true) {
                $status = $this->connectPortal();
            }
            try {
                $portal = $this->request('/api/v1/gib-portal/me', $this->operatorAuthorization);
            } catch (DeliveryException $error) {
                if (!in_array($error->reason, ['GIB_PORTAL_SESSION_EXPIRED', 'GIB_PORTAL_NOT_CONNECTED'], true)) {
                    throw $error;
                }
                $status = $this->connectPortal();
                $portal = $this->request('/api/v1/gib-portal/me', $this->operatorAuthorization);
            }
            if (($portal['taxId'] ?? '') === '' || $portal['taxId'] !== ($status['portalTaxId'] ?? null)) {
                throw new DeliveryException('PORTAL_IDENTITY', true, 300);
            }
            $this->portalReady = true;
        } catch (DeliveryException $error) {
            throw $this->portalError = $error;
        }
    }

    private function connectPortal(): array
    {
        $user = trim((string) getenv('GIBFRAMEWORK_GIB_USER'));
        $password = (string) getenv('GIBFRAMEWORK_GIB_PASSWORD');
        if ($user === '' || $password === '') {
            throw new DeliveryException('PORTAL_DISCONNECTED', true, 300);
        }
        $status = $this->request('/api/v1/gib-portal/connect', $this->operatorAuthorization, [
            'environment' => $this->environment, 'userCode' => $user, 'password' => $password,
        ]);
        if (($status['connected'] ?? false) !== true || ($status['environment'] ?? null) !== $this->environment) {
            throw new DeliveryException('PORTAL_DISCONNECTED', true, 300);
        }
        return $status;
    }

    public function hasPolicy(string $policy): bool
    {
        return in_array($policy, $this->operatorPermissions['policies'] ?? [], true);
    }

    public function integratorAvailable(): bool
    {
        return ($this->operatorPermissions['integratorAvailable'] ?? false) === true;
    }

    public function advance(array $invoice, string $tenantId): array
    {
        $id = $invoice['id'];
        if ($invoice['status'] === 'Draft') {
            if (!empty($invoice['lastError'])) {
                throw new DeliveryException('INVOICE_REQUIRES_REVIEW');
            }
            $invoice = $this->validateInvoice($this->request('/api/v1/invoices/' . $id . '/submit', 'X-Api-Key: ' . $this->apiKey, []), $id);
        }
        if ($invoice['status'] === 'AwaitingApproval') {
            $this->verifyOperator($tenantId);
            if (!$this->hasPolicy('InvoiceApprove')) {
                throw new DeliveryException('OPERATOR_APPROVAL_PERMISSION');
            }
            $invoice = $this->validateInvoice($this->request('/api/v1/invoices/' . $id . '/approve', $this->operatorAuthorization, []), $id);
        }
        if ($invoice['status'] === 'Approved' && ($invoice['documentType'] ?? '') === 'EArsiv') {
            $this->verifyPortal($tenantId);
            $this->request('/api/v1/gib-portal/invoices/' . $id . '/draft', $this->operatorAuthorization, []);
            $invoice = $this->invoice($id);
        }

        if ($invoice['status'] === 'Signing' && ($invoice['issuanceChannel'] ?? '') === 'GibPortal'
            && (($invoice['portalEnvironment'] ?? null) === 'Test' || !empty($invoice['portalReconciliationRequired']))) {
            if (($invoice['portalEnvironment'] ?? null) !== $this->environment) {
                throw new DeliveryException('PORTAL_ENVIRONMENT_MISMATCH');
            }
            $this->verifyPortal($tenantId);
            $this->requireSigningPermission();
            // In production this only reconciles a previous signature. An empty body cannot approve an SMS challenge.
            return $this->validateInvoice($this->request('/api/v1/gib-portal/invoices/' . $id . '/sign', $this->operatorAuthorization, []), $id);
        }
        if (in_array($invoice['status'], ['Approved', 'Signing'], true) && ($invoice['documentType'] ?? '') === 'EFatura'
            && ($invoice['issuanceChannel'] ?? '') === 'Integrator') {
            $this->verifyOperator($tenantId);
            if (!$this->integratorAvailable()) {
                throw new DeliveryException('INTEGRATOR_UNAVAILABLE', false, 1800);
            }
            $this->requireSigningPermission();
            return $this->validateInvoice($this->request('/api/v1/invoices/' . $id . '/sign', $this->operatorAuthorization, []), $id);
        }
        return $invoice;
    }

    private function requireSigningPermission(): void
    {
        if (!$this->hasPolicy('InvoiceSign')) {
            throw new DeliveryException('OPERATOR_SIGN_PERMISSION');
        }
>>>>>>> Stashed changes
    }

    public function createDraft(array $invoice, string $key): array
    {
        $response = $this->request('/api/v1/invoices', 'X-Api-Key: ' . $this->apiKey, $invoice, $key);
        $id = $response['id'] ?? '';
        $status = $response['status'] ?? '';
        if (!is_string($id) || !self::isUuid($id) || !is_string($status) || $status === ''
            || ($response['_httpStatus'] === 201 && $status !== 'Draft')
            || !in_array($response['_httpStatus'], [200, 201], true)) {
            throw new DeliveryException('FRAMEWORK_DRAFT_RESPONSE', true, 300);
        }

        return $response;
    }

    public function invoice(string $id): array
    {
        if (!self::isUuid($id)) {
            throw new DeliveryException('INVALID_INVOICE_ID');
        }

<<<<<<< Updated upstream
        $response = $this->request('/api/v1/invoices/' . $id, 'X-Api-Key: ' . $this->apiKey);
=======
        return $this->validateInvoice($this->request('/api/v1/invoices/' . $id, 'X-Api-Key: ' . $this->apiKey), $id);
    }

    private function validateInvoice(array $response, string $id): array
    {
>>>>>>> Stashed changes
        if (!is_string($response['id'] ?? null) || strcasecmp($response['id'], $id) !== 0
            || !is_string($response['status'] ?? null) || $response['status'] === '') {
            throw new DeliveryException('FRAMEWORK_INVOICE_RESPONSE');
        }

        return $response;
    }

    public static function isUuid(string $value): bool
    {
        return preg_match('/^[a-f0-9]{8}-(?:[a-f0-9]{4}-){3}[a-f0-9]{12}$/iD', $value) === 1;
    }

<<<<<<< Updated upstream
    private function portalToken(): string
=======
    private function operatorToken(): string
>>>>>>> Stashed changes
    {
        $token = trim((string) getenv('GIBFRAMEWORK_PORTAL_BEARER'));
        if ($token === '') {
            $userCode = trim((string) getenv('GIBFRAMEWORK_PORTAL_USER'));
            $password = (string) getenv('GIBFRAMEWORK_PORTAL_PASSWORD');
            if ($userCode === '' || $password === '') {
                throw new DeliveryException('CONFIG_PORTAL_AUTH', true, 300);
            }

            $response = $this->request('/api/v1/auth/login', null, ['userCode' => $userCode, 'password' => $password]);
<<<<<<< Updated upstream
            $token = (string) ($response['accessToken'] ?? '');
        }

        if (!preg_match('/^[\w-]+\.[\w-]+\.[\w-]+$/D', $token)) {
=======
            $token = $response['accessToken'] ?? '';
        }

        if (!is_string($token) || !preg_match('/^[\w-]+\.[\w-]+\.[\w-]+$/D', $token)) {
>>>>>>> Stashed changes
            throw new DeliveryException('PORTAL_TOKEN_INVALID', true, 300);
        }

        return $token;
    }

    private function request(string $path, ?string $auth, ?array $body = null, ?string $key = null): array
    {
        $headers = ['Accept: application/json'];
        if ($auth !== null) {
            $headers[] = $auth;
        }
        if ($key !== null) {
            $headers[] = 'Idempotency-Key: ' . $key;
        }
        $content = '';
        $retryAfter = 0;
        $curl = curl_init($this->baseUrl . $path);
        curl_setopt_array($curl, [
            CURLOPT_CONNECTTIMEOUT => 3,
<<<<<<< Updated upstream
            CURLOPT_TIMEOUT => 8,
=======
            CURLOPT_TIMEOUT => str_starts_with($path, '/api/v1/gib-portal/') || str_ends_with($path, '/sign') ? 30 : 8,
>>>>>>> Stashed changes
            CURLOPT_PROTOCOLS => CURLPROTO_HTTPS,
            CURLOPT_SSL_VERIFYPEER => true,
            CURLOPT_SSL_VERIFYHOST => 2,
            CURLOPT_FOLLOWLOCATION => false,
            CURLOPT_WRITEFUNCTION => static function ($curl, string $chunk) use (&$content): int {
                if (strlen($content) + strlen($chunk) > 2097152) {
                    return 0;
                }
                $content .= $chunk;
                return strlen($chunk);
            },
            CURLOPT_HEADERFUNCTION => static function ($curl, string $line) use (&$retryAfter): int {
                if (strncasecmp($line, 'Retry-After:', 12) === 0) {
                    $value = trim(substr($line, 12));
                    $retryAfter = min(86400 * 30, max(0, ctype_digit($value) ? (int) $value : (int) strtotime($value) - time()));
                }
                return strlen($line);
            },
        ]);
        if ($body !== null) {
            $headers[] = 'Content-Type: application/json';
            curl_setopt($curl, CURLOPT_POST, true);
<<<<<<< Updated upstream
            curl_setopt($curl, CURLOPT_POSTFIELDS, json_encode($body, JSON_THROW_ON_ERROR | JSON_UNESCAPED_UNICODE));
=======
            curl_setopt($curl, CURLOPT_POSTFIELDS, json_encode((object) $body, JSON_THROW_ON_ERROR | JSON_UNESCAPED_UNICODE));
>>>>>>> Stashed changes
        }
        curl_setopt($curl, CURLOPT_HTTPHEADER, $headers);
        $sent = curl_exec($curl);
        $status = (int) curl_getinfo($curl, CURLINFO_HTTP_CODE);
<<<<<<< Updated upstream

        if ($sent === false) {
=======
        $duration = (int) round(1000 * curl_getinfo($curl, CURLINFO_TOTAL_TIME));
        $endpoint = preg_replace('~/[a-f0-9-]{36}(?=/|$)~i', '/{id}', $path);

        if ($sent === false) {
            ($this->observe)?->__invoke($body === null ? 'GET' : 'POST', $endpoint, $status, $duration, 'FRAMEWORK_CONNECTION');
>>>>>>> Stashed changes
            throw new DeliveryException('FRAMEWORK_CONNECTION', true, 60);
        }
        $data = json_decode($content, true);
        if ($status < 200 || $status >= 300) {
            $code = is_array($data) ? ($data['code'] ?? '') : '';
            $reason = is_string($code) && preg_match('/^[A-Z][A-Z0-9_-]{1,63}$/D', $code)
                ? $code : 'HTTP_' . $status;
<<<<<<< Updated upstream
            $invoiceRequest = str_starts_with($path, '/api/v1/invoices');
            $pause = !$invoiceRequest || !in_array($status, [400, 404, 409, 422], true);
            $minimumWait = in_array($status, [401, 403, 423], true) ? 300 : ($pause ? 60 : 0);
            throw new DeliveryException($reason, $pause, max($minimumWait, $retryAfter));
        }
        if (!is_array($data)) {
            throw new DeliveryException('FRAMEWORK_RESPONSE', true, 60);
        }

=======
            ($this->observe)?->__invoke($body === null ? 'GET' : 'POST', $endpoint, $status, $duration, $reason);
            $invoiceRequest = str_starts_with($path, '/api/v1/invoices') || str_starts_with($path, '/api/v1/gib-portal/invoices/');
            $pause = !$invoiceRequest || !in_array($status, [400, 404, 409, 422], true);
            $minimumWait = in_array($status, [401, 403, 423], true) ? 300 : ($pause ? 60 : 0);
            if (in_array($reason, ['GIB_PORTAL_SESSION_EXPIRED', 'GIB_PORTAL_NOT_CONNECTED'], true)) {
                $this->portalReady = false;
            }
            throw new DeliveryException($reason, $pause, max($minimumWait, $retryAfter));
        }
        if (!is_array($data)) {
            ($this->observe)?->__invoke($body === null ? 'GET' : 'POST', $endpoint, $status, $duration, 'FRAMEWORK_RESPONSE');
            throw new DeliveryException('FRAMEWORK_RESPONSE', true, 60);
        }

        ($this->observe)?->__invoke($body === null ? 'GET' : 'POST', $endpoint, $status, $duration, null);
>>>>>>> Stashed changes
        $data['_httpStatus'] = $status;
        return $data;
    }
}
