import { useEffect, useRef, useState } from 'react';
import { Link, NavLink, useLocation, useNavigate } from 'react-router';
import { api, LOGO_EVENT } from '../api.js';
import { ROLE_LABELS } from '../format.js';
import { useSession } from '../session.jsx';
import { useToast } from './ui.jsx';
import { LegalStrip } from './Brand.jsx';
import { readAppearance, resolvedTheme, saveAppearance } from '../theme.js';

export const MENU = [
  { to: '/', icon: 'speedometer2', label: 'Panel', end: true },
  {
    label: 'Faturalar', icon: 'receipt', items: [
      { to: '/invoices', icon: 'list-ul', label: 'Tüm faturalar', policy: 'InvoiceRead' },
      { to: '/invoices/new', icon: 'plus-circle', label: 'Yeni fatura', policy: 'InvoiceCreate' },
      { to: '/incoming', icon: 'inbox', label: 'Gelen faturalar', policy: 'IncomingManage' },
    ],
  },
  {
    label: 'Kayıtlar', icon: 'journal-bookmark', items: [
      { to: '/customers', icon: 'person-vcard', label: 'Cari kartlar', policy: 'InvoiceRead' },
      { to: '/products', icon: 'box-seam', label: 'Ürün & hizmetler', policy: 'InvoiceRead' },
    ],
  },
  {
    label: 'GİB', icon: 'bank', items: [
      { to: '/gib-portal', icon: 'plug', label: 'e-Arşiv Portal bağlantısı', policy: 'GibPortal' },
      { to: '/gib-portal/documents', icon: 'files', label: 'Portal belgeleri', policy: 'GibPortal' },
    ],
  },
  {
    label: 'Mevzuat', icon: 'journal-check', items: [
      { to: '/compliance', icon: 'journal-text', label: 'Mevzuat & uyum' },
      { to: '/tax-offices', icon: 'building', label: 'Vergi daireleri' },
    ],
  },
  { to: '/settings', icon: 'gear', label: 'Ayarlar' },
];

function useDropdown() {
  const [open, setOpen] = useState(false);
  const ref = useRef(null);
  const location = useLocation();
  useEffect(() => { setOpen(false); }, [location.pathname]);
  useEffect(() => {
    if (!open) return undefined;
    const onDown = (e) => { if (ref.current && !ref.current.contains(e.target)) setOpen(false); };
    const onKey = (e) => e.key === 'Escape' && setOpen(false);
    document.addEventListener('mousedown', onDown);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onDown);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);
  return { open, setOpen, ref };
}

function NavDropdown({ entry }) {
  const { open, setOpen, ref } = useDropdown();
  const location = useLocation();
  const active = entry.items.some((i) => location.pathname === i.to || (i.to !== '/' && location.pathname.startsWith(`${i.to}/`)));
  return (
    <li className={`nav-item dropdown ${open ? 'show' : ''}`} ref={ref}>
      <button type="button" className={`nav-link dropdown-toggle ${active ? 'active' : ''}`} aria-expanded={open} onClick={() => setOpen(!open)}>
        <i className={`bi bi-${entry.icon} me-1`} />{entry.label}
      </button>
      <ul className={`dropdown-menu shadow-sm ${open ? 'show' : ''}`}>
        {entry.items.map((i) => (
          <li key={i.to}>
            <NavLink className="dropdown-item" to={i.to} end><i className={`bi bi-${i.icon} me-2 text-body-secondary`} />{i.label}</NavLink>
          </li>
        ))}
      </ul>
    </li>
  );
}

function ApiMenu() {
  const { open, setOpen, ref } = useDropdown();
  const session = useSession();
  return (
    <div className="dropdown" ref={ref}>
      <button type="button" className="btn btn-sm btn-outline-primary btn-pill dropdown-toggle px-3" aria-expanded={open} onClick={() => setOpen(!open)}>
        <i className="bi bi-code-slash me-1" />API
      </button>
      <ul className={`dropdown-menu dropdown-menu-end shadow ${open ? 'show' : ''}`} style={{ right: 0 }}>
        <li><a className="dropdown-item" href="/swagger" target="_blank" rel="noopener noreferrer"><i className="bi bi-braces me-2" />Swagger UI</a></li>
        <li><a className="dropdown-item" href="/redoc" target="_blank" rel="noopener noreferrer"><i className="bi bi-book me-2" />API dokümantasyonu (ReDoc)</a></li>
        <li><hr className="dropdown-divider" /></li>
        <li><Link className="dropdown-item" to="/developers" onClick={() => setOpen(false)}><i className="bi bi-code-square me-2" />Geliştirici dokümanı</Link></li>
        {session.can('ApiKeyManage') && <li><Link className="dropdown-item" to="/settings/api-keys" onClick={() => setOpen(false)}><i className="bi bi-key me-2" />API anahtarları</Link></li>}
      </ul>
    </div>
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
    <button type="button" className="btn btn-sm user-chip theme-toggle" title={next === 'dark' ? 'Koyu tema' : 'Açık tema'} aria-label={next === 'dark' ? 'Koyu temaya geç' : 'Açık temaya geç'}
      onClick={() => {
        saveAppearance({ ...readAppearance(), theme: next });
        setTheme(next);
      }}>
      <i className={`bi bi-${theme === 'dark' ? 'sun' : 'moon-stars'}`} />
    </button>
  );
}

function TenantSwitcher() {
  const session = useSession();
  const toast = useToast();
  const [tenants, setTenants] = useState([]);
  useEffect(() => { api.get('/api/v1/tenants').then(setTenants).catch(() => setTenants([])); }, []);
  return (
    <select className="form-select form-select-sm tenant-switch" aria-label="Firma seç" value={session.tenantOverride || ''}
      onChange={async (e) => {
        await session.selectTenant(e.target.value);
        toast(e.target.value ? 'Firma seçildi.' : 'Firma bağlamı kaldırıldı.', 'info');
      }}>
      <option value="">Firma seçin…</option>
      {tenants.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
    </select>
  );
}

function UserMenu() {
  const session = useSession();
  const navigate = useNavigate();
  const { open, setOpen, ref } = useDropdown();
  const p = session.permissions;
  const initials = (p.displayName || p.userId || '?').split(' ').map((x) => x[0]).join('').slice(0, 2).toLocaleUpperCase('tr-TR');
  return (
    <div className="dropdown" ref={ref}>
      <button className="btn btn-sm user-chip d-flex align-items-center gap-2" type="button" aria-expanded={open} onClick={() => setOpen(!open)}>
        <span className="avatar">{initials}</span>
        <span className="d-none d-xl-inline text-start lh-sm">
          <span className="d-block fw-semibold">{p.displayName || p.userId}</span>
          <span className="d-block opacity-75 small">{session.tenant?.name || ROLE_LABELS[p.roles[0]] || p.roles[0]}</span>
        </span>
        <i className="bi bi-chevron-down small" />
      </button>
      <ul className={`dropdown-menu dropdown-menu-end shadow ${open ? 'show' : ''}`} style={{ right: 0, minWidth: '16rem' }}>
        <li className="px-3 py-2">
          <div className="fw-semibold">{p.displayName || p.userId}</div>
          <div className="small text-body-secondary">{p.roles.map((r) => ROLE_LABELS[r] || r).join(', ')}</div>
        </li>
        <li><hr className="dropdown-divider" /></li>
        <li><Link className="dropdown-item" to="/settings/account" onClick={() => setOpen(false)}><i className="bi bi-person-gear me-2" />Hesabım ve güvenlik</Link></li>
        <li><Link className="dropdown-item" to="/settings/appearance" onClick={() => setOpen(false)}><i className="bi bi-palette me-2" />Görünüm</Link></li>
        <li><Link className="dropdown-item" to="/settings" onClick={() => setOpen(false)}><i className="bi bi-gear me-2" />Ayarlar</Link></li>
        <li>
          <button className="dropdown-item text-danger" type="button" onClick={async () => {
            setOpen(false);
            await session.logout();
            navigate('/login');
          }}><i className="bi bi-box-arrow-right me-2" />Çıkış</button>
        </li>
      </ul>
    </div>
  );
}

export function Layout({ children }) {
  const session = useSession();
  const [collapsed, setCollapsed] = useState(true);
  const location = useLocation();
  useEffect(() => {
    setCollapsed(true);
    window.scrollTo(0, 0);
  }, [location.pathname]);

  const visible = MENU
    .map((e) => (e.items ? { ...e, items: e.items.filter((i) => !i.policy || session.can(i.policy)) } : e))
    .filter((e) => !e.items || e.items.length > 0);

  return (
    <div className="app-shell">
      <nav className="navbar navbar-expand-xl app-navbar sticky-top" aria-label="Ana menü">
        <div className="container-fluid px-3 px-xl-4">
          <Link className="navbar-brand d-flex align-items-center gap-2 fw-bold" to="/">
            <span className="app-brand-icon"><i className="bi bi-receipt-cutoff" /></span>
            GIB Framework
          </Link>
          <TenantLogo />
          <button className="navbar-toggler" type="button" aria-controls="app-nav" aria-expanded={!collapsed} aria-label="Menüyü aç/kapat" onClick={() => setCollapsed(!collapsed)}>
            <span className="navbar-toggler-icon" />
          </button>
          <div className={`collapse navbar-collapse ${collapsed ? '' : 'show'}`} id="app-nav">
            <ul className="navbar-nav me-auto mb-2 mb-xl-0">
              {visible.map((e) => (e.items
                ? <NavDropdown key={e.label} entry={e} />
                : (
                  <li className="nav-item" key={e.to}>
                    <NavLink className="nav-link" to={e.to} end={e.end}><i className={`bi bi-${e.icon} me-1`} />{e.label}</NavLink>
                  </li>
                )))}
            </ul>
            <div className="d-flex flex-wrap align-items-center gap-2 pb-2 pb-xl-0">
              {session.hasRole('PlatformSuperAdmin') && <TenantSwitcher />}
              <ThemeToggle />
              <ApiMenu />
              <UserMenu />
            </div>
          </div>
        </div>
      </nav>
      <main className="app-content container-fluid px-3 px-xl-4" tabIndex={-1}>{children}</main>
      <LegalStrip />
    </div>
  );
}
