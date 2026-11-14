import { useCallback, useEffect, useState } from 'react';
import { api } from '../api.js';
import { fmtDate, ROLE_LABELS } from '../format.js';
import { BusyButton, Card, Empty, ErrorAlert, PageHeader, Spinner, useDialogs } from '../components/ui.jsx';
import { useSession } from '../session.jsx';

const TAKE = 100;

export function Audit() {
  const dialogs = useDialogs();
  const tenantId = useSession().tenant?.id;
  const [entityId, setEntityId] = useState('');
  const [userId, setUserId] = useState('');
  const [action, setAction] = useState('');
  const [role, setRole] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [result, setResult] = useState('');
  const [filter, setFilter] = useState({});
  const [rows, setRows] = useState(null);
  const [hasMore, setHasMore] = useState(false);
  const [verify, setVerify] = useState(null);
  const [error, setError] = useState(null);

  const load = useCallback(async (append, current) => {
    const q = new URLSearchParams({ take: String(TAKE) });
    for (const [key, value] of Object.entries(filter)) if (value) q.set(key, value);
    if (append && current?.length) q.set('before', String(current[current.length - 1].sequence));
    const page = await api.get(`/api/v1/audit/events?${q}`);
    setRows(append ? [...current, ...page] : page);
    setHasMore(page.length === TAKE);
    setError(null);
  }, [filter]);

  useEffect(() => {
    setRows(null);
    setVerify(null);
    load(false).catch(setError);
  }, [load, tenantId]);

  return (
    <>
      <PageHeader breadcrumb={[[ 'Ayarlar', '/settings' ], [ 'Denetim izi' ]]} icon="shield-check" title="Denetim izi" subtitle="İşlem kayıtları ve hash zinciri doğrulaması" />

      <Card title="Zincir bütünlüğü" className="mb-4" actions={(
        <BusyButton className="btn btn-outline-primary" onClick={async () => setVerify(await api.get('/api/v1/audit/verify'))}>
          <i className="bi bi-link-45deg" aria-hidden="true" />Hash zincirini doğrula
        </BusyButton>
      )}>
        {verify ? (verify.isValid
          ? <div role="status"><span className="badge text-bg-success">Bütünlük doğrulandı</span> {verify.eventCount} kayıt. Son hash <span className="mono">{(verify.lastHash || '').slice(0, 24)}…</span></div>
          : <div className="alert alert-danger mb-0" role="alert">İlk bozuk kayıt #{verify.firstBrokenSequence}</div>)
          : <p className="text-body-secondary mb-0">Bu oturumda zincir doğrulaması henüz çalıştırılmadı.</p>}
      </Card>

      <Card title="Denetim günlükleri" footer={hasMore && <BusyButton className="btn btn-sm btn-outline-secondary" onClick={() => load(true, rows)}>Daha eski kayıtlar</BusyButton>}>
        <form className="row g-2 mb-3" onSubmit={(event) => {
          event.preventDefault();
          setFilter({
            entityId: entityId.trim(), userId: userId.trim(), action: action.trim(), role, result,
            from: from ? new Date(`${from}T00:00:00+03:00`).toISOString() : '',
            until: to ? new Date(new Date(`${to}T00:00:00+03:00`).valueOf() + 86_400_000).toISOString() : '',
          });
        }}>
          <div className="col-md-3"><input className="form-control" placeholder="Kayıt kimliği" aria-label="Kayıt kimliği" value={entityId} onChange={(e) => setEntityId(e.target.value)} /></div>
          <div className="col-md-3"><input className="form-control" placeholder="Kullanıcı kodu" aria-label="Kullanıcı kodu" value={userId} onChange={(e) => setUserId(e.target.value)} /></div>
          <div className="col-md-3"><input className="form-control" placeholder="İşlem kodu" aria-label="İşlem kodu" value={action} onChange={(e) => setAction(e.target.value)} /></div>
          <div className="col-md-3"><select className="form-select" aria-label="İşlem anındaki rol" value={role} onChange={(e) => setRole(e.target.value)}>
            <option value="">Tüm roller</option>{Object.entries(ROLE_LABELS).map(([code, label]) => <option key={code} value={code}>{label}</option>)}
          </select></div>
          <div className="col-md-3"><label className="form-label" htmlFor="audit-from">Başlangıç</label><input className="form-control" id="audit-from" type="date" max={to || undefined} value={from} onChange={(e) => setFrom(e.target.value)} /></div>
          <div className="col-md-3"><label className="form-label" htmlFor="audit-to">Bitiş</label><input className="form-control" id="audit-to" type="date" min={from || undefined} value={to} onChange={(e) => setTo(e.target.value)} /></div>
          <div className="col-md-3"><label className="form-label" htmlFor="audit-result">Sonuç</label><select className="form-select" id="audit-result" value={result} onChange={(e) => setResult(e.target.value)}>
            <option value="">Tümü</option><option value="Success">Başarılı</option><option value="Failure">Başarısız</option>
          </select></div>
          <div className="col-auto align-self-end"><button className="btn btn-outline-primary" type="submit">Filtrele</button></div>
        </form>
        <ErrorAlert error={error} />
        {!rows ? !error && <Spinner /> : rows.length ? (
          <div className="table-responsive">
            <table className="table table-sm align-middle mb-0">
              <thead><tr><th>#</th><th>Zaman</th><th>Kullanıcı</th><th>İşlem</th><th>Kayıt</th><th>Sonuç</th><th>Hash</th><th>Ayrıntı</th></tr></thead>
              <tbody>
                {rows.map((e) => (
                  <tr key={e.sequence}>
                    <td className="mono">{e.sequence}</td>
                    <td className="small">{fmtDate(e.timestampUtc)}</td>
                    <td className="small">{e.userId || '—'} <span className="text-body-secondary">({e.actorType})</span></td>
                    <td className="mono small">{e.action}</td>
                    <td className="small">{e.entityType} <span className="mono">{(e.entityId || '').slice(0, 13)}</span></td>
                    <td>{e.result === 'Success' ? <span className="badge text-bg-success">Başarılı</span> : <span className="badge text-bg-danger" title={e.failureReason || ''}>Hata</span>}</td>
                    <td className="mono small">{e.hash.slice(0, 12)}…</td>
                    <td><button className="btn btn-sm btn-outline-secondary" type="button" onClick={() => dialogs.show('Denetim kaydı', (
                      <dl className="mb-0">
                        <dt>Kayıt kimliği</dt><dd className="text-break">{e.entityId || '—'}</dd>
                        <dt>IP adresi</dt><dd>{e.ip || '—'}</dd>
                        <dt>İz kimliği</dt><dd className="text-break">{e.traceId || e.correlationId || '—'}</dd>
                        <dt>İşlem verisi</dt><dd><pre className="text-wrap text-break mb-0">{formatAuditData(e.dataJson)}</pre></dd>
                      </dl>
                    ), 'lg')}>Aç</button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : <Empty icon="shield-check">Kayıt yok.</Empty>}
      </Card>
    </>
  );
}

function formatAuditData(value) {
  try { return value ? JSON.stringify(JSON.parse(value), null, 2) : '—'; }
  catch { return value; }
}
