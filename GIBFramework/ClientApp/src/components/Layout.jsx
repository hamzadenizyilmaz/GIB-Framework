import { useEffect, useId, useRef, useState } from 'react';
import { Link, NavLink, useLocation, useNavigate } from 'react-router';
import { api, LOGO_EVENT } from '../api.js';
import { ROLE_LABELS } from '../format.js';
import { useSession } from '../session.jsx';
import { useLoad, useToast } from './ui.jsx';
import { Brand, LegalStrip } from './Brand.jsx';
import { Icon } from './Icon.jsx';
import { readAppearance, resolvedTheme, saveAppearance } from '../theme.js';

export const MENU = [
  { to: '/', icon: 'panel', label: 'Panel', end: true },
  {
    label: 'Faturalar', icon: 'invoices', items: [
      { to: '/invoices', icon: 'list-ul', label: 'Tüm faturalar', policy: 'InvoiceRead' },
      { to: '/invoices/new', icon: 'plus-circle', label: 'Yeni fatura', policy: 'InvoiceCreate' },
      { to: '/incoming', icon: 'inbox', label: 'Gelen faturalar', policy: 'IncomingManage' },
    ],
  },
  {
    label: 'Kayıtlar', icon: 'users', items: [
      { to: '/customers', icon: 'person-vcard', label: 'Cari kartlar', policy: 'InvoiceRead' },
      { to: '/products', icon: 'box-seam', label: 'Ürün & hizmetler', policy: 'InvoiceRead' },
    ],
  },
  {
    label: 'GİB', icon: 'shield', items: [
      { to: '/gib-portal', icon: 'plug', label: 'e-Arşiv Portal bağlantısı', policy: 'GibPortal' },
      { to: '/gib-portal/documents', icon: 'files', label: 'Portal belgeleri', policy: 'GibPortal' },
    ],
  },
  {
    label: 'Mevzuat', icon: 'rules', items: [
      { to: '/compliance', icon: 'journal-text', label: 'Mevzuat & uyum' },
      { to: '/tax-offices', icon: 'building', label: 'Vergi daireleri' },
    ],
  },
  { to: '/settings', icon: 'settings', label: 'Ayarlar' },
];

function useDropdown() {
  const [open, setOpen] = useState(false);
  const ref = useRef(null);
  const location = useLocation();
  useEffect(() => { setOpen(false); }, [location.pathname]);
  useEffect(() => {
    if (!open) return undefined;
    const closeOutside = (e) => { if (ref.current && !ref.current.contains(e.target)) setOpen(false); };
    const onKey = (e) => {
      if (e.key === 'Escape' && ref.current?.contains(e.target)) {
        e.preventDefault();
        setOpen(false);
        ref.current.querySelector('button[aria-expanded]')?.focus();
      }
    };
    document.addEventListener('pointerdown', closeOutside);
    document.addEventListener('focusin', closeOutside);
    ref.current?.addEventListener('keydown', onKey);
    const menu = ref.current;
    return () => {
      document.removeEventListener('pointerdown', closeOutside);
      document.removeEventListener('focusin', closeOutside);
      menu?.removeEventListener('keydown', onKey);
    };
  }, [open]);
  return { open, setOpen, ref };
}

function NavDropdown({ entry }) {
  const { open, setOpen, ref } = useDropdown();
  const id = useId();
  const location = useLocation();
  const active = entry.items.some((i) => location.pathname === i.to || (i.to !== '/' && location.pathname.startsWith(`${i.to}/`)));
  return (
    <li className={`nav-item dropdown ${open ? 'show' : ''}`} ref={ref}>
      <button type="button" className={`nav-link ${active ? 'active' : ''}`} aria-expanded={open} aria-controls={id} onClick={() => setOpen(!open)}>
        <Icon name={entry.icon} />{entry.label}
      </button>
      <ul id={id} className={`dropdown-menu shadow-sm ${open ? 'show' : ''}`}>
        {entry.items.map((i) => (
          <li key={i.to}>
            <NavLink className="dropdown-item" to={i.to} end><i className={`bi bi-${i.icon} me-2 text-body-secondary`} aria-hidden="true" />{i.label}</NavLink>
          </li>
        ))}
      </ul>
    </li>
  );
}

function ApiLinks() {
  const session = useSession();
  return (
    <>
        <li><a className="dropdown-item" href="/swagger" target="_blank" rel="noopener noreferrer"><i className="bi bi-braces me-2" />Swagger UI</a></li>
        <li><a className="dropdown-item" href="/redoc" target="_blank" rel="noopener noreferrer"><i className="bi bi-book me-2" />API dokümantasyonu (ReDoc)</a></li>
        <li><Link className="dropdown-item" to="/developers"><i className="bi bi-code-square me-2" />Geliştirici dokümanı</Link></li>
        {session.can('ApiKeyManage') && <li><Link className="dropdown-item" to="/settings/api-keys"><i className="bi bi-key me-2" />API anahtarları</Link></li>}
    </>
  );
}

function TenantLogo() {
  const session = useSession();
  const [url, setUrl] = useState(null);
  const tenantId = session.tenant?.id;
  useEffect(() => {
    if (!tenantId) {
      setUrl(null);
      return undefined;
    }
    let alive = true;
    const load = () => api.get('/api/v1/tenants/current/logo-info').then((i) => alive && setUrl(i.hasLogo ? i.url : null)).catch(() => alive && setUrl(null));
    load();
    window.addEventListener(LOGO_EVENT, load);
    return () => {
      alive = false;
      window.removeEventListener(LOGO_EVENT, load);
    };
  }, [tenantId]);
  if (!url) return null;
  return <img className="navbar-tenant-logo d-none d-md-inline" src={url} alt={session.tenant?.name || 'Firma logosu'} />;
}

function ThemeToggle() {
  const [theme, setTheme] = useState(() => resolvedTheme(readAppearance().theme));
  const next = theme === 'dark' ? 'light' : 'dark';
  return (
    <button type="button" className="dropdown-item" title={next === 'dark' ? 'Koyu tema' : 'Açık tema'}
      onClick={() => {
        saveAppearance({ ...readAppearance(), theme: next });
        setTheme(next);
      }}>
      <i className={`bi bi-${theme === 'dark' ? 'sun' : 'moon-stars'} me-2`} aria-hidden="true" />
      {next === 'dark' ? 'Koyu temaya geç' : 'Açık temaya geç'}
    </button>
  );
}

function TenantSwitcher() {
  const session = useSession();
  const toast = useToast();
  const { data, loading, error, reload } = useLoad(() => api.get('/api/v1/tenants'), []);
  if (error) return <button type="button" className="btn btn-outline-danger btn-sm" onClick={reload} title={error.message}>Firmaları tekrar yükle</button>;
  return (
    <select className="form-select form-select-sm tenant-switch" aria-label="Firma seç" value={session.tenantOverride || ''} disabled={loading}
      onChange={async (e) => {
        const id = e.target.value;
        const selected = data?.find((tenant) => tenant.id === id);
        if (await session.selectTenant(id)) {
          toast(selected ? `${selected.name} seçildi.` : 'Firma bağlamı kaldırıldı.', 'info');
        }
      }}>
      <option value="">{loading ? 'Firmalar yükleniyor…' : 'Platform geneli'}</option>
      {(data || []).filter((tenant) => tenant.isActive).map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
    </select>
  );
}

function UserMenu() {
  const session = useSession();
  const navigate = useNavigate();
  const { open, setOpen, ref } = useDropdown();
  const p = session.permissions;
  return (
    <div className="dropdown" ref={ref}>
      <button className="btn btn-sm user-chip d-flex align-items-center gap-2" type="button" aria-expanded={open} onClick={() => setOpen(!open)}>
        <span className="app-account-icon"><Icon name="person" /></span>
        Hesabım
        <Icon name="chevron" />
      </button>
      <ul className={`dropdown-menu dropdown-menu-end shadow ${open ? 'show' : ''}`} style={{ right: 0, minWidth: '16rem' }}>
        <li className="px-3 py-2">
          <div className="fw-semibold">{p.displayName || p.userId}</div>
          {session.tenant?.name && <div className="small">{session.tenant.name}</div>}
          <div className="small text-body-secondary">{p.roles.map((r) => ROLE_LABELS[r] || r).join(', ')}</div>
        </li>
        {session.hasRole('PlatformSuperAdmin') && <li className="px-3 py-2"><TenantSwitcher /></li>}
        <li><hr className="dropdown-divider" /></li>
        <li><Link className="dropdown-item" to="/settings/account" onClick={() => setOpen(false)}><i className="bi bi-person-gear me-2" />Hesabım ve güvenlik</Link></li>
        <li><Link className="dropdown-item" to="/settings/appearance" onClick={() => setOpen(false)}><i className="bi bi-palette me-2" />Görünüm</Link></li>
        <li><Link className="dropdown-item" to="/settings" onClick={() => setOpen(false)}><i className="bi bi-gear me-2" />Ayarlar</Link></li>
        <li><ThemeToggle /></li>
        <li><hr className="dropdown-divider" /></li>
        <ApiLinks />
        <li><hr className="dropdown-divider" /></li>
        <li>
          <button className="dropdown-item text-danger" type="button" onClick={async () => {
            setOpen(false);
            await session.logout();
            navigate('/login');
          }}><i className="bi bi-box-arrow-right me-2" aria-hidden="true" />Çıkış</button>
        </li>
      </ul>
    </div>
  );
}

function InvoiceSearch() {
  const navigate = useNavigate();
  const [query, setQuery] = useState('');
  return (
    <form className="app-search" role="search" onSubmit={(event) => {
      event.preventDefault();
      const q = query.trim();
      if (q) navigate({ pathname: '/invoices', search: new URLSearchParams({ q }).toString() });
    }}>
      <Icon name="search" />
      <input className="form-control" type="search" aria-label="Faturalarda ara" placeholder="Ara…" maxLength={100} value={query} onChange={(event) => setQuery(event.target.value)} />
    </form>
  );
}

export function Layout({ children }) {
  const session = useSession();
  const [collapsed, setCollapsed] = useState(true);
  const location = useLocation();
  const menuToggle = useRef(null);
  useEffect(() => {
    setCollapsed(true);
    window.scrollTo(0, 0);
  }, [location.pathname]);

  const visible = MENU
    .map((e) => (e.items ? { ...e, items: e.items.filter((i) => !i.policy || session.can(i.policy)) } : e))
    .filter((e) => !e.items || e.items.length > 0);

  return (
    <div className="app-shell">
      <a className="visually-hidden-focusable app-skip" href="#app-main" onClick={(event) => {
        event.preventDefault();
        document.getElementById('app-main')?.focus();
      }}>Ana içeriğe geç</a>
      <nav className="navbar navbar-expand-xl app-navbar sticky-top" aria-label="Ana menü" onKeyDown={(event) => {
        if (event.key === 'Escape' && !event.defaultPrevented && !collapsed) {
          setCollapsed(true);
          menuToggle.current?.focus();
        }
      }}>
        <div className="container-fluid px-3 px-xl-4">
          <div className="app-nav-brand">
            <Link className="navbar-brand" to="/"><Brand /></Link>
            <TenantLogo />
          </div>
          <button ref={menuToggle} className="navbar-toggler" type="button" aria-controls="app-nav" aria-expanded={!collapsed} aria-label="Menüyü aç/kapat" onClick={() => setCollapsed(!collapsed)}>
            <Icon name="menu" />
          </button>
          <div className={`collapse navbar-collapse ${collapsed ? '' : 'show'}`} id="app-nav">
            <ul className="navbar-nav app-primary-nav mb-2 mb-xl-0">
              {visible.map((e) => (e.items
                ? <NavDropdown key={e.label} entry={e} />
                : (
                  <li className="nav-item" key={e.to}>
                    <NavLink className="nav-link" to={e.to} end={e.end}><Icon name={e.icon} />{e.label}</NavLink>
                  </li>
                )))}
            </ul>
            <div className="app-nav-tools d-flex flex-wrap align-items-center gap-2 pb-2 pb-xl-0">
              {session.can('InvoiceRead') && session.tenant && <InvoiceSearch />}
              {session.can('GibPortal') && session.tenant && <Link className="btn btn-primary app-test-link" to="/gib-portal?test-user=1"><Icon name="test" />Test Kullanıcı Al</Link>}
              <UserMenu />
            </div>
          </div>
        </div>
      </nav>
      <main id="app-main" className="app-content container-fluid" tabIndex={-1}>{children}</main>
      <LegalStrip />
    </div>
  );
}
