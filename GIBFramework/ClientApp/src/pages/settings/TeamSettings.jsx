import { useMemo, useState } from 'react';
import { api } from '../../api.js';
import { fmtDate } from '../../format.js';
import { useSession } from '../../session.jsx';
import { BusyButton, Card, Empty, Spinner, useDialogs, useLoad, useToast } from '../../components/ui.jsx';
import { SettingsLayout } from './SettingsLayout.jsx';

function useRoles() {
  return useLoad(() => api.get('/api/v1/roles'), []);
}

export function RoleBadges({ roles, catalog }) {
  const byCode = Object.fromEntries((catalog || []).map((r) => [r.code, r]));
  return [...roles].sort((a, b) => (byCode[b]?.rank || 0) - (byCode[a]?.rank || 0)).map((r) => (
    <span key={r} className={`badge me-1 mb-1 ${byCode[r]?.rank >= 80 ? 'text-bg-primary' : 'text-bg-light border'}`}>{byCode[r]?.label || r}</span>
  ));
}

function ResetRequests({ users, onReset }) {
  const toast = useToast();
  const { data, loading, reload } = useLoad(() => api.get('/api/v1/users/password-reset-requests'), []);
  if (loading) return <Spinner />;
  if (!data.length) return <Empty icon="key">Şifre sıfırlama talebi yok.</Empty>;
  return (
    <div className="table-responsive">
      <table className="table align-middle mb-0">
        <thead><tr><th>Kullanıcı</th><th>Talep zamanı</th><th>IP</th><th>Durum</th><th className="text-end">İşlem</th></tr></thead>
        <tbody>
          {data.map((r) => {
            const user = users.find((u) => u.id === r.userId);
            return (
              <tr key={r.id} className={r.status === 'Open' ? '' : 'opacity-50'}>
                <td><div className="fw-semibold">{r.displayName || r.userCode}</div><div className="small font-monospace text-body-secondary">{r.userCode}</div></td>
                <td className="small">{fmtDate(r.requestedAt)}</td>
                <td className="small font-monospace">{r.ip || '—'}</td>
                <td>{r.status === 'Open' ? <span className="badge text-bg-warning">Bekliyor</span> : <span className="badge text-bg-secondary">{r.status === 'Done' ? 'Sıfırlandı' : 'Kapatıldı'}{r.handledBy ? ` · ${r.handledBy}` : ''}</span>}</td>
                <td className="text-end">
                  {r.status === 'Open' && (
                    <div className="btn-group btn-group-sm">
                      {user && (
                        <BusyButton className="btn btn-outline-warning" onClick={async () => {
                          if (await onReset(user)) {
                            await api.post(`/api/v1/users/password-reset-requests/${r.id}/close`, { status: 'Done' });
                            await reload();
                          }
                        }}><i className="bi bi-key me-1" />Şifre sıfırla</BusyButton>
                      )}
                      <BusyButton className="btn btn-outline-secondary" onClick={async () => {
                        await api.post(`/api/v1/users/password-reset-requests/${r.id}/close`, { status: 'Dismissed' });
                        toast('Talep kapatıldı.', 'info');
                        await reload();
                      }}>Kapat</BusyButton>
                    </div>
                  )}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

export function UsersSettings() {
  const session = useSession();
  const dialogs = useDialogs();
  const toast = useToast();
  const [tab, setTab] = useState('users');
  const platform = session.hasRole('PlatformSuperAdmin');
  const { data: roleData } = useRoles();
  const { data, loading, reload } = useLoad(async () => {
    const [users, tenants] = await Promise.all([api.get('/api/v1/users'), platform ? api.get('/api/v1/tenants').catch(() => []) : []]);
    return { users, tenants };
  }, [platform]);

  const myRank = useMemo(() => Math.max(0, ...((roleData?.roles || []).filter((r) => session.permissions.roles.includes(r.code)).map((r) => r.rank))), [roleData, session.permissions]);
  if (loading || !roleData) return <SettingsLayout icon="people" title="Kullanıcılar"><Spinner /></SettingsLayout>;

  const catalog = roleData.roles;
  const assignable = catalog.filter((r) => r.assignable).map((r) => ({ value: r.code, label: `${r.label} · ${r.group}` }));
  const rankOf = (roles) => Math.max(0, ...roles.map((code) => catalog.find((r) => r.code === code)?.rank || 0));
  const canManage = (u) => platform || (rankOf(u.roles) < myRank && u.userCode !== session.permissions.userId);
  const tenantName = Object.fromEntries((data.tenants || []).map((t) => [t.id, t.name]));

  const SEND = [{ value: 'Email', label: 'E-posta ile gönder' }, { value: 'Sms', label: 'SMS ile gönder' }];
  const report = (notification, done) => {
    if (!notification) {
      toast(done);
      return;
    }
    if (notification.queued > 0) toast(`${done} Giriş bilgileri ${notification.queued} kanaldan gönderim kuyruğuna alındı.`);
    else toast(`${done} Bildirim gönderilemedi: ${notification.skipped.join(' ')}`, 'warning');
  };

  const create = async () => {
    const f = await dialogs.form({
      title: 'Yeni kullanıcı', submitText: 'Oluştur', size: 'lg',
      fields: [
        { name: 'userCode', label: 'Kullanıcı kodu', required: true, pattern: '[A-Za-z0-9._-]{3,50}', help: '3-50 karakter: harf, rakam, nokta, tire, alt çizgi.' },
        { name: 'displayName', label: 'Ad soyad', required: true },
        { name: 'email', label: 'E-posta', type: 'email' },
        { name: 'phone', label: 'Cep telefonu', type: 'tel', inputmode: 'tel', help: 'SMS bildirimleri için (5XX XXX XX XX).' },
        ...(platform ? [{ name: 'tenantId', label: 'Firma', type: 'select', value: session.tenantOverride || '', options: [{ value: '', label: '— Platform —' }, ...data.tenants.map((t) => ({ value: t.id, label: t.name }))] }] : []),
        { name: 'roles', label: 'Roller', type: 'checkboxes', options: assignable },
        { name: 'initialPassword', label: 'Geçici şifre', type: 'password', required: true, min: 10, help: 'Kullanıcı ilk girişte değiştirir.', autocomplete: 'new-password' },
        { name: 'sendCredentials', label: 'Giriş bilgilerini kullanıcıya gönder', type: 'checkboxes', options: SEND, value: ['Email'] },
      ],
    });
    if (!f) return;
    if (!f.roles.length) {
      toast('En az bir rol seçin.', 'warning');
      return;
    }
    const result = await api.post('/api/v1/users', { ...f, tenantId: f.tenantId || null, email: f.email || null, phone: f.phone || null });
    report(result?.notification, 'Kullanıcı oluşturuldu.');
    await reload();
  };

  const editProfile = async (u) => {
    const f = await dialogs.form({
      title: `Profil — ${u.displayName}`,
      fields: [
        { name: 'displayName', label: 'Ad soyad', required: true, value: u.displayName },
        { name: 'email', label: 'E-posta', type: 'email', value: u.email || '' },
        { name: 'phone', label: 'Cep telefonu', type: 'tel', inputmode: 'tel', value: u.phone || '', help: 'Boş bırakılırsa silinir.' },
      ],
    });
    if (!f) return;
    await api.patch(`/api/v1/users/${u.id}`, { displayName: f.displayName, email: f.email || '', phone: f.phone || '' });
    toast('Profil güncellendi.');
    await reload();
  };

  const editRoles = async (u) => {
    const f = await dialogs.form({ title: `Roller — ${u.displayName}`, size: 'lg', fields: [{ name: 'roles', label: 'Roller', type: 'checkboxes', value: u.roles.filter((r) => assignable.some((a) => a.value === r)), options: assignable }] });
    if (!f) return;
    if (!f.roles.length) {
      toast('En az bir rol seçin.', 'warning');
      return;
    }
    await api.patch(`/api/v1/users/${u.id}`, { roles: f.roles });
    toast('Roller güncellendi.');
    await reload();
  };

  const resetPassword = async (u) => {
    const f = await dialogs.form({
      title: `Şifre sıfırla — ${u.displayName}`, submitVariant: 'warning', submitText: 'Sıfırla',
      fields: [
        { name: 'temporaryPassword', label: 'Geçici şifre', type: 'password', required: true, min: 10, autocomplete: 'new-password' },
        { name: 'send', label: 'Yeni şifreyi kullanıcıya gönder', type: 'checkboxes', options: SEND, value: u.email ? ['Email'] : [] },
      ],
    });
    if (!f) return false;
    const result = await api.post(`/api/v1/users/${u.id}/reset-password`, f);
    report(result?.notification, 'Şifre sıfırlandı; kullanıcı ilk girişte değiştirecek.');
    await reload();
    return true;
  };

  return (
    <SettingsLayout icon="people" title="Kullanıcılar" subtitle={`${data.users.length} kullanıcı`}
      actions={<BusyButton className="btn btn-primary btn-pill px-4" onClick={create}><i className="bi bi-person-plus me-1" />Yeni kullanıcı</BusyButton>}>
      <Card bodyClass="">
        <div className="card-header bg-body">
          <ul className="nav nav-tabs card-header-tabs">
            <li className="nav-item"><button type="button" className={`nav-link ${tab === 'users' ? 'active' : ''}`} onClick={() => setTab('users')}>Kullanıcılar</button></li>
            <li className="nav-item"><button type="button" className={`nav-link ${tab === 'requests' ? 'active' : ''}`} onClick={() => setTab('requests')}>Şifre talepleri</button></li>
          </ul>
        </div>
        {tab === 'requests' ? <div className="card-body"><ResetRequests users={data.users} onReset={resetPassword} /></div> : (
          <div className="table-responsive">
            <table className="table align-middle mb-0">
              <thead><tr><th>Kullanıcı</th>{platform && <th>Firma</th>}<th>Roller</th><th>Durum</th><th>Son giriş</th><th className="text-end">İşlemler</th></tr></thead>
              <tbody>
                {[...data.users].sort((a, b) => rankOf(b.roles) - rankOf(a.roles)).map((u) => {
                  const manageable = canManage(u);
                  return (
                    <tr key={u.id}>
                      <td>
                        <div className="d-flex align-items-center gap-2">
                          <span className="avatar">{(u.displayName || u.userCode).split(' ').map((x) => x[0]).join('').slice(0, 2).toLocaleUpperCase('tr-TR')}</span>
                          <div><div className="fw-semibold">{u.displayName}</div><div className="small text-body-secondary"><span className="font-monospace">{u.userCode}</span>{u.email ? ` · ${u.email}` : ''}{u.phone ? ` · ${u.phone}` : ''}</div></div>
                        </div>
                      </td>
                      {platform && <td className="small">{u.tenantId ? tenantName[u.tenantId] || '—' : 'Platform'}</td>}
                      <td><RoleBadges roles={u.roles} catalog={catalog} /></td>
                      <td>
                        {u.isActive ? <span className="badge text-bg-success">Aktif</span> : <span className="badge text-bg-dark">Pasif</span>}
                        {u.isLockedOut && <span className="badge text-bg-danger ms-1">Kilitli</span>}
                        {u.totpEnabled && <span className="badge text-bg-info ms-1">2FA</span>}
                        {u.mustChangePassword && <span className="badge text-bg-warning ms-1">Şifre değişmeli</span>}
                      </td>
                      <td className="small">{u.lastLoginAt ? fmtDate(u.lastLoginAt) : '—'}</td>
                      <td className="text-end">
                        {manageable ? (
                          <div className="btn-group btn-group-sm">
                            <BusyButton className="btn btn-outline-secondary" title="Profil" onClick={() => editProfile(u)}><i className="bi bi-pencil" /></BusyButton>
                            <BusyButton className="btn btn-outline-primary" title="Roller" onClick={() => editRoles(u)}><i className="bi bi-person-badge" /></BusyButton>
                            <BusyButton className="btn btn-outline-warning" title="Şifre sıfırla" onClick={() => resetPassword(u)}><i className="bi bi-key" /></BusyButton>
                            <BusyButton className={`btn btn-outline-${u.isActive ? 'danger' : 'success'}`} title={u.isActive ? 'Pasifleştir' : 'Aktifleştir'} onClick={async () => {
                              await api.patch(`/api/v1/users/${u.id}`, { isActive: !u.isActive });
                              toast('Kullanıcı durumu güncellendi.');
                              await reload();
                            }}><i className={`bi bi-${u.isActive ? 'person-dash' : 'person-check'}`} /></BusyButton>
                          </div>
                        ) : <i className="bi bi-lock text-body-tertiary" title="Üst yetki seviyesi" />}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </SettingsLayout>
  );
}

export function RolesSettings() {
  const session = useSession();
  const { data, loading } = useRoles();
  const [open, setOpen] = useState(null);
  if (loading) return <SettingsLayout icon="diagram-3" title="Roller ve yetkiler"><Spinner /></SettingsLayout>;
  const roles = [...data.roles].sort((a, b) => b.rank - a.rank);
  const labels = Object.fromEntries(data.policies.map((p) => [p.code, p.label]));
  const mine = session.permissions.roles;

  return (
    <SettingsLayout icon="diagram-3" title="Roller ve yetkiler">
      <div className="card shadow-sm" style={{ maxWidth: '46rem' }}>
        <div className="list-group list-group-flush">
          {roles.map((r) => (
            <div key={r.code} className="list-group-item px-3 py-2">
              <button type="button" className="btn btn-link text-reset text-decoration-none p-0 w-100 d-flex align-items-center gap-2 text-start"
                aria-expanded={open === r.code} onClick={() => setOpen(open === r.code ? null : r.code)}>
                <span className="badge rounded-pill text-bg-light border font-monospace" style={{ minWidth: '2.4rem' }}>{r.rank}</span>
                <span className="fw-semibold small flex-grow-1">{r.label}</span>
                {mine.includes(r.code) && <span className="badge text-bg-primary">Rolünüz</span>}
                <span className="small text-body-secondary">{r.policies.length} yetki</span>
                <i className={`bi bi-chevron-${open === r.code ? 'up' : 'down'} small text-body-secondary`} />
              </button>
              {open === r.code && (
                <div className="pt-2 ps-5">
                  <div className="small text-body-secondary mb-2">{r.description}</div>
                  <div className="d-flex flex-wrap gap-1">
                    {r.policies.map((c) => <span key={c} className="badge text-bg-light border fw-normal">{labels[c] || c}</span>)}
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>
      </div>
    </SettingsLayout>
  );
}
