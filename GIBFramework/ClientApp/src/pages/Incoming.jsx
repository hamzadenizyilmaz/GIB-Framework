import { useRef, useState } from 'react';
import { api } from '../api.js';
import { fmtDate, fmtMoney } from '../format.js';
import { BusyButton, Card, Empty, ErrorAlert, PageHeader, Spinner, useLoad, useToast } from '../components/ui.jsx';

const STATUS = {
  Received: ['Alındı', 'secondary'], Validated: ['Doğrulandı', 'success'], Rejected: ['Doğrulanamadı', 'danger'],
  Accepted: ['Kabul edildi', 'primary'], Declined: ['Reddedildi', 'dark'],
};

export function Incoming() {
  const toast = useToast();
  const fileRef = useRef(null);
  const [result, setResult] = useState(null);
  const { data: rows, loading, reload } = useLoad(() => api.get('/api/v1/incoming-invoices?take=200'), []);

  const upload = async () => {
    const file = fileRef.current.files[0];
    if (!file) {
      setResult({ error: { message: 'Bir XML dosyası seçin.' } });
      return;
    }
    try {
      const r = await api.postRaw('/api/v1/incoming-invoices', await file.arrayBuffer(), 'application/xml');
      setResult({ invoice: r });
      fileRef.current.value = '';
      await reload();
    } catch (error) {
      setResult({ error });
    }
  };

  return (
    <>
      <PageHeader icon="inbox" title="Gelen faturalar" />
      <Card title="Fatura yükle" icon="upload" className="mb-4">
        <div className="row g-2 align-items-end">
          <div className="col-md-8">
            <label className="form-label" htmlFor="file">UBL-TR fatura XML dosyası</label>
            <input ref={fileRef} className="form-control" type="file" id="file" accept=".xml,application/xml,text/xml" />
          </div>
          <div className="col-md-4 d-grid">
            <BusyButton className="btn btn-primary" onClick={upload}><i className="bi bi-upload me-1" />Yükle ve doğrula</BusyButton>
          </div>
        </div>
        {result?.error && <div className="mt-3"><ErrorAlert error={result.error} /></div>}
        {result?.invoice && (result.invoice.status === 'Validated'
          ? <div className="alert alert-success mt-3 mb-0"><i className="bi bi-check-circle me-1" />{result.invoice.documentNumber} doğrulandı (imza geçerli).</div>
          : (
            <div className="alert alert-warning mt-3 mb-0">
              <div className="fw-semibold">{result.invoice.documentNumber}: doğrulanamadı</div>
              <ul className="mb-0 small">{result.invoice.issues.map((x) => <li key={x}>{x}</li>)}</ul>
            </div>
          ))}
      </Card>

      <Card title="Gelen kutusu" icon="inbox" bodyClass="">
        {loading ? <div className="px-3"><Spinner /></div> : rows?.length ? (
          <div className="table-responsive">
            <table className="table align-middle mb-0">
              <thead><tr><th>Belge no</th><th>Tarih</th><th>Satıcı</th><th className="num">Tutar</th><th>İmza</th><th>Durum</th><th /></tr></thead>
              <tbody>
                {rows.map((r) => {
                  const [label, color] = STATUS[r.status] || [r.status, 'secondary'];
                  return (
                    <tr key={r.id}>
                      <td className="mono">{r.documentNumber}<div className="small text-body-secondary">{r.profile}</div></td>
                      <td>{fmtDate(r.issueDate)}</td>
                      <td>{r.supplierTitle}<div className="small mono">{r.supplierTaxId}</div></td>
                      <td className="num">{fmtMoney(r.payableAmount, r.currency)}</td>
                      <td>{r.signatureValid ? <span className="badge text-bg-success"><i className="bi bi-patch-check" /> Geçerli</span> : <span className="badge text-bg-danger">Geçersiz</span>}</td>
                      <td>
                        <span className={`badge text-bg-${color}`}>{label}</span>
                        {r.issues.length > 0 && (
                          <details className="small mt-1"><summary>{r.issues.length} sorun</summary><ul className="mb-0">{r.issues.map((x) => <li key={x}>{x}</li>)}</ul></details>
                        )}
                      </td>
                      <td className="text-end">
                        {r.status === 'Validated' && (
                          <div className="btn-group btn-group-sm">
                            {[[true, 'Kabul', 'success'], [false, 'Ret', 'danger']].map(([accept, text, variant]) => (
                              <BusyButton key={text} className={`btn btn-outline-${variant}`} onClick={async () => {
                                await api.post(`/api/v1/incoming-invoices/${r.id}/decision`, { accept });
                                toast('Karar kaydedildi.');
                                await reload();
                              }}>{text}</BusyButton>
                            ))}
                          </div>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        ) : <Empty icon="inbox">Gelen fatura yok.</Empty>}
      </Card>
    </>
  );
}
