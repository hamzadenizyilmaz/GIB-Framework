import { useState } from 'react';
import { Link } from 'react-router';
import { api } from '../api.js';
import { todayIso } from '../format.js';
import { BusyButton, Card, Empty, ErrorAlert, PageHeader, Spinner, useDialogs, useLoad } from '../components/ui.jsx';

const daysAgo = (n) => {
  const d = new Date(`${todayIso()}T12:00:00+03:00`);
  d.setDate(d.getDate() - n);
  return d.toISOString().slice(0, 10);
};

const pick = (row, ...keys) => keys.map((k) => row?.[k]).find((v) => v !== undefined && v !== null && v !== '');

export function GibDocuments() {
  const dialogs = useDialogs();
  const { data: status, loading } = useLoad(() => api.get('/api/v1/gib-portal/status'), []);
  const [tab, setTab] = useState('issued');
  const [range, setRange] = useState({ from: daysAgo(30), to: todayIso() });
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);

  if (loading) return <Spinner />;

  const load = async (which = tab) => {
    setError(null);
    try {
      const url = which === 'issued' ? '/api/v1/gib-portal/documents' : '/api/v1/gib-portal/documents/issued-to-me';
      const rows = await api.get(`${url}?from=${range.from}&to=${range.to}`);
      setResult({ tab: which, rows });
    } catch (err) {
      setResult(null);
      setError(err);
    }
  };

  const view = async (ettn, approved) => {
    const html = await api.text(`/api/v1/gib-portal/documents/${ettn}/html?approved=${approved}`);
    await dialogs.show('GİB görünümü', <iframe className="preview-frame" sandbox="" title="Belge önizleme" srcDoc={html} />, 'xl');
  };

  if (!status.connected) {
    return (
      <>
        <PageHeader icon="files" title="GİB portal belgeleri" />
        <div className="alert alert-info d-flex justify-content-between align-items-center">
          <span><i className="bi bi-plug me-1" />Belgeleri görmek için GİB e-Arşiv Portal'a bağlanın.</span>
          <Link className="btn btn-sm btn-primary" to="/gib-portal">Bağlan</Link>
        </div>
      </>
    );
  }

  const rows = result?.tab === tab ? result.rows : null;

  return (
    <>
      <PageHeader icon="files" title="GİB portal belgeleri" subtitle={`${status.portalTitle} · ${status.environment === 'Production' ? 'Canlı' : 'Test'} ortam`} />
      <Card bodyClass="">
        <div className="card-header bg-body">
          <ul className="nav nav-tabs card-header-tabs">
            {[['issued', 'Düzenlediğim belgeler'], ['received', 'Adıma düzenlenen belgeler']].map(([key, label]) => (
              <li className="nav-item" key={key}>
                <button type="button" className={`nav-link ${tab === key ? 'active' : ''}`} onClick={() => { setTab(key); setError(null); }}>{label}</button>
              </li>
            ))}
          </ul>
        </div>
        <div className="card-body">
          <div className="row g-2 align-items-end mb-3">
            <div className="col-sm-4 col-lg-3"><label className="form-label small" htmlFor="g-from">Başlangıç</label><input className="form-control" type="date" id="g-from" value={range.from} max={range.to} onChange={(e) => setRange((r) => ({ ...r, from: e.target.value }))} /></div>
            <div className="col-sm-4 col-lg-3"><label className="form-label small" htmlFor="g-to">Bitiş</label><input className="form-control" type="date" id="g-to" value={range.to} max={todayIso()} onChange={(e) => setRange((r) => ({ ...r, to: e.target.value }))} /></div>
            <div className="col-sm-4 col-lg-2 d-grid"><BusyButton onClick={() => load()}><i className="bi bi-arrow-repeat me-1" />Getir</BusyButton></div>
            <div className="col-lg-4 small text-body-secondary">En fazla 31 günlük aralık sorgulanabilir.</div>
          </div>
          <ErrorAlert error={error} />
          {rows === null ? <Empty icon="files">Tarih aralığı seçip “Getir”e basın.</Empty> : rows.length === 0 ? <Empty icon="inbox">Bu aralıkta belge yok.</Empty> : (
            <div className="table-responsive">
              <table className="table table-hover align-middle mb-0">
                <thead><tr><th>Belge no</th><th>Tarih</th><th>{tab === 'issued' ? 'Alıcı' : 'Düzenleyen'}</th><th>ETTN</th><th>Durum</th><th /></tr></thead>
                <tbody>
                  {rows.map((r) => {
                    const raw = r.raw || r;
                    const ettn = r.ettn || pick(raw, 'ettn');
                    const approval = r.approvalStatus || pick(raw, 'onayDurumu');
                    const approved = approval === 'Onaylandı';
                    return (
                      <tr key={ettn}>
                        <td className="font-monospace">{r.documentNumber || pick(raw, 'belgeNumarasi') || '—'}</td>
                        <td>{r.documentDate || pick(raw, 'belgeTarihi') || '—'}</td>
                        <td>
                          <div className="text-truncate" style={{ maxWidth: '20rem' }}>{r.recipientTitle || pick(raw, 'aliciUnvanAdSoyad', 'saticiUnvanAdSoyad', 'unvan') || '—'}</div>
                          <div className="small text-body-secondary font-monospace">{r.recipientTaxId || pick(raw, 'aliciVknTckn', 'saticiVknTckn', 'vknTckn') || ''}</div>
                        </td>
                        <td className="mono small">{ettn}</td>
                        <td>{approval ? <span className={`badge text-bg-${approved ? 'success' : 'warning'}`}>{approval}</span> : '—'}</td>
                        <td className="text-end">{ettn && <BusyButton className="btn btn-sm btn-outline-primary" onClick={() => view(ettn, approved)}><i className="bi bi-eye me-1" />Görüntüle</BusyButton>}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </Card>
    </>
  );
}
