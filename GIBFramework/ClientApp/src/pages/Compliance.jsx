import { useState } from 'react';
import { api } from '../api.js';
import { fmtDate, todayIso } from '../format.js';
import { useSession } from '../session.jsx';
import { BusyButton, Card, ErrorAlert, PageHeader, Spinner, useLoad, useToast } from '../components/ui.jsx';

const REVIEW = { Approved: ['Onaylı', 'success'], PendingExpertReview: ['İncelemede', 'warning'], Draft: ['Taslak', 'secondary'], Retired: ['Geri çekildi', 'dark'] };

const ROUTING_KINDS = ['EFaturaRouting', 'EArchiveRouting'];

function ReviewPanel({ rule, onChanged }) {
  const session = useSession();
  const toast = useToast();
  const [note, setNote] = useState('');
  const [error, setError] = useState(null);
  const r = rule.review || {};
  const me = session.permissions?.userId?.toLowerCase();
  const canAct = session.can('RuleApprove');
  const panelApproved = r.source === 'Panel' && r.status === 'Approved' && !r.stale;
  const reviewed = r.source === 'Panel' && !r.stale && r.reviewedBy;
  const sameReviewer = reviewed && r.reviewedBy?.toLowerCase() === me;
  const call = async (action, message) => {
    setError(null);
    try {
      await api.post(`/api/v1/compliance/rules/${encodeURIComponent(rule.code)}/${rule.version}/${action}`, action === 'revoke' ? {} : { note: note || null });
      setNote('');
      toast(message);
      await onChanged();
    } catch (err) {
      setError(err);
    }
  };
  const step = (done, active, n, title, who, when, text) => (
    <li className={`rule-step ${done ? 'done' : active ? 'active' : ''}`}>
      <span className="rule-step-no">{done ? <i className="bi bi-check-lg" /> : n}</span>
      <div>
        <div className="fw-semibold">{title}</div>
        {who ? <div className="small">{who} · {fmtDate(when)}</div> : <div className="small text-body-secondary">Bekliyor</div>}
        {text && <div className="small fst-italic text-body-secondary">“{text}”</div>}
      </div>
    </li>
  );

  if (rule.packageStatus === 'Approved' || rule.packageStatus === 'Retired') {
    return <div className="alert alert-light border small mb-0 mt-3"><i className="bi bi-patch-check me-1" />Bu kuralın durumu onaylı kural paketinden gelir ({REVIEW[rule.packageStatus]?.[0]}); panelden değiştirilemez.</div>;
  }

  return (
    <div className="rule-review mt-3">
      <div className="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-2">
        <div className="fw-semibold"><i className="bi bi-people me-1" />İki kişili onay (dört göz ilkesi)</div>
        {r.stale && <span className="badge text-bg-warning">Kural değişti · önceki onay geçersiz</span>}
      </div>
      <ol className="rule-steps">
        {step(reviewed, !reviewed, 1, 'İnceleme', reviewed ? r.reviewedBy : null, r.reviewedAt, reviewed ? r.reviewNote : null)}
        {step(panelApproved, reviewed && !panelApproved, 2, 'Onay (farklı kişi)', panelApproved ? r.approvedBy : null, r.approvedAt, panelApproved ? r.approvalNote : null)}
      </ol>
      <ErrorAlert error={error} />
      {canAct ? (
        <>
          {!panelApproved && (
            <textarea className="form-control form-control-sm mb-2" rows={2} maxLength={1000} placeholder={reviewed ? 'Onay notu (isteğe bağlı)' : 'İnceleme notu: hangi kaynakla karşılaştırıldı? (isteğe bağlı)'}
              value={note} onChange={(e) => setNote(e.target.value)} aria-label="Not" />
          )}
          <div className="d-flex flex-wrap gap-2">
            {!reviewed && !panelApproved && <BusyButton className="btn btn-sm btn-primary" onClick={() => call('review', 'Kural incelendi olarak işaretlendi. Onay için başka bir yetkili gerekir.')}><i className="bi bi-search" />İncelendi olarak işaretle</BusyButton>}
            {reviewed && !panelApproved && <BusyButton className="btn btn-sm btn-success" disabled={sameReviewer} onClick={() => call('approve', 'Kural onaylandı ve uygulanmaya başladı.')}><i className="bi bi-patch-check" />Onayla</BusyButton>}
            {(reviewed || panelApproved) && <BusyButton className="btn btn-sm btn-outline-danger" onClick={() => call('revoke', panelApproved ? 'Onay geri alındı.' : 'İnceleme geri alındı.')}><i className="bi bi-arrow-counterclockwise" />{panelApproved ? 'Onayı geri al' : 'İncelemeyi geri al'}</BusyButton>}
          </div>
          {sameReviewer && !panelApproved && <div className="small text-body-secondary mt-2"><i className="bi bi-info-circle me-1" />Bu kuralı siz incelediniz; onay başka bir yetkili tarafından verilmelidir.</div>}
        </>
      ) : <div className="small text-body-secondary">İnceleme ve onay için “Mevzuat kurallarını inceleme ve onaylama” yetkisi gerekir (Platform yöneticisi, Uyum sorumlusu).</div>}
    </div>
  );
}

function RoutingBanner({ rules }) {
  const routing = rules.filter((x) => ROUTING_KINDS.includes(x.kind));
  if (!routing.length) return null;
  const approved = routing.filter((x) => x.reviewStatus === 'Approved').length;
  return approved > 0
    ? <div className="alert alert-success small"><i className="bi bi-check-circle me-1" />Bu tarihte {approved} / {routing.length} belge türü kuralı onaylı; faturaların belge türü belirlenebilir.</div>
    : <div className="alert alert-warning small"><i className="bi bi-exclamation-triangle me-1" /><strong>Bu tarihte onaylı belge türü kuralı yok.</strong> Faturalar “Belirlenemedi” olarak kalır ve düzenlenemez. Aşağıdaki e-Fatura / e-Arşiv kurallarını iki farklı yetkili inceleyip onaylamalıdır.</div>;
}

function Rules() {
  const [date, setDate] = useState(todayIso());
  const [query, setQuery] = useState(todayIso());
  const [open, setOpen] = useState(null);
  const { data, loading, reload } = useLoad(() => api.get(`/api/v1/compliance/rules${query ? `?date=${query}` : ''}`), [query]);

  return (
    <>
      <div className="d-flex flex-wrap gap-2 align-items-end mb-3">
        <div><label className="form-label small" htmlFor="rd">Yürürlük tarihi</label><input className="form-control" type="date" id="rd" value={date} onChange={(e) => setDate(e.target.value)} /></div>
        <button className="btn btn-outline-primary" type="button" onClick={() => setQuery(date)}>Göster</button>
        <button className="btn btn-outline-secondary" type="button" onClick={() => setQuery(null)}>Tüm versiyonlar</button>
      </div>
      {loading ? <Spinner /> : (
        <>
          {query && <RoutingBanner rules={data.rules} />}
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
                          {x.reviewNotes && <><dt className="col-sm-3">Paket notu</dt><dd className="col-sm-9">{x.reviewNotes}</dd></>}
                        </dl>
                        <ReviewPanel rule={x} onChanged={reload} />
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
