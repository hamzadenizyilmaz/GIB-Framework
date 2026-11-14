import { useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { api } from '../api.js';
import { fmtDate } from '../format.js';
import { validateTaxId } from '../taxid.js';
import { TaxIdInput } from '../components/inputs.jsx';
import { BusyButton, Card, ErrorAlert, PageHeader, Spinner, useLoad, useToast } from '../components/ui.jsx';

function ConnectForm({ status, onConnected }) {
  const [params] = useSearchParams();
  const toast = useToast();
  const [environment, setEnvironment] = useState('Test');
  const [userCode, setUserCode] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState(null);

  const connect = async () => {
    if (!userCode.trim() || !password) {
      setError({ message: 'GİB kullanıcı kodu ve şifre zorunludur.' });
      return;
    }
    try {
      await api.post('/api/v1/gib-portal/connect', { environment, userCode: userCode.trim(), password });
      setPassword('');
      toast(`GİB e-Arşiv Portal'a bağlanıldı (${environment === 'Production' ? 'canlı' : 'test'}).`);
      await onConnected();
    } catch (err) {
      setPassword('');
      setError(err);
    }
  };

  const testUser = async () => {
    setError(null);
    const r = await api.post('/api/v1/gib-portal/test-user');
    setEnvironment('Test');
    setUserCode(r.userCode);
    setPassword(r.password);
    toast(`Test kullanıcısı alındı: ${r.userCode}`, 'info');
  };

  return (
    <Card title="GİB e-Arşiv bağlantısı">
      <ErrorAlert error={error} />
      <form className="portal-connection-form" noValidate autoComplete="off" onSubmit={(e) => { e.preventDefault(); connect(); }}>
        <div className="mb-3">
          <div className="form-label">Ortam</div>
          <div className="btn-group w-100" role="group" aria-label="Ortam">
            <input type="radio" className="btn-check" name="environment" id="env-test" checked={environment === 'Test'} onChange={() => setEnvironment('Test')} />
            <label className="btn btn-outline-primary" htmlFor="env-test"><i className="bi bi-flask me-1" />Test portalı</label>
            <input type="radio" className="btn-check" name="environment" id="env-prod" checked={environment === 'Production'} disabled={!status.productionAllowed} onChange={() => setEnvironment('Production')} />
            <label className="btn btn-outline-danger" htmlFor="env-prod"><i className="bi bi-lightning-charge me-1" />Canlı (GİB)</label>
          </div>
        </div>
        {environment === 'Test' && (
          <div className={`d-flex flex-wrap gap-2 justify-content-between align-items-center border rounded p-2 mb-3 ${params.get('test-user') === '1' ? 'portal-test-highlight' : 'bg-body-tertiary'}`}>
            <span className="small">e-Arşiv test portalından hazır test hesabı</span>
            <BusyButton className="btn btn-sm btn-outline-primary" onClick={testUser}><i className="bi bi-person-plus me-1" />Test kullanıcısı al</BusyButton>
          </div>
        )}
        <div className="portal-credentials">
          <div className="mb-3">
            <label className="form-label" htmlFor="gib-user">GİB kullanıcı kodu</label>
            <input className="form-control" id="gib-user" maxLength={50} autoComplete="off" value={userCode} onChange={(e) => setUserCode(e.target.value)} />
          </div>
          <div className="mb-3">
            <label className="form-label" htmlFor="gib-pass">GİB şifresi</label>
            <input className="form-control" id="gib-pass" type="password" maxLength={100} autoComplete="off" value={password} onChange={(e) => setPassword(e.target.value)} />
            <div className="form-text">Şifre saklanmaz.</div>
          </div>
        </div>
        <BusyButton className="btn btn-primary w-100" onClick={connect}><i className="bi bi-link-45deg" aria-hidden="true" />Bağlan</BusyButton>
      </form>
    </Card>
  );
}

function RecipientLookup() {
  const [taxId, setTaxId] = useState('');
  const [result, setResult] = useState(null);
  const search = async () => {
    const problem = validateTaxId(taxId);
    if (problem) {
      setResult({ error: problem });
      return;
    }
    setResult(await api.get(`/api/v1/gib-portal/recipients/${taxId}`));
  };
  return (
    <>
      <TaxIdInput id="recipient-tax-id" value={taxId} required={false} onChange={(v) => { setTaxId(v); setResult(null); }}
        append={<BusyButton className="btn btn-outline-primary" onClick={search}><i className="bi bi-search" /></BusyButton>} />
      {result?.error && <div className="small text-danger mt-2">{result.error}</div>}
      {result && !result.error && (result.found ? (
        <div className="border rounded p-2 mt-2 small">
          <div className="fw-semibold">{result.title || [result.firstName, result.lastName].filter(Boolean).join(' ')}</div>
          {result.taxOffice && <div className="text-body-secondary">Vergi dairesi: {result.taxOffice}</div>}
        </div>
      ) : (
        <div className="alert alert-secondary small mt-2 mb-0">GİB sicil / MERNİS kaydı bulunamadı.</div>
      ))}
    </>
  );
}

export function GibPortal() {
  const toast = useToast();
  const { data: status, loading, error, reload } = useLoad(() => api.get('/api/v1/gib-portal/status'), []);

  if (loading && !status) return <Spinner />;

  return (
    <>
      <PageHeader icon="bank" title="GİB e-Arşiv Portal" subtitle="Portal oturumunu ve test veya canlı ortam bağlantısını yönetin."
        actions={<Link className="btn btn-primary" to="/gib-portal/documents"><i className="bi bi-search" aria-hidden="true" />Belgeleri görüntüle</Link>} />
      {error && <Card title="Portal durumu alınamadı"><ErrorAlert error={error} /><BusyButton onClick={reload}>Tekrar dene</BusyButton></Card>}
      {status && <div className="portal-shell">
        <div>
          {status.connected ? (
            <Card title="Bağlantı" icon="plug" className="border-success">
              <h2 className="h5"><span className="badge text-bg-success me-2">Bağlı</span>{status.environment === 'Production' ? 'Canlı ortam' : 'Test ortamı'}</h2>
              <dl className="row small mb-3">
                <dt className="col-5">Mükellef</dt><dd className="col-7">{status.portalTitle}</dd>
                <dt className="col-5">VKN/TCKN</dt><dd className="col-7 mono">{status.portalTaxId}</dd>
                <dt className="col-5">Kullanıcı kodu</dt><dd className="col-7 mono">{status.portalUserCode}</dd>
                <dt className="col-5">Bağlantı zamanı</dt><dd className="col-7">{fmtDate(status.connectedAt)}</dd>
              </dl>
              <BusyButton className="btn btn-outline-danger" onClick={async () => {
                await api.post('/api/v1/gib-portal/disconnect');
                toast('GİB bağlantısı kapatıldı.', 'info');
                await reload();
              }}><i className="bi bi-plug me-1" />Bağlantıyı kes</BusyButton>
            </Card>
          ) : <ConnectForm status={status} onConnected={reload} />}
        </div>
        <div>
          <Card title="Akış" icon="diagram-3" className="h-100">
            <ol className="small mb-3">
              <li>Faturayı oluşturun, doğrulamaya gönderin ve onaylatın.</li>
              <li>Fatura ekranında <strong>“GİB e-Arşiv Portal'da düzenle”</strong>.</li>
              <li>Canlı ortamda <strong>“SMS onay kodu gönder”</strong>, ardından <strong>“GİB ile imzala”</strong>.</li>
              <li>İmzalı UBL ve GİB görünümü kanıt kasasına alınır.</li>
            </ol>
            {status.connected && (
              <>
                <h2 className="h6">Alıcı sorgula (GİB sicil / MERNİS)</h2>
                <RecipientLookup />
              </>
            )}
          </Card>
        </div>
      </div>}
    </>
  );
}
