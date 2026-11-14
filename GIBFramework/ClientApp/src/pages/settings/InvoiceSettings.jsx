import { useEffect, useState } from 'react';
import { Link } from 'react-router';
import { api, uuid } from '../../api.js';
import { fmtDate, UNITS } from '../../format.js';
import { useSession } from '../../session.jsx';
import { BusyButton, Card, Empty, ErrorAlert, Spinner, useLoad, useToast } from '../../components/ui.jsx';
import { SettingsLayout, TenantRequired, useTenantSettings } from './SettingsLayout.jsx';

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';
const CURRENCIES = ['TRY', 'USD', 'EUR', 'GBP'];

function useEditable(source) {
  const [value, setValue] = useState(null);
  useEffect(() => { if (source !== undefined && source !== null) setValue(structuredClone(source)); }, [source]);
  return [value, setValue];
}

function SaveButton({ onSave, canEdit }) {
  return canEdit ? <BusyButton className="btn btn-primary btn-pill px-4" onClick={onSave}><i className="bi bi-save me-1" aria-hidden="true" />Kaydet</BusyButton> : null;
}

const updatedText = (s) => (s?.updatedAt ? `Son güncelleme: ${fmtDate(s.updatedAt)} · ${s.updatedBy}` : null);

function DefaultsPage() {
  const toast = useToast();
  const settings = useTenantSettings();
  const { data: defs } = useLoad(() => api.get('/api/v1/compliance/tax-definitions').catch(() => ({ vatRates: [20, 10, 1, 0] })), []);
  const [d, setD] = useEditable(settings.data?.defaults);
  const [error, setError] = useState(null);
  const set = (name, value) => setD((x) => ({ ...x, [name]: value }));

  const save = async () => {
    try {
      await settings.save({ defaults: { ...d, vatRate: Number(d.vatRate), paymentDueDays: d.paymentDueDays === '' || d.paymentDueDays == null ? null : Number(d.paymentDueDays) } });
      setError(null);
      toast('Fatura varsayılanları kaydedildi.');
    } catch (err) {
      setError(err);
    }
  };

  return (
    <SettingsLayout icon="receipt" title="Fatura varsayılanları" subtitle="Senaryo, para birimi, KDV, birim, vade ve kayıt tercihleri" actions={<SaveButton onSave={save} canEdit={settings.canEdit && Boolean(d)} />}>
      <ErrorAlert error={error || settings.error} />
      {!settings.error && (settings.loading || !d || !defs) && <Spinner />}
      {d && defs && <div className="d-grid gap-4">
        <Card title="Belge oluşturma varsayılanları">
          <fieldset disabled={!settings.canEdit} className="row g-4">
            <div className="col-md-6">
              <label className="form-label" htmlFor="d-profile">e-Fatura senaryosu</label>
              <select className="form-select" id="d-profile" value={d.profile} onChange={(e) => set('profile', e.target.value)}>
                <option value="TEMELFATURA">Temel fatura</option>
                <option value="TICARIFATURA">Ticari fatura</option>
              </select>
            </div>
            <div className="col-md-6">
              <label className="form-label" htmlFor="d-cur">Para birimi</label>
              <select className="form-select" id="d-cur" value={d.currency} onChange={(e) => set('currency', e.target.value)}>{CURRENCIES.map((c) => <option key={c}>{c}</option>)}</select>
            </div>
            <div className="col-md-6">
              <label className="form-label" htmlFor="d-vat">KDV oranı</label>
              <select className="form-select" id="d-vat" value={String(Number(d.vatRate))} onChange={(e) => set('vatRate', e.target.value)}>
                {defs.vatRates.map((r) => <option key={r} value={String(r)}>%{r}</option>)}
              </select>
            </div>
            <div className="col-md-6">
              <label className="form-label" htmlFor="d-unit">Birim</label>
              <select className="form-select" id="d-unit" value={d.unitCode} onChange={(e) => set('unitCode', e.target.value)}>{UNITS.map(([c, l]) => <option key={c} value={c}>{l}</option>)}</select>
            </div>
            <div className="col-md-6">
              <label className="form-label" htmlFor="d-due">Vade (gün)</label>
              <input className="form-control" id="d-due" type="number" min="0" max="3650" value={d.paymentDueDays ?? ''} onChange={(e) => set('paymentDueDays', e.target.value)} />
            </div>
            <div className="col-12">
              <div className="form-check form-switch">
                <input className="form-check-input" type="checkbox" role="switch" id="d-save" checked={d.saveCustomer} onChange={(e) => set('saveCustomer', e.target.checked)} />
                <label className="form-check-label" htmlFor="d-save">Alıcıyı cari kart olarak kaydet</label>
              </div>
            </div>
          </fieldset>
        </Card>
        <Card title="Yeni belge önizlemesi">
          <div className="d-flex flex-wrap gap-2">
            <span className="badge text-bg-success">{d.profile}</span>
            <span className="badge text-bg-secondary">{d.currency}</span>
            <span className="badge text-bg-secondary">KDV %{d.vatRate}</span>
          </div>
          {updatedText(settings.data) && <div className="form-text mt-3">{updatedText(settings.data)}</div>}
        </Card>
      </div>}
    </SettingsLayout>
  );
}

function NotesPage() {
  const toast = useToast();
  const settings = useTenantSettings();
  const [list, setList] = useEditable(settings.data?.noteTemplates);
  const [error, setError] = useState(null);
  const [selectedId, setSelectedId] = useState(null);
  const selected = list?.find((item) => item.id === selectedId) || list?.[0];
  const update = (id, patch) => setList((l) => l.map((n) => (n.id === id ? { ...n, ...patch } : n)));

  const add = () => {
    const id = `new-${uuid()}`;
    setList((l) => [...l, { id, title: '', text: '', autoAdd: false }]);
    setSelectedId(id);
  };

  const save = async () => {
    try {
      const saved = await settings.save({ noteTemplates: list.map((n) => ({ ...n, id: String(n.id).startsWith('new-') ? EMPTY_GUID : n.id })) });
      setList(structuredClone(saved.noteTemplates));
      setError(null);
      toast('Not şablonları kaydedildi.');
    } catch (err) {
      setError(err);
    }
  };

  return (
    <SettingsLayout icon="sticky" title="Not şablonları" subtitle="Faturalara eklenecek otomatik ve seçilebilir notlar"
      actions={<SaveButton onSave={save} canEdit={settings.canEdit && Boolean(list)} />}>
      <ErrorAlert error={error || settings.error} />
      {!settings.error && !list && <Spinner />}
      {list && <div className="row g-3 align-items-start">
        <div className="col-lg-5">
          <Card title="Tanımlı şablonlar" actions={settings.canEdit && (
            <button type="button" className="btn btn-primary" onClick={add}><i className="bi bi-plus-lg" aria-hidden="true" />Not ekle</button>
          )}>
            {list.length ? <ul className="list-unstyled d-grid gap-2 mb-0">
              {list.map((note) => <li key={note.id} className="d-flex flex-wrap align-items-center gap-2 rounded border p-2">
                <button type="button" className="btn btn-sm btn-outline-secondary flex-grow-1 justify-content-start text-break"
                  aria-pressed={selected?.id === note.id} onClick={() => setSelectedId(note.id)}>{note.title || 'Adsız şablon'}</button>
                <span className={`badge text-bg-${note.autoAdd ? 'success' : 'secondary'}`}>{note.autoAdd ? 'Otomatik' : 'İsteğe bağlı'}</span>
                {settings.canEdit && <button type="button" className="btn btn-sm btn-outline-danger" aria-label={`${note.title || 'Adsız şablon'} sil`}
                  onClick={() => setList((items) => items.filter((item) => item.id !== note.id))}><i className="bi bi-trash" /></button>}
              </li>)}
            </ul> : <Empty icon="sticky">Henüz not şablonu yok.</Empty>}
          </Card>
        </div>
        {selected && <div className="col-lg-7">
          <Card title="Şablon düzenleyici">
            <fieldset key={selected.id} disabled={!settings.canEdit} className="d-grid gap-3">
              <div>
                <label className="form-label" htmlFor="note-title">Şablon başlığı</label>
                <input className="form-control" id="note-title" maxLength={60} value={selected.title} onChange={(e) => update(selected.id, { title: e.target.value })} />
              </div>
              <div>
                <label className="form-label" htmlFor="note-text">Not metni</label>
                <textarea className="form-control" id="note-text" rows={4} maxLength={500} value={selected.text} onChange={(e) => update(selected.id, { text: e.target.value })} />
                <div className="form-text text-end">{selected.text.length}/500</div>
              </div>
              <div className="form-check form-switch">
                <input className="form-check-input" type="checkbox" role="switch" id="note-auto" checked={selected.autoAdd} onChange={(e) => update(selected.id, { autoAdd: e.target.checked })} />
                <label className="form-check-label" htmlFor="note-auto">Her faturaya otomatik ekle</label>
              </div>
            </fieldset>
            {selected.text && <div className="mt-4">
              <div className="form-label">Not önizlemesi</div>
              <div className="rounded border p-3 text-break" style={{ whiteSpace: 'pre-wrap' }}>{selected.text}</div>
            </div>}
          </Card>
        </div>}
      </div>}
    </SettingsLayout>
  );
}

function ibanValid(iban) {
  if (!/^[A-Z]{2}[0-9A-Z]{13,32}$/.test(iban) || (iban.startsWith('TR') && iban.length !== 26)) return false;
  const rearranged = iban.slice(4) + iban.slice(0, 4);
  let rest = 0;
  for (const ch of rearranged) {
    const v = /[A-Z]/.test(ch) ? String(ch.charCodeAt(0) - 55) : ch;
    for (const digit of v) rest = (rest * 10 + Number(digit)) % 97;
  }
  return rest === 1;
}

const formatIban = (iban) => iban.replace(/(.{4})/g, '$1 ').trim();
const maskIban = (iban) => iban.length > 6 ? `${iban.slice(0, 2)} •••• ${iban.slice(-4)}` : '—';

function BanksPage() {
  const session = useSession();
  const toast = useToast();
  const settings = useTenantSettings();
  const [list, setList] = useEditable(settings.data?.bankAccounts);
  const [error, setError] = useState(null);
  const update = (id, patch) => setList((l) => l.map((b) => (b.id === id ? { ...b, ...patch } : b)));

  const save = async () => {
    const bad = list.filter((b) => !ibanValid(b.iban)).map((b) => b.iban || '(boş)');
    if (bad.length) {
      setError({ message: 'Geçersiz IBAN.', details: bad });
      return;
    }
    try {
      const saved = await settings.save({ bankAccounts: list.map((b) => ({ ...b, id: String(b.id).startsWith('new-') ? EMPTY_GUID : b.id })) });
      setList(structuredClone(saved.bankAccounts));
      setError(null);
      toast('Banka hesapları kaydedildi.');
    } catch (err) {
      setError(err);
    }
  };

  return (
    <SettingsLayout icon="bank2" title="Banka hesapları" subtitle={updatedText(settings.data)}
      actions={settings.canEdit && list && (
        <>
          <button type="button" className="btn btn-outline-primary btn-pill px-3"
            onClick={() => setList((l) => [...l, { id: `new-${uuid()}`, bankName: '', branch: '', iban: 'TR', accountHolder: session.tenant?.profile?.title || '', currency: 'TRY', addToNotes: true }])}>
            <i className="bi bi-plus-lg me-1" />Hesap ekle
          </button>
          <SaveButton onSave={save} canEdit />
        </>
      )}>
      <ErrorAlert error={error || settings.error} />
      {!settings.error && !list && <Spinner />}
      {list?.length === 0 && <Card><Empty icon="bank2">Henüz banka hesabı yok.</Empty></Card>}
      {Boolean(list?.length) && (
        <Card title="Tanımlı hesaplar" className="mb-4">
          <div className="table-responsive">
            <table className="table align-middle mb-0">
              <thead><tr><th scope="col">Banka</th><th scope="col">Hesap sahibi</th><th scope="col">IBAN</th><th scope="col">Para birimi</th></tr></thead>
              <tbody>{list.map((account) => (
                <tr key={account.id}>
                  <th scope="row">{account.bankName || 'Yeni hesap'}</th>
                  <td>{account.accountHolder || '—'}</td>
                  <td className="text-nowrap font-monospace">{maskIban(account.iban)}</td>
                  <td>{account.currency}</td>
                </tr>
              ))}</tbody>
            </table>
          </div>
        </Card>
      )}
      {Boolean(list?.length) && (
        <fieldset disabled={!settings.canEdit} className="d-flex flex-column gap-3">
          {list.map((b) => {
            const valid = ibanValid(b.iban);
            return (
              <Card key={b.id} title={b.bankName || 'Banka hesabı'}>
                <div className="row g-3">
                  <div className="col-md-4"><label className="form-label small" htmlFor={`bn-${b.id}`}>Banka</label><input className="form-control" id={`bn-${b.id}`} maxLength={100} value={b.bankName} onChange={(e) => update(b.id, { bankName: e.target.value })} /></div>
                  <div className="col-md-3"><label className="form-label small" htmlFor={`br-${b.id}`}>Şube</label><input className="form-control" id={`br-${b.id}`} maxLength={100} value={b.branch || ''} onChange={(e) => update(b.id, { branch: e.target.value })} /></div>
                  <div className="col-md-3">
                    <label className="form-label small" htmlFor={`bc-${b.id}`}>Para birimi</label>
                    <select className="form-select" id={`bc-${b.id}`} value={b.currency} onChange={(e) => update(b.id, { currency: e.target.value })}>{CURRENCIES.map((c) => <option key={c}>{c}</option>)}</select>
                  </div>
                  <div className="col-md-2 d-flex align-items-end justify-content-end">
                    <button type="button" className="btn btn-sm btn-outline-danger" aria-label="Hesabı sil" onClick={() => setList((l) => l.filter((x) => x.id !== b.id))}><i className="bi bi-trash" /></button>
                  </div>
                  <div className="col-md-7">
                    <label className="form-label small" htmlFor={`ib-${b.id}`}>IBAN</label>
                    <input className={`form-control font-monospace ${b.iban.length > 2 ? (valid ? 'is-valid' : 'is-invalid') : ''}`} id={`ib-${b.id}`} maxLength={42}
                      value={formatIban(b.iban)} onChange={(e) => update(b.id, { iban: e.target.value.toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, 34) })} />
                    <div className="invalid-feedback">IBAN kontrol hanesi geçersiz.</div>
                  </div>
                  <div className="col-md-5"><label className="form-label small" htmlFor={`ah-${b.id}`}>Hesap sahibi</label><input className="form-control" id={`ah-${b.id}`} maxLength={150} value={b.accountHolder} onChange={(e) => update(b.id, { accountHolder: e.target.value })} /></div>
                  <div className="col-12">
                    <div className="form-check form-switch mb-0">
                      <input className="form-check-input" type="checkbox" role="switch" id={`an-${b.id}`} checked={b.addToNotes} onChange={(e) => update(b.id, { addToNotes: e.target.checked })} />
                      <label className="form-check-label" htmlFor={`an-${b.id}`}>Faturada nota otomatik ekle</label>
                    </div>
                  </div>
                </div>
              </Card>
            );
          })}
        </fieldset>
      )}
    </SettingsLayout>
  );
}

const KINDS = { EFatura: 'e-Fatura', EArsiv: 'e-Arşiv', Draft: 'Taslak' };

function NumberingPage() {
  const session = useSession();
  const { data, loading, error, reload } = useLoad(() => api.get('/api/v1/tenants/current/sequences'), [session.tenant?.id]);
  const t = session.tenant;
  const series = [
    ['e-Fatura', t.eFaturaPrefix],
    ['e-Arşiv', t.eArsivPrefix],
    ['Taslak', 'TSL'],
  ];
  const year = new Date().getFullYear();
  return (
    <SettingsLayout title="Numaralandırma" subtitle="Belge serileri, son atanan numaralar ve sıradaki sayaçlar"
      actions={session.can('CompanyManage') && <Link className="btn btn-outline-primary" to="/settings/company">Serileri düzenle</Link>}>
      <Card title="Seri numarası yapılandırması" className="mb-4">
        <dl className="row g-3 mb-0">
          {series.map(([label, prefix]) => (
            <div className="col-md-4" key={label}>
              <dt className="form-label">{label}</dt>
              <dd className="rounded border p-2 font-monospace mb-0">{prefix || '—'}</dd>
            </div>
          ))}
        </dl>
        <p className="form-text mt-3 mb-0">Sayaçlar belge türü ve yıl bazında izlenir. Geçerli yıl {year}.</p>
        </Card>
        <Card title="Sayaçlar" icon="list-ol" bodyClass="">
          {loading ? <div className="px-3"><Spinner /></div> : error ? (
            <div className="card-body"><ErrorAlert error={error} /><BusyButton className="btn btn-outline-primary" onClick={reload}>Yenile</BusyButton></div>
          ) : data?.length ? (
            <div className="table-responsive">
              <table className="table align-middle mb-0">
                <thead><tr><th>Tür</th><th>Seri</th><th>Yıl</th><th className="num">Son numara</th><th>Sonraki belge</th><th>Güncelleme</th></tr></thead>
                <tbody>
                  {data.map((s) => (
                    <tr key={`${s.documentKind}-${s.prefix}-${s.fiscalYear}`}>
                      <td>{KINDS[s.documentKind] || s.documentKind}</td>
                      <td className="font-monospace">{s.prefix}</td>
                      <td>{s.fiscalYear}</td>
                      <td className="num">{s.lastValue}</td>
                      <td className="font-monospace">{`${s.prefix}${s.fiscalYear}${String(s.lastValue + 1).padStart(9, '0')}`}</td>
                      <td className="small text-body-secondary">{fmtDate(s.updatedAt)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : <Empty icon="list-ol">Henüz numara verilmedi.</Empty>}
        </Card>
      </SettingsLayout>
    );
  }

  export const InvoiceDefaultsSettings = () => <TenantRequired><DefaultsPage /></TenantRequired>;
  export const NoteTemplateSettings = () => <TenantRequired><NotesPage /></TenantRequired>;
  export const BankAccountSettings = () => <TenantRequired><BanksPage /></TenantRequired>;
  export const NumberingSettings = () => <TenantRequired><NumberingPage /></TenantRequired>;
