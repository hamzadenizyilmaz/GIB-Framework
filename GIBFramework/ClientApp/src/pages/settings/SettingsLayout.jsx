import { useCallback, useEffect, useState } from 'react';
import { Link, NavLink } from 'react-router';
import { api } from '../../api.js';
import { useSession } from '../../session.jsx';
import { PageHeader, Spinner } from '../../components/ui.jsx';

export const SETTINGS = [
  {
    group: 'Firma',
    items: [
      { to: '/settings/company', icon: 'building-gear', label: 'Firma profili', text: 'Unvan, adres, vergi dairesi, e-Belge kayıtları', policy: 'InvoiceRead', tenant: true },
      { to: '/settings/invoice', icon: 'receipt', label: 'Fatura varsayılanları', text: 'Senaryo, para birimi, KDV, birim, vade', policy: 'InvoiceRead', tenant: true },
      { to: '/settings/notes', icon: 'sticky', label: 'Not şablonları', text: 'Faturaya eklenecek hazır notlar', policy: 'InvoiceRead', tenant: true },
      { to: '/settings/banks', icon: 'bank2', label: 'Banka hesapları', text: 'IBAN bilgileri ve fatura notu', policy: 'InvoiceRead', tenant: true },
      { to: '/settings/numbering', icon: '123', label: 'Numaralandırma', text: 'Belge serileri ve sayaçlar', policy: 'InvoiceRead', tenant: true },
      { to: '/settings/certificate', icon: 'patch-check', label: 'İmza sertifikası', text: 'Mali mühür / e-imza sertifika bilgileri ve süresi' },
    ],
  },
  {
    group: 'İletişim',
    items: [
      { to: '/settings/email', icon: 'envelope-at', label: 'E-posta (SMTP)', text: 'Gönderen adres, sunucu, marka rengi', policy: 'MessagingManage', tenant: true },
      { to: '/settings/sms', icon: 'chat-dots', label: 'SMS (Netgsm)', text: 'Netgsm hesabı, başlık, bakiye', policy: 'MessagingManage', tenant: true },
      { to: '/settings/templates', icon: 'file-earmark-richtext', label: 'Bildirim şablonları', text: 'Müşteri ve kullanıcı e-posta / SMS içerikleri', policy: 'MessagingManage', tenant: true },
      { to: '/settings/messages', icon: 'mailbox', label: 'Gönderim kayıtları', text: 'Kuyruk, durum, hata ve yeniden gönderim', policy: 'MessagingManage', tenant: true },
    ],
  },
  {
    group: 'Ekip ve yetki',
    items: [
      { to: '/settings/users', icon: 'people', label: 'Kullanıcılar', text: 'Kullanıcılar, roller, şifre talepleri', policy: 'UserManage' },
      { to: '/settings/roles', icon: 'diagram-3', label: 'Roller ve yetkiler', text: 'Rol hiyerarşisi ve yetki matrisi' },
    ],
  },
  {
    group: 'Entegrasyon',
    items: [
      { to: '/settings/integrations', icon: 'plug', label: 'Entegrasyonlar', text: 'WooCommerce, Shopify, WHMCS, WISECP, webhook', policy: 'IntegrationManage', tenant: true },
      { to: '/settings/api-keys', icon: 'key', label: 'API anahtarları', text: 'Üçüncü taraf uygulama erişimi', policy: 'ApiKeyManage', tenant: true },
      { to: '/settings/developers', icon: 'code-square', label: 'Geliştirici dokümanı', text: 'Kimlik doğrulama, örnekler, uç noktalar' },
      { to: '/settings/gib-portal', icon: 'bank', label: 'GİB e-Arşiv Portal', text: 'Portal bağlantısı ve test kullanıcısı', policy: 'GibPortal', tenant: true },
    ],
  },
  {
    group: 'Veri',
    items: [
      { to: '/settings/locations', icon: 'geo-alt', label: 'İl / ilçe / mahalle', text: 'TKGM idari birim verisi' },
      { to: '/settings/tax-offices', icon: 'building', label: 'Vergi daireleri', text: 'GİB Defterdarlık ve Vergi Daireleri listesi' },
    ],
  },
  {
    group: 'Güvenlik',
    items: [
      { to: '/settings/account', icon: 'person-lock', label: 'Hesabım ve 2FA', text: 'Şifre, iki adımlı doğrulama, oturumlar' },
      { to: '/settings/security', icon: 'shield-lock', label: 'Güvenlik politikası', text: '2FA zorunluluğu, oturum süresi, API anahtarı süresi', tenant: true },
      { to: '/settings/audit', icon: 'shield-check', label: 'Denetim izi', text: 'Hash zincirli işlem kayıtları', policy: 'AuditRead', tenant: true },
    ],
  },
  {
    group: 'Kişisel',
    items: [
      { to: '/settings/appearance', icon: 'palette', label: 'Görünüm', text: 'Açık / koyu tema, vurgu rengi, yoğunluk' },
    ],
  },
  {
    group: 'Sistem',
    items: [
      { to: '/settings/system', icon: 'activity', label: 'Sistem durumu', text: 'Sürüm, veritabanı, kayıtlar, güvenlik başlıkları', policy: 'SystemRead' },
      { to: '/settings/tenants', icon: 'buildings', label: 'Firmalar', text: 'Firma oluşturma ve seçme', policy: 'PlatformAdmin' },
      { to: '/settings/announcements', icon: 'megaphone', label: 'Duyurular', text: 'Açılır pencere duyuruları', policy: 'PlatformAdmin' },
    ],
  },
];

export function useSettingsMenu() {
  const session = useSession();
  return SETTINGS
    .map((g) => ({ ...g, items: g.items.filter((i) => (!i.policy || session.can(i.policy)) && (!i.tenant || session.tenant)) }))
    .filter((g) => g.items.length);
}

export function SettingsShell({ children }) {
  const menu = useSettingsMenu();
  return (
    <div className="row g-4">
      <div className="col-lg-3 col-xxl-2">
        <div className="card shadow-sm position-sticky" style={{ top: '5.5rem' }}>
          <div className="list-group list-group-flush settings-nav p-2">
            {menu.map((g) => (
              <div key={g.group}>
                <div className="settings-group">{g.group}</div>
                {g.items.map((i) => (
                  <NavLink key={i.to} to={i.to} end={i.to !== '/settings/integrations'} className="list-group-item list-group-item-action">
                    <i className={`bi bi-${i.icon} me-2`} />{i.label}
                  </NavLink>
                ))}
              </div>
            ))}
          </div>
        </div>
      </div>
      <div className="col-lg-9 col-xxl-10 min-w-0">{children}</div>
    </div>
  );
}

export function SettingsLayout({ icon, title, subtitle, actions, children }) {
  return (
    <SettingsShell>
      <PageHeader breadcrumb={[['Ayarlar', '/settings'], [title]]} icon={icon} title={title} subtitle={subtitle} actions={actions} />
      {children}
    </SettingsShell>
  );
}

export function SettingsHub() {
  const menu = useSettingsMenu();
  const session = useSession();
  return (
    <>
      <PageHeader icon="gear" title="Ayarlar" subtitle={session.tenant?.name} />
      {menu.map((g) => (
        <div key={g.group} className="mb-4">
          <h2 className="h6 text-uppercase text-body-secondary fw-semibold mb-3" style={{ letterSpacing: '.06em' }}>{g.group}</h2>
          <div className="row row-cols-1 row-cols-md-2 row-cols-xl-3 row-cols-xxl-4 g-3">
            {g.items.map((i) => (
              <div className="col" key={i.to}>
                <Link to={i.to} className="card shadow-sm h-100 settings-tile">
                  <div className="card-body d-flex gap-3 align-items-start">
                    <span className="tile-icon flex-shrink-0"><i className={`bi bi-${i.icon}`} /></span>
                    <div>
                      <div className="fw-semibold">{i.label}</div>
                      <div className="small text-body-secondary">{i.text}</div>
                    </div>
                  </div>
                </Link>
              </div>
            ))}
          </div>
        </div>
      ))}
    </>
  );
}

export function useTenantSettings() {
  const session = useSession();
  const [state, setState] = useState({ loading: true, data: null, error: null });
  const load = useCallback(async () => {
    try {
      setState({ loading: false, data: await api.get('/api/v1/settings'), error: null });
    } catch (error) {
      setState({ loading: false, data: null, error });
    }
  }, []);
  useEffect(() => { load(); }, [load, session.tenant?.id]);

  const save = async (patch) => {
    const current = state.data;
    const saved = await api.put('/api/v1/settings', {
      defaults: current.defaults,
      noteTemplates: current.noteTemplates,
      bankAccounts: current.bankAccounts,
      ...patch,
    });
    setState({ loading: false, data: saved, error: null });
    return saved;
  };

  return { ...state, save, reload: load, canEdit: session.can('SettingsManage') };
}

export function TenantRequired({ children }) {
  const session = useSession();
  if (session.loading) return <Spinner />;
  if (!session.tenant) return <div className="alert alert-info">Bu ayar için üst çubuktan bir firma seçin.</div>;
  return children;
}
