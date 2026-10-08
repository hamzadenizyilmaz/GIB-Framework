import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { api } from '../../api.js';
import { fmtDate } from '../../format.js';
import { BusyButton, Card, Empty, ErrorAlert, Modal, Spinner, useDialogs, useLoad, useToast } from '../../components/ui.jsx';
import { SettingsLayout, TenantRequired } from './SettingsLayout.jsx';

const DELIVERY = {
  Pending: ['Bekliyor', 'secondary'],
  Succeeded: ['Başarılı', 'success'],
  Failed: ['Başarısız', 'danger'],
  Ignored: ['Yok sayıldı', 'light'],
};

function useCatalog() {
  return useLoad(() => api.get('/api/v1/integrations/catalog'), []);
}

async function copy(text, toast) {
  try {
    await navigator.clipboard.writeText(text);
    toast('Kopyalandı.', 'info');
  } catch {
    toast('Kopyalanamadı; metni seçip kopyalayın.', 'warning');
  }
}

function saveFile(name, text) {
  const url = URL.createObjectURL(new Blob([text], { type: 'text/plain;charset=utf-8' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = name;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 5000);
}

function IntegrationsHome() {
  const dialogs = useDialogs();
  const navigate = useNavigate();
  const { data: catalog, loading: lc } = useCatalog();
  const { data: list, loading: ll } = useLoad(() => api.get('/api/v1/integrations'), []);
  if (lc || ll) return <Spinner />;
  const info = Object.fromEntries(catalog.kinds.map((k) => [k.kind, k]));

  const add = async (kind) => {
    const k = info[kind];
    const f = await dialogs.form({ title: `${k.label} entegrasyonu`, submitText: 'Oluştur', fields: [{ name: 'name', label: 'Ad', required: true, min: 2, value: k.label }] });
    if (!f) return;
    const created = await api.post('/api/v1/integrations', { kind, name: f.name, isActive: false, settings: {}, secrets: {}, events: [] });
    navigate(`/settings/integrations/${created.id}`);
  };

  return (
    <>
      {list.length > 0 && (
        <Card title="Bağlı entegrasyonlar" icon="plug" bodyClass="" className="mb-4">
          <div className="list-group list-group-flush">
            {list.map((i) => (
              <Link key={i.id} to={`/settings/integrations/${i.id}`} className="list-group-item list-group-item-action d-flex align-items-center gap-3 py-3">
                <span className="tile-icon flex-shrink-0"><i className={`bi bi-${info[i.kind]?.icon || 'plug'}`} /></span>
                <div className="flex-grow-1 min-w-0">
                  <div className="fw-semibold">{i.name} <span className="badge text-bg-light border fw-normal ms-1">{info[i.kind]?.label}</span></div>
                  <div className="small text-body-secondary">
                    Son gelen: {i.lastInboundAt ? fmtDate(i.lastInboundAt) : '—'} · Son giden: {i.lastOutboundAt ? fmtDate(i.lastOutboundAt) : '—'}
                  </div>
                </div>
                {i.isActive ? <span className="badge text-bg-success">Etkin</span> : <span className="badge text-bg-secondary">Kapalı</span>}
                <i className="bi bi-chevron-right text-body-tertiary" />
              </Link>
            ))}
          </div>
        </Card>
      )}
      {['Pazaryeri', 'E-ticaret', 'Hosting faturalama', 'Geliştirici'].map((cat) => (
        <div key={cat} className="mb-4">
          <h2 className="h6 text-uppercase text-body-secondary fw-semibold mb-3" style={{ letterSpacing: '.06em' }}>{cat}</h2>
          <div className="row row-cols-1 row-cols-md-2 row-cols-xxl-3 g-3">
            {catalog.kinds.filter((k) => k.category === cat).map((k) => (
              <div className="col" key={k.kind}>
                <div className="card shadow-sm h-100">
                  <div className="card-body d-flex flex-column">
                    <div className="d-flex align-items-center gap-3 mb-2">
                      <span className="tile-icon"><i className={`bi bi-${k.icon}`} /></span>
                      <div className="fw-semibold">{k.label}</div>
                    </div>
                    <div className="small text-body-secondary flex-grow-1">{k.description}</div>
                    <div className="mt-3"><BusyButton className="btn btn-outline-primary btn-pill btn-sm px-3" onClick={() => add(k.kind)}><i className="bi bi-plus-lg me-1" />Bağla</BusyButton></div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      ))}
    </>
  );
}

export const IntegrationSettings = () => (
  <SettingsLayout icon="plug" title="Entegrasyonlar" subtitle="E-ticaret, WHMCS, WISECP ve özel uygulamalardan otomatik faturalama; fatura olaylarını dış sistemlere bildirme">
    <TenantRequired><IntegrationsHome /></TenantRequired>
  </SettingsLayout>
);

function FieldInput({ field, value, onChange, hasSecret, secretValue, onSecret }) {
  const id = `if-${field.key}`;
  if (field.type === 'bool') {
    return (
      <div className="form-check form-switch">
        <input className="form-check-input" type="checkbox" role="switch" id={id} checked={value === 'true'} onChange={(e) => onChange(e.target.checked ? 'true' : 'false')} />
        <label className="form-check-label" htmlFor={id}>{field.label}</label>
        {field.help && <div className="form-text">{field.help}</div>}
      </div>
    );
  }
  if (field.type === 'generated') return null;
  return (
    <>
      <label className="form-label" htmlFor={id}>{field.label}{field.required && <span className="text-danger"> *</span>}</label>
      {field.type === 'select' ? (
        <select className="form-select" id={id} value={value ?? field.default ?? ''} onChange={(e) => onChange(e.target.value)}>
          {field.options.map((o) => <option key={o} value={o}>{o}</option>)}
        </select>
      ) : field.type === 'secret' ? (
        <input className="form-control" id={id} type="password" autoComplete="new-password" placeholder={hasSecret ? '•••••••• (kayıtlı)' : ''} value={secretValue ?? ''} onChange={(e) => onSecret(e.target.value)} />
      ) : (
        <input className="form-control" id={id} type={field.type === 'url' ? 'url' : 'text'} value={value ?? ''} placeholder={field.default ?? ''} onChange={(e) => onChange(e.target.value)} />
      )}
      {field.help && <div className="form-text">{field.help}</div>}
    </>
  );
}

function SetupCard({ id, info }) {
  const toast = useToast();
  const { data, loading, reload } = useLoad(() => api.get(`/api/v1/integrations/${id}/setup`), [id]);
  const [show, setShow] = useState(false);
  if (loading) return <Card title="Kurulum" icon="tools"><Spinner /></Card>;
  return (
    <Card title="Kurulum" icon="tools" className="mb-4">
      <label className="form-label small text-body-secondary mb-1">Gelen adres (webhook URL)</label>
      <div className="input-group mb-3">
        <input className="form-control font-monospace small" readOnly value={data.inboundUrl} onFocus={(e) => e.target.select()} aria-label="Gelen adres" />
        <button className="btn btn-outline-secondary" type="button" aria-label="Kopyala" onClick={() => copy(data.inboundUrl, toast)}><i className="bi bi-clipboard" /></button>
      </div>
      {data.signingSecret && (
        <>
          <label className="form-label small text-body-secondary mb-1">İmza anahtarı · {data.signatureHeader}</label>
          <div className="input-group mb-3">
            <input className="form-control font-monospace small" readOnly type={show ? 'text' : 'password'} value={data.signingSecret} aria-label="İmza anahtarı" />
            <button className="btn btn-outline-secondary" type="button" aria-label="Göster" onClick={() => setShow(!show)}><i className={`bi bi-${show ? 'eye-slash' : 'eye'}`} /></button>
            <button className="btn btn-outline-secondary" type="button" aria-label="Kopyala" onClick={() => copy(data.signingSecret, toast)}><i className="bi bi-clipboard" /></button>
            {info.fields.some((f) => f.key === 'signingSecret' && f.type === 'generated') && (
              <BusyButton className="btn btn-outline-danger" title="Yeni anahtar üret" onClick={async () => {
                await api.post(`/api/v1/integrations/${id}/rotate-secret`);
                toast('Yeni imza anahtarı üretildi; karşı sistemi güncelleyin.', 'warning');
                await reload();
              }}><i className="bi bi-arrow-repeat" /></BusyButton>
            )}
          </div>
        </>
      )}
      <ol className="small ps-3 mb-0">
        {data.steps.map((s) => <li key={s} className="mb-1">{s}</li>)}
      </ol>
      {data.code && (
        <div className="mt-3">
          <div className="d-flex justify-content-between align-items-center mb-2">
            <span className="small fw-semibold font-monospace">{data.fileName}</span>
            <div className="btn-group btn-group-sm">
              <button type="button" className="btn btn-outline-secondary" onClick={() => copy(data.code, toast)}><i className="bi bi-clipboard me-1" />Kopyala</button>
              <button type="button" className="btn btn-outline-primary" onClick={() => saveFile(data.fileName, data.code)}><i className="bi bi-download me-1" />İndir</button>
            </div>
          </div>
          <pre className="code-block code-scroll">{data.code}</pre>
        </div>
      )}
    </Card>
  );
}

function SyncCard({ id, info, item, onSynced }) {
  const toast = useToast();
  const [result, setResult] = useState(null);
  return (
    <Card title="Eşitleme" icon="arrow-repeat" className="mb-4"
      actions={(
        <BusyButton className="btn btn-sm btn-primary btn-pill px-3" disabled={!item.isActive} onClick={async () => {
          const r = await api.post(`/api/v1/integrations/${id}/sync`);
          setResult(r);
          if (r.error) toast(r.error, 'danger');
          else toast(`${r.created} yeni sipariş faturalandı, ${r.skipped} sipariş zaten aktarılmış.`);
          await onSynced();
        }}><i className="bi bi-arrow-repeat me-1" />Şimdi eşitle</BusyButton>
      )}>
      <dl className="kv mb-3">
        <dt>Son eşitleme</dt><dd>{item.lastInboundAt ? fmtDate(item.lastInboundAt) : 'Henüz yapılmadı'}</dd>
        <dt>Son fatura linki gönderimi</dt><dd className="mb-0">{item.lastOutboundAt ? fmtDate(item.lastOutboundAt) : '—'}</dd>
      </dl>
      {result && (
        <div className={`alert ${result.error ? 'alert-danger' : 'alert-success'} small py-2`}>
          {result.error || `${result.created} yeni · ${result.skipped} mevcut · ${result.failed} hatalı`}
        </div>
      )}
      <ol className="small ps-3 mb-0">
        {info.steps.map((s) => <li key={s} className="mb-1">{s}</li>)}
      </ol>
    </Card>
  );
}

function Deliveries({ id, refreshKey }) {
  const toast = useToast();
  const [open, setOpen] = useState(null);
  const { data, loading, reload } = useLoad(() => api.get(`/api/v1/integrations/${id}/deliveries`), [id, refreshKey]);
  return (
    <Card title="Kayıtlar" icon="arrow-left-right" bodyClass="" actions={<BusyButton className="btn btn-sm btn-outline-secondary" onClick={reload}><i className="bi bi-arrow-clockwise" /></BusyButton>}>
      {loading ? <div className="px-3"><Spinner /></div> : data.length ? (
        <div className="table-responsive">
          <table className="table table-hover align-middle mb-0 table-click">
            <thead><tr><th>Zaman</th><th>Yön</th><th>Olay</th><th>Durum</th><th>HTTP</th><th>Ayrıntı</th><th /></tr></thead>
            <tbody>
              {data.map((d) => (
                <tr key={d.id} onClick={() => setOpen(d)}>
                  <td className="small text-nowrap">{fmtDate(d.createdAt)}</td>
                  <td>{d.direction === 'In' ? <span className="badge text-bg-info"><i className="bi bi-box-arrow-in-down me-1" />Gelen</span> : <span className="badge text-bg-primary"><i className="bi bi-box-arrow-up me-1" />Giden</span>}</td>
                  <td className="small font-monospace">{d.eventType}</td>
                  <td><span className={`badge text-bg-${DELIVERY[d.status][1]} ${d.status === 'Ignored' ? 'border' : ''}`}>{DELIVERY[d.status][0]}</span></td>
                  <td className="small">{d.httpStatus || '—'}</td>
                  <td className="small text-truncate" style={{ maxWidth: '16rem' }}>{d.error || d.externalId || ''}</td>
                  <td className="text-end" onClick={(e) => e.stopPropagation()}>
                    {d.status === 'Failed' && d.direction === 'Out' && (
                      <BusyButton className="btn btn-sm btn-outline-primary" onClick={async () => { await api.post(`/api/v1/integrations/${id}/deliveries/${d.id}/retry`); toast('Yeniden denenecek.'); await reload(); }}>
                        <i className="bi bi-arrow-repeat" />
                      </BusyButton>
                    )}
                    {d.invoiceId && <Link className="btn btn-sm btn-outline-secondary ms-1" to={`/invoices/${d.invoiceId}`} title="Faturayı aç"><i className="bi bi-receipt" /></Link>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : <Empty icon="arrow-left-right">Henüz istek yok.</Empty>}
      {open && (
        <Modal title="Kayıt ayrıntısı" size="lg" scrollable onClose={() => setOpen(null)}>
          <div className="modal-body">
            {open.error && <div className="alert alert-danger small">{open.error}</div>}
            <div className="small fw-semibold mb-1">İstek</div>
            <pre className="code-block code-scroll mb-3">{pretty(open.requestBody)}</pre>
            <div className="small fw-semibold mb-1">Yanıt</div>
            <pre className="code-block code-scroll mb-0">{pretty(open.responseBody)}</pre>
          </div>
        </Modal>
      )}
    </Card>
  );
}

function pretty(text) {
  if (!text) return '—';
  try {
    return JSON.stringify(JSON.parse(text), null, 2);
  } catch {
    return text;
  }
}

function IntegrationEditor() {
  const { id } = useParams();
  const navigate = useNavigate();
  const toast = useToast();
  const dialogs = useDialogs();
  const { data: catalog } = useCatalog();
  const { data: item, loading, error, reload } = useLoad(() => api.get(`/api/v1/integrations/${id}`), [id]);
  const [form, setForm] = useState(null);
  const [saveError, setSaveError] = useState(null);
  const [refresh, setRefresh] = useState(0);

  useEffect(() => {
    if (item) setForm({ name: item.name, isActive: item.isActive, settings: { ...item.settings }, secrets: {}, events: [...item.events] });
  }, [item]);

  if (error) return <ErrorAlert error={error} />;
  if (loading || !catalog || !form) return <Spinner />;
  const info = catalog.kinds.find((k) => k.kind === item.kind);
  const setSetting = (k, v) => setForm((f) => ({ ...f, settings: { ...f.settings, [k]: v } }));
  const setSecret = (k, v) => setForm((f) => ({ ...f, secrets: { ...f.secrets, [k]: v } }));

  const save = async () => {
    setSaveError(null);
    try {
      await api.put(`/api/v1/integrations/${id}`, { kind: item.kind, ...form });
      toast('Entegrasyon kaydedildi.');
      await reload();
      setRefresh((x) => x + 1);
    } catch (err) {
      setSaveError(err);
    }
  };

  return (
    <>
      <div className="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-3">
        <Link to="/settings/integrations" className="btn btn-sm btn-outline-secondary btn-pill"><i className="bi bi-arrow-left me-1" />Entegrasyonlar</Link>
        <div className="d-flex gap-2">
          <BusyButton className="btn btn-sm btn-outline-primary btn-pill" disabled={!form.settings.webhookUrl} onClick={async () => {
            const d = await api.post(`/api/v1/integrations/${id}/test`);
            toast(d.status === 'Succeeded' ? `Deneme başarılı (HTTP ${d.httpStatus}).` : `Deneme başarısız: ${d.error || d.httpStatus}`, d.status === 'Succeeded' ? 'success' : 'danger');
            setRefresh((x) => x + 1);
          }}><i className="bi bi-broadcast me-1" />Webhook dene</BusyButton>
          <BusyButton className="btn btn-sm btn-outline-danger btn-pill" onClick={async () => {
            if (!await dialogs.confirm('Entegrasyonu sil', `"${item.name}" ve kayıtları silinsin mi? Oluşturulmuş faturalar etkilenmez.`, 'Sil', 'danger')) return;
            await api.del(`/api/v1/integrations/${id}`);
            toast('Entegrasyon silindi.', 'warning');
            navigate('/settings/integrations');
          }}><i className="bi bi-trash me-1" />Sil</BusyButton>
        </div>
      </div>
      <div className="row g-4">
        <div className="col-xl-7">
          <Card title={<><i className={`bi bi-${info.icon} me-2`} />{info.label}</>} className="mb-4"
            actions={(
              <div className="form-check form-switch m-0">
                <input className="form-check-input" type="checkbox" role="switch" id="intOn" checked={form.isActive} onChange={(e) => setForm((f) => ({ ...f, isActive: e.target.checked }))} />
                <label className="form-check-label small" htmlFor="intOn">{form.isActive ? 'Etkin' : 'Kapalı'}</label>
              </div>
            )}
            footer={<div className="d-flex justify-content-end"><BusyButton onClick={save}><i className="bi bi-save me-1" />Kaydet</BusyButton></div>}>
            <ErrorAlert error={saveError} />
            <div className="row g-3">
              <div className="col-12">
                <label className="form-label" htmlFor="intName">Ad</label>
                <input className="form-control" id="intName" maxLength={100} value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} />
              </div>
              {info.fields.filter((f) => f.type !== 'generated').map((field) => (
                <div key={field.key} className={field.type === 'bool' ? 'col-12' : 'col-md-6'}>
                  <FieldInput field={field} value={form.settings[field.key]} onChange={(v) => setSetting(field.key, v)}
                    hasSecret={item.secrets[field.key]} secretValue={form.secrets[field.key]} onSecret={(v) => setSecret(field.key, v)} />
                </div>
              ))}
              <div className="col-12">
                <div className="form-label">Bildirim adresine gönderilecek olaylar</div>
                <div className="row row-cols-1 row-cols-md-2 g-1">
                  {catalog.events.map((e) => (
                    <div className="col" key={e.code}>
                      <div className="form-check">
                        <input className="form-check-input" type="checkbox" id={`ev-${e.code}`} checked={form.events.includes(e.code)}
                          onChange={(ev) => setForm((f) => ({ ...f, events: ev.target.checked ? [...f.events, e.code] : f.events.filter((x) => x !== e.code) }))} />
                        <label className="form-check-label small" htmlFor={`ev-${e.code}`}>{e.label} <span className="font-monospace text-body-secondary">{e.code}</span></label>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </Card>
        </div>
        <div className="col-xl-5">
          {info.inbound
            ? <SetupCard key={refresh} id={id} info={info} />
            : <SyncCard id={id} info={info} item={item} onSynced={async () => { await reload(); setRefresh((x) => x + 1); }} />}
        </div>
        <div className="col-12">
          <Deliveries id={id} refreshKey={refresh} />
        </div>
      </div>
    </>
  );
}

export const IntegrationDetailSettings = () => (
  <SettingsLayout icon="plug" title="Entegrasyon">
    <TenantRequired><IntegrationEditor /></TenantRequired>
  </SettingsLayout>
);
