import { useState } from 'react';
import { Card, useToast } from '../../components/ui.jsx';
import { ACCENTS, DEFAULT_APPEARANCE, FONT_SIZES, readAppearance, resolvedTheme, saveAppearance } from '../../theme.js';
import { SettingsLayout } from './SettingsLayout.jsx';

const THEMES = [
  ['light', 'Açık', 'sun', 'Gündüz kullanımı için krem tonlu arayüz'],
  ['dark', 'Koyu', 'moon-stars', 'Düşük ışıkta göz yormayan koyu arayüz'],
  ['auto', 'Sistem', 'circle-half', 'İşletim sisteminin tercihini izler'],
];

const DENSITIES = [
  ['comfortable', 'Rahat', 'Varsayılan boşluklar, okunaklı satırlar'],
  ['compact', 'Sıkı', 'Ekrana daha fazla satır sığar'],
];

const WIDTHS = [
  ['wide', 'Tam genişlik', 'İçerik ekran genişliğine yayılır'],
  ['boxed', 'Ortalanmış', 'İçerik ortada sabit genişlikte'],
];

const FONT_LABELS = { sm: 'Küçük', md: 'Normal', lg: 'Büyük', xl: 'Çok büyük' };

function Option({ active, onClick, children, label }) {
  return (
    <button type="button" className={`appearance-option ${active ? 'active' : ''}`} aria-pressed={active} aria-label={label} onClick={onClick}>
      <span className="check"><i className="bi bi-check-lg" /></span>
      {children}
    </button>
  );
}

function Toggle({ id, title, text, checked, onChange }) {
  return (
    <div className="pref-row">
      <label htmlFor={id}>
        <span className="appearance-label mb-0">{title}</span>
        <span className="small text-body-secondary">{text}</span>
      </label>
      <div className="form-check form-switch m-0">
        <input id={id} className="form-check-input" type="checkbox" role="switch" checked={checked} onChange={(e) => onChange(e.target.checked)} />
      </div>
    </div>
  );
}

function Preview({ value }) {
  const accent = ACCENTS[value.accent] || ACCENTS.forest;
  return (
    <Card title="Canlı önizleme" icon="eye" className="appearance-preview">
      <div className="preview-surface">
        <div className="pv-bar">
          <span style={{ width: 22, height: 22, borderRadius: 6, background: accent.color, display: 'inline-block' }} />
          GIB Framework
          <span className="pill">Faturalar</span>
        </div>
        <div className="pv-body">
          <div className="d-flex justify-content-between align-items-center gap-2">
            <div>
              <div className="fw-bold" style={{ fontFamily: 'var(--ivory-heading)', fontSize: '1.15rem' }}>Fatura listesi</div>
              <div className="small text-body-secondary">Son 30 gün</div>
            </div>
            <button type="button" className="btn btn-primary btn-sm" tabIndex={-1}><i className="bi bi-plus-lg" />Yeni fatura</button>
          </div>
          <div className="card shadow-sm">
            <div className="table-responsive">
              <table className="table align-middle mb-0">
                <thead><tr><th>Belge no</th><th>Alıcı</th><th className="text-end">Tutar</th></tr></thead>
                <tbody>
                  <tr><td className="fw-semibold">GIB2026000000124</td><td>Ayşe Yılmaz</td><td className="text-end">1.200,00 ₺</td></tr>
                  <tr><td className="fw-semibold">GIB2026000000123</td><td>Demo A.Ş.</td><td className="text-end">8.450,00 ₺</td></tr>
                  <tr><td className="fw-semibold">GIB2026000000122</td><td>Mehmet Kaya</td><td className="text-end">640,00 ₺</td></tr>
                </tbody>
              </table>
            </div>
          </div>
          <div className="d-flex flex-wrap gap-2">
            <span className="badge text-bg-success">Kesinleşti</span>
            <span className="badge text-bg-warning">Onay bekliyor</span>
            <a href="#preview" onClick={(e) => e.preventDefault()}>Bağlantı örneği</a>
          </div>
        </div>
      </div>
      <p className="small text-body-secondary mt-3 mb-0"><i className="bi bi-info-circle me-1" />Tercihler bu tarayıcıda saklanır ve anında uygulanır.</p>
    </Card>
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
    <SettingsLayout icon="palette" title="Görünüm" subtitle="Tema, renk, yazı boyutu, yoğunluk ve erişilebilirlik tercihleri"
      actions={<button type="button" className="btn btn-outline-secondary" onClick={() => { update(DEFAULT_APPEARANCE); toast('Varsayılan görünüm yüklendi.', 'info'); }}><i className="bi bi-arrow-counterclockwise" />Varsayılana dön</button>}>
      <div className="appearance-grid">
        <div>
          <Card title="Tema" icon="circle-half" className="appearance-section">
            <p className="appearance-help">Arayüzün renk modunu seçin. “Sistem” seçeneği şu an <strong>{resolvedTheme('auto') === 'dark' ? 'koyu' : 'açık'}</strong> modu kullanıyor.</p>
            <div className="appearance-options">
              {THEMES.map(([key, label, icon, text]) => (
                <Option key={key} active={value.theme === key} onClick={() => update({ theme: key })} label={`${label} tema`}>
                  <div className={`mini-app ${key}`} style={{ '--mini-accent': (ACCENTS[value.accent] || ACCENTS.forest).color }}>
                    <div className="bar"><i /><i /><i /></div>
                    <div className="body"><span /><span /><span /></div>
                  </div>
                  <div><strong><i className={`bi bi-${icon} me-2`} />{label}</strong><small className="d-block">{text}</small></div>
                </Option>
              ))}
            </div>
          </Card>

          <Card title="Vurgu rengi" icon="droplet-half" className="appearance-section">
            <p className="appearance-help">Düğmeler, bağlantılar ve seçili öğelerde kullanılan ana renk.</p>
            <div className="accent-grid">
              {Object.entries(ACCENTS).map(([key, a]) => (
                <button key={key} type="button" className={`accent-option ${value.accent === key ? 'active' : ''}`} aria-pressed={value.accent === key} onClick={() => update({ accent: key })}>
                  <span className="swatch" style={{ background: a.color }}>{value.accent === key && <i className="bi bi-check-lg" />}</span>
                  {a.label}
                </button>
              ))}
            </div>
          </Card>

          <Card title="Yazı ve yerleşim" icon="type" className="appearance-section">
            <span className="appearance-label">Yazı boyutu</span>
            <p className="appearance-help">Tüm paneldeki metin ve bileşenleri orantılı olarak büyütür veya küçültür.</p>
            <div className="font-scale mb-4" role="group" aria-label="Yazı boyutu">
              {Object.entries(FONT_SIZES).map(([key, px]) => (
                <button key={key} type="button" className={value.fontSize === key ? 'active' : ''} aria-pressed={value.fontSize === key}
                  style={{ fontSize: `${px}px` }} onClick={() => update({ fontSize: key })}>
                  Aa<small>{FONT_LABELS[key]}</small>
                </button>
              ))}
            </div>

            <span className="appearance-label">Yoğunluk</span>
            <p className="appearance-help">Tablo ve kartlardaki boşluk miktarı.</p>
            <div className="appearance-options mb-4">
              {DENSITIES.map(([key, label, text]) => (
                <Option key={key} active={value.density === key} onClick={() => update({ density: key })} label={`${label} yoğunluk`}>
                  <div className={`mini-density ${key}`}>{Array.from({ length: key === 'compact' ? 9 : 6 }, (_, i) => <i key={i} />)}</div>
                  <div><strong>{label}</strong><small className="d-block">{text}</small></div>
                </Option>
              ))}
            </div>

            <span className="appearance-label">Sayfa genişliği</span>
            <p className="appearance-help">Geniş ekranlarda içeriğin yerleşimi.</p>
            <div className="appearance-options">
              {WIDTHS.map(([key, label, text]) => (
                <Option key={key} active={value.width === key} onClick={() => update({ width: key })} label={label}>
                  <div className={`mini-width ${key}`}><i /></div>
                  <div><strong>{label}</strong><small className="d-block">{text}</small></div>
                </Option>
              ))}
            </div>
          </Card>

          <Card title="Erişilebilirlik ve tablolar" icon="universal-access" className="appearance-section">
            <Toggle id="pref-contrast" title="Yüksek kontrast" text="Kenarlıkları ve ikincil metinleri belirginleştirir." checked={Boolean(value.contrast)} onChange={(contrast) => update({ contrast })} />
            <Toggle id="pref-motion" title="Hareketi azalt" text="Geçiş ve animasyonları kapatır." checked={Boolean(value.reduceMotion)} onChange={(reduceMotion) => update({ reduceMotion })} />
            <Toggle id="pref-stripes" title="Tablolarda zebra satırlar" text="Satırları dönüşümlü arka planla ayırır." checked={value.stripes !== false} onChange={(stripes) => update({ stripes })} />
          </Card>
        </div>
        <Preview value={value} />
      </div>
    </SettingsLayout>
  );
}
