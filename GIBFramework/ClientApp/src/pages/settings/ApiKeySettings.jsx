import { useState } from 'react';
import { Link } from 'react-router';
import { api } from '../../api.js';
import { fmtDate, ROLE_LABELS } from '../../format.js';
import { BusyButton, Card, Empty, Modal, Spinner, useDialogs, useLoad, useToast } from '../../components/ui.jsx';
import { SettingsLayout, TenantRequired } from './SettingsLayout.jsx';

function ApiKeys() {
  const dialogs = useDialogs();
  const toast = useToast();
  const [created, setCreated] = useState(null);
  const { data, loading, reload } = useLoad(async () => {
    const [keys, roles, security] = await Promise.all([
      api.get('/api/v1/api-keys'),
      api.get('/api/v1/api-keys/roles'),
      api.get('/api/v1/settings/security').catch(() => null),
    ]);
    return { keys, roles, maxDays: security?.policy?.apiKeyMaxDays || null };
  }, []);

  const create = async () => {
    const max = data.maxDays;
    const days = [30, 90, 180, 365, 730].filter((d) => !max || d <= max);
    if (max && !days.includes(max)) days.push(max);
    const expiry = [
      ...days.map((d) => ({ value: String(d), label: d % 365 === 0 ? `${d / 365} yıl` : `${d} gün` })),
      ...(max ? [] : [{ value: '', label: 'Süresiz' }]),
    ];
    const f = await dialogs.form({
      title: 'Yeni API anahtarı', submitText: 'Oluştur', size: 'lg',
      fields: [
        { name: 'name', label: 'Uygulama adı', required: true, min: 3 },
        { name: 'roles', label: 'Yetkiler', type: 'checkboxes', value: ['ApiClient'], options: data.roles.map((r) => ({ value: r, label: ROLE_LABELS[r] || r })) },
        { name: 'expiresInDays', label: max ? `Geçerlilik (politika: en fazla ${max} gün)` : 'Geçerlilik', type: 'select', value: String(Math.min(365, max || 365)), options: expiry },
      ],
    });
    if (!f) return;
    if (!f.roles.length) {
      toast('En az bir yetki seçin.', 'warning');
      return;
    }
    setCreated(await api.post('/api/v1/api-keys', { name: f.name, roles: f.roles, expiresInDays: f.expiresInDays ? Number(f.expiresInDays) : null }));
    await reload();
  };

  return (
    <SettingsLayout icon="key" title="API anahtarları" subtitle="Yalnızca Genel Müdür / CEO oluşturabilir ve yetki atayabilir."
      actions={(
        <>
          <Link className="btn btn-outline-primary btn-pill px-3" to="/developers"><i className="bi bi-code-square me-1" />Geliştirici dokümanı</Link>
          {data && <BusyButton className="btn btn-primary btn-pill px-4" onClick={create}><i className="bi bi-plus-lg me-1" />Yeni anahtar</BusyButton>}
        </>
      )}>
      <Card bodyClass="">
        {loading ? <div className="px-3"><Spinner /></div> : data.keys.length ? (
          <div className="table-responsive">
            <table className="table align-middle mb-0">
              <thead><tr><th>Uygulama</th><th>Anahtar</th><th>Yetkiler</th><th>Son kullanım</th><th>Durum</th><th /></tr></thead>
              <tbody>
                {data.keys.map((k) => {
                  const active = !k.revokedAt && (!k.expiresAt || new Date(k.expiresAt) > new Date());
                  return (
                    <tr key={k.id} className={active ? '' : 'opacity-50'}>
                      <td><div className="fw-semibold">{k.name}</div><div className="small text-body-secondary">{k.createdBy} · {fmtDate(k.createdAt)}</div></td>
                      <td className="font-monospace small">gfk_{k.prefix}_••••</td>
                      <td>{k.roles.map((r) => <span key={r} className="badge text-bg-light border me-1 mb-1">{ROLE_LABELS[r] || r}</span>)}</td>
                      <td className="small">{k.lastUsedAt ? fmtDate(k.lastUsedAt) : '—'}</td>
                      <td>
                        {k.revokedAt ? <span className="badge text-bg-dark">İptal edildi</span> : active ? <span className="badge text-bg-success">Aktif</span> : <span className="badge text-bg-secondary">Süresi doldu</span>}
                        {k.expiresAt && active && <div className="small text-body-secondary">{fmtDate(k.expiresAt)}</div>}
                      </td>
                      <td className="text-end">
                        {!k.revokedAt && (
                          <BusyButton className="btn btn-sm btn-outline-danger" onClick={async () => {
                            if (!await dialogs.confirm('Anahtarı iptal et', `"${k.name}" anahtarıyla yapılan istekler hemen reddedilir.`, 'İptal et', 'danger')) return;
                            await api.del(`/api/v1/api-keys/${k.id}`);
                            toast('API anahtarı iptal edildi.', 'warning');
                            await reload();
                          }}>İptal et</BusyButton>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        ) : <Empty icon="key">Henüz API anahtarı yok.</Empty>}
      </Card>
      {created && (
        <Modal title="API anahtarı oluşturuldu" onClose={() => setCreated(null)}
          footer={<div className="modal-footer"><button type="button" className="btn btn-primary btn-pill px-4" onClick={() => setCreated(null)}>Kaydettim</button></div>}>
          <div className="modal-body">
            <p className="mb-2">Bu anahtar yalnızca şimdi gösterilir.</p>
            <div className="input-group">
              <input className="form-control font-monospace" readOnly value={created.key} aria-label="API anahtarı" onFocus={(e) => e.target.select()} />
              <button className="btn btn-outline-primary" type="button" aria-label="Kopyala" onClick={async () => {
                try {
                  await navigator.clipboard.writeText(created.key);
                  toast('Kopyalandı.', 'info');
                } catch {
                  toast('Kopyalanamadı; metni seçip kopyalayın.', 'warning');
                }
              }}><i className="bi bi-clipboard" /></button>
            </div>
          </div>
        </Modal>
      )}
    </SettingsLayout>
  );
}

export const ApiKeySettings = () => <TenantRequired><ApiKeys /></TenantRequired>;
