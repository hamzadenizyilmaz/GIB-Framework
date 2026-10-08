import { useState } from 'react';
import { api } from '../api.js';
import { fmtDate, todayIso } from '../format.js';
import { useSession } from '../session.jsx';
import { Card, PageHeader, Spinner, useLoad } from '../components/ui.jsx';

const REVIEW = { Approved: ['Onaylı', 'success'], PendingExpertReview: ['İncelemede', 'warning'], Draft: ['Taslak', 'secondary'], Retired: ['Geri çekildi', 'dark'] };

function Rules() {
  const [date, setDate] = useState(todayIso());
  const [query, setQuery] = useState(todayIso());
  const [open, setOpen] = useState(null);
  const { data, loading } = useLoad(() => api.get(`/api/v1/compliance/rules${query ? `?date=${query}` : ''}`), [query]);

  return (
    <>
      <div className="d-flex flex-wrap gap-2 align-items-end mb-3">
        <div><label className="form-label small" htmlFor="rd">Yürürlük tarihi</label><input className="form-control" type="date" id="rd" value={date} onChange={(e) => setDate(e.target.value)} /></div>
        <button className="btn btn-outline-primary" type="button" onClick={() => setQuery(date)}>Göster</button>
        <button className="btn btn-outline-secondary" type="button" onClick={() => setQuery(null)}>Tüm versiyonlar</button>
      </div>
      {loading ? <Spinner /> : (
        <>
          <div className="small text-body-secondary mb-2">Kural seti hash: <span className="mono">{data.ruleSetHash}</span></div>
          <div className="accordion">
            {data.rules.map((x, idx) => {
              const [label, color] = REVIEW[x.reviewStatus] || [x.reviewStatus, 'secondary'];
              const isOpen = open === idx;
              return (
                <div className="accordion-item" key={`${x.rule}-${x.effectiveFrom}`}>
                  <h2 className="accordion-header">
                    <button className={`accordion-button ${isOpen ? '' : 'collapsed'}`} type="button" aria-expanded={isOpen} onClick={() => setOpen(isOpen ? null : idx)}>
                      <span className="mono me-2">{x.rule}</span> {x.title}
                      <span className="ms-auto me-2 d-none d-md-inline small text-body-secondary">{fmtDate(x.effectiveFrom)} - {x.effectiveTo ? fmtDate(x.effectiveTo) : '…'}</span>
                      <span className={`badge text-bg-${color} me-2`}>{label}</span>
                    </button>
                  </h2>
                  {isOpen && (
                    <div className="accordion-collapse collapse show">
                      <div className="accordion-body small">
                        {x.description && <p>{x.description}</p>}
                        <dl className="row mb-0">
                          <dt className="col-sm-3">Tür</dt><dd className="col-sm-9 mono">{x.kind}</dd>
                          <dt className="col-sm-3">Önem / engelleyici</dt><dd className="col-sm-9">{x.severity} / {x.blocking ? 'Evet' : 'Hayır'}</dd>
                          <dt className="col-sm-3">Hukuki dayanak</dt>
                          <dd className="col-sm-9">{x.legalBasis.map((b) => <div key={b.key}>{b.key} — {b.uri ? <a href={b.uri} target="_blank" rel="noopener noreferrer">{b.title}</a> : b.title}</div>)}</dd>
                          <dt className="col-sm-3">Parametreler</dt><dd className="col-sm-9"><code>{JSON.stringify(x.parameters)}</code></dd>
                        </dl>
                      </div>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </>
      )}
    </>
  );
}

function Sources() {
  const { data, loading } = useLoad(() => api.get('/api/v1/compliance/sources'), []);
  if (loading) return <Spinner />;
  return (
    <div className="list-group">
      {data.map((s) => (
        <div className="list-group-item" key={s.code}>
          <div className="d-flex justify-content-between flex-wrap gap-2"><div className="fw-semibold">{s.title}</div><span className="badge text-bg-light border">{s.code}</span></div>
          {s.uri && <a className="small text-break" href={s.uri} target="_blank" rel="noopener noreferrer">{s.uri}</a>}
        </div>
      ))}
    </div>
  );
}

function TaxDefinitions() {
  const { data: tax, loading } = useLoad(() => api.get('/api/v1/compliance/tax-definitions'), []);
  if (loading) return <Spinner />;
  return (
    <div className="row g-4">
      <div className="col-md-3"><h2 className="h6">KDV oranları</h2>{tax.vatRates.map((r) => <span key={r} className="badge text-bg-primary me-1">%{r}</span>)}</div>
      <div className="col-md-5">
        <h2 className="h6">KDV tevkifatı</h2>
        <table className="table table-sm small"><tbody>{tax.withholdings.map((w) => <tr key={w.code}><td className="mono">{w.code}</td><td>{w.name}</td><td>{w.ratio}</td></tr>)}</tbody></table>
      </div>
      <div className="col-md-4">
        <h2 className="h6">İstisna kodları</h2>
        <table className="table table-sm small"><tbody>{tax.exemptions.map((x) => <tr key={x.code}><td className="mono">{x.code}</td><td>{x.name}</td></tr>)}</tbody></table>
      </div>
    </div>
  );
}

function SourceChecks() {
  const { data, loading } = useLoad(() => api.get('/api/v1/compliance/source-checks?take=100'), []);
  if (loading) return <Spinner />;
  if (!data.length) return <p className="text-body-secondary small mb-0">Henüz kontrol yok.</p>;
  return (
    <table className="table table-sm small">
      <thead><tr><th>Zaman</th><th>Kaynak</th><th>HTTP</th><th>Değişti</th><th>SHA-256</th></tr></thead>
      <tbody>
        {data.map((c) => (
          <tr key={c.id} className={c.changed ? 'table-warning' : ''}>
            <td>{fmtDate(c.checkedAt)}</td><td>{c.sourceCode}</td><td>{c.httpStatus ?? c.error}</td><td>{c.changed ? 'EVET' : '—'}</td><td className="mono">{(c.sha256 || '').slice(0, 16)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

export function Compliance() {
  const session = useSession();
  const tabs = [
    ['rules', 'Kurallar', Rules],
    ['sources', 'Resmî kaynaklar', Sources],
    ['tax', 'Vergi tanımları', TaxDefinitions],
    ...(session.can('ComplianceManage') ? [['watch', 'Kaynak izleme', SourceChecks]] : []),
  ];
  const [tab, setTab] = useState('rules');
  const Active = tabs.find(([k]) => k === tab)[2];

  return (
    <>
      <PageHeader icon="journal-check" title="Mevzuat & uyum" />
      <Card bodyClass="">
        <div className="card-header bg-body">
          <ul className="nav nav-tabs card-header-tabs" role="tablist">
            {tabs.map(([key, label]) => (
              <li className="nav-item" role="presentation" key={key}>
                <button className={`nav-link ${tab === key ? 'active' : ''}`} type="button" role="tab" aria-selected={tab === key} onClick={() => setTab(key)}>{label}</button>
              </li>
            ))}
          </ul>
        </div>
        <div className="card-body"><Active /></div>
      </Card>
    </>
  );
}
