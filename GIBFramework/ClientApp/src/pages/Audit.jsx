import { useCallback, useEffect, useState } from 'react';
import { api } from '../api.js';
import { fmtDate } from '../format.js';
import { BusyButton, Card, Empty, PageHeader, Spinner, useToast } from '../components/ui.jsx';

const TAKE = 100;

export function Audit() {
  const toast = useToast();
  const [entityId, setEntityId] = useState('');
  const [filter, setFilter] = useState('');
  const [rows, setRows] = useState(null);
  const [hasMore, setHasMore] = useState(false);
  const [verify, setVerify] = useState(null);

  const load = useCallback(async (append, current) => {
    const q = new URLSearchParams({ take: String(TAKE) });
    if (filter) q.set('entityId', filter);
    if (append && current?.length) q.set('before', String(current[current.length - 1].sequence));
    const page = await api.get(`/api/v1/audit/events?${q}`);
    setRows(append ? [...current, ...page] : page);
    setHasMore(page.length === TAKE);
  }, [filter]);

  useEffect(() => { load(false).catch(toast.error); }, [load]);

  return (
    <>
      <PageHeader icon="shield-check" title="Denetim izi"
        actions={(
          <BusyButton className="btn btn-outline-success" onClick={async () => setVerify(await api.get('/api/v1/audit/verify'))}>
            <i className="bi bi-link-45deg me-1" />Hash zincirini doğrula
          </BusyButton>
        )} />

      {verify && (verify.isValid
        ? <div className="alert alert-success"><i className="bi bi-check-circle me-1" />Zincir sağlam: {verify.eventCount} kayıt. Son hash <span className="mono">{(verify.lastHash || '').slice(0, 24)}…</span></div>
        : <div className="alert alert-danger"><i className="bi bi-x-octagon me-1" />Zincir bozuk! İlk bozuk kayıt: #{verify.firstBrokenSequence}</div>)}

      <Card className="mb-4">
        <form className="row g-2" onSubmit={(e) => { e.preventDefault(); setFilter(entityId.trim()); }}>
          <div className="col-md-6"><input className="form-control" placeholder="Kayıt kimliği ile filtrele (ör. fatura id)" aria-label="Kayıt kimliği" value={entityId} onChange={(e) => setEntityId(e.target.value)} /></div>
          <div className="col-auto"><button className="btn btn-outline-primary" type="submit">Filtrele</button></div>
        </form>
      </Card>

      <Card bodyClass="" footer={hasMore && <BusyButton className="btn btn-sm btn-outline-secondary" onClick={() => load(true, rows)}>Daha eski kayıtlar</BusyButton>}>
        {!rows ? <div className="px-3"><Spinner /></div> : rows.length ? (
          <div className="table-responsive">
            <table className="table table-sm align-middle mb-0">
              <thead><tr><th>#</th><th>Zaman</th><th>Kullanıcı</th><th>İşlem</th><th>Kayıt</th><th>Sonuç</th><th>Hash</th></tr></thead>
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
