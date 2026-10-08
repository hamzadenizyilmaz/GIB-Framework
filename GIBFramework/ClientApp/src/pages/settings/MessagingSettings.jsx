import { useEffect, useMemo, useRef, useState } from 'react';
import { api } from '../../api.js';
import { fmtDate } from '../../format.js';
import { useSession } from '../../session.jsx';
import { BusyButton, Card, Empty, ErrorAlert, Modal, Spinner, useLoad, useToast } from '../../components/ui.jsx';
import { SettingsLayout, TenantRequired } from './SettingsLayout.jsx';

const SECURITY = [
  ['StartTls', 'STARTTLS (587)'],
  ['SslOnConnect', 'SSL / TLS (465)'],
  ['None', 'Şifrelemesiz (25)'],
];

const PRESETS = [
  ['Gmail / Google Workspace', { host: 'smtp.gmail.com', port: 587, security: 'StartTls' }],
  ['Outlook / Microsoft 365', { host: 'smtp.office365.com', port: 587, security: 'StartTls' }],
  ['Yandex', { host: 'smtp.yandex.com.tr', port: 465, security: 'SslOnConnect' }],
  ['Yandex 360 (kurumsal)', { host: 'smtp.yandex.com', port: 465, security: 'SslOnConnect' }],
];

export const STATUS_BADGE = {
  Queued: ['Kuyrukta', 'secondary'],
  Sending: ['Gönderiliyor', 'info'],
  Sent: ['Gönderildi', 'success'],
  Failed: ['Başarısız', 'danger'],
  Cancelled: ['İptal', 'dark'],
};

const CHANNEL = { Email: ['envelope', 'E-posta'], Sms: ['chat-dots', 'SMS'] };

function useMessaging() {
  const state = useLoad(() => api.get('/api/v1/messaging/settings'), []);
  return state;
}

function TestCard({ channel, placeholder, defaultTo }) {
  const [to, setTo] = useState(defaultTo || '');
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);
  useEffect(() => { if (defaultTo) setTo(defaultTo); }, [defaultTo]);
  return (
    <Card title="Deneme gönderimi" icon="send">
      <form className="input-group" onSubmit={(e) => e.preventDefault()}>
        <input className="form-control" type={channel === 'Email' ? 'email' : 'tel'} placeholder={placeholder} aria-label="Alıcı" value={to} onChange={(e) => setTo(e.target.value)} />
        <BusyButton className="btn btn-outline-primary" disabled={!to.trim()} onClick={async () => {
          setError(null);
          setResult(null);
          try {
            setResult(await api.post('/api/v1/messaging/test', { channel, to }));
          } catch (err) {
            setError(err);
          }
        }}><i className="bi bi-send me-1" />Gönder</BusyButton>
      </form>
      <div className="mt-3">
        <ErrorAlert error={error} />
        {result && (result.status === 'Sent'
          ? <div className="alert alert-success mb-0 small"><i className="bi bi-check-circle me-1" />Gönderildi. <span className="text-body-secondary font-monospace">{result.providerMessageId}</span></div>
          : <div className="alert alert-danger mb-0 small"><i className="bi bi-x-circle me-1" />{result.lastError || 'Gönderilemedi.'}</div>)}
      </div>
    </Card>
  );
}

function EmailForm() {
  const session = useSession();
  const toast = useToast();
  const { data, loading, error, reload } = useMessaging();
  const [form, setForm] = useState(null);
  const [saveError, setSaveError] = useState(null);
  useEffect(() => {
    if (data) {
      const s = data.smtp;
      setForm({
        enabled: s.enabled, host: s.host || '', port: s.port || 587, security: s.security || 'StartTls', userName: s.userName || '', password: '', clearPassword: false,
        fromAddress: s.fromAddress || session.tenant?.profile?.email || '', fromName: s.fromName || session.tenant?.name || '', replyTo: s.replyTo || '',
        emailFooter: data.emailFooter || '', brandColor: data.brandColor || '#0778E6',
      });
    }
  }, [data, session.tenant]);

  if (loading || !form) return error ? <ErrorAlert error={error} /> : <Spinner />;
  const set = (k) => (e) => setForm((f) => ({ ...f, [k]: e.target.type === 'checkbox' ? e.target.checked : e.target.value }));

  const save = async () => {
    setSaveError(null);
    try {
      await api.put('/api/v1/messaging/settings/smtp', { ...form, port: Number(form.port) || 587 });
      toast('E-posta ayarları kaydedildi.');
      await reload();
    } catch (err) {
      setSaveError(err);
    }
  };

  return (
    <div className="row g-4">
      <div className="col-xl-8">
        <Card title="SMTP sunucusu" icon="hdd-network" actions={(
          <div className="form-check form-switch m-0">
            <input className="form-check-input" type="checkbox" role="switch" id="smtpOn" checked={form.enabled} onChange={set('enabled')} />
            <label className="form-check-label small" htmlFor="smtpOn">{form.enabled ? 'Açık' : 'Kapalı'}</label>
          </div>
        )} footer={(
          <div className="d-flex justify-content-between align-items-center gap-2">
            <span className="small text-body-secondary">{data.updatedAt ? `Son değişiklik: ${fmtDate(data.updatedAt)} · ${data.updatedBy}` : ''}</span>
            <BusyButton onClick={save}><i className="bi bi-save me-1" />Kaydet</BusyButton>
          </div>
        )}>
          <ErrorAlert error={saveError} />
          <div className="d-flex flex-wrap gap-2 mb-3">
            {PRESETS.map(([label, p]) => (
              <button key={label} type="button" className="btn btn-sm btn-outline-secondary btn-pill" onClick={() => setForm((f) => ({ ...f, ...p }))}>{label}</button>
            ))}
          </div>
          <div className="row g-3">
            <div className="col-md-6"><label className="form-label" htmlFor="host">Sunucu</label><input className="form-control" id="host" placeholder="smtp.alanadi.com" value={form.host} onChange={set('host')} /></div>
            <div className="col-md-2"><label className="form-label" htmlFor="port">Port</label><input className="form-control" id="port" type="number" min={1} max={65535} value={form.port} onChange={set('port')} /></div>
            <div className="col-md-4">
              <label className="form-label" htmlFor="sec">Güvenlik</label>
              <select className="form-select" id="sec" value={form.security} onChange={set('security')}>{SECURITY.map(([v, l]) => <option key={v} value={v}>{l}</option>)}</select>
            </div>
            <div className="col-md-6"><label className="form-label" htmlFor="user">Kullanıcı adı</label><input className="form-control" id="user" autoComplete="off" value={form.userName} onChange={set('userName')} /></div>
            <div className="col-md-6">
              <label className="form-label" htmlFor="pass">Şifre</label>
              <input className="form-control" id="pass" type="password" autoComplete="new-password" placeholder={data.smtp.hasPassword ? '•••••••• (kayıtlı)' : ''} value={form.password} onChange={set('password')} disabled={form.clearPassword} />
              {data.smtp.hasPassword && (
                <div className="form-check mt-1">
                  <input className="form-check-input" type="checkbox" id="clearPass" checked={form.clearPassword} onChange={set('clearPassword')} />
                  <label className="form-check-label small" htmlFor="clearPass">Kayıtlı şifreyi sil</label>
                </div>
              )}
            </div>
            <div className="col-md-6"><label className="form-label" htmlFor="from">Gönderen adresi</label><input className="form-control" id="from" type="email" placeholder="fatura@alanadi.com" value={form.fromAddress} onChange={set('fromAddress')} /></div>
            <div className="col-md-6"><label className="form-label" htmlFor="fromName">Gönderen adı</label><input className="form-control" id="fromName" value={form.fromName} onChange={set('fromName')} /></div>
            <div className="col-md-6"><label className="form-label" htmlFor="reply">Yanıt adresi</label><input className="form-control" id="reply" type="email" value={form.replyTo} onChange={set('replyTo')} /></div>
            <div className="col-md-6">
              <label className="form-label" htmlFor="color">Marka rengi</label>
              <div className="input-group">
                <input className="form-control form-control-color" id="color" type="color" value={form.brandColor} onChange={set('brandColor')} title="Marka rengi" />
                <input className="form-control font-monospace" aria-label="Renk kodu" value={form.brandColor} onChange={set('brandColor')} maxLength={7} />
              </div>
            </div>
            <div className="col-12">
              <label className="form-label" htmlFor="footer">E-posta alt bilgisi</label>
              <textarea className="form-control" id="footer" rows={3} maxLength={1000} placeholder="Örn. Mersis no, KEP adresi, çalışma saatleri" value={form.emailFooter} onChange={set('emailFooter')} />
            </div>
          </div>
        </Card>
      </div>
      <div className="col-xl-4">
        <TestCard channel="Email" placeholder="ornek@alanadi.com" defaultTo={form.fromAddress} />
      </div>
    </div>
  );
}

function SmsForm() {
  const toast = useToast();
  const { data, loading, error, reload } = useMessaging();
  const [form, setForm] = useState(null);
  const [saveError, setSaveError] = useState(null);
  const [headers, setHeaders] = useState(null);
  const [balance, setBalance] = useState(null);
  useEffect(() => {
    if (data) {
      const s = data.sms;
      setForm({ enabled: s.enabled, userCode: s.userCode || '', password: '', clearPassword: false, header: s.header || '', encoding: s.encoding || 'TR' });
    }
  }, [data]);

  if (loading || !form) return error ? <ErrorAlert error={error} /> : <Spinner />;
  const set = (k) => (e) => setForm((f) => ({ ...f, [k]: e.target.type === 'checkbox' ? e.target.checked : e.target.value }));

  const save = async () => {
    setSaveError(null);
    try {
      await api.put('/api/v1/messaging/settings/sms', form);
      toast('SMS ayarları kaydedildi.');
      await reload();
    } catch (err) {
      setSaveError(err);
    }
  };

  return (
    <div className="row g-4">
      <div className="col-xl-8">
        <Card title="Netgsm" icon="broadcast" actions={(
          <div className="form-check form-switch m-0">
            <input className="form-check-input" type="checkbox" role="switch" id="smsOn" checked={form.enabled} onChange={set('enabled')} />
            <label className="form-check-label small" htmlFor="smsOn">{form.enabled ? 'Açık' : 'Kapalı'}</label>
          </div>
        )} footer={(
          <div className="d-flex flex-wrap justify-content-between align-items-center gap-2">
            <div className="d-flex gap-2">
              <BusyButton className="btn btn-outline-secondary btn-sm" onClick={async () => setHeaders(await api.get('/api/v1/messaging/netgsm/headers'))}><i className="bi bi-list-ul me-1" />Başlıkları getir</BusyButton>
              <BusyButton className="btn btn-outline-secondary btn-sm" onClick={async () => setBalance(await api.get('/api/v1/messaging/netgsm/balance'))}><i className="bi bi-wallet2 me-1" />Bakiye</BusyButton>
            </div>
            <BusyButton onClick={save}><i className="bi bi-save me-1" />Kaydet</BusyButton>
          </div>
        )}>
          <ErrorAlert error={saveError} />
          <div className="row g-3">
            <div className="col-md-6">
              <label className="form-label" htmlFor="uc">Abone numarası (kullanıcı adı)</label>
              <input className="form-control font-monospace" id="uc" inputMode="numeric" placeholder="850xxxxxxx" value={form.userCode} onChange={set('userCode')} />
            </div>
            <div className="col-md-6">
              <label className="form-label" htmlFor="sp">API alt kullanıcı şifresi</label>
              <input className="form-control" id="sp" type="password" autoComplete="new-password" placeholder={data.sms.hasPassword ? '•••••••• (kayıtlı)' : ''} value={form.password} onChange={set('password')} disabled={form.clearPassword} />
              {data.sms.hasPassword && (
                <div className="form-check mt-1">
                  <input className="form-check-input" type="checkbox" id="clearSms" checked={form.clearPassword} onChange={set('clearPassword')} />
                  <label className="form-check-label small" htmlFor="clearSms">Kayıtlı şifreyi sil</label>
                </div>
              )}
            </div>
            <div className="col-md-6">
              <label className="form-label" htmlFor="hd">SMS başlığı (gönderici adı)</label>
              {headers?.length ? (
                <select className="form-select" id="hd" value={form.header} onChange={set('header')}>
                  <option value="">Seçin…</option>
                  {headers.map((h) => <option key={h} value={h}>{h}</option>)}
                </select>
              ) : <input className="form-control" id="hd" maxLength={11} value={form.header} onChange={set('header')} />}
            </div>
            <div className="col-md-6">
              <label className="form-label" htmlFor="enc">Karakter kodlaması</label>
              <select className="form-select" id="enc" value={form.encoding} onChange={set('encoding')}>
                <option value="TR">Türkçe karakterli (TR)</option>
                <option value="UTF-8">Türkçe karaktersiz (UTF-8)</option>
                <option value="UNICODE">Emoji destekli (UNICODE)</option>
              </select>
            </div>
          </div>
          {balance && (
            <div className="d-flex flex-wrap gap-2 mt-3">
              {balance.map((b) => <span key={b.name} className="badge text-bg-light border fw-normal fs-6">{b.name}: <strong>{b.amount}</strong></span>)}
            </div>
          )}
        </Card>
      </div>
      <div className="col-xl-4">
        <TestCard channel="Sms" placeholder="5XX XXX XX XX" />
      </div>
    </div>
  );
}

export const EmailSettings = () => (
  <SettingsLayout icon="envelope-at" title="E-posta (SMTP)" subtitle="Fatura, onay ve kullanıcı bildirimlerinin gönderileceği e-posta sunucusu">
    <TenantRequired><EmailForm /></TenantRequired>
  </SettingsLayout>
);

export const SmsSettings = () => (
  <SettingsLayout icon="chat-dots" title="SMS (Netgsm)" subtitle="Müşteri ve kullanıcı SMS bildirimleri">
    <TenantRequired><SmsForm /></TenantRequired>
  </SettingsLayout>
);

function insertAt(el, text, value, setValue) {
  if (!el) {
    setValue(value + text);
    return;
  }
  const start = el.selectionStart ?? value.length;
  const end = el.selectionEnd ?? value.length;
  const next = value.slice(0, start) + text + value.slice(end);
  setValue(next);
  requestAnimationFrame(() => {
    el.focus();
    el.selectionStart = start + text.length;
    el.selectionEnd = start + text.length;
  });
}

function TemplateEditor({ template, onSaved }) {
  const toast = useToast();
  const [enabled, setEnabled] = useState(template.enabled);
  const [subject, setSubject] = useState(template.subject || '');
  const [body, setBody] = useState(template.body);
  const [preview, setPreview] = useState(null);
  const [error, setError] = useState(null);
  const bodyRef = useRef(null);
  const isEmail = template.channel === 'Email';

  useEffect(() => {
    const handle = setTimeout(async () => {
      try {
        setPreview(await api.post('/api/v1/messaging/templates/preview', { key: template.key, channel: template.channel, subject, body }));
      } catch {
        setPreview(null);
      }
    }, 350);
    return () => clearTimeout(handle);
  }, [template.key, template.channel, subject, body]);

  const save = async () => {
    setError(null);
    try {
      await api.put('/api/v1/messaging/templates', { key: template.key, channel: template.channel, enabled, subject: isEmail ? subject : null, body });
      toast('Şablon kaydedildi.');
      await onSaved();
    } catch (err) {
      setError(err);
    }
  };

  return (
    <div className="row g-4">
      <div className="col-xxl-6">
        <Card title={<>{template.label} <span className="badge text-bg-light border ms-1 fw-normal">{CHANNEL[template.channel][1]}</span></>} icon={CHANNEL[template.channel][0]}
          actions={(
            <div className="form-check form-switch m-0">
              <input className="form-check-input" type="checkbox" role="switch" id="tplOn" checked={enabled} onChange={(e) => setEnabled(e.target.checked)} />
              <label className="form-check-label small" htmlFor="tplOn">{enabled ? 'Otomatik gönderim açık' : 'Otomatik gönderim kapalı'}</label>
            </div>
          )}
          footer={(
            <div className="d-flex justify-content-between gap-2">
              <BusyButton className="btn btn-outline-secondary" disabled={!template.isCustom} onClick={async () => {
                await api.del(`/api/v1/messaging/templates/${template.key}/${template.channel}`);
                toast('Varsayılan şablona dönüldü.', 'info');
                await onSaved();
              }}><i className="bi bi-arrow-counterclockwise me-1" />Varsayılana dön</BusyButton>
              <BusyButton onClick={save}><i className="bi bi-save me-1" />Kaydet</BusyButton>
            </div>
          )}>
          <div className="small text-body-secondary mb-3">{template.description}</div>
          <ErrorAlert error={error} />
          {isEmail && (
            <div className="mb-3">
              <label className="form-label" htmlFor="subj">Konu</label>
              <input className="form-control" id="subj" maxLength={300} value={subject} onChange={(e) => setSubject(e.target.value)} />
            </div>
          )}
          <label className="form-label" htmlFor="tplBody">{isEmail ? 'İçerik (HTML)' : 'Mesaj'}</label>
          <textarea ref={bodyRef} className="form-control font-monospace small" id="tplBody" rows={isEmail ? 14 : 5} value={body} onChange={(e) => setBody(e.target.value)} />
          <div className="mt-3">
            <div className="small text-body-secondary mb-1">Değişkenler (eklemek için tıklayın)</div>
            <div className="d-flex flex-wrap gap-1">
              {template.variables.map((v) => (
                <button key={v.name} type="button" className="btn btn-sm btn-outline-secondary py-0 px-2 font-monospace" title={`${v.description} · örn. ${v.sample}`}
                  onClick={() => insertAt(bodyRef.current, `{{${v.name}}}`, body, setBody)}>{`{{${v.name}}}`}</button>
              ))}
            </div>
          </div>
        </Card>
      </div>
      <div className="col-xxl-6">
        <Card title="Önizleme" icon="eye" bodyClass="card-body p-0">
          {!preview ? <div className="p-3"><Spinner /></div> : isEmail ? (
            <>
              <div className="px-3 py-2 border-bottom small"><span className="text-body-secondary">Konu:</span> <strong>{preview.subject}</strong></div>
              {preview.unknown?.length > 0 && <div className="alert alert-warning rounded-0 mb-0 small py-2">Tanımsız değişken: {preview.unknown.join(', ')}</div>}
              <iframe title="E-posta önizleme" className="w-100 border-0 d-block" style={{ height: '34rem', background: '#f4f7fb' }} sandbox="" srcDoc={preview.html} />
            </>
          ) : (
            <div className="p-4">
              <div className="sms-bubble">{preview.text}</div>
              <div className="small text-body-secondary mt-2">{preview.length} karakter, {preview.segments} SMS</div>
              {preview.unknown?.length > 0 && <div className="alert alert-warning mt-2 mb-0 small py-2">Tanımsız değişken: {preview.unknown.join(', ')}</div>}
            </div>
          )}
        </Card>
      </div>
    </div>
  );
}

function Templates() {
  const { data, loading, error, reload } = useLoad(() => api.get('/api/v1/messaging/templates'), []);
  const [selected, setSelected] = useState(null);
  const groups = useMemo(() => {
    const map = new Map();
    (data || []).forEach((t) => {
      if (!map.has(t.key)) map.set(t.key, { key: t.key, label: t.label, audience: t.audience, items: [] });
      map.get(t.key).items.push(t);
    });
    return [...map.values()];
  }, [data]);
  useEffect(() => {
    if (data && !selected) setSelected({ key: data[0].key, channel: data[0].channel });
  }, [data, selected]);

  if (loading) return <Spinner />;
  if (error) return <ErrorAlert error={error} />;
  const current = data.find((t) => t.key === selected?.key && t.channel === selected?.channel);

  return (
    <div className="row g-4">
      <div className="col-lg-4 col-xxl-3">
        <div className="card shadow-sm">
          <div className="list-group list-group-flush">
            {groups.map((g) => (
              <div key={g.key} className="list-group-item">
                <div className="fw-semibold small">{g.label}</div>
                <div className="small text-body-secondary mb-2">{g.audience}</div>
                <div className="d-flex gap-1 flex-wrap">
                  {g.items.map((t) => (
                    <button key={t.channel} type="button" onClick={() => setSelected({ key: t.key, channel: t.channel })}
                      className={`btn btn-sm btn-pill ${selected?.key === t.key && selected?.channel === t.channel ? 'btn-primary' : 'btn-outline-secondary'}`}>
                      <i className={`bi bi-${CHANNEL[t.channel][0]} me-1`} />{CHANNEL[t.channel][1]}
                      <span className={`status-dot ms-2 me-0 ${t.enabled ? 'bg-success' : 'bg-secondary'}`} />
                    </button>
                  ))}
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
      <div className="col-lg-8 col-xxl-9">
        {current && <TemplateEditor key={`${current.key}-${current.channel}-${current.isCustom}-${current.body.length}`} template={current} onSaved={reload} />}
      </div>
    </div>
  );
}

export const TemplateSettings = () => (
  <SettingsLayout icon="file-earmark-richtext" title="Bildirim şablonları" subtitle="Müşteri ve kullanıcılara giden e-posta / SMS içerikleri">
    <TenantRequired><Templates /></TenantRequired>
  </SettingsLayout>
);

function MessageDetail({ id, onClose, onChanged }) {
  const toast = useToast();
  const { data, loading, error, reload } = useLoad(() => api.get(`/api/v1/messaging/messages/${id}`), [id]);
  const m = data?.message;
  return (
    <Modal title="Mesaj ayrıntısı" size="lg" scrollable onClose={onClose}
      footer={m && (
        <div className="modal-footer">
          {m.status === 'Queued' && <BusyButton className="btn btn-outline-danger" onClick={async () => { await api.post(`/api/v1/messaging/messages/${id}/cancel`); toast('İptal edildi.', 'warning'); await reload(); await onChanged(); }}>İptal et</BusyButton>}
          {(m.status === 'Failed' || m.status === 'Cancelled') && <BusyButton onClick={async () => { await api.post(`/api/v1/messaging/messages/${id}/retry`); toast('Yeniden kuyruğa alındı.'); await reload(); await onChanged(); }}><i className="bi bi-arrow-repeat me-1" />Yeniden gönder</BusyButton>}
          <button type="button" className="btn btn-outline-secondary" onClick={onClose}>Kapat</button>
        </div>
      )}>
      <div className="modal-body">
        <ErrorAlert error={error} />
        {loading ? <Spinner /> : m && (
          <>
            <dl className="row small kv">
              <dt className="col-sm-3">Alıcı</dt><dd className="col-sm-9">{m.recipientName ? `${m.recipientName} · ` : ''}<span className="font-monospace">{m.recipient}</span></dd>
              {m.subject && <><dt className="col-sm-3">Konu</dt><dd className="col-sm-9">{m.subject}</dd></>}
              <dt className="col-sm-3">Durum</dt><dd className="col-sm-9"><span className={`badge text-bg-${STATUS_BADGE[m.status][1]}`}>{STATUS_BADGE[m.status][0]}</span> · {m.attempts} deneme</dd>
              {m.lastError && <><dt className="col-sm-3">Hata</dt><dd className="col-sm-9 text-danger">{m.lastError}</dd></>}
              {m.providerMessageId && <><dt className="col-sm-3">Sağlayıcı no</dt><dd className="col-sm-9 font-monospace">{m.providerMessageId}</dd></>}
              <dt className="col-sm-3">Oluşturma</dt><dd className="col-sm-9">{fmtDate(m.createdAt)} · {m.createdBy}</dd>
              {m.sentAt && <><dt className="col-sm-3">Gönderim</dt><dd className="col-sm-9">{fmtDate(m.sentAt)}</dd></>}
              {data.attachments.length > 0 && <><dt className="col-sm-3">Ekler</dt><dd className="col-sm-9">{data.attachments.map((a) => <span key={a} className="badge text-bg-light border me-1">{a === 'InvoiceHtml' ? 'Fatura (HTML)' : 'UBL-TR XML'}</span>)}</dd></>}
            </dl>
            {m.channel === 'Email'
              ? <iframe title="E-posta içeriği" className="w-100 border rounded" style={{ height: '28rem', background: '#f4f7fb' }} sandbox="" srcDoc={data.body} />
              : <div className="sms-bubble">{data.body}</div>}
          </>
        )}
      </div>
    </Modal>
  );
}

function MessageLog() {
  const [channel, setChannel] = useState('');
  const [status, setStatus] = useState('');
  const [q, setQ] = useState('');
  const [open, setOpen] = useState(null);
  const query = new URLSearchParams({ take: '300', ...(channel && { channel }), ...(status && { status }), ...(q.trim() && { q: q.trim() }) }).toString();
  const { data, loading, reload } = useLoad(() => api.get(`/api/v1/messaging/messages?${query}`), [query]);
  const { data: stats, reload: reloadStats } = useLoad(() => api.get('/api/v1/messaging/messages/stats'), []);
  const stat = (ch, st) => stats?.[`${ch}:${st}`] || 0;

  return (
    <>
      <div className="row g-3 mb-4">
        {['Email', 'Sms'].map((ch) => (
          <div className="col-md-6" key={ch}>
            <div className="card shadow-sm h-100"><div className="card-body d-flex align-items-center gap-3">
              <span className="tile-icon flex-shrink-0"><i className={`bi bi-${CHANNEL[ch][0]}`} /></span>
              <div className="flex-grow-1">
                <div className="fw-semibold">{CHANNEL[ch][1]} · son 30 gün</div>
                <div className="d-flex flex-wrap gap-3 small mt-1">
                  <span className="text-success"><i className="bi bi-check-circle me-1" />{stat(ch, 'Sent')} gönderildi</span>
                  <span className="text-body-secondary"><i className="bi bi-hourglass me-1" />{stat(ch, 'Queued') + stat(ch, 'Sending')} kuyrukta</span>
                  <span className="text-danger"><i className="bi bi-x-circle me-1" />{stat(ch, 'Failed')} başarısız</span>
                </div>
              </div>
            </div></div>
          </div>
        ))}
      </div>
      <Card className="mb-4">
        <div className="row g-2">
          <div className="col-md-6"><input className="form-control" placeholder="Alıcı veya konu ara" aria-label="Ara" value={q} onChange={(e) => setQ(e.target.value)} /></div>
          <div className="col-6 col-md-3">
            <select className="form-select" aria-label="Kanal" value={channel} onChange={(e) => setChannel(e.target.value)}>
              <option value="">Tüm kanallar</option><option value="Email">E-posta</option><option value="Sms">SMS</option>
            </select>
          </div>
          <div className="col-6 col-md-3">
            <select className="form-select" aria-label="Durum" value={status} onChange={(e) => setStatus(e.target.value)}>
              <option value="">Tüm durumlar</option>
              {Object.entries(STATUS_BADGE).map(([k, [l]]) => <option key={k} value={k}>{l}</option>)}
            </select>
          </div>
        </div>
      </Card>
      <Card bodyClass="" title="Gönderimler" icon="list-ul" actions={<BusyButton className="btn btn-sm btn-outline-secondary" onClick={async () => { await reload(); await reloadStats(); }}><i className="bi bi-arrow-clockwise" /></BusyButton>}>
        {loading ? <div className="px-3"><Spinner /></div> : data?.length ? (
          <div className="table-responsive">
            <table className="table table-hover align-middle mb-0 table-click">
              <thead><tr><th>Zaman</th><th>Kanal</th><th>Alıcı</th><th>Konu / şablon</th><th>Durum</th><th className="num">Deneme</th></tr></thead>
              <tbody>
                {data.map((m) => (
                  <tr key={m.id} onClick={() => setOpen(m.id)}>
                    <td className="small text-nowrap">{fmtDate(m.createdAt)}</td>
                    <td><i className={`bi bi-${CHANNEL[m.channel][0]} me-1`} />{CHANNEL[m.channel][1]}</td>
                    <td className="small"><div>{m.recipientName}</div><div className="font-monospace text-body-secondary">{m.recipient}</div></td>
                    <td className="small">{m.subject || <span className="font-monospace text-body-secondary">{m.templateKey}</span>}</td>
                    <td>
                      <span className={`badge text-bg-${STATUS_BADGE[m.status][1]}`}>{STATUS_BADGE[m.status][0]}</span>
                      {m.lastError && <div className="small text-danger text-truncate" style={{ maxWidth: '18rem' }} title={m.lastError}>{m.lastError}</div>}
                    </td>
                    <td className="num">{m.attempts}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : <Empty icon="envelope">Henüz gönderim yok.</Empty>}
      </Card>
      {open && <MessageDetail id={open} onClose={() => setOpen(null)} onChanged={async () => { await reload(); await reloadStats(); }} />}
    </>
  );
}

export const MessageLogSettings = () => (
  <SettingsLayout icon="mailbox" title="Gönderim kayıtları" subtitle="E-posta ve SMS kuyruğu, durumları ve hataları">
    <TenantRequired><MessageLog /></TenantRequired>
  </SettingsLayout>
);
