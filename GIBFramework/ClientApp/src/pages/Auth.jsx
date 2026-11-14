import { useEffect, useRef, useState } from 'react';
import { useLocation, useNavigate } from 'react-router';
import { api, ApiError, storage } from '../api.js';
import { fmtDate, ROLE_LABELS } from '../format.js';
import { useSession } from '../session.jsx';
import { BusyButton, Card, ErrorAlert, Modal, PageHeader, useToast } from '../components/ui.jsx';
import { Brand, LegalStrip } from '../components/Brand.jsx';
import { Icon } from '../components/Icon.jsx';

function ForgotPassword({ onClose }) {
  const [userCode, setUserCode] = useState('');
  const [done, setDone] = useState(null);
  const [error, setError] = useState(null);
  const submit = async (e) => {
    e.preventDefault();
    if (userCode.trim().length < 3) {
      setError({ message: 'Kullanıcı kodunuzu girin.' });
      return;
    }
    try {
      const r = await api.post('/api/v1/auth/password-reset-requests', { userCode: userCode.trim() });
      setDone(r.message);
    } catch (err) {
      setError(err);
    }
  };
  return (
    <Modal title="Şifremi unuttum" onClose={onClose}>
      <div className="modal-body">
        {done ? <div className="alert alert-success mb-0"><i className="bi bi-check-circle me-1" />{done}</div> : (
          <form id="forgot-form" onSubmit={submit} noValidate>
            <ErrorAlert error={error} />
            <div className="login-field">
              <i className="bi bi-person" />
              <input className="form-control" placeholder="Kullanıcı kodu" autoComplete="username" autoFocus value={userCode} onChange={(e) => setUserCode(e.target.value)} aria-label="Kullanıcı kodu" />
            </div>
          </form>
        )}
      </div>
      <div className="modal-footer">
        <button type="button" className="btn btn-outline-secondary rounded-pill px-4" onClick={onClose}>Kapat</button>
        {!done && <button type="submit" form="forgot-form" className="btn btn-primary rounded-pill px-4">Talep gönder</button>}
      </div>
    </Modal>
  );
}

export function Login() {
  const session = useSession();
  const navigate = useNavigate();
  const location = useLocation();
  const toast = useToast();
  const [form, setForm] = useState({ userCode: '', password: '', totpCode: '' });
  const [needTotp, setNeedTotp] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [forgot, setForgot] = useState(false);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  const totpRef = useRef(null);

  useEffect(() => { if (needTotp) totpRef.current?.focus(); }, [needTotp]);

  const submit = async (e) => {
    e.preventDefault();
    if (!form.userCode.trim() || !form.password) {
      setError(new ApiError(400, null, 'Kullanıcı kodu ve şifre zorunludur.'));
      return;
    }

    setBusy(true);
    try {
      const result = await session.login(form.userCode.trim(), form.password, form.totpCode);
      toast(`Hoş geldiniz, ${result.user.displayName}.`);
      const destination = location.state?.from;
      navigate(result.user.mustChangePassword ? '/change-password' : /^\/(?!\/)/.test(destination || '') ? destination : '/', { replace: true });
    } catch (err) {
      if (err instanceof ApiError && err.code === 'TOTP_REQUIRED') {
        setNeedTotp(true);
        setError(null);
      } else {
        setError(err);
        setForm((f) => ({ ...f, password: '', totpCode: '' }));
      }
    } finally {
      setBusy(false);
    }
  };

  const set = (name) => (e) => setForm((f) => ({ ...f, [name]: name === 'totpCode' ? e.target.value.replace(/\D/g, '').slice(0, 6) : e.target.value }));

  return (
    <div className="login-page" data-bs-theme="light">
      <div className="login-main">
        <section className="login-panel">
          <form className="login-card" noValidate onSubmit={submit}>
            <div className="login-brand"><Brand variant="full" /></div>
            <h1 className="login-title">Hesabınıza giriş yapın</h1>
            <p className="login-subtitle">e-Belge işlemlerinize güvenle devam edin.</p>
            <ErrorAlert error={error} />
            <label className="form-label" htmlFor="userCode">Kullanıcı kodu</label>
            <div className="login-field">
              <Icon name="person" />
              <input className="form-control" id="userCode" placeholder="Kullanıcı kodunuzu girin" autoComplete="username" autoFocus value={form.userCode} onChange={set('userCode')} />
            </div>
            <label className="form-label" htmlFor="password">Şifre</label>
            <div className="login-field">
              <Icon name="shield" />
              <input className="form-control" id="password" type={showPassword ? 'text' : 'password'} autoComplete="current-password" value={form.password} onChange={set('password')} />
              <button type="button" className="login-eye" onClick={() => setShowPassword((v) => !v)} aria-label={showPassword ? 'Şifreyi gizle' : 'Şifreyi göster'}>
                <i className={`bi bi-${showPassword ? 'eye-slash' : 'eye'}`} />
              </button>
            </div>
            {needTotp && (
              <>
              <label className="form-label" htmlFor="totpCode">Doğrulama kodu</label>
              <div className="login-field">
                <i className="bi bi-shield-lock" aria-hidden="true" />
                <input ref={totpRef} className="form-control" id="totpCode" inputMode="numeric" autoComplete="one-time-code" value={form.totpCode} onChange={set('totpCode')} />
              </div>
              </>
            )}
            <button className="btn login-submit" type="submit" disabled={busy}>
              {busy ? <span className="spinner-border spinner-border-sm" aria-hidden="true" /> : <Icon name="shield" />}
              {busy ? 'Giriş yapılıyor…' : 'Güvenli giriş yap'}
            </button>
            <button type="button" className="login-forgot" onClick={() => setForgot(true)}>Şifremi unuttum</button>
            <span className="login-copyright">© 2026 GIB Framework</span>
          </form>
        </section>
      </div>
      <LegalStrip />
      {forgot && <ForgotPassword onClose={() => setForgot(false)} />}
    </div>
  );
}

function PasswordForm({ onDone, fullWidth = false }) {
  const [values, setValues] = useState({ currentPassword: '', newPassword: '', newPassword2: '' });
  const [validated, setValidated] = useState(false);
  const [error, setError] = useState(null);
  const ref = useRef(null);
  const mismatch = values.newPassword2 && values.newPassword2 !== values.newPassword;

  const submit = async (e) => {
    e.preventDefault();
    if (!ref.current.checkValidity() || mismatch) {
      setValidated(true);
      return;
    }

    try {
      await api.post('/api/v1/auth/change-password', { currentPassword: values.currentPassword, newPassword: values.newPassword });
      await onDone();
    } catch (err) {
      setError(err);
    }
  };

  const set = (name) => (e) => setValues((v) => ({ ...v, [name]: e.target.value }));

  return (
    <form ref={ref} noValidate onSubmit={submit} className={validated ? 'was-validated' : ''}>
      <ErrorAlert error={error} />
      <div className="mb-3">
        <label className="form-label" htmlFor="currentPassword">Mevcut şifre</label>
        <input className="form-control" id="currentPassword" type="password" autoComplete="current-password" required value={values.currentPassword} onChange={set('currentPassword')} />
      </div>
      <div className="mb-3">
        <label className="form-label" htmlFor="newPassword">Yeni şifre</label>
        <input className="form-control" id="newPassword" type="password" autoComplete="new-password" required minLength={10} value={values.newPassword} onChange={set('newPassword')} />
        <div className="form-text">En az 10 karakter; harf ve rakam içermeli.</div>
        <div className="invalid-feedback">En az 10 karakter olmalıdır.</div>
      </div>
      <div className="mb-3">
        <label className="form-label" htmlFor="newPassword2">Yeni şifre (tekrar)</label>
        <input className={`form-control ${mismatch ? 'is-invalid' : ''}`} id="newPassword2" type="password" autoComplete="new-password" required minLength={10} value={values.newPassword2} onChange={set('newPassword2')} />
        <div className="invalid-feedback">Şifreler aynı olmalıdır.</div>
      </div>
      <button className={`btn btn-primary ${fullWidth ? 'w-100' : ''}`} type="submit"><i className="bi bi-check2" aria-hidden="true" />Şifreyi değiştir</button>
    </form>
  );
}

function useRelogin() {
  const session = useSession();
  const navigate = useNavigate();
  const toast = useToast();
  return async (message) => {
    toast(message);
    storage.clear();
    await session.reload();
    navigate('/login');
  };
}

export function ChangePassword() {
  const relogin = useRelogin();
  return (
    <>
      <PageHeader title="Şifre değiştir" subtitle="İlk giriş veya güvenlik politikası nedeniyle parola yenileme" />
      <section className="card password-layout" aria-labelledby="password-heading">
        <aside className="password-guidance">
          <i className="bi bi-lock" aria-hidden="true" />
          <h2>Hesabınızın kontrolü sizde.</h2>
          <ul><li>En az 10 karakter</li><li>Harf ve rakam kombinasyonu</li><li>Değişiklik sonrası yeniden giriş</li></ul>
        </aside>
        <div className="password-content">
          <h2 id="password-heading">Şifrenizi güncelleyin</h2>
          <p className="text-body-secondary">Güvenlik politikanız gereği mevcut şifrenizi yenileyin.</p>
          <PasswordForm fullWidth onDone={() => relogin('Şifreniz değiştirildi. Yeni şifrenizle giriş yapın.')} />
        </div>
      </section>
    </>
  );
}

export function Account() {
  const session = useSession();
  const relogin = useRelogin();
  const [me, setMe] = useState(undefined);
  const [setup, setSetup] = useState(null);
  const [code, setCode] = useState('');
  const [error, setError] = useState(null);
  const p = session.permissions;

  useEffect(() => { api.get('/api/v1/auth/me').then(setMe).catch(() => setMe(null)); }, []);

  return (
    <>
      <PageHeader breadcrumb={[['Ayarlar', '/settings'], ['Hesabım ve 2FA']]} icon="person-gear" title="Hesabım ve güvenlik" />
      {p.mfaRequired && (
        <div className="alert alert-warning d-flex gap-2 align-items-start">
          <i className="bi bi-shield-exclamation fs-5" />
          <div><div className="fw-semibold">İki adımlı doğrulama zorunlu</div>Firma güvenlik politikası gereği devam etmek için aşağıdan iki adımlı doğrulamayı etkinleştirin.</div>
        </div>
      )}
      <ErrorAlert error={error} />
      <div className="row g-4">
        <div className="col-lg-4">
          <Card title="Profil" icon="person" className="h-100">
            <dl className="row mb-0 small">
              <dt className="col-5">Kullanıcı kodu</dt><dd className="col-7 mono">{p.userId}</dd>
              <dt className="col-5">Ad</dt><dd className="col-7">{p.displayName || '-'}</dd>
              <dt className="col-5">Roller</dt>
              <dd className="col-7">{p.roles.map((r) => <span key={r} className="badge text-bg-secondary me-1 mb-1">{ROLE_LABELS[r] || r}</span>)}</dd>
              <dt className="col-5">Son giriş</dt><dd className="col-7">{me?.lastLoginAt ? fmtDate(me.lastLoginAt) : '-'}</dd>
              <dt className="col-5">İki adımlı doğrulama</dt>
              <dd className="col-7">{me?.totpEnabled ? <span className="badge text-bg-success">Etkin</span> : <span className="badge text-bg-warning">Kapalı</span>}</dd>
            </dl>
          </Card>
        </div>
        <div className="col-lg-4">
          <Card title="Şifre değiştir" icon="key" className="h-100">
            {me ? <PasswordForm onDone={() => relogin('Şifre değiştirildi; tekrar giriş yapın.')} /> : <p className="small text-body-secondary mb-0">Yerel hesap gerekli.</p>}
          </Card>
        </div>
        <div className="col-lg-4">
          <Card title="İki adımlı doğrulama" icon="shield-lock" className="h-100">
            {me && !me.totpEnabled && !setup && (
              <BusyButton className="btn btn-outline-primary" onClick={async () => setSetup(await api.post('/api/v1/auth/totp/setup'))}>
                <i className="bi bi-qr-code me-1" />Kurulumu başlat
              </BusyButton>
            )}
            {setup && (
              <>
                <ol className="small ps-3">
                  <li>Doğrulama uygulamasında karekodu okutun.</li>
                  <li>Uygulamanın gösterdiği 6 haneli kodu girin.</li>
                </ol>
                <img className="img-fluid border rounded mb-2" alt="TOTP karekodu" src={`data:image/png;base64,${setup.qrPngBase64}`} />
                <div className="small mb-3">Anahtar: <span className="mono user-select-all">{setup.secret}</span></div>
                <form className="input-group" onSubmit={async (e) => {
                  e.preventDefault();
                  try {
                    await api.post('/api/v1/auth/totp/enable', { code });
                    await relogin('İki adımlı doğrulama etkinleştirildi; tekrar giriş yapın.');
                  } catch (err) {
                    setError(err);
                  }
                }}>
                  <input className="form-control" inputMode="numeric" pattern="[0-9]{6}" maxLength={6} required placeholder="000000" aria-label="Doğrulama kodu" value={code} onChange={(e) => setCode(e.target.value)} />
                  <button className="btn btn-primary" type="submit">Etkinleştir</button>
                </form>
              </>
            )}
            {me?.totpEnabled && <p className="small mb-2"><i className="bi bi-shield-check text-success me-1" />Girişlerde doğrulama kodu istenir.</p>}
            <hr />
            <BusyButton className="btn btn-outline-danger btn-sm" onClick={async () => {
              await api.post('/api/v1/auth/logout');
              await relogin('Tüm oturumlar kapatıldı.');
            }}><i className="bi bi-power me-1" />Tüm oturumları kapat</BusyButton>
          </Card>
        </div>
      </div>
    </>
  );
}
