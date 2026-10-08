import { useEffect, useMemo, useState } from 'react';
import { api } from '../api.js';
import { digitsOnly, taxIdKind, validateTaxId, ANONYMOUS_TCKN } from '../taxid.js';

export function TaxIdInput({ id = 'taxId', value, onChange, required = true, showError = true, className = '', autoFocus, append, anonymous = false }) {
  const [touched, setTouched] = useState(false);
  const error = value ? validateTaxId(value) : (required && touched ? 'VKN/TCKN zorunludur.' : null);
  const invalid = showError && touched && !!error;

  return (
    <>
      <div className={`input-group ${invalid ? 'has-validation' : ''}`}>
        <span className="input-group-text small fw-semibold" style={{ minWidth: '4.2rem' }}>{value ? taxIdKind(value) : 'VKN/TC'}</span>
        <input
          id={id}
          className={`form-control font-monospace ${invalid ? 'is-invalid' : value && !error ? 'is-valid' : ''} ${className}`}
          inputMode="numeric"
          autoComplete="off"
          maxLength={11}
          pattern="[0-9]{10,11}"
          required={required}
          autoFocus={autoFocus}
          value={value}
          placeholder={anonymous ? 'Boşsa 11111111111' : '10 veya 11 hane'}
          onChange={(e) => onChange(digitsOnly(e.target.value))}
          onPaste={(e) => {
            e.preventDefault();
            onChange(digitsOnly(e.clipboardData.getData('text')));
          }}
          onBlur={() => setTouched(true)}
        />
        {anonymous && value !== ANONYMOUS_TCKN && (
          <button type="button" className="btn btn-outline-secondary" title="Alıcının TCKN'si yoksa nihai tüketici numarası kullanılır" onClick={() => onChange(ANONYMOUS_TCKN)}>TCKN yok</button>
        )}
        {append}
        {invalid && <div className="invalid-feedback">{error}</div>}
      </div>
    </>
  );
}

function useList(url) {
  const [state, setState] = useState({ items: [], complete: true, message: null, loading: false });
  useEffect(() => {
    if (!url) {
      setState({ items: [], complete: true, message: null, loading: false });
      return undefined;
    }

    let cancelled = false;
    setState((s) => ({ ...s, loading: true }));
    api.get(url).then((r) => {
      if (cancelled) return;
      if (Array.isArray(r)) setState({ items: r, complete: true, message: null, loading: false });
      else setState({ items: r.items || [], complete: r.complete, message: r.message, loading: false });
    }).catch((e) => !cancelled && setState({ items: [], complete: false, message: e.message, loading: false }));
    return () => { cancelled = true; };
  }, [url]);
  return state;
}

const norm = (s) => (s || '').toLocaleUpperCase('tr-TR').trim();

export function LocationPicker({ value, onChange, required = true }) {
  const provinces = useList('/api/v1/locations/provinces');
  const province = useMemo(() => provinces.items.find((p) => norm(p.name) === norm(value.city)), [provinces.items, value.city]);
  const districts = useList(province ? `/api/v1/locations/provinces/${province.id}/districts` : null);
  const district = useMemo(() => districts.items.find((d) => norm(d.name) === norm(value.district)), [districts.items, value.district]);
  const neighborhoods = useList(district ? `/api/v1/locations/districts/${district.id}/neighborhoods` : null);

  const set = (patch) => onChange({ ...value, ...patch });

  return (
    <div className="row g-3">
      <div className="col-md-4">
        <label className="form-label" htmlFor="loc-city">İl</label>
        <select className="form-select" id="loc-city" required={required} value={province ? province.name : value.city || ''}
          onChange={(e) => set({ city: e.target.value, district: '', neighborhood: '' })}>
          <option value="">Seçin…</option>
          {value.city && !province && <option value={value.city}>{value.city}</option>}
          {provinces.items.map((p) => <option key={p.id} value={p.name}>{p.name}</option>)}
        </select>
        <div className="invalid-feedback">İl seçin.</div>
      </div>
      <div className="col-md-4">
        <label className="form-label" htmlFor="loc-district">İlçe <span className="badge text-bg-light border source-badge" title="Tapu ve Kadastro Genel Müdürlüğü">TKGM</span></label>
        {districts.items.length > 0 ? (
          <select className="form-select" id="loc-district" value={district ? district.name : value.district || ''}
            onChange={(e) => set({ district: e.target.value, neighborhood: '' })}>
            <option value="">Seçin…</option>
            {value.district && !district && <option value={value.district}>{value.district}</option>}
            {districts.items.map((d) => <option key={d.id} value={d.name}>{d.name}</option>)}
          </select>
        ) : (
          <input className="form-control" id="loc-district" maxLength={100} disabled={!value.city} placeholder={districts.loading ? 'Yükleniyor…' : ''}
            value={value.district || ''} onChange={(e) => set({ district: e.target.value })} />
        )}
      </div>
      <div className="col-md-4">
        <label className="form-label" htmlFor="loc-neighborhood">Mahalle / köy <span className="badge text-bg-light border source-badge" title="Tapu ve Kadastro Genel Müdürlüğü">TKGM</span></label>
        {neighborhoods.items.length > 0 ? (
          <select className="form-select" id="loc-neighborhood" value={value.neighborhood || ''} onChange={(e) => set({ neighborhood: e.target.value })}>
            <option value="">Seçin…</option>
            {value.neighborhood && !neighborhoods.items.some((n) => norm(n.name) === norm(value.neighborhood)) && <option value={value.neighborhood}>{value.neighborhood}</option>}
            {neighborhoods.items.map((n) => <option key={n.id} value={n.name}>{n.name}</option>)}
          </select>
        ) : (
          <input className="form-control" id="loc-neighborhood" maxLength={150} disabled={!value.district} placeholder={neighborhoods.loading ? 'Yükleniyor…' : ''}
            value={value.neighborhood || ''} onChange={(e) => set({ neighborhood: e.target.value })} />
        )}
      </div>
      {(districts.message || neighborhoods.message) && (
        <div className="col-12"><div className="form-text text-warning-emphasis"><i className="bi bi-info-circle me-1" />{districts.message || neighborhoods.message}</div></div>
      )}
    </div>
  );
}
