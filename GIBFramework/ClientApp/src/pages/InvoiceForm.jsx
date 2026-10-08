import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { api, uuid } from '../api.js';
import { fmtDate, fmtMoney, istanbulNow, round2, todayIso, UNITS } from '../format.js';
import { validateTaxId, ANONYMOUS_TCKN } from '../taxid.js';
import { useSession } from '../session.jsx';
import { LocationPicker, TaxIdInput } from '../components/inputs.jsx';
import { BusyButton, Card, ErrorAlert, PageHeader, Spinner, useLoad, useToast } from '../components/ui.jsx';

const EMPTY_CUSTOMER = {
  taxId: '', title: '', firstName: '', familyName: '', regime: 'NotATaxpayer', taxOffice: '', neighborhood: '', street: '', buildingNumber: '',
  district: '', city: '', postalCode: '', email: '', phone: '',
};

const REGIMES = [
  ['NotATaxpayer', 'Nihai tüketici'], ['Bilanco', 'Bilanço esası'], ['IsletmeHesabi', 'İşletme hesabı'], ['BasitUsul', 'Basit usul'], ['SerbestMeslek', 'Serbest meslek'],
];

const newLine = (defaults) => ({
  key: uuid(), productId: '', name: '', description: '', quantity: '1', unitCode: defaults?.unitCode || 'C62', unitPrice: '',
  discountMode: 'amount', discount: '', vatRate: String(defaults?.vatRate ?? 20), vatExemptionCode: '', withholdingCode: '', advanced: false,
});

const num = (v) => {
  const n = Number(String(v ?? '').replace(',', '.'));
  return Number.isFinite(n) ? n : 0;
};

function lineAmounts(l, ratios) {
  const gross = round2(num(l.quantity) * num(l.unitPrice));
  const discount = l.discountMode === 'percent' ? round2(gross * Math.min(100, num(l.discount)) / 100) : round2(num(l.discount));
  const net = round2(gross - discount);
  const vat = round2(net * num(l.vatRate) / 100);
  let withholding = 0;
  if (l.withholdingCode && ratios[l.withholdingCode]) {
    const [a, b] = ratios[l.withholdingCode].split('/').map(Number);
    withholding = round2(vat * a / b);
  }
  return { gross, discount, net, vat, withholding };
}

function Clock() {
  const [now, setNow] = useState(istanbulNow());
  useEffect(() => {
    const t = setInterval(() => setNow(istanbulNow()), 1000);
    return () => clearInterval(t);
  }, []);
  return <span className="font-monospace">{fmtDate(now.date)} {now.time}</span>;
}

export function InvoiceForm() {
  const session = useSession();
  const navigate = useNavigate();
  const toast = useToast();
  const formRef = useRef(null);
  const idempotencyKey = useMemo(() => uuid(), []);

  const { data: refs } = useLoad(async () => {
    const [defs, settings, products, customers] = await Promise.all([
      api.get('/api/v1/compliance/tax-definitions').catch(() => ({ vatRates: [20, 10, 1, 0], withholdings: [], exemptions: [] })),
      api.get('/api/v1/settings').catch(() => ({ defaults: {}, noteTemplates: [], bankAccounts: [] })),
      api.get('/api/v1/products?activeOnly=true').catch(() => []),
      api.get('/api/v1/customers?take=2000').catch(() => []),
    ]);
    return { defs, settings, products, customers };
  }, []);

  const [customer, setCustomer] = useState(EMPTY_CUSTOMER);
  const [card, setCard] = useState({ known: null, isEFatura: false, save: true });
  const [doc, setDoc] = useState({ deliveryDate: todayIso(), profile: 'TEMELFATURA', currency: 'TRY', exchangeRate: '', orderNumber: '', despatchNumber: '', despatchRequired: false });
  const [lines, setLines] = useState(null);
  const [notes, setNotes] = useState({ selected: [], banks: [], free: '' });
  const [lookupInfo, setLookupInfo] = useState(null);
  const [customerSearch, setCustomerSearch] = useState('');
  const [taxOffices, setTaxOffices] = useState([]);
  const [validated, setValidated] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (!refs) return;
    const d = refs.settings.defaults || {};
    setDoc((x) => ({ ...x, profile: d.profile || 'TEMELFATURA', currency: d.currency || 'TRY' }));
    setLines([newLine(d)]);
    setCard((c) => ({ ...c, save: d.saveCustomer !== false }));
    setNotes({
      selected: refs.settings.noteTemplates.filter((n) => n.autoAdd).map((n) => n.id),
      banks: refs.settings.bankAccounts.filter((b) => b.addToNotes).map((b) => b.id),
      free: '',
    });
  }, [refs]);

  useEffect(() => {
    if (!customer.city) {
      setTaxOffices([]);
      return undefined;
    }
    let cancelled = false;
    api.get(`/api/v1/tax-offices?q=${encodeURIComponent(customer.city)}&take=100`)
      .then((rows) => !cancelled && setTaxOffices([...new Set(rows.map((r) => r.data.name.replace(/ Vergi Dairesi Müdürlüğü$/, '')))]))
      .catch(() => !cancelled && setTaxOffices([]));
    return () => { cancelled = true; };
  }, [customer.city]);

  const ratios = useMemo(() => Object.fromEntries((refs?.defs.withholdings || []).map((w) => [w.code, w.ratio])), [refs]);
  const totals = useMemo(() => {
    const t = { net: 0, discount: 0, vat: 0, withholding: 0, byRate: {}, perLine: {} };
    (lines || []).forEach((l) => {
      const a = lineAmounts(l, ratios);
      t.net += a.net; t.discount += a.discount; t.vat += a.vat; t.withholding += a.withholding;
      t.byRate[l.vatRate] = (t.byRate[l.vatRate] || 0) + a.vat;
      t.perLine[l.key] = a;
    });
    return t;
  }, [lines, ratios]);

  if (!refs || !lines) return <Spinner />;

  const { defs, settings, products, customers } = refs;
  const docType = card.isEFatura ? 'EFatura' : 'EArsiv';
  const cur = doc.currency;
  const dueDays = settings.defaults?.paymentDueDays;

  const applyCustomer = (c) => {
    setCustomer({
      taxId: c.taxId, title: c.title || '', firstName: c.firstName || '', familyName: c.familyName || '', regime: c.regime || 'NotATaxpayer',
      taxOffice: c.taxOffice || '', neighborhood: c.neighborhoodName || '', street: c.street || '', buildingNumber: c.buildingNumber || '',
      district: c.districtName || '', city: c.provinceName || '', postalCode: c.postalCode || '', email: c.email || '', phone: c.phone || '',
    });
    setCard((x) => ({ ...x, known: true, isEFatura: !!c.isEFaturaRegistered }));
    setLookupInfo({ inSystem: true, isEFatura: !!c.isEFaturaRegistered });
  };

  const setC = (name) => (e) => setCustomer((c) => ({ ...c, [name]: e.target.value }));
  const setD = (name) => (e) => setDoc((d) => ({ ...d, [name]: e.target.type === 'checkbox' ? e.target.checked : e.target.value }));
  const setL = (key, patch) => setLines((ls) => ls.map((l) => {
    if (l.key !== key) return l;
    const next = { ...l, ...patch };
    if ('vatRate' in patch && patch.vatRate !== '0') next.vatExemptionCode = '';
    if ('vatRate' in patch && patch.vatRate === '0') next.advanced = true;
    return next;
  }));

  const pickProduct = (key, productId) => {
    const p = products.find((x) => x.id === productId);
    if (!p) {
      setL(key, { productId: '' });
      return;
    }
    setL(key, {
      productId, name: p.name, description: p.description || '', unitCode: p.unitCode, unitPrice: String(p.unitPrice), vatRate: String(Number(p.vatRate)),
      vatExemptionCode: p.vatExemptionCode || '', withholdingCode: p.withholdingCode || '', advanced: !!(p.vatExemptionCode || p.withholdingCode),
    });
  };

  const lookup = async () => {
    const taxId = customer.taxId;
    if (taxId === ANONYMOUS_TCKN) {
      setLookupInfo({ error: 'Nihai tüketici (11111111111) için sorgu yapılmaz; belge e-Arşiv olarak kesilir.' });
      return;
    }
    const problem = validateTaxId(taxId);
    if (problem) {
      setLookupInfo({ error: problem });
      return;
    }
    const info = await api.get(`/api/v1/taxpayers/${taxId}`);
    if (info.customer) {
      applyCustomer(info.customer);
    } else {
      setCard((x) => ({ ...x, known: false, isEFatura: false }));
    }
    const result = { inSystem: info.inSystem, isEFatura: info.isEFaturaRegistered };
    if (session.can('GibPortal')) {
      const status = await api.get('/api/v1/gib-portal/status').catch(() => null);
      if (status?.connected) {
        const r = await api.get(`/api/v1/gib-portal/recipients/${taxId}`).catch(() => null);
        if (r?.found) {
          setCustomer((c) => ({
            ...c,
            title: r.title || [r.firstName, r.lastName].filter(Boolean).join(' ') || c.title,
            firstName: r.firstName || c.firstName,
            familyName: r.lastName || c.familyName,
            taxOffice: r.taxOffice || c.taxOffice,
          }));
          result.gib = 'found';
        } else {
          result.gib = 'notFound';
        }
      }
    }
    setLookupInfo(result);
  };

  const buildNotes = () => {
    const list = [];
    settings.noteTemplates.filter((n) => notes.selected.includes(n.id)).forEach((n) => list.push(n.text));
    settings.bankAccounts.filter((b) => notes.banks.includes(b.id)).forEach((b) => list.push(`${b.bankName}${b.branch ? ` ${b.branch}` : ''} - ${b.accountHolder} - IBAN: ${b.iban} (${b.currency})`));
    if (dueDays) {
      const d = new Date(`${todayIso()}T12:00:00+03:00`);
      d.setDate(d.getDate() + Number(dueDays));
      list.push(`Son ödeme tarihi: ${fmtDate(d.toISOString().slice(0, 10))}`);
    }
    notes.free.split('\n').map((n) => n.trim()).filter(Boolean).forEach((n) => list.push(n));
    return list;
  };

  const submit = async () => {
    setValidated(true);
    const problems = [];
    const taxIdValue = customer.taxId || ANONYMOUS_TCKN;
    const taxProblem = validateTaxId(taxIdValue);
    if (taxProblem) problems.push(taxProblem);
    if (!formRef.current.checkValidity()) problems.push('Zorunlu alanları doldurun.');
    if (lines.some((l) => num(l.quantity) <= 0)) problems.push('Miktar sıfırdan büyük olmalıdır.');
    if (lines.some((l) => totals.perLine[l.key].net < 0)) problems.push('İskonto satır tutarını aşamaz.');
    if (problems.length) {
      setError({ message: 'Eksik veya hatalı alanlar var.', details: problems });
      window.scrollTo({ top: 0, behavior: 'smooth' });
      return;
    }

    const trim = (v) => (v || '').trim() || null;
    const taxId = taxIdValue;
    const anonymousBuyer = taxId === ANONYMOUS_TCKN;
    const body = {
      deliveryDate: doc.deliveryDate || null,
      profile: docType === 'EFatura' ? doc.profile : 'EARSIVFATURA',
      currency: doc.currency,
      exchangeRate: doc.currency === 'TRY' ? null : num(doc.exchangeRate),
      customer: {
        taxId,
        kind: taxId.length === 11 ? 'NaturalPerson' : 'LegalEntity',
        regime: customer.regime,
        title: customer.title.trim(),
        firstName: trim(customer.firstName),
        familyName: trim(customer.familyName),
        taxOffice: trim(customer.taxOffice),
        neighborhood: trim(customer.neighborhood),
        street: trim(customer.street),
        buildingNumber: trim(customer.buildingNumber),
        district: trim(customer.district),
        city: customer.city.trim(),
        postalCode: trim(customer.postalCode),
        email: trim(customer.email),
        phone: trim(customer.phone),
      },
      lines: lines.map((l) => ({
        name: l.name.trim(),
        description: trim(l.description),
        quantity: num(l.quantity),
        unitCode: l.unitCode,
        unitPrice: num(l.unitPrice),
        discountAmount: totals.perLine[l.key].discount,
        vatRate: num(l.vatRate),
        vatExemptionCode: l.vatExemptionCode || null,
        withholdingCode: l.withholdingCode || null,
      })),
      notes: buildNotes(),
      orderNumber: trim(doc.orderNumber),
      despatchNumber: trim(doc.despatchNumber),
      despatchRequired: doc.despatchRequired,
      saveCustomer: card.save && !anonymousBuyer,
      customerIsEFaturaRegistered: card.save && !anonymousBuyer ? card.isEFatura : null,
    };

    try {
      const created = await api.post('/api/v1/invoices', body, { headers: { 'Idempotency-Key': idempotencyKey } });
      toast(`${created.draftNumber || 'Taslak'} oluşturuldu (${created.documentType === 'EFatura' ? 'e-Fatura' : 'e-Arşiv'}).`);
      navigate(`/invoices/${created.id}`);
    } catch (err) {
      setError(err);
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  };

  const filteredCustomers = customerSearch.trim()
    ? customers.filter((c) => `${c.title} ${c.taxId}`.toLocaleLowerCase('tr-TR').includes(customerSearch.toLocaleLowerCase('tr-TR'))).slice(0, 8)
    : [];

  return (
    <>
      <PageHeader breadcrumb={[['Faturalar', '/invoices'], ['Yeni fatura']]} icon="plus-circle" title="Yeni fatura"
        actions={(
          <span className={`badge fs-6 px-3 py-2 text-bg-${docType === 'EFatura' ? 'primary' : 'info'}`}>
            <i className="bi bi-file-earmark-text me-1" />{docType === 'EFatura' ? 'e-Fatura' : 'e-Arşiv Fatura'}
          </span>
        )} />
      <ErrorAlert error={error} />
      <form ref={formRef} noValidate className={validated ? 'was-validated' : ''} onSubmit={(e) => e.preventDefault()}>
        <div className="row g-4">
          <div className="col-xxl-8 d-flex flex-column gap-4">
            <Card title="Alıcı" icon="person-vcard"
              actions={customers.length > 0 && (
                <div className="position-relative" style={{ width: '18rem' }}>
                  <input className="form-control form-control-sm" placeholder="Kayıtlı cari ara…" value={customerSearch} onChange={(e) => setCustomerSearch(e.target.value)} />
                  {filteredCustomers.length > 0 && (
                    <div className="list-group position-absolute w-100 shadow mt-1" style={{ zIndex: 20 }}>
                      {filteredCustomers.map((c) => (
                        <button key={c.id} type="button" className="list-group-item list-group-item-action small" onClick={() => { applyCustomer(c); setCustomerSearch(''); }}>
                          <div className="fw-semibold text-truncate">{c.title}</div>
                          <div className="text-body-secondary font-monospace">{c.taxId}{c.isEFaturaRegistered && <span className="badge text-bg-primary ms-2">e-Fatura</span>}</div>
                        </button>
                      ))}
                    </div>
                  )}
                </div>
              )}>
              <div className="row g-3">
                <div className="col-md-5">
                  <label className="form-label" htmlFor="taxId">VKN / TCKN</label>
                  <TaxIdInput value={customer.taxId} autoFocus anonymous required={false} onChange={(v) => { setCustomer((c) => ({ ...c, taxId: v })); setLookupInfo(null); setCard((x) => ({ ...x, known: null })); }}
                    append={<BusyButton className="btn btn-outline-primary" onClick={lookup}><i className="bi bi-search me-1" />Sorgula</BusyButton>} />
                  {lookupInfo && (
                    <div className="form-text">
                      {lookupInfo.error ? <span className="text-danger">{lookupInfo.error}</span> : (
                        <>
                          {lookupInfo.inSystem ? 'Kayıtlı cari.' : 'Yeni cari.'}{' '}
                          {lookupInfo.isEFatura ? 'e-Fatura mükellefi: belge e-Fatura olarak kesilir.' : 'Belge e-Arşiv olarak kesilir.'}
                          {lookupInfo.gib === 'found' && ' GİB kaydı dolduruldu.'}
                          {lookupInfo.gib === 'notFound' && ' GİB sicil/MERNİS kaydı bulunamadı.'}
                        </>
                      )}
                    </div>
                  )}
                </div>
                <div className="col-md-7">
                  <label className="form-label" htmlFor="title">Unvan / ad soyad</label>
                  <input className="form-control" id="title" required maxLength={250} value={customer.title} onChange={setC('title')} />
                  <div className="invalid-feedback">Zorunlu.</div>
                </div>
                {(customer.taxId.length === 11 || !customer.taxId) && (
                  <>
                    <div className="col-md-6"><label className="form-label" htmlFor="firstName">Ad</label><input className="form-control" id="firstName" maxLength={100} value={customer.firstName} onChange={setC('firstName')} /></div>
                    <div className="col-md-6"><label className="form-label" htmlFor="familyName">Soyad</label><input className="form-control" id="familyName" maxLength={100} value={customer.familyName} onChange={setC('familyName')} /></div>
                  </>
                )}
                <div className="col-md-4">
                  <label className="form-label" htmlFor="regime">Alıcı türü</label>
                  <select className="form-select" id="regime" value={customer.regime} onChange={setC('regime')}>
                    {REGIMES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
                  </select>
                </div>
                <div className="col-md-8">
                  <label className="form-label" htmlFor="taxOffice">Vergi dairesi</label>
                  <input className="form-control" id="taxOffice" list="tax-office-list" maxLength={100} value={customer.taxOffice} onChange={setC('taxOffice')} />
                  <datalist id="tax-office-list">{taxOffices.map((t) => <option key={t} value={t} />)}</datalist>
                </div>
                <div className="col-12">
                  <LocationPicker value={{ city: customer.city, district: customer.district, neighborhood: customer.neighborhood }}
                    onChange={(v) => setCustomer((c) => ({ ...c, city: v.city, district: v.district, neighborhood: v.neighborhood }))} />
                </div>
                <div className="col-md-7"><label className="form-label" htmlFor="street">Cadde / sokak</label><input className="form-control" id="street" maxLength={250} value={customer.street} onChange={setC('street')} /></div>
                <div className="col-md-2"><label className="form-label" htmlFor="buildingNumber">No</label><input className="form-control" id="buildingNumber" maxLength={20} value={customer.buildingNumber} onChange={setC('buildingNumber')} /></div>
                <div className="col-md-3"><label className="form-label" htmlFor="postalCode">Posta kodu</label><input className="form-control" id="postalCode" inputMode="numeric" maxLength={5} value={customer.postalCode} onChange={(e) => setCustomer((c) => ({ ...c, postalCode: e.target.value.replace(/\D/g, '').slice(0, 5) }))} /></div>
                <div className="col-md-6"><label className="form-label" htmlFor="email">E-posta</label><input className="form-control" id="email" type="email" maxLength={200} value={customer.email} onChange={setC('email')} /></div>
                <div className="col-md-6"><label className="form-label" htmlFor="phone">Telefon</label><input className="form-control" id="phone" type="tel" maxLength={30} value={customer.phone} onChange={setC('phone')} /></div>
                <div className="col-12 d-flex flex-wrap gap-4">
                  <div className="form-check form-switch">
                    <input className="form-check-input" type="checkbox" role="switch" id="saveCustomer" checked={card.save} onChange={(e) => setCard((x) => ({ ...x, save: e.target.checked }))} />
                    <label className="form-check-label" htmlFor="saveCustomer">Cari kartı kaydet / güncelle</label>
                  </div>
                  <div className="form-check form-switch">
                    <input className="form-check-input" type="checkbox" role="switch" id="isEFatura" checked={card.isEFatura} disabled={!card.save}
                      onChange={(e) => setCard((x) => ({ ...x, isEFatura: e.target.checked }))} />
                    <label className="form-check-label" htmlFor="isEFatura">e-Fatura mükellefi</label>
                  </div>
                </div>
              </div>
            </Card>

            <Card title="Mal / hizmet satırları" icon="list-ul"
              actions={<button className="btn btn-sm btn-primary" type="button" onClick={() => setLines((ls) => [...ls, newLine(settings.defaults)])}><i className="bi bi-plus-lg me-1" />Satır ekle</button>}>
              {lines.map((l, idx) => {
                const a = totals.perLine[l.key];
                return (
                  <div className="line-card" key={l.key}>
                    <span className="line-no">{idx + 1}</span>
                    <div className="row g-2 align-items-end">
                      {products.length > 0 && (
                        <div className="col-md-4">
                          <label className="form-label" htmlFor={`p-${l.key}`}>Kayıtlı ürün</label>
                          <select className="form-select form-select-sm" id={`p-${l.key}`} value={l.productId} onChange={(e) => pickProduct(l.key, e.target.value)}>
                            <option value="">— Seçin —</option>
                            {products.map((p) => <option key={p.id} value={p.id}>{p.code ? `${p.code} · ` : ''}{p.name}</option>)}
                          </select>
                        </div>
                      )}
                      <div className={products.length > 0 ? 'col-md-8' : 'col-12'}>
                        <label className="form-label" htmlFor={`n-${l.key}`}>Mal / hizmet adı</label>
                        <input className="form-control form-control-sm" id={`n-${l.key}`} required maxLength={250} value={l.name} onChange={(e) => setL(l.key, { name: e.target.value })} />
                      </div>
                      <div className="col-6 col-md-2">
                        <label className="form-label" htmlFor={`q-${l.key}`}>Miktar</label>
                        <input className="form-control form-control-sm text-end" id={`q-${l.key}`} type="number" step="0.0001" min="0.0001" required value={l.quantity} onChange={(e) => setL(l.key, { quantity: e.target.value })} />
                      </div>
                      <div className="col-6 col-md-2">
                        <label className="form-label" htmlFor={`u-${l.key}`}>Birim</label>
                        <select className="form-select form-select-sm" id={`u-${l.key}`} value={l.unitCode} onChange={(e) => setL(l.key, { unitCode: e.target.value })}>
                          {UNITS.map(([c, label]) => <option key={c} value={c}>{label}</option>)}
                        </select>
                      </div>
                      <div className="col-6 col-md-2">
                        <label className="form-label" htmlFor={`pr-${l.key}`}>Birim fiyat ({cur})</label>
                        <input className="form-control form-control-sm text-end" id={`pr-${l.key}`} type="number" step="0.01" min="0" required placeholder="0,00" value={l.unitPrice} onChange={(e) => setL(l.key, { unitPrice: e.target.value })} />
                      </div>
                      <div className="col-6 col-md-2">
                        <label className="form-label" htmlFor={`d-${l.key}`}>İskonto</label>
                        <div className="input-group input-group-sm">
                          <input className="form-control text-end" id={`d-${l.key}`} type="number" step="0.01" min="0" placeholder="0" value={l.discount} onChange={(e) => setL(l.key, { discount: e.target.value })} />
                          <button className="btn btn-outline-secondary" type="button" title="Tutar / yüzde" onClick={() => setL(l.key, { discountMode: l.discountMode === 'amount' ? 'percent' : 'amount' })}>
                            {l.discountMode === 'amount' ? '₺' : '%'}
                          </button>
                        </div>
                      </div>
                      <div className="col-6 col-md-2">
                        <label className="form-label" htmlFor={`v-${l.key}`}>KDV</label>
                        <select className="form-select form-select-sm" id={`v-${l.key}`} value={l.vatRate} onChange={(e) => setL(l.key, { vatRate: e.target.value })}>
                          {defs.vatRates.map((r) => <option key={r} value={String(r)}>%{r}</option>)}
                        </select>
                      </div>
                      <div className="col-6 col-md-2 text-end">
                        <div className="form-label">Tutar</div>
                        <div className="line-total">{fmtMoney(a.net, cur)}</div>
                      </div>
                      {l.advanced && (
                        <>
                          <div className="col-md-4">
                            <label className="form-label" htmlFor={`ex-${l.key}`}>KDV istisna kodu</label>
                            <select className="form-select form-select-sm" id={`ex-${l.key}`} disabled={l.vatRate !== '0'} required={l.vatRate === '0'} value={l.vatExemptionCode} onChange={(e) => setL(l.key, { vatExemptionCode: e.target.value })}>
                              <option value="">—</option>
                              {defs.exemptions.map((x) => <option key={x.code} value={x.code}>{x.code} · {x.name}</option>)}
                            </select>
                          </div>
                          <div className="col-md-4">
                            <label className="form-label" htmlFor={`wh-${l.key}`}>KDV tevkifatı</label>
                            <select className="form-select form-select-sm" id={`wh-${l.key}`} value={l.withholdingCode} onChange={(e) => setL(l.key, { withholdingCode: e.target.value })}>
                              <option value="">—</option>
                              {defs.withholdings.map((w) => <option key={w.code} value={w.code}>{w.code} ({w.ratio}) · {w.name}</option>)}
                            </select>
                          </div>
                          <div className="col-md-4">
                            <label className="form-label" htmlFor={`ds-${l.key}`}>Açıklama</label>
                            <input className="form-control form-control-sm" id={`ds-${l.key}`} maxLength={500} value={l.description} onChange={(e) => setL(l.key, { description: e.target.value })} />
                          </div>
                        </>
                      )}
                    </div>
                    <div className="d-flex justify-content-between align-items-center mt-2 small">
                      <button type="button" className="btn btn-link btn-sm p-0 text-decoration-none" onClick={() => setL(l.key, { advanced: !l.advanced })}>
                        <i className={`bi bi-chevron-${l.advanced ? 'up' : 'down'} me-1`} />İstisna, tevkifat, açıklama
                      </button>
                      <span className="text-body-secondary">
                        KDV {fmtMoney(a.vat, cur)}{a.discount > 0 && ` · İskonto ${fmtMoney(a.discount, cur)}`}{a.withholding > 0 && ` · Tevkifat ${fmtMoney(a.withholding, cur)}`}
                        <button type="button" className="btn btn-sm btn-outline-danger ms-3" aria-label="Satırı sil" disabled={lines.length === 1}
                          onClick={() => setLines((ls) => ls.filter((x) => x.key !== l.key))}><i className="bi bi-trash" /></button>
                      </span>
                    </div>
                  </div>
                );
              })}
            </Card>

            <Card title="Notlar" icon="sticky" actions={session.can('SettingsManage') && <Link className="btn btn-sm btn-outline-secondary" to="/settings/notes"><i className="bi bi-sliders me-1" />Şablonları düzenle</Link>}>
              {settings.noteTemplates.length > 0 && (
                <div className="d-flex flex-wrap gap-2 mb-3">
                  {settings.noteTemplates.map((n) => {
                    const on = notes.selected.includes(n.id);
                    return (
                      <button key={n.id} type="button" className={`btn btn-sm ${on ? 'btn-primary' : 'btn-outline-primary'} note-chip`} title={n.text}
                        onClick={() => setNotes((x) => ({ ...x, selected: on ? x.selected.filter((id) => id !== n.id) : [...x.selected, n.id] }))}>
                        <i className={`bi bi-${on ? 'check2-square' : 'square'} me-1`} />{n.title}
                      </button>
                    );
                  })}
                </div>
              )}
              {settings.bankAccounts.length > 0 && (
                <div className="mb-3">
                  {settings.bankAccounts.map((b) => (
                    <div className="form-check" key={b.id}>
                      <input className="form-check-input" type="checkbox" id={`bank-${b.id}`} checked={notes.banks.includes(b.id)}
                        onChange={(e) => setNotes((x) => ({ ...x, banks: e.target.checked ? [...x.banks, b.id] : x.banks.filter((id) => id !== b.id) }))} />
                      <label className="form-check-label small" htmlFor={`bank-${b.id}`}>{b.bankName} · <span className="font-monospace">{b.iban}</span> ({b.currency})</label>
                    </div>
                  ))}
                </div>
              )}
              <textarea className="form-control" rows={3} maxLength={1000} placeholder="Ek not (her satır ayrı not)" aria-label="Ek not" value={notes.free} onChange={(e) => setNotes((x) => ({ ...x, free: e.target.value }))} />
              {buildNotes().length > 0 && (
                <ul className="small text-body-secondary mt-2 mb-0">{buildNotes().map((n, i) => <li key={`${i}-${n}`}>{n}</li>)}</ul>
              )}
            </Card>
          </div>

          <div className="col-xxl-4 d-flex flex-column gap-4">
            <Card title="Belge" icon="calendar3">
              <dl className="row small mb-3">
                <dt className="col-5">Düzenleme</dt><dd className="col-7"><Clock /></dd>
                <dt className="col-5">Belge no</dt><dd className="col-7">Sistem atar</dd>
              </dl>
              <div className="mb-3"><label className="form-label" htmlFor="deliveryDate">Teslim / hizmet tarihi</label><input className="form-control" type="date" id="deliveryDate" max={todayIso()} value={doc.deliveryDate} onChange={setD('deliveryDate')} /></div>
              {docType === 'EFatura' && (
                <div className="mb-3">
                  <label className="form-label" htmlFor="profile">Senaryo</label>
                  <select className="form-select" id="profile" value={doc.profile} onChange={setD('profile')}>
                    <option value="TEMELFATURA">Temel fatura</option>
                    <option value="TICARIFATURA">Ticari fatura</option>
                  </select>
                </div>
              )}
              <div className="row g-2 mb-3">
                <div className="col-6">
                  <label className="form-label" htmlFor="currency">Para birimi</label>
                  <select className="form-select" id="currency" value={doc.currency} onChange={setD('currency')}>
                    {['TRY', 'USD', 'EUR', 'GBP'].map((c) => <option key={c}>{c}</option>)}
                  </select>
                </div>
                <div className="col-6">
                  <label className="form-label" htmlFor="exchangeRate">TL kuru</label>
                  <input className="form-control" id="exchangeRate" type="number" step="0.0001" min="0.0001" disabled={doc.currency === 'TRY'} required={doc.currency !== 'TRY'} value={doc.exchangeRate} onChange={setD('exchangeRate')} />
                </div>
              </div>
              <div className="row g-2 mb-3">
                <div className="col-6"><label className="form-label" htmlFor="orderNumber">Sipariş no</label><input className="form-control" id="orderNumber" maxLength={50} value={doc.orderNumber} onChange={setD('orderNumber')} /></div>
                <div className="col-6"><label className="form-label" htmlFor="despatchNumber">İrsaliye no</label><input className="form-control" id="despatchNumber" maxLength={50} value={doc.despatchNumber} onChange={setD('despatchNumber')} /></div>
              </div>
              <div className="form-check">
                <input className="form-check-input" type="checkbox" id="despatchRequired" checked={doc.despatchRequired} onChange={setD('despatchRequired')} />
                <label className="form-check-label" htmlFor="despatchRequired">İrsaliyeli mal teslimi</label>
              </div>
            </Card>

            <Card title="Toplamlar" icon="calculator" className="border-primary position-sticky" bodyClass="card-body"
              footer={<BusyButton className="btn btn-primary btn-lg w-100" onClick={submit}><i className="bi bi-save me-1" />Taslak oluştur</BusyButton>}>
              <table className="table table-sm mb-0">
                <tbody>
                  <tr><th>Mal/hizmet (net)</th><td className="num">{fmtMoney(totals.net, cur)}</td></tr>
                  {totals.discount > 0 && <tr><th>İskonto</th><td className="num">{fmtMoney(totals.discount, cur)}</td></tr>}
                  {Object.entries(totals.byRate).filter(([, v]) => v).map(([r, v]) => <tr key={r}><th className="fw-normal">KDV %{r}</th><td className="num">{fmtMoney(v, cur)}</td></tr>)}
                  <tr><th>Vergiler dahil</th><td className="num">{fmtMoney(totals.net + totals.vat, cur)}</td></tr>
                  {totals.withholding > 0 && <tr><th>Tevkifat</th><td className="num">− {fmtMoney(totals.withholding, cur)}</td></tr>}
                  <tr className="table-primary"><th>Ödenecek</th><td className="num fw-bold fs-5">{fmtMoney(totals.net + totals.vat - totals.withholding, cur)}</td></tr>
                </tbody>
              </table>
            </Card>
          </div>
        </div>
      </form>
    </>
  );
}
