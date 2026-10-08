import { useNavigate, useParams } from 'react-router';
import { api, download } from '../api.js';
import { fmtDate, fmtMoney, fmtNumber, STATUS, todayIso, UNITS } from '../format.js';
import { useSession } from '../session.jsx';
import {
  BusyButton, Card, DocTypeBadge, ErrorAlert, PageHeader, Spinner, StatusBadge, useDialogs, useLoad, useToast,
} from '../components/ui.jsx';

const UNIT_LABELS = Object.fromEntries(UNITS);

const isPortal = (i) => i.issuanceChannel === 'GibPortal';

const STEP_ICONS = {
  Draft: 'file-earmark-plus', Validating: 'search', Validated: 'check2-circle', AwaitingApproval: 'hourglass-split', Approved: 'hand-thumbs-up',
  Signing: 'pen', Signed: 'patch-check', Queued: 'clock', Transmitting: 'send', InDoubt: 'question-circle', Sent: 'send-check',
  Acknowledged: 'envelope-check', Delivered: 'envelope-open', Accepted: 'check2-all', Rejected: 'x-octagon', Cancelled: 'slash-circle',
  Objected: 'exclamation-octagon', Failed: 'exclamation-triangle',
};

const GUID = /([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})/i;

function cleanNote(note) {
  if (!note || note.includes('CN=') || note.startsWith('Uyum kararı')) return null;
  const m = GUID.exec(note);
  return m ? { text: note.replace(m[0], '').replace(/[:s—-]+$/, ''), ref: m[0] } : { text: note, ref: null };
}

function gap(from, to) {
  const s = Math.max(0, Math.round((new Date(to) - new Date(from)) / 1000));
  if (s < 60) return `${s} sn`;
  if (s < 3600) return `${Math.round(s / 60)} dk`;
  if (s < 86400) return `${Math.round(s / 3600)} sa`;
  return `${Math.round(s / 86400)} gün`;
}

function HistoryTimeline({ items }) {
  const rows = [...items].reverse();
  return (
    <ol className="history">
      {rows.map((h, idx) => {
        const [label, color] = STATUS[h.to] || [h.to, 'secondary'];
        const note = cleanNote(h.note);
        const prev = rows[idx + 1];
        const [date, time] = fmtDate(h.at).split(' ');
        return (
          <li key={`${h.at}-${h.to}`} className={idx === 0 ? 'current' : ''}>
            <span className={`history-icon text-bg-${color}`}><i className={`bi bi-${STEP_ICONS[h.to] || 'dot'}`} /></span>
            <div className="history-body">
              <div className="d-flex justify-content-between align-items-start gap-2">
                <span className="fw-semibold">{label}{idx === 0 && <span className="badge rounded-pill text-bg-light border ms-2 fw-normal">Güncel</span>}</span>
                <span className="text-body-secondary small text-nowrap" title={fmtDate(h.at)}>{time}</span>
              </div>
              <div className="small text-body-secondary d-flex flex-wrap gap-2">
                <span><i className="bi bi-person me-1" />{h.actor}</span>
                <span>{date}</span>
                {prev && <span><i className="bi bi-stopwatch me-1" />+{gap(prev.at, h.at)}</span>}
              </div>
              {note && (
                <div className="history-note small">
                  {note.text}
                  {note.ref && <div className="font-monospace text-body-secondary text-truncate" title={note.ref}>{note.ref}</div>}
                </div>
              )}
            </div>
          </li>
        );
      })}
    </ol>
  );
}

const ACTIONS = [
  { id: 'submit', label: 'Doğrulamaya gönder', icon: 'check2-square', variant: 'primary', policy: 'InvoiceCreate', when: (i) => i.status === 'Draft' },
  { id: 'approve', label: 'Onayla', icon: 'hand-thumbs-up', variant: 'success', policy: 'InvoiceApprove', when: (i) => i.status === 'AwaitingApproval' },
  { id: 'reject', label: 'Reddet', icon: 'hand-thumbs-down', variant: 'outline-danger', policy: 'InvoiceApprove', when: (i) => i.status === 'AwaitingApproval' },
  { id: 'sign', label: 'İmzala (Mali Mühür)', icon: 'pen', variant: 'primary', policy: 'InvoiceSign', when: (i) => i.status === 'Approved' },
  { id: 'portal-draft', label: "GİB e-Arşiv Portal'da düzenle", icon: 'bank', variant: 'outline-primary', policy: 'GibPortal', when: (i) => i.status === 'Approved' && i.documentType === 'EArsiv' },
  { id: 'portal-preview', label: 'GİB önizleme', icon: 'eye', variant: 'outline-secondary', policy: 'GibPortal', when: (i) => i.status === 'Signing' && isPortal(i) },
  { id: 'portal-sms', label: 'SMS onay kodu gönder', icon: 'phone', variant: 'outline-primary', policy: 'InvoiceSign', when: (i) => i.status === 'Signing' && isPortal(i) && i.portalEnvironment === 'Production' },
  { id: 'portal-sign', label: 'GİB ile imzala', icon: 'patch-check', variant: 'success', policy: 'InvoiceSign', when: (i) => i.status === 'Signing' && isPortal(i) },
  { id: 'portal-delete', label: 'Portal taslağını sil', icon: 'trash', variant: 'outline-danger', policy: 'GibPortal', when: (i) => i.status === 'Signing' && isPortal(i) },
  { id: 'transmit', label: 'Gönder', icon: 'send', variant: 'primary', policy: 'InvoiceTransmit', when: (i) => i.status === 'Queued' },
  { id: 'refresh', label: 'Durum sorgula', icon: 'arrow-repeat', variant: 'outline-primary', policy: 'InvoiceTransmit', when: (i) => !isPortal(i) && ['Transmitting', 'InDoubt', 'Sent', 'Acknowledged', 'Delivered'].includes(i.status) },
  { id: 'retry', label: 'Yeniden dene', icon: 'arrow-clockwise', variant: 'outline-warning', policy: 'InvoiceTransmit', when: (i) => i.status === 'Failed' },
  { id: 'cancel', label: 'İptal et', icon: 'x-octagon', variant: 'outline-danger', policy: 'InvoiceCancel', when: (i) => !isPortal(i) && i.status === 'Delivered' && i.documentType === 'EArsiv' },
  { id: 'portal-cancel', label: 'İptal talebi (GİB)', icon: 'x-octagon', variant: 'outline-danger', policy: 'InvoiceCancel', when: (i) => isPortal(i) && i.status === 'Sent' },
  { id: 'objection', label: 'İtiraz kaydet', icon: 'megaphone', variant: 'outline-dark', policy: 'InvoiceCancel', when: (i) => ['Delivered', 'Accepted'].includes(i.status) },
];

const REASON_FIELD = [{ name: 'reason', label: 'Gerekçe', type: 'textarea', required: true, min: 3, invalid: 'En az 3 karakterlik bir gerekçe yazın.' }];

function Party({ title, party, showRegistration }) {
  return (
    <Card title={title} icon={title === 'Satıcı' ? 'shop' : 'person-vcard'} className="h-100">
      <div className="fw-semibold">{party.title}</div>
      <div className="small mono">{party.taxId}</div>
      <div className="small">{[party.street, party.buildingNumber, party.district, party.city].filter(Boolean).join(' ')}</div>
      {party.taxOffice && <div className="small text-body-secondary">VD: {party.taxOffice}</div>}
      {showRegistration && (
        <div className="mt-2">
          {party.isEFaturaRegistered ? <span className="badge text-bg-primary">e-Fatura kayıtlı</span> : <span className="badge text-bg-light border">e-Fatura kayıtlı değil</span>}
        </div>
      )}
    </Card>
  );
}

export function InvoiceDetail() {
  const { id } = useParams();
  const session = useSession();
  const dialogs = useDialogs();
  const toast = useToast();
  const navigate = useNavigate();

  const { data, loading, error, reload } = useLoad(async () => {
    const [invoice, history] = await Promise.all([api.get(`/api/v1/invoices/${id}`), api.get(`/api/v1/invoices/${id}/history`)]);
    return { invoice, history };
  }, [id]);

  if (loading && !data) return <Spinner />;
  if (error) return <ErrorAlert error={error} />;

  const { invoice: i, history } = data;
  const d = i.decision;
  const signed = !!i.signedXmlSha256;
  const p = session.permissions;
  const selfApprovalBlocked = p.requireMakerChecker && i.createdBy?.toLowerCase() === p.userId?.toLowerCase()
    && !p.roles.some((r) => (p.selfApprovalRoles || []).includes(r));
  const blocking = (d?.findings || []).filter((f) => f.blocking && f.code !== 'COMPLIANCE-UNAPPROVED-RULES');
  const actions = ACTIONS.filter((a) => a.when(i) && session.can(a.policy) && !(selfApprovalBlocked && (a.id === 'approve' || a.id === 'reject')));
  const base = `/api/v1/invoices/${i.id}`;
  const portal = `/api/v1/gib-portal/invoices/${i.id}`;

  const showHtml = async (content) => dialogs.show('GİB görünümü', <iframe className="preview-frame" sandbox="" title="Fatura önizleme" srcDoc={content} />, 'xl');

  const run = async (action) => {
    switch (action) {
      case 'submit': await api.post(`${base}/submit`); toast('Fatura doğrulandı ve onaya gönderildi.'); break;
      case 'approve': await api.post(`${base}/approve`); toast('Fatura onaylandı.'); break;
      case 'reject': {
        const f = await dialogs.form({ title: 'Faturayı reddet', fields: REASON_FIELD, submitText: 'Reddet', submitVariant: 'danger' });
        if (!f) return;
        await api.post(`${base}/reject`, f); toast('Fatura taslağa geri gönderildi.', 'warning');
        break;
      }
      case 'sign':
        if (!await dialogs.confirm('İmzala', 'Belge numarası atanacak, UBL-TR üretilip imzalanacak ve gönderim kuyruğuna alınacak. Bu işlemden sonra fatura içeriği değiştirilemez.', 'İmzala')) return;
        await api.post(`${base}/sign`); toast('Fatura imzalandı ve gönderim kuyruğuna alındı.');
        break;
      case 'portal-draft': {
        const status = await api.get('/api/v1/gib-portal/status');
        if (!status.connected) {
          toast('Önce GİB e-Arşiv Portal’a bağlanın.', 'warning');
          navigate('/gib-portal');
          return;
        }
        if (!await dialogs.confirm('GİB portal taslağı', `Fatura GİB e-Arşiv Portal (${status.environment === 'Production' ? 'CANLI' : 'test'}) üzerinde taslak olarak oluşturulacak. ETTN'yi GİB atar.`, 'Taslak oluştur')) return;
        const r = await api.post(`${portal}/draft`); toast(`GİB taslağı oluşturuldu: ${r.ettn}`);
        break;
      }
      case 'portal-preview': await showHtml(await api.text(`${portal}/html`)); return;
      case 'portal-sms': {
        const r = await api.post(`${portal}/sms`);
        toast(`Onay kodu GİB'e kayıtlı ${r.maskedPhone} numarasına gönderildi (${r.validMinutes} dk).`, 'info');
        return;
      }
      case 'portal-sign': {
        let smsCode = null;
        if (i.portalEnvironment === 'Production') {
          const f = await dialogs.form({
            title: 'GİB imza — SMS onayı', submitText: 'İmzala', submitVariant: 'success',
            fields: [{ name: 'smsCode', label: 'SMS kodu', required: true, pattern: '[0-9]{6}', inputmode: 'numeric', autocomplete: 'one-time-code', invalid: '6 haneli kod girin.' }],
          });
          if (!f) return;
          smsCode = f.smsCode;
        } else if (!await dialogs.confirm('GİB imza (test portalı)', 'Fatura test portalının imza komutuyla imzalanacak.', 'İmzala', 'success')) {
          return;
        }
        await api.post(`${portal}/sign`, { smsCode }); toast('Fatura GİB tarafından imzalandı ve düzenlendi.');
        break;
      }
      case 'portal-delete': {
        const f = await dialogs.form({ title: 'Portal taslağını sil', fields: REASON_FIELD, submitText: 'Sil', submitVariant: 'danger' });
        if (!f) return;
        await api.post(`${portal}/delete-draft`, f); toast('GİB portal taslağı silindi; fatura onaylı duruma döndü.', 'warning');
        break;
      }
      case 'transmit': await api.post(`${base}/transmit`); toast('Gönderim tamamlandı.'); break;
      case 'refresh': await api.post(`${base}/refresh-status`); toast('Durum güncellendi.', 'info'); break;
      case 'retry': await api.post(`${base}/retry`); toast('Fatura yeniden kuyruğa alındı.'); break;
      case 'cancel': {
        const f = await dialogs.form({ title: 'e-Arşiv iptali', fields: REASON_FIELD, submitText: 'İptal et', submitVariant: 'danger' });
        if (!f) return;
        await api.post(`${base}/cancel`, f); toast('Fatura iptal edildi.', 'warning');
        break;
      }
      case 'portal-cancel': {
        const f = await dialogs.form({ title: 'GİB iptal talebi', fields: REASON_FIELD, submitText: 'Talep oluştur', submitVariant: 'danger' });
        if (!f) return;
        const r = await api.post(`${portal}/cancel`, f); toast(r.message || 'İptal talebi oluşturuldu.', 'warning');
        break;
      }
      case 'objection': {
        const f = await dialogs.form({
          title: 'İtiraz kaydı', submitText: 'Kaydet', submitVariant: 'dark',
          fields: [
            {
              name: 'method', label: 'Bildirim yöntemi', type: 'select', required: true, value: 'NOTER',
              options: [{ value: 'NOTER', label: 'Noter' }, { value: 'TAAHHUTLU_MEKTUP', label: 'Taahhütlü mektup' }, { value: 'TELGRAF', label: 'Telgraf' }, { value: 'KEP', label: 'KEP' }],
            },
            { name: 'referenceNumber', label: 'Referans / yevmiye no' },
            { name: 'notificationDate', label: 'Bildirim tarihi', type: 'date', required: true, value: todayIso() },
            ...REASON_FIELD,
          ],
        });
        if (!f) return;
        await api.post(`${base}/objection`, f); toast('İtiraz kaydedildi.', 'info');
        break;
      }
      default: return;
    }
    await reload();
  };

  const showQr = async () => {
    const { blob } = await api.blob(`${base}/qr`);
    const url = URL.createObjectURL(blob);
    await dialogs.show('GİB karekodu', <div className="text-center"><img className="img-fluid" alt="Fatura karekodu" src={url} /></div>, null);
    URL.revokeObjectURL(url);
  };

  const sendToCustomer = async () => {
    const f = await dialogs.form({
      title: 'Faturayı müşteriye gönder', submitText: 'Gönder',
      fields: [
        { name: 'channels', label: 'Kanal', type: 'checkboxes', value: [i.customer.email ? 'Email' : null, i.customer.phone ? 'Sms' : null].filter(Boolean), options: [{ value: 'Email', label: 'E-posta (fatura + XML ekli)' }, { value: 'Sms', label: 'SMS (görüntüleme bağlantısı)' }] },
        { name: 'email', label: 'E-posta', type: 'email', value: i.customer.email || '' },
        { name: 'phone', label: 'Cep telefonu', type: 'tel', value: i.customer.phone || '' },
      ],
    });
    if (!f) return;
    if (!f.channels.length) {
      toast('En az bir kanal seçin.', 'warning');
      return;
    }
    const r = await api.post(`${base}/send`, { channels: f.channels, email: f.email || null, phone: f.phone || null });
    if (r.queued > 0) toast(`${r.queued} gönderim kuyruğa alındı.${r.skipped.length ? ` ${r.skipped.join(' ')}` : ''}`);
    else toast(`Gönderilemedi: ${r.skipped.join(' ')}`, 'warning');
  };

  const share = async () => {
    const { url } = await api.get(`${base}/share-link`);
    await dialogs.show('Fatura bağlantısı', (
      <>
        <div className="input-group">
          <input className="form-control font-monospace small" readOnly value={url} aria-label="Bağlantı" onFocus={(e) => e.target.select()} />
          <button className="btn btn-outline-secondary" type="button" onClick={async () => { try { await navigator.clipboard.writeText(url); toast('Kopyalandı.', 'info'); } catch { toast('Kopyalanamadı.', 'warning'); } }}><i className="bi bi-clipboard" /></button>
          <a className="btn btn-outline-primary" href={`${url}/fatura.pdf`} target="_blank" rel="noopener noreferrer"><i className="bi bi-filetype-pdf me-1" />PDF</a>
          <a className="btn btn-primary" href={url} target="_blank" rel="noopener noreferrer"><i className="bi bi-box-arrow-up-right me-1" />Aç</a>
        </div>
      </>
    ), 'lg');
  };

  const verify = async () => {
    const v = await api.get(`${base}/evidence/verify`);
    if (v.isValid) toast(`Kanıt paketi doğrulandı (${v.manifestCount} manifest, zincir sağlam).`);
    else toast(`Kanıt bütünlüğü sorunu: ${v.problems.join('; ')}`, 'danger');
  };

  return (
    <>
      <PageHeader
        breadcrumb={[['Faturalar', '/invoices'], [i.documentNumber || i.draftNumber || 'Taslak']]}
        title={<>{i.documentNumber || i.draftNumber} <StatusBadge status={i.status} /></>}
        subtitle={(
          <span className="d-inline-flex flex-wrap gap-2 align-items-center">
            <DocTypeBadge type={i.documentType} /> <span className="fw-semibold text-body">{i.customer.title}</span>
            {isPortal(i) && <span className="badge text-bg-warning"><i className="bi bi-bank me-1" />GİB e-Arşiv Portal</span>}
          </span>
        )}
        actions={(
          <div className="text-end">
            <div className="small text-body-secondary">Ödenecek</div>
            <div className="fs-3 fw-bold">{fmtMoney(i.totals.payableAmount, i.currency)}</div>
          </div>
        )}
      />

      {i.lastError && <div className="alert alert-warning"><i className="bi bi-exclamation-triangle me-1" />{i.lastError}</div>}

      <Card className="mb-4">
        <div className="d-flex flex-wrap gap-2 align-items-center">
          {actions.length
            ? actions.map((a) => <BusyButton key={a.id} className={`btn btn-${a.variant}`} onClick={() => run(a.id)}><i className={`bi bi-${a.icon} me-1`} />{a.label}</BusyButton>)
            : <span className="text-body-secondary small">{selfApprovalBlocked && i.status === 'AwaitingApproval' ? 'Kendi oluşturduğunuz faturayı başka bir onaylayıcı onaylamalıdır.' : 'Bu aşamada işlem yok.'}</span>}
          {signed && <span className="vr mx-1" />}
          {signed && <BusyButton className="btn btn-outline-secondary" onClick={() => download(`${base}/ubl`, `${i.documentNumber || i.id}.xml`)}><i className="bi bi-filetype-xml me-1" />UBL indir</BusyButton>}
          {signed && !isPortal(i) && <BusyButton className="btn btn-outline-secondary" onClick={showQr}><i className="bi bi-qr-code me-1" />Karekod</BusyButton>}
          {signed && isPortal(i) && <BusyButton className="btn btn-outline-secondary" onClick={async () => showHtml(await api.text(`${portal}/html`))}><i className="bi bi-file-earmark-richtext me-1" />GİB görünümü</BusyButton>}
          {signed && session.can('AuditRead') && <BusyButton className="btn btn-outline-secondary" onClick={verify}><i className="bi bi-shield-check me-1" />Kanıt doğrula</BusyButton>}
          {i.documentNumber && (session.can('InvoiceCreate') || session.can('InvoiceTransmit')) && <BusyButton className="btn btn-outline-primary" onClick={sendToCustomer}><i className="bi bi-send me-1" />Müşteriye gönder</BusyButton>}
          <BusyButton className="btn btn-outline-secondary" onClick={() => download(`${base}/pdf`, `${i.documentNumber || i.draftNumber || i.id}.pdf`)}><i className="bi bi-filetype-pdf me-1" />PDF indir</BusyButton>
          <BusyButton className="btn btn-outline-secondary" onClick={share}><i className="bi bi-link-45deg me-1" />Görüntüle / paylaş</BusyButton>
        </div>
      </Card>

      <div className="row g-4">
        <div className="col-xl-8 d-flex flex-column gap-4">
          <div className="row g-4">
            <div className="col-md-6"><Party title="Satıcı" party={i.supplier} /></div>
            <div className="col-md-6"><Party title="Alıcı" party={i.customer} showRegistration /></div>
          </div>

          <Card title="Satırlar" icon="list-ul" bodyClass="">
            <div className="table-responsive">
              <table className="table align-middle mb-0">
                <thead><tr><th>#</th><th>Mal / hizmet</th><th className="num">Miktar</th><th className="num">Birim fiyat</th><th className="num">İskonto</th><th className="num">KDV</th><th className="num">Tevkifat</th><th className="num">Tutar</th></tr></thead>
                <tbody>
                  {i.lines.map((l) => (
                    <tr key={l.lineNo}>
                      <td>{l.lineNo}</td>
                      <td>{l.name}{l.description && <div className="small text-body-secondary">{l.description}</div>}</td>
                      <td className="num">{fmtNumber(l.quantity)} {UNIT_LABELS[l.unitCode] || l.unitCode}</td>
                      <td className="num">{fmtMoney(l.unitPrice, i.currency)}</td>
                      <td className="num">{l.discountAmount ? fmtMoney(l.discountAmount, i.currency) : '—'}</td>
                      <td className="num">%{fmtNumber(l.vatRate)} · {fmtMoney(l.vatAmount, i.currency)}{l.vatExemptionCode && <div className="small">İstisna {l.vatExemptionCode}</div>}</td>
                      <td className="num">{l.withholdingAmount ? `${l.withholdingCode} · ${fmtMoney(l.withholdingAmount, i.currency)}` : '—'}</td>
                      <td className="num">{fmtMoney(l.lineExtensionAmount, i.currency)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="card-body border-top">
              <div className="row justify-content-end">
                <div className="col-md-6">
                  <table className="table table-sm mb-0">
                    <tbody>
                      <tr><th>Mal/hizmet toplamı</th><td className="num">{fmtMoney(i.totals.lineExtensionAmount, i.currency)}</td></tr>
                      <tr><th>Toplam iskonto</th><td className="num">{fmtMoney(i.totals.allowanceTotalAmount, i.currency)}</td></tr>
                      {i.totals.vatSubtotals.map((s) => (
                        <tr key={s.percent}><th className="fw-normal">KDV %{fmtNumber(s.percent)} (matrah {fmtMoney(s.taxableAmount, i.currency)})</th><td className="num">{fmtMoney(s.taxAmount, i.currency)}</td></tr>
                      ))}
                      <tr><th>Vergiler dahil toplam</th><td className="num">{fmtMoney(i.totals.taxInclusiveAmount, i.currency)}</td></tr>
                      {i.totals.withholdingTotal > 0 && <tr><th>KDV tevkifatı</th><td className="num">− {fmtMoney(i.totals.withholdingTotal, i.currency)}</td></tr>}
                      <tr className="table-primary"><th>Ödenecek tutar</th><td className="num fw-bold">{fmtMoney(i.totals.payableAmount, i.currency)}</td></tr>
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          </Card>

          {blocking.length > 0 && (
            <div className="alert alert-danger mb-0">
              <div className="fw-semibold mb-1"><i className="bi bi-exclamation-octagon me-1" />Düzeltilmesi gerekenler</div>
              <ul className="mb-0 small">{blocking.map((f) => <li key={f.code + f.message}>{f.message}</li>)}</ul>
            </div>
          )}
        </div>

        <div className="col-xl-4 d-flex flex-column gap-4">
          <Card title="Bilgiler" icon="info-circle">
            <dl className="row mb-0 small">
              <dt className="col-6">Belge no</dt><dd className="col-6 mono">{i.documentNumber || '—'}</dd>
              <dt className="col-6">Taslak no</dt><dd className="col-6 mono">{i.draftNumber || '—'}</dd>
              <dt className="col-6">ETTN</dt><dd className="col-6 mono">{i.ettn}</dd>
              <dt className="col-6">Düzenleme</dt><dd className="col-6">{fmtDate(i.issueDate)} {String(i.issueTime).slice(0, 8)}</dd>
              <dt className="col-6">Teslim/hizmet</dt><dd className="col-6">{fmtDate(i.deliveryDate) || '—'}</dd>
              <dt className="col-6">Oluşturan</dt><dd className="col-6">{i.createdBy}</dd>
              <dt className="col-6">Onaylayan</dt><dd className="col-6">{i.approvedBy || '—'}</dd>
              <dt className="col-6">İmzalayan</dt><dd className="col-6">{i.signedBy || '—'}</dd>
              <dt className="col-6">Sağlayıcı ref.</dt><dd className="col-6 mono">{i.providerReference || '—'}</dd>
              {i.orderNumber && <><dt className="col-6">Sipariş</dt><dd className="col-6">{i.orderNumber}</dd></>}
              {i.despatchNumber && <><dt className="col-6">İrsaliye</dt><dd className="col-6">{i.despatchNumber}</dd></>}
              {signed && <><dt className="col-6">İmzalı XML SHA-256</dt><dd className="col-6 mono">{i.signedXmlSha256.slice(0, 20)}…</dd></>}
            </dl>
            {i.notes.length > 0 && (
              <>
                <hr />
                <div className="text-body-secondary small mb-1">Notlar</div>
                {i.notes.map((n) => <div key={n} className="small">{n}</div>)}
              </>
            )}
          </Card>
          <Card title="Durum geçmişi" icon="clock-history">
            <HistoryTimeline items={history.history} />
            {history.actions.length > 0 && (
              <>
                <h2 className="h6 text-body-secondary mt-3">İptal / itiraz kayıtları</h2>
                {history.actions.map((a) => (
                  <div key={a.id || a.createdAt} className="small border rounded p-2 mb-2">
                    <span className="badge text-bg-dark me-1">{a.kind === 'Cancellation' ? 'İptal' : 'İtiraz'}</span>{a.status}
                    <div>{a.reason}</div>
                    <div className="text-body-secondary">{a.requestedBy} · {fmtDate(a.createdAt)}</div>
                  </div>
                ))}
              </>
            )}
          </Card>
        </div>
      </div>
    </>
  );
}
