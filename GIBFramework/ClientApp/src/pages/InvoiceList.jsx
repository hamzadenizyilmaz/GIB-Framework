import { useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { api } from '../api.js';
import { fmtDate, fmtMoney, STATUS } from '../format.js';
import { useSession } from '../session.jsx';
import { Card, DocTypeBadge, Empty, ErrorAlert, PageHeader, Spinner, StatusBadge, useLoad } from '../components/ui.jsx';

const PAGE_SIZES = [[25, '25'], [50, '50'], [100, '100'], [500, '500'], [5000, 'Tümü']];
const EMPTY = { q: '', status: '', documentType: '', from: '', to: '' };

export function InvoiceList() {
  const session = useSession();
  const navigate = useNavigate();
  const [draft, setDraft] = useState(EMPTY);
  const [filters, setFilters] = useState({ ...EMPTY, skip: 0, take: 50 });

  const { data, loading, error } = useLoad(() => {
    const q = new URLSearchParams({ take: String(filters.take), skip: String(filters.skip) });
    ['q', 'status', 'documentType', 'from', 'to'].forEach((k) => filters[k] && q.set(k, filters[k]));
    return api.get(`/api/v1/invoices/page?${q}`);
  }, [filters]);

  const set = (name) => (e) => setDraft((d) => ({ ...d, [name]: e.target.value }));
  const rows = data?.items || [];
  const total = data?.total || 0;
  const pageSum = rows.filter((i) => i.currency === 'TRY' && !['Cancelled', 'Objected'].includes(i.status)).reduce((s, i) => s + Number(i.payableAmount || 0), 0);
  const page = Math.floor(filters.skip / filters.take) + 1;
  const pages = Math.max(1, Math.ceil(total / filters.take));

  return (
    <>
      <PageHeader icon="receipt" title="Faturalar" subtitle={`${total} fatura`}
        actions={session.can('InvoiceCreate') && <Link className="btn btn-primary" to="/invoices/new"><i className="bi bi-plus-lg me-1" />Yeni fatura</Link>} />

      <Card className="mb-4">
        <form className="row g-2 align-items-end" onSubmit={(e) => { e.preventDefault(); setFilters((f) => ({ ...f, ...draft, q: draft.q.trim(), skip: 0 })); }}>
          <div className="col-lg-3">
            <label className="form-label small" htmlFor="f-q">Ara</label>
            <input className="form-control" id="f-q" placeholder="Belge no, taslak no, alıcı, VKN/TCKN" maxLength={100} value={draft.q} onChange={set('q')} />
          </div>
          <div className="col-sm-6 col-lg-2">
            <label className="form-label small" htmlFor="f-status">Durum</label>
            <select className="form-select" id="f-status" value={draft.status} onChange={set('status')}>
              <option value="">Tümü</option>
              {Object.entries(STATUS).map(([k, [label]]) => <option key={k} value={k}>{label}</option>)}
            </select>
          </div>
          <div className="col-sm-6 col-lg-2">
            <label className="form-label small" htmlFor="f-type">Tür</label>
            <select className="form-select" id="f-type" value={draft.documentType} onChange={set('documentType')}>
              <option value="">Tümü</option>
              <option value="EArsiv">e-Arşiv</option>
              <option value="EFatura">e-Fatura</option>
            </select>
          </div>
          <div className="col-sm-6 col-lg-2"><label className="form-label small" htmlFor="f-from">Başlangıç</label><input className="form-control" type="date" id="f-from" value={draft.from} onChange={set('from')} /></div>
          <div className="col-sm-6 col-lg-2"><label className="form-label small" htmlFor="f-to">Bitiş</label><input className="form-control" type="date" id="f-to" value={draft.to} onChange={set('to')} /></div>
          <div className="col-lg-1 d-grid"><button className="btn btn-primary" type="submit" title="Filtrele"><i className="bi bi-funnel" /></button></div>
        </form>
      </Card>

      <Card bodyClass="" footer={(
        <div className="d-flex flex-wrap justify-content-between align-items-center gap-2">
          <div className="d-flex align-items-center gap-2 small text-body-secondary">
            <span>Sayfa başına</span>
            <select className="form-select form-select-sm" style={{ width: 'auto' }} aria-label="Sayfa başına" value={filters.take}
              onChange={(e) => setFilters((f) => ({ ...f, take: Number(e.target.value), skip: 0 }))}>
              {PAGE_SIZES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
            </select>
            <span>{total ? `${filters.skip + 1}–${filters.skip + rows.length} / ${total}` : ''}</span>
            {rows.length > 0 && <span className="ms-2">Sayfa toplamı (TL): <strong>{fmtMoney(pageSum)}</strong></span>}
          </div>
          {pages > 1 && (
            <div className="btn-group">
              <button className="btn btn-sm btn-outline-secondary" disabled={page === 1} onClick={() => setFilters((f) => ({ ...f, skip: Math.max(0, f.skip - f.take) }))}><i className="bi bi-chevron-left" /> Önceki</button>
              <span className="btn btn-sm btn-outline-secondary disabled">{page} / {pages}</span>
              <button className="btn btn-sm btn-outline-secondary" disabled={page >= pages} onClick={() => setFilters((f) => ({ ...f, skip: f.skip + f.take }))}>Sonraki <i className="bi bi-chevron-right" /></button>
            </div>
          )}
        </div>
      )}>
        {loading ? <div className="px-3"><Spinner /></div>
          : error ? <div className="card-body"><ErrorAlert error={error} /></div>
            : rows.length ? (
              <div className="table-responsive">
                <table className="table table-hover table-click align-middle mb-0">
                  <thead><tr><th>Belge no</th><th>Tarih</th><th>Tür</th><th>Alıcı</th><th className="num">Ödenecek</th><th>Durum</th><th>Oluşturan</th><th>Güncelleme</th></tr></thead>
                  <tbody>
                    {rows.map((i) => (
                      <tr key={i.id} onClick={() => navigate(`/invoices/${i.id}`)}>
                        <td className="mono">
                          {i.documentNumber || <span className="text-body-secondary">{i.draftNumber}</span>}
                          {i.issuanceChannel === 'GibPortal' && <i className="bi bi-bank ms-1 text-warning" title="GİB e-Arşiv Portal" />}
                        </td>
                        <td>{fmtDate(i.issueDate)}</td>
                        <td><DocTypeBadge type={i.documentType} /></td>
                        <td>
                          <div className="text-truncate" style={{ maxWidth: '22rem' }}>{i.customerTitle || '—'}</div>
                          <div className="small text-body-secondary font-monospace">{i.customerTaxId}</div>
                        </td>
                        <td className="num">{fmtMoney(i.payableAmount, i.currency)}</td>
                        <td><StatusBadge status={i.status} /></td>
                        <td className="small">{i.createdBy}</td>
                        <td className="small text-body-secondary">{fmtDate(i.updatedAt)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : <Empty icon="search">Kriterlere uyan fatura yok.</Empty>}
      </Card>
    </>
  );
}
