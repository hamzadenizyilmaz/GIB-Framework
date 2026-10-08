const TOKEN_KEY = 'gibframework.token';
const TENANT_KEY = 'gibframework.tenant';

const read = (key) => {
  try { return sessionStorage.getItem(key); } catch { return null; }
};

const write = (key, value) => {
  try {
    if (value) sessionStorage.setItem(key, value);
    else sessionStorage.removeItem(key);
  } catch { }
};

export const storage = {
  get token() { return read(TOKEN_KEY); },
  set token(value) { write(TOKEN_KEY, value); },
  get tenantOverride() { return read(TENANT_KEY); },
  set tenantOverride(value) { write(TENANT_KEY, value); },
  clear() {
    write(TOKEN_KEY, null);
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

async function request(method, path, { body, headers = {}, raw, responseType = 'json' } = {}) {
  const h = { ...headers };
  const token = storage.token;
  if (token) h.Authorization = `Bearer ${token}`;
  if (storage.tenantOverride) h['X-Tenant-Id'] = storage.tenantOverride;

  let payload;
  if (raw !== undefined) {
    payload = raw;
  } else if (body !== undefined) {
    h['Content-Type'] = 'application/json';
    payload = JSON.stringify(body);
  }

  let response;
  try {
    response = await fetch(path, { method, headers: h, body: payload });
  } catch {
    throw new ApiError(0, 'NETWORK', 'Sunucuya ulaşılamadı.');
  }

  if (response.status === 401 && token && !path.startsWith('/api/v1/auth/login')) {
    storage.clear();
    window.dispatchEvent(new Event(UNAUTHORIZED_EVENT));
    throw new ApiError(401, 'UNAUTHORIZED', 'Oturumunuz sona erdi; lütfen yeniden giriş yapın.');
  }

  if (!response.ok) {
    let problem = {};
    try { problem = await response.json(); } catch { }
    if (problem.code === 'PASSWORD_CHANGE_REQUIRED') window.dispatchEvent(new Event(PASSWORD_CHANGE_EVENT));
    if (problem.code === 'MFA_REQUIRED') window.dispatchEvent(new Event(MFA_REQUIRED_EVENT));
    const details = problem.details || (problem.errors ? Object.values(problem.errors).flat() : []);
    throw new ApiError(response.status, problem.code || `HTTP_${response.status}`, problem.title || response.statusText, details);
  }

  if (response.status === 204) return null;
  if (responseType === 'blob') return { blob: await response.blob(), name: fileName(response) };
  if (responseType === 'text') return response.text();
  const text = await response.text();
  return text ? JSON.parse(text) : null;
}

function fileName(response) {
  const cd = response.headers.get('Content-Disposition') || '';
  const m = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(cd);
  return m ? decodeURIComponent(m[1]) : null;
}

export const api = {
  get: (path) => request('GET', path),
  post: (path, body, opts = {}) => request('POST', path, { ...opts, body }),
  put: (path, body) => request('PUT', path, { body }),
  patch: (path, body) => request('PATCH', path, { body }),
  del: (path) => request('DELETE', path),
  upload: (path, formData) => request('POST', path, { raw: formData }),
  putUpload: (path, formData) => request('PUT', path, { raw: formData }),
  postRaw: (path, data, contentType) => request('POST', path, { raw: data, headers: { 'Content-Type': contentType } }),
  blob: (path) => request('GET', path, { responseType: 'blob' }),
  text: (path) => request('GET', path, { responseType: 'text' }),
};

export async function download(path, fallbackName) {
  const { blob, name } = await api.blob(path);
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = name || fallbackName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 10000);
}

export const uuid = () => crypto.randomUUID();
