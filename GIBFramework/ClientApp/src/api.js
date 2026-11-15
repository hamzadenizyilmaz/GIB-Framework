const TOKEN_KEY = 'gibframework.token';
const TENANT_KEY = 'gibframework.tenant';
write(TOKEN_KEY, null);

const read = (key) => {
  try { return sessionStorage.getItem(key); } catch { return null; }
};

function write(key, value) {
  try {
    if (value) sessionStorage.setItem(key, value);
    else sessionStorage.removeItem(key);
  } catch { }
}

let sessionVersion = 0;

export const storage = {
  get sessionVersion() { return sessionVersion; },
  get tenantOverride() { return read(TENANT_KEY); },
  set tenantOverride(value) { write(TENANT_KEY, value); },
  clear() {
    sessionVersion++;
    write(TENANT_KEY, null);
  },
};

export class ApiError extends Error {
  constructor(status, code, title, details) {
    super(title || code || `HTTP ${status}`);
    this.status = status;
    this.code = code;
    this.details = details || [];
  }
}

export const UNAUTHORIZED_EVENT = 'gibframework:unauthorized';
export const PASSWORD_CHANGE_EVENT = 'gibframework:password-change';
export const LOGO_EVENT = 'gibframework:logo';
export const MFA_REQUIRED_EVENT = 'gibframework:mfa-required';

async function request(method, path, {
  body, headers = {}, raw, responseType = 'json', signal, timeoutMs = 30000, quietUnauthorized = false,
} = {}) {
  const url = new URL(path, window.location.origin);
  if (!path.startsWith('/') || url.origin !== window.location.origin || url.username || url.password) {
    throw new ApiError(0, 'INVALID_URL', 'İstek yalnızca panel sunucusuna gönderilebilir.');
  }
  const h = new Headers(headers);
  h.set('X-GibFramework-Panel', '1');
  const version = storage.sessionVersion;
  const tenant = storage.tenantOverride;
  if (tenant) h.set('X-Tenant-Id', tenant);

  let payload;
  if (raw !== undefined) {
    payload = raw;
  } else if (body !== undefined) {
    h.set('Content-Type', 'application/json');
    payload = JSON.stringify(body);
  }

  const deadline = new AbortController();
  const cancel = () => deadline.abort();
  signal?.addEventListener('abort', cancel, { once: true });
  if (signal?.aborted) cancel();
  const timer = setTimeout(cancel, timeoutMs);
  const init = { method, headers: h, body: payload, signal: deadline.signal, credentials: 'same-origin', redirect: 'error', cache: 'no-store' };
  try {
    for (let attempt = 0; ; attempt++) {
      try {
        const response = await fetch(url, init);
        return await readResponse(response, url.pathname, responseType, version, tenant, quietUnauthorized);
      } catch (error) {
        if (signal?.aborted) throw new ApiError(0, 'ABORTED', 'İstek iptal edildi.');
        if (deadline.signal.aborted) throw new ApiError(0, 'TIMEOUT', 'İstek zaman aşımına uğradı.');

        const retryable = error instanceof TypeError || [502, 503, 504].includes(error.status);
        if (method !== 'GET' || !retryable || attempt >= 2 || storage.sessionVersion !== version || storage.tenantOverride !== tenant) {
          if (error instanceof TypeError) throw new ApiError(0, 'NETWORK', 'Sunucuya ulaşılamadı.');
          throw error;
        }
        await waitForRetry(300 * (attempt + 1), deadline.signal);
      }
    }
  } finally {
    clearTimeout(timer);
    signal?.removeEventListener('abort', cancel);
  }
}

function waitForRetry(ms, signal) {
  if (signal.aborted) return Promise.resolve();
  return new Promise((resolve) => {
    const finish = () => {
      clearTimeout(timer);
      signal.removeEventListener('abort', finish);
      resolve();
    };
    const timer = setTimeout(finish, ms);
    signal.addEventListener('abort', finish, { once: true });
  });
}

async function readResponse(response, path, responseType, version, tenant, quietUnauthorized) {
  const isCurrent = () => storage.sessionVersion === version && storage.tenantOverride === tenant;
  if (response.status === 401 && path !== '/api/v1/auth/login') {
    if (!quietUnauthorized && isCurrent()) {
      storage.clear();
      window.dispatchEvent(new Event(UNAUTHORIZED_EVENT));
    }
    throw new ApiError(401, 'UNAUTHORIZED', 'Oturumunuz sona erdi; lütfen yeniden giriş yapın.');
  }

  if (!response.ok) {
    let problem = {};
    try { problem = (await response.json()) || {}; } catch (error) {
      if (!(error instanceof SyntaxError)) throw error;
    }
    if (isCurrent()) {
      if (problem.code === 'PASSWORD_CHANGE_REQUIRED') window.dispatchEvent(new Event(PASSWORD_CHANGE_EVENT));
      if (problem.code === 'MFA_REQUIRED') window.dispatchEvent(new Event(MFA_REQUIRED_EVENT));
    }
    const details = problem.details || (problem.errors ? Object.values(problem.errors).flat() : []);
    if (response.status === 403 && ['/api/v1/settings/ip-access', '/api/v1/settings/ip-access/update'].includes(path) && !problem.code) {
      throw new ApiError(403, 'IP_ACCESS_ADMIN_REQUIRED', 'IP erişimini düzenlemek için Platform Yöneticisi veya Genel Müdür yetkisi gerekir.');
    }
    throw new ApiError(response.status, problem.code || `HTTP_${response.status}`, problem.title || response.statusText, details);
  }

  if (response.status === 204) return null;
  if (responseType === 'blob') return { blob: await response.blob(), name: fileName(response) };
  if (responseType === 'text') return response.text();
  const text = await response.text();
  try { return text ? JSON.parse(text) : null; } catch {
    throw new ApiError(response.status, 'INVALID_RESPONSE', 'Sunucu yanıtı okunamadı.');
  }
}

function fileName(response) {
  const cd = response.headers.get('Content-Disposition') || '';
  const encoded = /(?:^|;)\s*filename\*\s*=\s*UTF-8'[^']*'([^;]+)/i.exec(cd);
  if (encoded) {
    try { return decodeURIComponent(encoded[1].trim()); } catch { }
  }
  const plain = /(?:^|;)\s*filename\s*=\s*(?:"((?:[^"\\]|\\.)*)"|([^;]+))/i.exec(cd);
  return plain ? (plain[1]?.replace(/\\(.)/g, '$1') ?? plain[2].trim()) : null;
}

export const api = {
  get: (path, opts) => request('GET', path, opts),
  post: (path, body, opts = {}) => request('POST', path, { ...opts, body }),
  put: (path, body) => request('PUT', path, { body }),
  patch: (path, body) => request('PATCH', path, { body }),
  del: (path) => request('DELETE', path),
  upload: (path, formData) => request('POST', path, { raw: formData }),
  putUpload: (path, formData) => request('PUT', path, { raw: formData }),
  postRaw: (path, data, contentType) => request('POST', path, { raw: data, headers: { 'Content-Type': contentType } }),
  blob: (path, opts) => request('GET', path, { ...opts, responseType: 'blob' }),
  text: (path) => request('GET', path, { responseType: 'text' }),
};

export async function download(path, fallbackName) {
  const { blob, name } = await api.blob(path);
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  try {
    a.href = url;
    a.download = name || fallbackName;
    document.body.appendChild(a);
    a.click();
  } finally {
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
}

export const uuid = () => crypto.randomUUID();
