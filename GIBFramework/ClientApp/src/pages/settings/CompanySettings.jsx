import { useEffect, useRef, useState } from 'react';
import { api, LOGO_EVENT } from '../../api.js';
import { useSession } from '../../session.jsx';
import { LocationPicker } from '../../components/inputs.jsx';
import { BusyButton, Card, ErrorAlert, useDialogs, useLoad, useToast } from '../../components/ui.jsx';
import { SettingsLayout, TenantRequired } from './SettingsLayout.jsx';

const REGIMES = [['Bilanco', 'Bilanço esası'], ['IsletmeHesabi', 'İşletme hesabı'], ['BasitUsul', 'Basit usul'], ['SerbestMeslek', 'Serbest meslek']];

function LogoCard({ canEdit }) {
  const toast = useToast();
  const dialogs = useDialogs();
  const fileRef = useRef(null);
  const [error, setError] = useState(null);
  const { data: info, reload } = useLoad(() => api.get('/api/v1/tenants/current/logo-info'), []);
  const changed = async () => {
    await reload();
    window.dispatchEvent(new Event(LOGO_EVENT));
  };

  return (
    <Card title="Logo" icon="image" className="mb-4">
      <ErrorAlert error={error} />
      <div className="d-flex flex-wrap align-items-center gap-4">
        <div className="logo-preview">{info?.hasLogo ? <img src={info.url} alt="Firma logosu" /> : <i className="bi bi-image fs-1 text-body-tertiary" />}</div>
        <div>
          <div className="small text-body-secondary mb-2">PNG, JPEG veya WebP · en fazla 512 KB. Faturada, e-postalarda ve üst menüde görünür.</div>
          {canEdit && (
            <div className="d-flex gap-2">
              <input ref={fileRef} type="file" accept="image/png,image/jpeg,image/webp" className="d-none" aria-label="Logo dosyası" onChange={async (e) => {
                const file = e.target.files[0];
                e.target.value = '';
                if (!file) return;
                setError(null);
                const data = new FormData();
                data.append('file', file);
                try {
                  await api.putUpload('/api/v1/tenants/current/logo', data);
                  toast('Logo yüklendi.');
                  await changed();
                } catch (err) {
                  setError(err);
                }
              }} />
              <button type="button" className="btn btn-outline-primary btn-pill px-3" onClick={() => fileRef.current.click()}><i className="bi bi-upload me-1" />{info?.hasLogo ? 'Değiştir' : 'Yükle'}</button>
              {info?.hasLogo && (
                <BusyButton className="btn btn-outline-danger btn-pill px-3" onClick={async () => {
                  if (!await dialogs.confirm('Logoyu kaldır', 'Firma logosu silinsin mi?', 'Kaldır', 'danger')) return;
                  await api.del('/api/v1/tenants/current/logo');
                  toast('Logo kaldırıldı.', 'warning');
                  await changed();
                }}><i className="bi bi-trash me-1" />Kaldır</BusyButton>
              )}
            </div>
          )}
        </div>
      </div>
    </Card>
  );
}

function CompanyForm() {
  const session = useSession();
  const toast = useToast();
  const canEdit = session.can('CompanyManage');
  const t = session.tenant;
  const ref = useRef(null);
  const [form, setForm] = useState(null);
  const [validated, setValidated] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (!t) return;
    const p = t.profile || {};
    setForm({
      name: t.name || '', title: p.title || '', regime: p.regime || 'Bilanco', taxOffice: p.taxOffice || '', neighborhood: p.neighborhood || '',
      street: p.street || '', buildingNumber: p.buildingNumber || '', district: p.district || '', city: p.city || '', postalCode: p.postalCode || '',
      email: p.email || '', phone: p.phone || '', isEFaturaRegistered: !!p.isEFaturaRegistered, isEArchiveRegistered: !!p.isEArchiveRegistered,
      eFaturaPrefix: t.eFaturaPrefix || 'EFT', eArsivPrefix: t.eArsivPrefix || 'EAR',
    });
  }, [t]);

  if (!form) return null;
  const set = (name) => (e) => setForm((f) => ({ ...f, [name]: e.target.type === 'checkbox' ? e.target.checked : e.target.value }));
  const prefix = (name) => (e) => setForm((f) => ({ ...f, [name]: e.target.value.toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, 3) }));

  const save = async () => {
    setValidated(true);
    if (!ref.current.checkValidity()) {
      setError({ message: 'Zorunlu alanları doldurun.' });
      return;
    }
    try {
      await api.put('/api/v1/tenants/current', { ...form, email: form.email || null });
      await session.reload();
      setError(null);
      toast('Firma profili kaydedildi.');
    } catch (err) {
      setError(err);
    }
  };

  return (
    <SettingsLayout icon="building-gear" title="Firma profili" subtitle={t.name}
      actions={canEdit && <BusyButton className="btn btn-primary btn-pill px-4" onClick={save}><i className="bi bi-save me-1" />Kaydet</BusyButton>}>
      <LogoCard canEdit={canEdit} />
      <ErrorAlert error={error} />
      <form ref={ref} noValidate className={validated ? 'was-validated' : ''} onSubmit={(e) => e.preventDefault()}>
        <fieldset disabled={!canEdit} className="d-flex flex-column gap-4">
          <Card title="Kimlik" icon="person-badge">
            <div className="row g-3">
              <div className="col-md-4">
                <label className="form-label" htmlFor="c-tax">VKN / TCKN</label>
                <input className="form-control font-monospace" id="c-tax" value={t.profile?.taxId || ''} disabled readOnly />
              </div>
              <div className="col-md-8"><label className="form-label" htmlFor="c-name">Firma adı</label><input className="form-control" id="c-name" required maxLength={250} value={form.name} onChange={set('name')} /></div>
              <div className="col-md-8"><label className="form-label" htmlFor="c-title">Ticaret unvanı</label><input className="form-control" id="c-title" required maxLength={250} value={form.title} onChange={set('title')} /></div>
              <div className="col-md-4">
                <label className="form-label" htmlFor="c-regime">Defter tutma usulü</label>
                <select className="form-select" id="c-regime" value={form.regime} onChange={set('regime')}>{REGIMES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}</select>
              </div>
              <div className="col-md-6"><label className="form-label" htmlFor="c-vd">Vergi dairesi</label><input className="form-control" id="c-vd" required maxLength={100} value={form.taxOffice} onChange={set('taxOffice')} /></div>
              <div className="col-md-3"><label className="form-label" htmlFor="c-mail">E-posta</label><input className="form-control" id="c-mail" type="email" maxLength={200} value={form.email} onChange={set('email')} /></div>
              <div className="col-md-3"><label className="form-label" htmlFor="c-tel">Telefon</label><input className="form-control" id="c-tel" type="tel" maxLength={30} value={form.phone} onChange={set('phone')} /></div>
            </div>
          </Card>
          <Card title="Adres" icon="geo-alt">
            <LocationPicker value={{ city: form.city, district: form.district, neighborhood: form.neighborhood }}
              onChange={(v) => setForm((f) => ({ ...f, city: v.city, district: v.district, neighborhood: v.neighborhood }))} />
            <div className="row g-3 mt-0">
              <div className="col-md-7"><label className="form-label" htmlFor="c-street">Cadde / sokak</label><input className="form-control" id="c-street" maxLength={250} value={form.street} onChange={set('street')} /></div>
              <div className="col-md-2"><label className="form-label" htmlFor="c-no">No</label><input className="form-control" id="c-no" maxLength={20} value={form.buildingNumber} onChange={set('buildingNumber')} /></div>
              <div className="col-md-3"><label className="form-label" htmlFor="c-pk">Posta kodu</label><input className="form-control" id="c-pk" inputMode="numeric" maxLength={5} value={form.postalCode} onChange={(e) => setForm((f) => ({ ...f, postalCode: e.target.value.replace(/\D/g, '').slice(0, 5) }))} /></div>
            </div>
          </Card>
          <Card title="e-Belge" icon="file-earmark-check">
            <div className="row g-3 align-items-end">
              <div className="col-md-3">
                <div className="form-check form-switch">
                  <input className="form-check-input" type="checkbox" role="switch" id="c-ef" checked={form.isEFaturaRegistered} onChange={set('isEFaturaRegistered')} />
                  <label className="form-check-label" htmlFor="c-ef">e-Fatura kullanıcısı</label>
                </div>
              </div>
              <div className="col-md-3">
                <div className="form-check form-switch">
                  <input className="form-check-input" type="checkbox" role="switch" id="c-ea" checked={form.isEArchiveRegistered} onChange={set('isEArchiveRegistered')} />
                  <label className="form-check-label" htmlFor="c-ea">e-Arşiv kullanıcısı</label>
                </div>
              </div>
              <div className="col-md-3"><label className="form-label" htmlFor="c-pef">e-Fatura serisi</label><input className="form-control font-monospace" id="c-pef" required pattern="[A-Z0-9]{3}" value={form.eFaturaPrefix} onChange={prefix('eFaturaPrefix')} /></div>
              <div className="col-md-3"><label className="form-label" htmlFor="c-pea">e-Arşiv serisi</label><input className="form-control font-monospace" id="c-pea" required pattern="[A-Z0-9]{3}" value={form.eArsivPrefix} onChange={prefix('eArsivPrefix')} /></div>
            </div>
          </Card>
        </fieldset>
      </form>
    </SettingsLayout>
  );
}

export function CompanySettings() {
  return <TenantRequired><CompanyForm /></TenantRequired>;
}
