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
    <SettingsLayout icon="palette" title="Görünüm" subtitle="Tema, vurgu rengi, yoğunluk ve sayfa genişliği"
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

      <div className="row g-4">
        <div className="col-lg-6">
        <Card title="Vurgu rengi" icon="droplet-half" className="h-100">
          <div className="d-flex flex-wrap gap-3">
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
        </Card>
        </div>
        <div className="col-lg-6">
          <Card title="Yoğunluk ve sayfa genişliği" className="h-100">
            <div className="mb-3">
              <label className="form-label" htmlFor="appearance-density">Yoğunluk</label>
              <select className="form-select" id="appearance-density" value={value.density}
                onChange={(event) => update({ density: event.target.value })}>
                {DENSITIES.map(([key, label]) => <option key={key} value={key}>{label}</option>)}
              </select>
              <div className="form-text">{DENSITIES.find(([key]) => key === value.density)?.[2]}</div>
            </div>
            <div>
              <label className="form-label" htmlFor="appearance-width">Sayfa genişliği</label>
              <select className="form-select" id="appearance-width" value={value.width}
                onChange={(event) => update({ width: event.target.value })}>
                {WIDTHS.map(([key, label]) => <option key={key} value={key}>{label}</option>)}
              </select>
              <div className="form-text">{WIDTHS.find(([key]) => key === value.width)?.[2]}</div>
            </div>
          </Card>
        </div>
      </div>
    </SettingsLayout>
  );
}
