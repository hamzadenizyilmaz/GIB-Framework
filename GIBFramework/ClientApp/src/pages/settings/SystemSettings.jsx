import { api } from '../../api.js';
import { fmtDate, fmtNumber } from '../../format.js';
import { BusyButton, Card, ErrorAlert, Spinner, useLoad } from '../../components/ui.jsx';
import { SettingsLayout } from './SettingsLayout.jsx';

const SIGNING_MODES = {
  DevSelfSigned: ['Geliştirme sertifikası', 'warning'],
  WindowsStore: ['Windows sertifika deposu', 'success'],
  Pkcs11: ['Akıllı kart / HSM (PKCS#11)', 'success'],
};

const ENVIRONMENTS = { Development: ['Geliştirme', 'warning'], Staging: ['Test', 'info'], Production: ['Canlı', 'success'] };

const TOTAL_LABELS = [
  ['invoices', 'Fatura', 'receipt'],
  ['customers', 'Cari kart', 'person-vcard'],
  ['products', 'Ürün / hizmet', 'box-seam'],
  ['users', 'Kullanıcı', 'people'],
  ['apiKeys', 'Aktif API anahtarı', 'key'],
  ['auditEvents', 'Denetim kaydı', 'shield-check'],
  ['tenants', 'Firma', 'buildings'],
];

function uptime(seconds) {
  const d = Math.floor(seconds / 86400);
  const h = Math.floor((seconds % 86400) / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  return [d && `${d} gün`, h && `${h} sa`, `${m} dk`].filter(Boolean).join(' ');
}

function Check({ ok, children }) {
  return (
    <li className="d-flex align-items-center gap-2 mb-2">
      <i className={`bi ${ok ? 'bi-check-circle-fill text-success' : 'bi-dash-circle text-body-tertiary'}`} />
      <span>{children}</span>
    </li>
  );
}

function CertificateBody({ cert }) {
  const total = Math.max(1, (new Date(cert.validTo) - new Date(cert.validFrom)) / 86400000);
  const used = Math.min(100, Math.max(0, Math.round(((total - cert.daysLeft) / total) * 100)));
  const [modeLabel, modeColor] = SIGNING_MODES[cert.mode] || [cert.mode, 'secondary'];
  const tone = cert.daysLeft <= 0 ? 'danger' : cert.daysLeft <= 30 ? 'warning' : 'success';
  return (
    <>
      <div className="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-3">
        <span className={`badge text-bg-${modeColor}`}>{modeLabel}</span>
        <span className={`badge text-bg-${tone}`}>{cert.daysLeft <= 0 ? 'Süresi doldu' : `${cert.daysLeft} gün kaldı`}</span>
      </div>
      <div className="progress mb-1" role="progressbar" aria-valuenow={used} aria-valuemin={0} aria-valuemax={100} style={{ height: '.45rem' }}>
        <div className={`progress-bar bg-${tone}`} style={{ width: `${used}%` }} />
      </div>
      <div className="d-flex justify-content-between small text-body-secondary mb-3">
        <span>{fmtDate(cert.validFrom)}</span><span>{fmtDate(cert.validTo)}</span>
      </div>
      <dl className="kv mb-0">
        <dt>Konu</dt><dd className="mono">{cert.subject}</dd>
        <dt>Yayıncı</dt><dd className="mono">{cert.issuer}</dd>
        <dt>Seri numarası</dt><dd className="mono">{cert.serialNumber}</dd>
        <dt>Parmak izi (SHA-1)</dt><dd className="mono user-select-all mb-0">{cert.thumbprint}</dd>
      </dl>
    </>
  );
}

export function CertificateSettings() {
  const { data: cert, loading, error } = useLoad(() => api.get('/api/v1/certificates/current'), []);
  return (
    <SettingsLayout icon="patch-check" title="İmza sertifikası" subtitle="UBL-TR belgelerini XAdES ile imzalayan mali mühür / e-imza sertifikası">
      <ErrorAlert error={error} />
      {loading ? <Spinner /> : cert && (
        <div className="row g-4">
          <div className="col-xl-7"><Card title="Sertifika" icon="patch-check"><CertificateBody cert={cert} /></Card></div>
          <div className="col-xl-5">
            <Card title="Yapılandırma" icon="sliders">
              <ul className="list-unstyled small mb-3">
                <Check ok={cert.mode !== 'DevSelfSigned'}>Gerçek mali mühür / e-imza</Check>
                <Check ok={cert.daysLeft > 30}>30 günden uzun geçerlilik</Check>
                <Check ok>XAdES-BES imza ve imza doğrulama</Check>
              </ul>
              <div className="small text-body-secondary mb-2">Sertifika kaynağı <span className="font-monospace">appsettings.json</span> dosyasındaki <span className="font-monospace">Signing</span> bölümünden seçilir:</div>
              <pre className="code-block">{`"Signing": {
  "Mode": "WindowsStore",
  "Thumbprint": "${cert.mode === 'WindowsStore' ? cert.thumbprint : 'SERTIFIKA_PARMAK_IZI'}"
}`}</pre>
            </Card>
          </div>
        </div>
      )}
    </SettingsLayout>
  );
}

export function SystemSettings() {
  const { data, loading, error, reload } = useLoad(() => Promise.all([
    api.get('/api/v1/system/status'),
    api.get('/api/v1/locations/status').catch(() => null),
    api.get('/api/v1/certificates/current').catch(() => null),
  ]), []);
  const [status, tkgm, cert] = data || [];
  const app = status?.application;
  const db = status?.database;
  const sec = status?.security;
  const [envLabel, envColor] = (app && ENVIRONMENTS[app.environment]) || [app?.environment, 'secondary'];

  return (
    <SettingsLayout icon="activity" title="Sistem durumu" subtitle={status ? (status.scope === 'platform' ? 'Platform geneli' : 'Seçili firma') : null}
      actions={<BusyButton className="btn btn-outline-primary btn-pill" onClick={reload}><i className="bi bi-arrow-clockwise me-1" />Yenile</BusyButton>}>
      <ErrorAlert error={error} />
      {loading && !status ? <Spinner /> : status && (
        <>
          <div className="row g-3 mb-4">
            <div className="col-sm-6 col-xl-3">
              <div className="card stat-card shadow-sm h-100"><div className="card-body">
                <div className="small text-body-secondary">Sürüm</div>
                <div className="metric">v{app.version}</div>
                <span className={`badge text-bg-${envColor}`}>{envLabel}</span>
              </div></div>
            </div>
            <div className="col-sm-6 col-xl-3">
              <div className="card stat-card shadow-sm h-100"><div className="card-body">
                <div className="small text-body-secondary">Çalışma süresi</div>
                <div className="metric">{uptime(app.uptimeSeconds)}</div>
                <div className="small text-body-secondary">Başlangıç {fmtDate(app.startedAt)}</div>
              </div></div>
            </div>
            <div className="col-sm-6 col-xl-3">
              <div className="card stat-card shadow-sm h-100"><div className="card-body">
                <div className="small text-body-secondary">Veritabanı şeması</div>
                <div className="metric">v{db.schemaVersion ?? '-'}</div>
                {db.schemaVersion >= db.requiredSchemaVersion
                  ? <span className="badge text-bg-success">Güncel</span>
                  : <span className="badge text-bg-danger">v{db.requiredSchemaVersion} gerekli</span>}
              </div></div>
            </div>
            <div className="col-sm-6 col-xl-3">
              <div className="card stat-card shadow-sm h-100"><div className="card-body">
                <div className="small text-body-secondary">Sunucu saati (İstanbul)</div>
                <div className="metric">{fmtDate(app.serverTime).split(' ')[1] || fmtDate(app.serverTime)}</div>
                <div className="small text-body-secondary">{fmtDate(app.serverTime).split(' ')[0]}</div>
              </div></div>
            </div>
          </div>

          <div className="row g-4 mb-4">
            {TOTAL_LABELS.filter(([k]) => k in status.totals).map(([k, label, icon]) => (
              <div className="col-6 col-md-4 col-xxl" key={k}>
                <div className="card shadow-sm h-100"><div className="card-body d-flex align-items-center gap-3">
                  <span className="tile-icon flex-shrink-0"><i className={`bi bi-${icon}`} /></span>
                  <div><div className="metric">{fmtNumber(status.totals[k])}</div><div className="small text-body-secondary">{label}</div></div>
                </div></div>
              </div>
            ))}
          </div>

          <div className="row g-4">
            <div className="col-xl-4">
              <Card title="Uygulama" icon="cpu" className="h-100">
                <dl className="kv mb-0">
                  <dt>Çalışma ortamı</dt><dd>{app.runtime}</dd>
                  <dt>İşletim sistemi</dt><dd>{app.os}</dd>
                  <dt>Sunucu</dt><dd className="font-monospace">{app.machine}</dd>
                  <dt>Bellek</dt><dd>{fmtNumber(app.memoryMb)} MB</dd>
                  <dt>Saat dilimi</dt><dd className="mb-0">{app.timeZone}</dd>
                </dl>
              </Card>
            </div>
            <div className="col-xl-4">
              <Card title="Veritabanı" icon="database" className="h-100">
                <dl className="kv mb-0">
                  <dt>Ad</dt><dd className="font-monospace">{db.name}</dd>
                  <dt>SQL Server</dt><dd>{db.version}</dd>
                  <dt>Sürüm</dt><dd>{db.edition}</dd>
                  <dt>Şema</dt><dd>v{db.schemaVersion} (gerekli v{db.requiredSchemaVersion})</dd>
                  <dt>Firma izolasyonu</dt><dd className="mb-0">Row-Level Security</dd>
                </dl>
              </Card>
            </div>
            <div className="col-xl-4">
              <Card title="Güvenlik" icon="shield-check" className="h-100">
                <ul className="list-unstyled small mb-3">
                  <Check ok={sec.https}>HTTPS bağlantı</Check>
                  <Check ok={sec.hsts}>HSTS</Check>
                  <Check ok>JWT oturum: {sec.sessionMinutes} dk</Check>
                  <Check ok={!sec.swaggerEnabled || envLabel !== 'Canlı'}>Swagger {sec.swaggerEnabled ? 'açık' : 'kapalı'}</Check>
                  <Check ok={sec.gibPortalEnabled}>GİB e-Arşiv Portal {sec.gibPortalProduction ? '(canlı)' : '(test)'}</Check>
                </ul>
                <div className="d-flex flex-wrap gap-1">
                  {sec.headers.map((h) => <span key={h} className="badge text-bg-light border fw-normal">{h}</span>)}
                </div>
              </Card>
            </div>
            <div className="col-xl-6">
              <Card title="İmza sertifikası" icon="patch-check" className="h-100">
                {cert ? <CertificateBody cert={cert} /> : <div className="small text-body-secondary">Sertifika bilgisi alınamadı.</div>}
              </Card>
            </div>
            <div className="col-xl-6">
              <Card title="TKGM adres verisi" icon="geo-alt" className="h-100">
                {tkgm ? (
                  <>
                    <div className="row g-3 text-center mb-3">
                      <div className="col-4"><div className="metric">{fmtNumber(tkgm.provinces)}</div><div className="small text-body-secondary">İl</div></div>
                      <div className="col-4"><div className="metric">{fmtNumber(tkgm.districts)}</div><div className="small text-body-secondary">İlçe</div></div>
                      <div className="col-4"><div className="metric">{fmtNumber(tkgm.neighborhoods)}</div><div className="small text-body-secondary">Mahalle / köy</div></div>
                    </div>
                    <div className="small mb-1">İlçeleri alınan il: {tkgm.provincesWithDistricts} / {tkgm.provinces}</div>
                    <div className="progress mb-2" style={{ height: '.45rem' }}><div className="progress-bar" style={{ width: `${tkgm.provinces ? (tkgm.provincesWithDistricts / tkgm.provinces) * 100 : 0}%` }} /></div>
                    <div className="small mb-1">Mahalleleri alınan ilçe: {tkgm.districtsWithNeighborhoods} / {tkgm.districts}</div>
                    <div className="progress mb-2" style={{ height: '.45rem' }}><div className="progress-bar bg-info" style={{ width: `${tkgm.districts ? (tkgm.districtsWithNeighborhoods / tkgm.districts) * 100 : 0}%` }} /></div>
                    {tkgm.limitUntil && <div className="small text-warning-emphasis"><i className="bi bi-hourglass-split me-1" />TKGM günlük sorgu limiti; aktarım {fmtDate(tkgm.limitUntil)} sonrası devam eder.</div>}
                  </>
                ) : <div className="small text-body-secondary">TKGM durumu alınamadı.</div>}
              </Card>
            </div>
          </div>
        </>
      )}
    </SettingsLayout>
  );
}
