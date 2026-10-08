import { api } from '../api.js';
import { ANNOUNCEMENT_AUDIENCES, ANNOUNCEMENT_LEVELS, fmtDate, fromLocalInput, toLocalInput } from '../format.js';
import { BusyButton, Card, Empty, PageHeader, Spinner, useDialogs, useLoad, useToast } from '../components/ui.jsx';

function State({ a }) {
  const now = Date.now();
  if (!a.isActive) return <span className="badge text-bg-dark">Kapalı</span>;
  if (new Date(a.startsAt).getTime() > now) return <span className="badge text-bg-info">Planlandı</span>;
  if (a.endsAt && new Date(a.endsAt).getTime() <= now) return <span className="badge text-bg-secondary">Süresi doldu</span>;
  return <span className="badge text-bg-success">Yayında</span>;
}

export function Announcements() {
  const dialogs = useDialogs();
  const toast = useToast();
  const { data, loading, reload } = useLoad(async () => {
    const [rows, tenants] = await Promise.all([api.get('/api/v1/announcements'), api.get('/api/v1/tenants').catch(() => [])]);
    return { rows, tenants };
  }, []);

  if (loading && !data) return <Spinner />;
  const tenantName = Object.fromEntries(data.tenants.map((t) => [t.id, t.name]));

  const ask = async (a) => {
    const f = await dialogs.form({
      title: a ? 'Duyuruyu düzenle' : 'Yeni duyuru', submitText: a ? 'Kaydet' : 'Yayınla',
      fields: [
        { name: 'title', label: 'Başlık', required: true, value: a?.title },
        { name: 'message', label: 'Mesaj', type: 'textarea', rows: 5, required: true, value: a?.message },
        { name: 'level', label: 'Tür', type: 'select', value: a?.level || 'Info', options: Object.entries(ANNOUNCEMENT_LEVELS).map(([value, [label]]) => ({ value, label })) },
        { name: 'audience', label: 'Kime', type: 'select', value: a?.audience || 'Everyone', options: Object.entries(ANNOUNCEMENT_AUDIENCES).map(([value, label]) => ({ value, label })) },
        { name: 'tenantId', label: 'Firma', type: 'select', value: a?.tenantId || '', options: [{ value: '', label: '—' }, ...data.tenants.map((t) => ({ value: t.id, label: t.name }))] },
        { name: 'startsAt', label: 'Başlangıç', type: 'datetime-local', value: toLocalInput(a?.startsAt) },
        { name: 'endsAt', label: 'Bitiş', type: 'datetime-local', value: toLocalInput(a?.endsAt) },
        { name: 'isActive', label: 'Durum', type: 'select', value: a && !a.isActive ? 'false' : 'true', options: [{ value: 'true', label: 'Yayında' }, { value: 'false', label: 'Kapalı' }] },
      ],
    });
    if (!f) return null;
    return {
      title: f.title,
      message: f.message,
      level: f.level,
      audience: f.audience,
      tenantId: f.audience === 'Tenant' ? f.tenantId || null : null,
      startsAt: fromLocalInput(f.startsAt),
      endsAt: fromLocalInput(f.endsAt),
      isActive: f.isActive === 'true',
    };
  };

  return (
    <>
      <PageHeader icon="megaphone" title="Duyurular"
        actions={(
          <BusyButton onClick={async () => {
            const body = await ask(null);
            if (!body) return;
            await api.post('/api/v1/announcements', body);
            toast('Duyuru yayınlandı.');
            await reload();
          }}><i className="bi bi-plus-lg me-1" />Yeni duyuru</BusyButton>
        )} />
      <Card bodyClass="">
        {data.rows.length ? (
          <div className="table-responsive">
            <table className="table align-middle mb-0">
              <thead><tr><th>Başlık</th><th>Tür</th><th>Kime</th><th>Yayın</th><th>Durum</th><th className="text-end">İşlemler</th></tr></thead>
              <tbody>
                {data.rows.map((a) => {
                  const [label, color, icon] = ANNOUNCEMENT_LEVELS[a.level] || ANNOUNCEMENT_LEVELS.Info;
                  return (
                    <tr key={a.id}>
                      <td><div className="fw-semibold">{a.title}</div><div className="small text-body-secondary text-truncate" style={{ maxWidth: '32rem' }}>{a.message}</div></td>
                      <td><span className={`badge text-bg-${color}`}><i className={`bi bi-${icon} me-1`} />{label}</span></td>
                      <td>{ANNOUNCEMENT_AUDIENCES[a.audience] || a.audience}{a.tenantId && <div className="small text-body-secondary">{tenantName[a.tenantId] || a.tenantId}</div>}</td>
                      <td className="small">{fmtDate(a.startsAt)}<br />{a.endsAt ? fmtDate(a.endsAt) : 'Süresiz'}</td>
                      <td><State a={a} /></td>
                      <td className="text-end">
                        <div className="btn-group btn-group-sm">
                          <BusyButton className="btn btn-outline-primary" title="Düzenle" onClick={async () => {
                            const body = await ask(a);
                            if (!body) return;
                            await api.put(`/api/v1/announcements/${a.id}`, body);
                            toast('Duyuru güncellendi.');
                            await reload();
                          }}><i className="bi bi-pencil" /></BusyButton>
                          <BusyButton className="btn btn-outline-danger" title="Sil" onClick={async () => {
                            if (!await dialogs.confirm('Duyuruyu sil', `"${a.title}" silinsin mi?`, 'Sil', 'danger')) return;
                            await api.del(`/api/v1/announcements/${a.id}`);
                            toast('Duyuru silindi.');
                            await reload();
                          }}><i className="bi bi-trash" /></BusyButton>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        ) : <Empty icon="megaphone">Duyuru yok.</Empty>}
      </Card>
    </>
  );
}
