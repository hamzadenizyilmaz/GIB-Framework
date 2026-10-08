import { useMemo, useRef, useState } from 'react';
import { api } from '../api.js';
import { fmtNumber, todayIso } from '../format.js';
import { useSession } from '../session.jsx';
import { BusyButton, Card, Empty, ErrorAlert, PageHeader, Spinner, useLoad, useToast } from '../components/ui.jsx';

const TYPES = { Defterdarlik: 'Defterdarlık', VergiDairesiMudurlugu: 'Vergi Dairesi Müdürlüğü', VergiDairesiBaskanligi: 'Vergi Dairesi Başkanlığı', Malmudurlugu: 'Malmüdürlüğü', Other: 'Diğer' };
const DEFAULT_SOURCE = 'https://cdn.gib.gov.tr/api/gibportal-file/file/getFileResources?objectKey=arsiv%2Fyardim-kaynaklar%2Fyararli-bilgiler%2FDefterdarl%C4%B1kveVergiDaireleriListesi.pdf';

function Import() {
  const toast = useToast();
  const fileRef = useRef(null);
  const [sourceUrl, setSourceUrl] = useState(DEFAULT_SOURCE);
  const [publishedAt, setPublishedAt] = useState('');
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);
  const [snapshotId, setSnapshotId] = useState('');
  const [effectiveFrom, setEffectiveFrom] = useState(todayIso());

  const doImport = async () => {
    const file = fileRef.current.files[0];
    if (!file) {
      setError({ message: 'Bir PDF veya CSV dosyası seçin.' });
      return;
    }
    const data = new FormData();
    data.append('file', file);
    data.append('sourceUrl', sourceUrl);
    if (publishedAt) data.append('publishedAt', publishedAt);
    try {
      const r = await api.upload('/api/v1/tax-offices/imports', data);
      setError(null);
      setResult(r);
      setSnapshotId(r.snapshot.id);
    } catch (err) {
      setError(err);
    }
  };

  const count = (kind) => result.diff.changes.filter((c) => c.kind === kind).length;

  return (
    <Card title="Resmî listeyi içe aktar" icon="cloud-upload">
      <div className="row g-2 align-items-end">
        <div className="col-md-5"><label className="form-label" htmlFor="tf">Dosya (GİB PDF veya CSV)</label><input ref={fileRef} className="form-control" type="file" id="tf" accept=".pdf,.csv" /></div>
        <div className="col-md-4"><label className="form-label" htmlFor="src">Kaynak adres</label><input className="form-control" id="src" type="url" value={sourceUrl} onChange={(e) => setSourceUrl(e.target.value)} /></div>
        <div className="col-md-3"><label className="form-label" htmlFor="pub">Yayım tarihi</label><input className="form-control" type="date" id="pub" value={publishedAt} onChange={(e) => setPublishedAt(e.target.value)} /></div>
        <div className="col-12"><BusyButton onClick={doImport}>Ayrıştır ve snapshot oluştur</BusyButton></div>
      </div>
      <div className="mt-3">
        <ErrorAlert error={error} />
        {result && (
          <div className={`alert alert-${result.snapshot.validationStatus === 'Validated' ? 'success' : 'danger'}`}>
            <div className="fw-semibold">Snapshot: <span className="mono user-select-all">{result.snapshot.id}</span> — {result.snapshot.validationStatus}</div>
            <div className="small">{result.records} kayıt · Eklenen {count('Added')} · Kapanan {count('Closed')} · Ad değişen {count('Renamed')} · Taşınan {count('Moved')} · Değişen {count('Changed')}</div>
            {result.snapshot.validationErrors.length > 0 && <ul className="small mb-0">{result.snapshot.validationErrors.slice(0, 20).map((x) => <li key={x}>{x}</li>)}</ul>}
            {result.warnings.length > 0 && <div className="small text-body-secondary mt-1">{result.warnings.join('; ')}</div>}
          </div>
        )}
      </div>
      <hr />
      <div className="row g-2 align-items-end">
        <div className="col-md-6"><label className="form-label" htmlFor="sid">Snapshot kimliği</label><input className="form-control mono" id="sid" value={snapshotId} onChange={(e) => setSnapshotId(e.target.value)} /></div>
        <div className="col-md-3"><label className="form-label" htmlFor="eff">Geçerlilik başlangıcı</label><input className="form-control" type="date" id="eff" value={effectiveFrom} onChange={(e) => setEffectiveFrom(e.target.value)} /></div>
        <div className="col-md-3 d-grid">
          <BusyButton className="btn btn-success" disabled={!/^[0-9a-fA-F-]{36}$/.test(snapshotId)} onClick={async () => {
            await api.post(`/api/v1/tax-offices/imports/${snapshotId}/approve`, { effectiveFrom });
            toast('Vergi dairesi listesi uygulandı.');
          }}><i className="bi bi-check2-all me-1" />Onayla ve uygula</BusyButton>
        </div>
      </div>
    </Card>
  );
}

const fold = (v) => (v || '').toLocaleLowerCase('tr-TR');

export function TaxOffices() {
  const session = useSession();
  const [q, setQ] = useState('');
  const [province, setProvince] = useState('');
  const [type, setType] = useState('');
  const { data: rows, loading, error } = useLoad(() => api.get('/api/v1/tax-offices?take=2000'), []);

  const provinces = useMemo(() => [...new Set((rows || []).map((v) => v.data.provinceName).filter(Boolean))].sort((a, b) => a.localeCompare(b, 'tr')), [rows]);
  const types = useMemo(() => [...new Set((rows || []).map((v) => v.data.officeType))], [rows]);
  const filtered = useMemo(() => {
    const text = fold(q.trim());
    return (rows || []).filter((v) => (!province || v.data.provinceName === province)
      && (!type || v.data.officeType === type)
      && (!text || fold(v.data.name).includes(text) || v.data.gibCode.startsWith(text) || fold(v.data.districtName).includes(text)));
  }, [rows, q, province, type]);
  const shown = filtered.slice(0, 300);

  return (
    <>
      <PageHeader icon="building" title="Vergi daireleri" subtitle={rows ? `${fmtNumber(rows.length)} birim · GİB Defterdarlık ve Vergi Daireleri listesi` : null} />
      <Card className="mb-4">
        <div className="row g-2" role="search">
          <div className="col-lg-6">
            <div className="input-group">
              <span className="input-group-text"><i className="bi bi-search" /></span>
              <input className="form-control" placeholder="Vergi dairesi adı, kodu veya ilçe" aria-label="Ara" value={q} onChange={(e) => setQ(e.target.value)} autoFocus />
              {q && <button className="btn btn-outline-secondary" type="button" aria-label="Temizle" onClick={() => setQ('')}><i className="bi bi-x-lg" /></button>}
            </div>
          </div>
          <div className="col-sm-6 col-lg-3">
            <select className="form-select" aria-label="İl" value={province} onChange={(e) => setProvince(e.target.value)}>
              <option value="">Tüm iller</option>
              {provinces.map((p) => <option key={p} value={p}>{p}</option>)}
            </select>
          </div>
          <div className="col-sm-6 col-lg-3">
            <select className="form-select" aria-label="Tür" value={type} onChange={(e) => setType(e.target.value)}>
              <option value="">Tüm türler</option>
              {types.map((t) => <option key={t} value={t}>{TYPES[t] || t}</option>)}
            </select>
          </div>
        </div>
      </Card>
      <ErrorAlert error={error} />
      <Card bodyClass="" className="mb-4" title={rows && <>{fmtNumber(filtered.length)} sonuç</>} icon="list-ul">
        {loading ? <div className="px-3"><Spinner /></div> : filtered.length ? (
          <div className="table-responsive">
            <table className="table table-hover align-middle mb-0">
              <thead><tr><th>Kod</th><th>Vergi dairesi</th><th>Tür</th><th>İl</th><th>İlçe</th></tr></thead>
              <tbody>
                {shown.map((v) => (
                  <tr key={`${v.data.gibCode}-${v.period.from}`}>
                    <td className="mono">{v.data.gibCode}</td>
                    <td className="fw-semibold">{v.data.name}</td>
                    <td className="small text-body-secondary">{TYPES[v.data.officeType] || v.data.officeType}</td>
                    <td>{v.data.provinceName}</td>
                    <td>{v.data.districtName || 'Merkez'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            {filtered.length > shown.length && <div className="small text-body-secondary px-3 py-2 border-top">İlk {shown.length} kayıt gösteriliyor; aramayı daraltın.</div>}
          </div>
        ) : <Empty icon="search">{rows?.length ? 'Eşleşen vergi dairesi yok.' : 'Liste henüz içe aktarılmamış.'}</Empty>}
      </Card>
      {session.can('TaxOfficeManage') && <Import />}
    </>
  );
}
