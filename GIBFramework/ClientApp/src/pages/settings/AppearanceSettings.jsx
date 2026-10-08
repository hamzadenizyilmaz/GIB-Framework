import { useState } from 'react';
import { Card, useToast } from '../../components/ui.jsx';
import { ACCENTS, DEFAULT_APPEARANCE, readAppearance, resolvedTheme, saveAppearance } from '../../theme.js';
import { SettingsLayout } from './SettingsLayout.jsx';

const THEMES = [
  ['light', 'Açık', 'sun'],
  ['dark', 'Koyu', 'moon-stars'],
  ['auto', 'Sistem', 'circle-half'],
];

const DENSITIES = [
  ['comfortable', 'Rahat', 'Varsayılan boşluklar'],
  ['compact', 'Sıkı', 'Daha fazla satır, daha küçük yazı'],
];

const WIDTHS = [
  ['wide', 'Geniş', 'İçerik ekran genişliğine yayılır'],
  ['boxed', 'Ortalanmış', 'İçerik ortada sabit genişlikte'],
];

function Choice({ active, onClick, children }) {
  return (
    <button type="button" className={`choice-card text-start w-100 ${active ? 'active' : ''}`} aria-pressed={active} onClick={onClick}>
      {children}
    </button>
  );
}

export function AppearanceSettings() {
  const toast = useToast();
  const [value, setValue] = useState(readAppearance);
  const update = (patch) => {
    const next = { ...value, ...patch };
    setValue(next);
    saveAppearance(next);
  };

  return (
    <SettingsLayout icon="palette" title="Görünüm" subtitle="Bu tarayıcıdaki panel görünümü"
      actions={<button type="button" className="btn btn-outline-secondary btn-pill" onClick={() => { update(DEFAULT_APPEARANCE); toast('Varsayılan görünüm yüklendi.', 'info'); }}><i className="bi bi-arrow-counterclockwise me-1" />Varsayılan</button>}>
      <Card title="Tema" icon="circle-half" className="mb-4">
        <div className="row g-3">
          {THEMES.map(([key, label, icon]) => (
            <div className="col-sm-4" key={key}>
              <Choice active={value.theme === key} onClick={() => update({ theme: key })}>
                <div className={`choice-preview ${key}`}><span /><span /></div>
                <div className="fw-semibold"><i className={`bi bi-${icon} me-2`} />{label}</div>
                {key === 'auto' && <div className="small text-body-secondary">Şu an: {resolvedTheme('auto') === 'dark' ? 'Koyu' : 'Açık'}</div>}
              </Choice>
            </div>
          ))}
        </div>
      </Card>

      <Card title="Vurgu rengi" icon="droplet-half" className="mb-4">
        <div className="d-flex flex-wrap gap-4">
          {Object.entries(ACCENTS).map(([key, a]) => (
            <div key={key} className="text-center">
              <button type="button" className={`accent-dot ${value.accent === key ? 'active' : ''}`} style={{ background: a.color }}
                aria-label={a.label} aria-pressed={value.accent === key} onClick={() => update({ accent: key })}>
                {value.accent === key && <i className="bi bi-check-lg" />}
              </button>
              <div className="small mt-1">{a.label}</div>
            </div>
          ))}
        </div>
        <hr />
        <div className="d-flex flex-wrap gap-2 align-items-center">
          <button type="button" className="btn btn-primary btn-sm">Birincil düğme</button>
          <button type="button" className="btn btn-outline-primary btn-sm">İkincil düğme</button>
          <span className="badge text-bg-primary">Etiket</span>
          <a href="#/settings/appearance" onClick={(e) => e.preventDefault()}>Bağlantı</a>
          <div className="form-check form-switch m-0 ms-2">
            <input className="form-check-input" type="checkbox" role="switch" id="demoSwitch" defaultChecked />
            <label className="form-check-label small" htmlFor="demoSwitch">Anahtar</label>
          </div>
        </div>
      </Card>

      <div className="row g-4">
        <div className="col-md-6">
          <Card title="Yoğunluk" icon="distribute-vertical" className="h-100">
            <div className="d-grid gap-2">
              {DENSITIES.map(([key, label, text]) => (
                <Choice key={key} active={value.density === key} onClick={() => update({ density: key })}>
                  <div className="fw-semibold">{label}</div>
                  <div className="small text-body-secondary">{text}</div>
                </Choice>
              ))}
            </div>
          </Card>
        </div>
        <div className="col-md-6">
          <Card title="Sayfa genişliği" icon="arrows-angle-expand" className="h-100">
            <div className="d-grid gap-2">
              {WIDTHS.map(([key, label, text]) => (
                <Choice key={key} active={value.width === key} onClick={() => update({ width: key })}>
                  <div className="fw-semibold">{label}</div>
                  <div className="small text-body-secondary">{text}</div>
                </Choice>
              ))}
            </div>
          </Card>
        </div>
      </div>
    </SettingsLayout>
  );
}
