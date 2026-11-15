import { useEffect, useState } from 'react';
import { Link, Navigate, useNavigate } from 'react-router';
import { api } from '../api.js';
import { Brand } from '../components/Brand.jsx';
import { ErrorAlert } from '../components/ui.jsx';

export function Install() {
  const [busy, setBusy] = useState(false);
  const navigate = useNavigate();
  const [error, setError] = useState(null);
  const [available, setAvailable] = useState(null);

  useEffect(() => {
    let active = true;
    api.get('/api/v1/install').then(() => { if (active) setAvailable(true); }).catch((failure) => {
      if (!active) return;
      if (failure.status === 404) setAvailable(false);
      else setError(failure);
    });
    return () => { active = false; };
  }, []);

  async function submit(event) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    const form = event.currentTarget;
    const { companyName, taxId, taxOffice, city, street, eFaturaPrefix, eArsivPrefix, ...credentials } = Object.fromEntries(new FormData(form));
    try {
      await api.post('/api/v1/install', {
        ...credentials,
        company: {
          name: companyName, eFaturaPrefix, eArsivPrefix,
          profile: { taxId, title: companyName, taxOffice, city, street, kind: taxId.length === 10 ? 'LegalEntity' : 'NaturalPerson' },
        },
      });
      form.reset();
      navigate('/login', { replace: true, state: { installed: true } });
    } catch (failure) {
      setError(failure.status === 404 ? { message: 'Web kurulumu sunucuda kapalı.' } : failure);
    } finally {
      setBusy(false);
    }
  }

  if (available === false) return <Navigate to="/login" replace />;

  return <main className="login-main">
    <div className="login-panel">
      <section className="login-card" style={{ width: 'min(100%, 760px)' }} aria-labelledby="install-title">
        <div className="login-brand"><Brand /></div>
        <h1 className="login-title" id="install-title">İlk kurulum</h1>
        <ErrorAlert error={error} />
        {available && <>
          <p className="mt-2 text-body-secondary">İlk firmayı, platform yöneticisini ve firma sahibini oluşturun.</p>
          <form onSubmit={submit} className="vstack gap-3">
            <fieldset className="row g-2">
              <legend className="h6">Firma bilgileri</legend>
              <InstallField name="companyName" label="Firma unvanı" maxLength={250} />
              <InstallField name="taxId" label="VKN / TCKN" pattern="[0-9]{10,11}" inputMode="numeric" maxLength={11} />
              <InstallField name="taxOffice" label="Vergi dairesi" maxLength={100} />
              <InstallField name="city" label="İl" maxLength={100} />
              <InstallField name="street" label="Adres" maxLength={250} />
              <InstallField name="eFaturaPrefix" label="e-Fatura serisi" pattern="[A-Z0-9]{3}" maxLength={3} defaultValue="EFT" />
              <InstallField name="eArsivPrefix" label="e-Arşiv serisi" pattern="[A-Z0-9]{3}" maxLength={3} defaultValue="EAR" />
            </fieldset>
            <fieldset>
            <legend className="h6">Platform yöneticisi ve firma sahibi</legend>
            <label className="form-label" htmlFor="install-token">Kurulum anahtarı</label>
            <input className="form-control mb-3" id="install-token" name="installToken" type="password" minLength={32} maxLength={256} autoComplete="off" required />
            <label className="form-label" htmlFor="install-user">Yönetici kullanıcı kodu</label>
            <input className="form-control mb-3" id="install-user" name="userCode" minLength={3} maxLength={50} pattern="[A-Za-z0-9._\-]+" autoComplete="username" required />
            <label className="form-label" htmlFor="install-password">Yönetici parolası</label>
            <input className="form-control mb-2" id="install-password" name="password" type="password" minLength={10} maxLength={200} autoComplete="new-password" required />
            <div className="row g-2">
              <InstallField name="ownerCode" label="Firma sahibi kullanıcı kodu" minLength={3} maxLength={50} pattern="[A-Za-z0-9._\-]+" autoComplete="username" />
              <InstallField name="ownerPassword" label="Firma sahibi parolası" type="password" minLength={10} maxLength={200} autoComplete="new-password" />
            </div>
            <p className="form-text">En az 10 karakter, bir harf ve bir rakam kullanın. Kullanıcı kodu parolada yer almamalıdır.</p>
            </fieldset>
            <button type="submit" className="btn btn-primary w-100" disabled={busy}>{busy ? 'Kuruluyor…' : 'Kurulumu tamamla'}</button>
          </form>
        </>}
        <Link className="d-inline-block mt-3 mb-2" to="/login">Giriş ekranına dön</Link>
      </section>
    </div>
  </main>;
}

function InstallField({ name, label, ...input }) {
  return <div className="col-sm-6">
    <label className="form-label" htmlFor={`install-${name}`}>{label}</label>
    <input className="form-control" id={`install-${name}`} name={name} {...input} required />
  </div>;
}
