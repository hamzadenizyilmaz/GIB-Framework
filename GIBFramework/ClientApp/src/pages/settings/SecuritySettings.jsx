import { useEffect, useState } from 'react';
import { Link } from 'react-router';
import { api } from '../../api.js';
import { fmtDate } from '../../format.js';
import { useSession } from '../../session.jsx';
import { BusyButton, Card, ErrorAlert, Spinner, useDialogs, useLoad, useToast } from '../../components/ui.jsx';
import { SettingsLayout } from './SettingsLayout.jsx';

const SESSION_OPTIONS = [30, 60, 120, 240, 480, 720, 1440];
const IDLE_OPTIONS = [0, 5, 10, 15, 30, 60, 120, 240];
const API_KEY_OPTIONS = [30, 90, 180, 365, 730];

const minutesText = (m) => (m >= 60 ? `${m / 60} saat` : `${m} dakika`);

function OptionSelect({ id, value, onChange, options, render, emptyLabel, disabled }) {
  return (
    <select className="form-select" id={id} disabled={disabled} value={value ?? ''} onChange={(e) => onChange(e.target.value === '' ? null : Number(e.target.value))}>
      {emptyLabel && <option value="">{emptyLabel}</option>}
      {options.map((o) => <option key={o} value={o}>{render(o)}</option>)}
    </select>
  );
}

function MfaCoverage() {
  const { data: users, loading } = useLoad(() => api.get('/api/v1/users'), []);
  if (loading) return <Spinner />;
  const active = (users || []).filter((u) => u.isActive);
  const missing = active.filter((u) => !u.totpEnabled);
  const pct = active.length ? Math.round(((active.length - missing.length) / active.length) * 100) : 0;
  return (
    <>
      <div className="d-flex justify-content-between align-items-end mb-2">
        <div><div className="metric">%{pct}</div><div className="small text-body-secondary">2FA kullanan aktif kullanıcı</div></div>
        <div className="small text-body-secondary">{active.length - missing.length} / {active.length}</div>
      </div>
      <div className="progress mb-3" role="progressbar" aria-valuenow={pct} aria-valuemin={0} aria-valuemax={100} style={{ height: '.5rem' }}>
        <div className={`progress-bar ${pct === 100 ? 'bg-success' : 'bg-warning'}`} style={{ width: `${pct}%` }} />
      </div>
      {missing.length > 0 ? (
        <ul className="list-group list-group-flush small">
          {missing.map((u) => (
            <li key={u.id} className="list-group-item px-0 d-flex justify-content-between">
              <span><i className="bi bi-person me-1 text-body-secondary" />{u.displayName}</span>
              <span className="font-monospace text-body-secondary">{u.userCode}</span>
            </li>
          ))}
        </ul>
      ) : <div className="small text-success"><i className="bi bi-shield-check me-1" />Tüm aktif kullanıcılar 2FA kullanıyor.</div>}
    </>
  );
}

function SecurityForm() {
  const session = useSession();
  const toast = useToast();
  const dialogs = useDialogs();
  const { data, loading, error, reload } = useLoad(() => api.get('/api/v1/settings/security'), [session.tenant?.id]);
  const [form, setForm] = useState(null);
  const [saveError, setSaveError] = useState(null);
  const canEdit = session.can('SecurityManage');

  useEffect(() => {
    if (data) setForm({ ...data.policy });
  }, [data]);

  if (loading || !form) return error ? <ErrorAlert error={error} /> : <Spinner />;
  const { platform } = data;
  const set = (name) => (value) => setForm((f) => ({ ...f, [name]: value }));

  const save = async () => {
    setSaveError(null);
    if (form.requireTotp && !data.policy.requireTotp
      && !await dialogs.confirm('2FA zorunluluğu', 'İki adımlı doğrulaması kapalı kullanıcılar bir sonraki işlemlerinde Hesabım sayfasına yönlendirilir ve 2FA kurmadan devam edemez.', 'Etkinleştir', 'warning')) {
      return;
    }
    try {
      await api.put('/api/v1/settings/security', {
        requireTotp: form.requireTotp,
        sessionMinutes: form.sessionMinutes,
        idleLogoutMinutes: form.idleLogoutMinutes || 0,
        apiKeyMaxDays: form.apiKeyMaxDays,
      });
      toast('Güvenlik politikası kaydedildi.');
      await reload();
      await session.reload();
    } catch (err) {
      setSaveError(err);
    }
  };

  return (
    <div className="row g-4">
      <div className="col-xl-8">
        <Card title="Politika" icon="shield-lock"
          footer={canEdit && <div className="d-flex justify-content-between align-items-center gap-2">
            <span className="small text-body-secondary">{data.policy.updatedAt ? `Son değişiklik: ${fmtDate(data.policy.updatedAt)} · ${data.policy.updatedBy}` : ''}</span>
            <BusyButton onClick={save}><i className="bi bi-save me-1" />Kaydet</BusyButton>
          </div>}>
          <ErrorAlert error={saveError} />
          <div className="d-flex gap-3 align-items-start pb-3 mb-3 border-bottom">
            <span className="tile-icon flex-shrink-0"><i className="bi bi-phone" /></span>
            <div className="flex-grow-1">
              <div className="d-flex justify-content-between align-items-center gap-3">
                <label className="fw-semibold mb-0" htmlFor="requireTotp">İki adımlı doğrulama zorunlu</label>
                <div className="form-check form-switch m-0">
                  <input className="form-check-input" type="checkbox" role="switch" id="requireTotp" disabled={!canEdit} checked={form.requireTotp} onChange={(e) => set('requireTotp')(e.target.checked)} />
                </div>
              </div>
              <div className="small text-body-secondary">Firmadaki tüm kullanıcılar girişte doğrulama uygulaması kodu kullanır.</div>
            </div>
          </div>
          <div className="row g-3">
            <div className="col-md-4">
              <label className="form-label fw-semibold" htmlFor="session">Oturum süresi</label>
              <OptionSelect id="session" disabled={!canEdit} value={form.sessionMinutes} onChange={set('sessionMinutes')} options={SESSION_OPTIONS}
                render={minutesText} emptyLabel={`Platform varsayılanı (${minutesText(platform.defaultSessionMinutes)})`} />
              <div className="form-text">Girişten sonra oturumun geçerli kalacağı süre.</div>
            </div>
            <div className="col-md-4">
              <label className="form-label fw-semibold" htmlFor="idle">Hareketsizlikte çıkış</label>
              <OptionSelect id="idle" disabled={!canEdit} value={form.idleLogoutMinutes || 0} onChange={(v) => set('idleLogoutMinutes')(v || 0)} options={IDLE_OPTIONS}
                render={(m) => (m === 0 ? 'Kapalı' : minutesText(m))} />
              <div className="form-text">Panel bu süre boyunca kullanılmazsa oturum kapanır.</div>
            </div>
            <div className="col-md-4">
              <label className="form-label fw-semibold" htmlFor="apikey">API anahtarı en uzun süre</label>
              <OptionSelect id="apikey" disabled={!canEdit} value={form.apiKeyMaxDays} onChange={set('apiKeyMaxDays')} options={API_KEY_OPTIONS}
                render={(d) => `${d} gün`} emptyLabel="Sınırsız" />
              <div className="form-text">Yeni anahtarlarda süre seçimi zorunlu olur.</div>
            </div>
          </div>
        </Card>
      </div>
      <div className="col-xl-4">
        <Card title="Platform kuralları" icon="key" className="mb-4">
          <ul className="list-unstyled small mb-0">
            {platform.passwordRules.map((r) => <li key={r} className="mb-2"><i className="bi bi-check2-circle text-success me-2" />{r}</li>)}
            <li className="mb-2"><i className="bi bi-lock me-2 text-warning" />{platform.maxFailedAttempts} hatalı denemede {platform.lockoutMinutes} dakika kilit</li>
            <li><i className="bi bi-arrow-repeat me-2 text-primary" />Şifre değişince tüm oturumlar kapanır</li>
          </ul>
        </Card>
        {session.can('UserManage') && (
          <Card title="2FA kapsamı" icon="people" actions={<Link className="btn btn-sm btn-outline-secondary" to="/settings/users">Kullanıcılar</Link>}>
            <MfaCoverage />
          </Card>
        )}
      </div>
    </div>
  );
}

export function IpAccessSettings() {
  const toast = useToast();
  const { data, loading, error, reload } = useLoad(() => api.get('/api/v1/settings/ip-access'), []);
  const [form, setForm] = useState(null);
  const [entry, setEntry] = useState('');
  const [kind, setKind] = useState('allow');
  const [entryError, setEntryError] = useState('');
  useEffect(() => {
    if (data) setForm({ enabled: Boolean(data.enabled), allow: data.allow || [], deny: data.deny || [] });
  }, [data]);
  const save = async () => {
    const result = await api.put('/api/v1/settings/ip-access', form);
    setForm({ enabled: result.enabled, allow: result.allow, deny: result.deny });
    toast('IP erişim ayarları kaydedildi.');
  };
  const add = (event) => {
    event.preventDefault();
    const value = entry.trim();
    if (!value) return;
    if (form.allow.includes(value) || form.deny.includes(value)) {
      setEntryError('Bu adres zaten listede.');
      return;
    }
    setForm({ ...form, [kind]: [...form[kind], value] });
    setEntry('');
    setEntryError('');
  };
  const rules = form ? [...form.allow.map((value) => ({ value, kind: 'allow' })), ...form.deny.map((value) => ({ value, kind: 'deny' }))] : [];

  return <SettingsLayout icon="shield" title="IP Erişimi" parent={['Güvenlik politikası', '/settings/security']}
    subtitle={data?.scope === 'Platform' ? 'Site genelinde geçerli kurallar' : 'Firmanızın panel ve API erişim kuralları'}
    actions={form && <BusyButton className="btn btn-primary" onClick={save}>IP ayarlarını kaydet</BusyButton>}>
  <Card title="İzin ve engel listeleri">
    <ErrorAlert error={error} />
    {error && <BusyButton className="btn btn-outline-secondary" onClick={reload}>Tekrar dene</BusyButton>}
    {loading && <Spinner />}
    {form && <>
      <div className="form-check form-switch mb-3">
        <input id="ip-active" className="form-check-input" type="checkbox" checked={form.enabled}
          onChange={(e) => setForm({ ...form, enabled: e.target.checked })} />
        <label htmlFor="ip-active" className="form-check-label">IP erişim denetimini etkinleştir</label>
      </div>
      {data?.clientIp && <p className="small">Mevcut IP adresiniz <span className="font-monospace user-select-all">{data.clientIp}</span></p>}
      <form className="row g-2 align-items-end mb-3" onSubmit={add}>
        <div className="col-sm-6">
          <label htmlFor="ip-rule" className="form-label">IP adresi veya CIDR</label>
          <input id="ip-rule" className="form-control font-monospace" value={entry} maxLength={64} required
            placeholder="192.0.2.10 veya 192.0.2.0/24" onChange={(event) => { setEntry(event.target.value); setEntryError(''); }} />
        </div>
        <div className="col-sm-3">
          <label htmlFor="ip-kind" className="form-label">Kural</label>
          <select className="form-select" id="ip-kind" value={kind} onChange={(event) => setKind(event.target.value)}>
            <option value="allow">İzin ver</option><option value="deny">Engelle</option>
          </select>
        </div>
        <div className="col-auto"><button type="submit" className="btn btn-outline-primary">Kural ekle</button></div>
      </form>
      {entryError && <p role="alert" className="text-danger small">{entryError}</p>}
      <div className="table-responsive">
        <table className="table align-middle">
          <thead><tr><th>IP adresi veya ağ</th><th>Kural</th><th className="text-end">İşlem</th></tr></thead>
          <tbody>{rules.length ? rules.map((rule, index) => <tr key={`${rule.kind}-${rule.value}-${index}`}>
            <td className="font-monospace">{rule.value}</td>
            <td>{rule.kind === 'allow' ? 'İzin ver' : 'Engelle'}</td>
            <td className="text-end">
              <button type="button" className="btn btn-sm btn-outline-danger" aria-label={`${rule.value} kuralını kaldır`}
                onClick={() => setForm({ ...form, [rule.kind]: form[rule.kind].filter((value) => value !== rule.value) })}>Kaldır</button>
            </td>
          </tr>) : <tr><td colSpan={3} className="text-body-secondary">Henüz IP kuralı eklenmedi.</td></tr>}</tbody>
        </table>
      </div>
      <p className="form-text mb-0">Engel listesi önceliklidir. İzin listesine bir adres eklediğinizde yalnızca listedeki adresler erişebilir.</p>
    </>}
  </Card>
  </SettingsLayout>;
}

export function SecuritySettings() {
  const session = useSession();
  return (
    <SettingsLayout icon="shield-lock" title="Güvenlik politikası" subtitle="Oturum, iki adımlı doğrulama ve API anahtarı kuralları">
      {session.tenant ? <SecurityForm /> : <div className="alert alert-info">Firma politikası için üst çubuktan bir firma seçin.</div>}
    </SettingsLayout>
  );
}
