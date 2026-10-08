import { Link, useNavigate } from 'react-router';
import { api } from '../api.js';
import { fmtDate, fmtMoney, istanbulNow, ROLE_LABELS } from '../format.js';
import { useSession } from '../session.jsx';
import { Card, DocTypeBadge, Empty, Spinner, StatusBadge, useLoad } from '../components/ui.jsx';

const MONTHS = ['Oca', 'Şub', 'Mar', 'Nis', 'May', 'Haz', 'Tem', 'Ağu', 'Eyl', 'Eki', 'Kas', 'Ara'];
const COUNTED = new Set(['Approved', 'Signing', 'Signed', 'Queued', 'Transmitting', 'InDoubt', 'Sent', 'Acknowledged', 'Delivered', 'Accepted']);

function greeting(hour) {
  if (hour < 6) return 'İyi geceler';
  if (hour < 12) return 'Günaydın';
  if (hour < 18) return 'İyi günler';
  return 'İyi akşamlar';
}

function Kpi({ icon, color, label, value, hint }) {
  return (
    <div className="card stat-card shadow-sm h-100">
      <div className="card-body d-flex align-items-center gap-3">
        <span className={`stat-icon bg-${color}-subtle text-${color}-emphasis`}><i className={`bi bi-${icon}`} /></span>
        <div className="min-w-0">
          <div className="stat-value text-truncate">{value}</div>
          <div className="small text-body-secondary">{label}</div>
          {hint && <div className="small text-body-tertiary">{hint}</div>}
        </div>
      </div>
    </div>
  );
}

function MonthlyChart({ invoices }) {
  const now = istanbulNow().date;
  const [y, m] = now.split('-').map(Number);
  const months = Array.from({ length: 6 }, (_, i) => {
    const d = new Date(Date.UTC(y, m - 1 - (5 - i), 1));
    return { key: `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, '0')}`, label: MONTHS[d.getUTCMonth()] };
  });
  const totals = months.map((mo) => invoices
    .filter((i) => i.currency === 'TRY' && COUNTED.has(i.status) && i.issueDate.startsWith(mo.key))
    .reduce((s, i) => s + Number(i.payableAmount || 0), 0));
  const max = Math.max(...totals, 1);
  return (
    <div className="chart-bars" role="img" aria-label="Son 6 ay fatura toplamları">
      {months.map((mo, i) => (
        <div className="chart-bar" key={mo.key} title={`${mo.label}: ${fmtMoney(totals[i])}`}>
          <small className="fw-semibold text-body">{totals[i] ? fmtMoney(totals[i]).replace(',00', '') : ''}</small>
          <div className={`bar ${totals[i] ? '' : 'muted'}`} style={{ height: `${Math.max(3, (totals[i] / max) * 100)}%` }} />
          <small>{mo.label}</small>
        </div>
      ))}
    </div>
  );
}

function TaskList({ title, icon, color, items, empty }) {
  const navigate = useNavigate();
  return (
    <Card title={<>{title} <span className={`badge text-bg-${color} ms-1`}>{items.length}</span></>} icon={icon} bodyClass="">
      {items.length ? (
        <div className="list-group list-group-flush">
          {items.slice(0, 5).map((i) => (
            <button key={i.id} type="button" className="list-group-item list-group-item-action d-flex justify-content-between align-items-center gap-2" onClick={() => navigate(`/invoices/${i.id}`)}>
              <span className="min-w-0">
                <span className="d-block text-truncate fw-semibold small">{i.customerTitle || i.customerTaxId}</span>
                <span className="d-block small text-body-secondary font-monospace">{i.documentNumber || i.draftNumber}</span>
              </span>
              <span className="small fw-semibold text-nowrap">{fmtMoney(i.payableAmount, i.currency)}</span>
            </button>
          ))}
        </div>
      ) : <div className="card-body small text-body-secondary py-4 text-center">{empty}</div>}
    </Card>
  );
}

export function Dashboard() {
  const session = useSession();
  const navigate = useNavigate();
  const p = session.permissions;
  const tenantId = p.tenantId || session.tenantOverride;
  const now = istanbulNow();

  const { data, loading } = useLoad(async () => {
    const [page, cert, portal, locations] = await Promise.all([
      session.can('InvoiceRead') && tenantId ? api.get('/api/v1/invoices/page?take=5000').catch(() => null) : null,
      api.get('/api/v1/certificates/current').catch(() => null),
      session.can('GibPortal') && tenantId ? api.get('/api/v1/gib-portal/status').catch(() => null) : null,
      api.get('/api/v1/locations/status').catch(() => null),
    ]);
    return { invoices: page?.items || null, cert, portal, locations };
  }, [tenantId]);

  const invoices = data?.invoices;
  const me = p.userId?.toLowerCase();
  const selfApprove = p.roles.some((r) => (p.selfApprovalRoles || []).includes(r)) || !p.requireMakerChecker;
  const thisMonth = now.date.slice(0, 7);
  const monthItems = (invoices || []).filter((i) => i.issueDate.startsWith(thisMonth) && COUNTED.has(i.status));
  const monthTotal = monthItems.filter((i) => i.currency === 'TRY').reduce((s, i) => s + Number(i.payableAmount || 0), 0);
  const tasks = [];
  if (invoices && session.can('InvoiceApprove')) {
    tasks.push({ key: 'approve', title: 'Onayınızı bekleyen', icon: 'hourglass-split', color: 'warning', empty: 'Onay bekleyen fatura yok.',
      items: invoices.filter((i) => i.status === 'AwaitingApproval' && (selfApprove || i.createdBy.toLowerCase() !== me)) });
  }
  if (invoices && session.can('InvoiceSign')) {
    tasks.push({ key: 'sign', title: 'İmza bekleyen', icon: 'pen', color: 'primary', empty: 'İmza bekleyen fatura yok.', items: invoices.filter((i) => ['Approved', 'Signing'].includes(i.status)) });
  }
  if (invoices && session.can('InvoiceTransmit')) {
    tasks.push({ key: 'send', title: 'Gönderim bekleyen', icon: 'send', color: 'info', empty: 'Gönderim bekleyen fatura yok.', items: invoices.filter((i) => ['Queued', 'Failed', 'InDoubt'].includes(i.status)) });
  }
  if (invoices && session.can('InvoiceCreate')) {
    tasks.push({ key: 'draft', title: 'Taslaklarınız', icon: 'pencil-square', color: 'secondary', empty: 'Taslak yok.', items: invoices.filter((i) => i.status === 'Draft' && i.createdBy.toLowerCase() === me) });
  }
  const pending = tasks.reduce((s, t) => s + t.items.length, 0);

  return (
    <>
      <div className="hero mb-4 shadow-sm">
        <div className="position-relative" style={{ zIndex: 1 }}>
          <div className="small opacity-75 mb-1">{fmtDate(now.date)} · {session.tenant?.name || 'Platform'}</div>
          <h1 className="h3 fw-bold mb-1">{greeting(Number(now.time.slice(0, 2)))}, {p.displayName || p.userId}</h1>
          <div className="opacity-75 mb-3">{p.roles.map((r) => ROLE_LABELS[r] || r).join(' · ')}</div>
          <div className="d-flex flex-wrap gap-2">
            {session.can('InvoiceCreate') && tenantId && <Link className="btn btn-light btn-pill px-4" to="/invoices/new"><i className="bi bi-plus-lg me-1" />Yeni fatura</Link>}
            {session.can('InvoiceRead') && tenantId && <Link className="btn btn-outline-light btn-pill px-4" to="/invoices"><i className="bi bi-list-ul me-1" />Tüm faturalar</Link>}
            {session.can('PlatformAdmin') && <Link className="btn btn-outline-light btn-pill px-4" to="/settings/tenants"><i className="bi bi-buildings me-1" />Firmalar</Link>}
            <Link className="btn btn-outline-light btn-pill px-4" to="/settings"><i className="bi bi-gear me-1" />Ayarlar</Link>
          </div>
        </div>
      </div>

      {!tenantId && (
        <div className="alert alert-info d-flex align-items-center gap-2"><i className="bi bi-info-circle" />Fatura işlemleri için üst çubuktan bir firma seçin.</div>
      )}

      {loading ? <Spinner /> : (
        <>
          {invoices && (
            <div className="row row-cols-1 row-cols-sm-2 row-cols-xl-4 g-3 mb-4">
              <div className="col"><Kpi icon="cash-stack" color="primary" label="Bu ay düzenlenen (TL)" value={fmtMoney(monthTotal)} hint={`${monthItems.length} fatura`} /></div>
              <div className="col"><Kpi icon="list-check" color="warning" label="Bekleyen işleriniz" value={pending} /></div>
              <div className="col"><Kpi icon="file-earmark-text" color="info" label="e-Arşiv / e-Fatura" value={`${invoices.filter((i) => i.documentType === 'EArsiv').length} / ${invoices.filter((i) => i.documentType === 'EFatura').length}`} /></div>
              <div className="col"><Kpi icon="check2-circle" color="success" label="Gönderildi" value={invoices.filter((i) => ['Sent', 'Acknowledged', 'Delivered', 'Accepted'].includes(i.status)).length} hint={`Toplam ${invoices.length} fatura`} /></div>
            </div>
          )}

          <div className="row g-4">
            <div className="col-xl-8 d-flex flex-column gap-4">
              {invoices && (
                <Card title="Son 6 ay" icon="bar-chart">
                  <MonthlyChart invoices={invoices} />
                </Card>
              )}
              {tasks.length > 0 && (
                <div className="row g-4">
                  {tasks.map((t) => <div className="col-md-6" key={t.key}><TaskList {...t} /></div>)}
                </div>
              )}
              {invoices && (
                <Card title="Son faturalar" icon="clock-history" bodyClass=""
                  actions={<Link className="btn btn-sm btn-outline-primary btn-pill px-3" to="/invoices">Tümü</Link>}>
                  {invoices.length ? (
                    <div className="table-responsive">
                      <table className="table table-hover table-click align-middle mb-0">
                        <thead><tr><th>Belge no</th><th>Tarih</th><th>Tür</th><th>Alıcı</th><th className="num">Tutar</th><th>Durum</th></tr></thead>
                        <tbody>
                          {invoices.slice(0, 8).map((i) => (
                            <tr key={i.id} onClick={() => navigate(`/invoices/${i.id}`)}>
                              <td className="mono">{i.documentNumber || <span className="text-body-secondary">{i.draftNumber}</span>}</td>
                              <td>{fmtDate(i.issueDate)}</td>
                              <td><DocTypeBadge type={i.documentType} /></td>
                              <td><div className="text-truncate" style={{ maxWidth: '16rem' }}>{i.customerTitle || i.customerTaxId}</div></td>
                              <td className="num">{fmtMoney(i.payableAmount, i.currency)}</td>
                              <td><StatusBadge status={i.status} /></td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  ) : <Empty icon="receipt">Henüz fatura yok.</Empty>}
                </Card>
              )}
            </div>

            <div className="col-xl-4 d-flex flex-column gap-4">
              {data?.portal && (
                <Card title="GİB e-Arşiv Portal" icon="bank">
                  {data.portal.connected ? (
                    <>
                      <div className="d-flex align-items-center gap-2 mb-2"><span className="badge text-bg-success">Bağlı</span><span className="small">{data.portal.environment === 'Production' ? 'Canlı ortam' : 'Test ortamı'}</span></div>
                      <div className="fw-semibold">{data.portal.portalTitle}</div>
                      <div className="small text-body-secondary font-monospace">{data.portal.portalTaxId}</div>
                      <Link className="btn btn-sm btn-outline-primary btn-pill px-3 mt-3" to="/gib-portal/documents">Portal belgeleri</Link>
                    </>
                  ) : (
                    <div className="d-flex justify-content-between align-items-center">
                      <span className="badge text-bg-secondary">Bağlı değil</span>
                      <Link className="btn btn-sm btn-primary btn-pill px-3" to="/gib-portal">Bağlan</Link>
                    </div>
                  )}
                </Card>
              )}
              <Card title="İmza sertifikası" icon="patch-check">
                {data?.cert ? (
                  <>
                    <div className="small mono mb-2">{data.cert.subject}</div>
                    <div className="d-flex justify-content-between small"><span>Geçerlilik sonu</span><span>{fmtDate(data.cert.validTo)}</span></div>
                    <div className="progress mt-2" role="progressbar" aria-label="Kalan gün" aria-valuenow={data.cert.daysLeft} aria-valuemin={0} aria-valuemax={365}>
                      <div className={`progress-bar ${data.cert.daysLeft <= 30 ? 'bg-danger' : data.cert.daysLeft <= 90 ? 'bg-warning' : 'bg-success'}`}
                        style={{ width: `${Math.max(2, Math.min(100, data.cert.daysLeft / 3.65))}%` }}>{data.cert.daysLeft} gün</div>
                    </div>
                  </>
                ) : <p className="small text-body-secondary mb-0">Sertifika bilgisi alınamadı.</p>}
              </Card>
              {data?.locations && (
                <Card title="Adres verisi" icon="geo-alt" actions={<span className="badge text-bg-primary source-badge">TKGM</span>}>
                  <div className="d-flex justify-content-between small mb-1"><span>İl</span><strong>{data.locations.provinces}</strong></div>
                  <div className="d-flex justify-content-between small mb-1"><span>İlçe</span><strong>{data.locations.districts.toLocaleString('tr-TR')}</strong></div>
                  <div className="d-flex justify-content-between small"><span>Mahalle / köy</span><strong>{data.locations.neighborhoods.toLocaleString('tr-TR')}</strong></div>
                  <Link className="btn btn-sm btn-outline-primary btn-pill px-3 mt-3" to="/settings/locations">Görüntüle</Link>
                </Card>
              )}
            </div>
          </div>
        </>
      )}
    </>
  );
}
