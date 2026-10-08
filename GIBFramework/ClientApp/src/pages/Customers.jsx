import { useRef, useState } from 'react';
import { api } from '../api.js';
import { fmtDate } from '../format.js';
import { validateTaxId } from '../taxid.js';
import { useSession } from '../session.jsx';
import { LocationPicker, TaxIdInput } from '../components/inputs.jsx';
import { BusyButton, Card, Empty, ErrorAlert, Modal, PageHeader, Spinner, useDialogs, useLoad, useToast } from '../components/ui.jsx';

const REGIMES = [
  ['NotATaxpayer', 'Nihai tüketici'], ['Bilanco', 'Bilanço esası'], ['IsletmeHesabi', 'İşletme hesabı'], ['BasitUsul', 'Basit usul'], ['SerbestMeslek', 'Serbest meslek'],
];

const EMPTY = {
  taxId: '', title: '', firstName: '', familyName: '', regime: 'NotATaxpayer', taxOffice: '', provinceName: '', districtName: '', neighborhoodName: '',
  street: '', buildingNumber: '', postalCode: '', country: 'Türkiye', email: '', phone: '', isEFaturaRegistered: false, eFaturaAlias: '', notes: '', isActive: true,
};

function CustomerEditor({ customer, onClose, onSaved }) {
  const [form, setForm] = useState(() => ({ ...EMPTY, ...Object.fromEntries(Object.entries(customer || {}).map(([k, v]) => [k, v ?? ''])) }));
  const [validated, setValidated] = useState(false);
  const [error, setError] = useState(null);
  const ref = useRef(null);
  const set = (name) => (e) => setForm((f) => ({ ...f, [name]: e.target.type === 'checkbox' ? e.target.checked : e.target.value }));

  const save = async () => {
    setValidated(true);
    const taxProblem = validateTaxId(form.taxId);
    if (taxProblem || !ref.current.checkValidity()) {
      setError({ message: taxProblem || 'Zorunlu alanları doldurun.' });
      return;
    }
    const body = { ...form, email: form.email || null };
    try {
      const saved = customer?.id ? await api.put(`/api/v1/customers/${customer.id}`, body) : await api.post('/api/v1/customers', body);
      onSaved(saved);
    } catch (err) {
      setError(err);
    }
  };

  return (
    <Modal title={customer?.id ? 'Cari kartı düzenle' : 'Yeni cari kart'} size="lg" onClose={onClose} scrollable
      footer={(
        <div className="modal-footer">
          <button type="button" className="btn btn-outline-secondary" onClick={onClose}>Vazgeç</button>
          <BusyButton onClick={save}><i className="bi bi-save me-1" />Kaydet</BusyButton>
        </div>
      )}>
      <div className="modal-body">
        <ErrorAlert error={error} />
        <form ref={ref} noValidate className={validated ? 'was-validated' : ''} onSubmit={(e) => e.preventDefault()}>
          <div className="row g-3">
            <div className="col-md-5">
              <label className="form-label" htmlFor="taxId">VKN / TCKN</label>
              {customer?.id
                ? <input className="form-control font-monospace" id="taxId" value={form.taxId} disabled />
                : <TaxIdInput value={form.taxId} onChange={(v) => setForm((f) => ({ ...f, taxId: v }))} autoFocus />}
            </div>
            <div className="col-md-7">
              <label className="form-label" htmlFor="title">Unvan / ad soyad</label>
              <input className="form-control" id="title" required maxLength={250} value={form.title} onChange={set('title')} />
              <div className="invalid-feedback">Zorunlu.</div>
            </div>
            {form.taxId.length === 11 && (
              <>
                <div className="col-md-6"><label className="form-label" htmlFor="firstName">Ad</label><input className="form-control" id="firstName" maxLength={100} value={form.firstName} onChange={set('firstName')} /></div>
                <div className="col-md-6"><label className="form-label" htmlFor="familyName">Soyad</label><input className="form-control" id="familyName" maxLength={100} value={form.familyName} onChange={set('familyName')} /></div>
              </>
            )}
            <div className="col-md-5">
              <label className="form-label" htmlFor="regime">Alıcı türü</label>
              <select className="form-select" id="regime" value={form.regime} onChange={set('regime')}>{REGIMES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}</select>
            </div>
            <div className="col-md-7"><label className="form-label" htmlFor="taxOffice">Vergi dairesi</label><input className="form-control" id="taxOffice" maxLength={100} value={form.taxOffice} onChange={set('taxOffice')} /></div>
            <div className="col-12">
              <LocationPicker required={false} value={{ city: form.provinceName, district: form.districtName, neighborhood: form.neighborhoodName }}
                onChange={(v) => setForm((f) => ({ ...f, provinceName: v.city, districtName: v.district, neighborhoodName: v.neighborhood }))} />
            </div>
            <div className="col-md-7"><label className="form-label" htmlFor="street">Cadde / sokak</label><input className="form-control" id="street" maxLength={250} value={form.street} onChange={set('street')} /></div>
            <div className="col-md-2"><label className="form-label" htmlFor="bno">No</label><input className="form-control" id="bno" maxLength={20} value={form.buildingNumber} onChange={set('buildingNumber')} /></div>
            <div className="col-md-3"><label className="form-label" htmlFor="pk">Posta kodu</label><input className="form-control" id="pk" inputMode="numeric" maxLength={5} value={form.postalCode} onChange={(e) => setForm((f) => ({ ...f, postalCode: e.target.value.replace(/\D/g, '').slice(0, 5) }))} /></div>
            <div className="col-md-6"><label className="form-label" htmlFor="email">E-posta</label><input className="form-control" id="email" type="email" maxLength={200} value={form.email} onChange={set('email')} /></div>
            <div className="col-md-6"><label className="form-label" htmlFor="phone">Telefon</label><input className="form-control" id="phone" type="tel" maxLength={30} value={form.phone} onChange={set('phone')} /></div>
            <div className="col-md-5 d-flex align-items-end">
              <div className="form-check form-switch">
                <input className="form-check-input" type="checkbox" role="switch" id="ef" checked={form.isEFaturaRegistered} onChange={set('isEFaturaRegistered')} />
                <label className="form-check-label" htmlFor="ef">e-Fatura mükellefi</label>
              </div>
            </div>
            <div className="col-md-7">
              <label className="form-label" htmlFor="alias">e-Fatura posta kutusu etiketi</label>
              <input className="form-control" id="alias" maxLength={200} placeholder="urn:mail:defaultpk@..." disabled={!form.isEFaturaRegistered} value={form.eFaturaAlias} onChange={set('eFaturaAlias')} />
            </div>
            <div className="col-12"><label className="form-label" htmlFor="notes">Not</label><textarea className="form-control" id="notes" rows={2} maxLength={1000} value={form.notes} onChange={set('notes')} /></div>
            <div className="col-12">
              <div className="form-check form-switch">
                <input className="form-check-input" type="checkbox" role="switch" id="active" checked={form.isActive} onChange={set('isActive')} />
                <label className="form-check-label" htmlFor="active">Aktif</label>
              </div>
            </div>
          </div>
        </form>
      </div>
    </Modal>
  );
}

export function Customers() {
  const session = useSession();
  const dialogs = useDialogs();
  const toast = useToast();
  const [q, setQ] = useState('');
  const [editing, setEditing] = useState(null);
  const { data: rows, loading, reload } = useLoad(() => api.get('/api/v1/customers?take=5000'), []);
  const canEdit = session.can('CatalogManage');

  const filtered = (rows || []).filter((c) => !q.trim() || `${c.title} ${c.taxId} ${c.email || ''}`.toLocaleLowerCase('tr-TR').includes(q.toLocaleLowerCase('tr-TR')));

  return (
    <>
      <PageHeader icon="person-vcard" title="Cari kartlar" subtitle={rows ? `${rows.length} kayıt · ${rows.filter((c) => c.isEFaturaRegistered).length} e-Fatura mükellefi` : null}
        actions={canEdit && <button className="btn btn-primary" type="button" onClick={() => setEditing({})}><i className="bi bi-plus-lg me-1" />Yeni cari</button>} />
      <Card className="mb-4">
        <input className="form-control" placeholder="Unvan, VKN/TCKN veya e-posta ile ara" aria-label="Ara" value={q} onChange={(e) => setQ(e.target.value)} />
      </Card>
      <Card bodyClass="">
        {loading ? <div className="px-3"><Spinner /></div> : filtered.length ? (
          <div className="table-responsive">
            <table className="table table-hover align-middle mb-0">
              <thead><tr><th>Unvan</th><th>VKN/TCKN</th><th>Adres</th><th>İletişim</th><th>Tür</th><th>Güncelleme</th><th /></tr></thead>
              <tbody>
                {filtered.map((c) => (
                  <tr key={c.id} className={c.isActive ? '' : 'opacity-50'}>
                    <td className="fw-semibold">{c.title}</td>
                    <td className="font-monospace">{c.taxId}</td>
                    <td className="small">{[c.neighborhoodName, c.districtName, c.provinceName].filter(Boolean).join(', ') || '—'}</td>
                    <td className="small">{c.email || ''}<div>{c.phone || ''}</div></td>
                    <td>{c.isEFaturaRegistered ? <span className="badge text-bg-primary">e-Fatura</span> : <span className="badge text-bg-info">e-Arşiv</span>}</td>
                    <td className="small text-body-secondary">{fmtDate(c.updatedAt)}</td>
                    <td className="text-end">
                      {canEdit && (
                        <div className="btn-group btn-group-sm">
                          <button className="btn btn-outline-primary" type="button" title="Düzenle" onClick={() => setEditing(c)}><i className="bi bi-pencil" /></button>
                          <BusyButton className="btn btn-outline-danger" title="Sil" onClick={async () => {
                            if (!await dialogs.confirm('Cari kartı sil', `"${c.title}" silinsin mi? Kesilmiş faturalar etkilenmez.`, 'Sil', 'danger')) return;
                            await api.del(`/api/v1/customers/${c.id}`);
                            toast('Cari kart silindi.');
                            await reload();
                          }}><i className="bi bi-trash" /></BusyButton>
                        </div>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : <Empty icon="person-vcard">{q ? 'Eşleşen cari yok.' : 'Henüz cari kart yok. Fatura keserken kaydedilen cariler burada görünür.'}</Empty>}
      </Card>
      {editing && (
        <CustomerEditor customer={editing.id ? editing : null} onClose={() => setEditing(null)}
          onSaved={async () => { setEditing(null); toast('Cari kart kaydedildi.'); await reload(); }} />
      )}
    </>
  );
}
