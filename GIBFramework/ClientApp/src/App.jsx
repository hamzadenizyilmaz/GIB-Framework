import { useEffect } from 'react';
import { Navigate, Route, Routes, useLocation, useNavigate } from 'react-router';
import { MFA_REQUIRED_EVENT, PASSWORD_CHANGE_EVENT } from './api.js';
import { AnnouncementPopup } from './components/AnnouncementPopup.jsx';
import { Layout } from './components/Layout.jsx';
import { Spinner, useToast } from './components/ui.jsx';
import { useSession } from './session.jsx';
import { Account, ChangePassword, Login } from './pages/Auth.jsx';
import { Dashboard } from './pages/Dashboard.jsx';
import { InvoiceList } from './pages/InvoiceList.jsx';
import { InvoiceDetail } from './pages/InvoiceDetail.jsx';
import { InvoiceForm } from './pages/InvoiceForm.jsx';
import { Incoming } from './pages/Incoming.jsx';
import { GibPortal } from './pages/GibPortal.jsx';
import { Compliance } from './pages/Compliance.jsx';
import { TaxOffices } from './pages/TaxOffices.jsx';
import { Audit } from './pages/Audit.jsx';
import { Tenants } from './pages/Tenants.jsx';
import { Announcements } from './pages/Announcements.jsx';
import { Customers } from './pages/Customers.jsx';
import { Products } from './pages/Products.jsx';
import { Developers } from './pages/Developers.jsx';
import { SettingsHub, SettingsShell } from './pages/settings/SettingsLayout.jsx';
import { SecuritySettings } from './pages/settings/SecuritySettings.jsx';
import { AppearanceSettings } from './pages/settings/AppearanceSettings.jsx';
import { CertificateSettings, SystemSettings } from './pages/settings/SystemSettings.jsx';
import { EmailSettings, MessageLogSettings, SmsSettings, TemplateSettings } from './pages/settings/MessagingSettings.jsx';
import { IntegrationDetailSettings, IntegrationSettings } from './pages/settings/IntegrationSettings.jsx';
import { CompanySettings } from './pages/settings/CompanySettings.jsx';
import { BankAccountSettings, InvoiceDefaultsSettings, NoteTemplateSettings, NumberingSettings } from './pages/settings/InvoiceSettings.jsx';
import { RolesSettings, UsersSettings } from './pages/settings/TeamSettings.jsx';
import { ApiKeySettings } from './pages/settings/ApiKeySettings.jsx';
import { LocationSettings } from './pages/settings/LocationSettings.jsx';
import { GibDocuments } from './pages/GibDocuments.jsx';

const MFA_PAGE = '/settings/account';
const ACTIVITY_EVENTS = ['mousedown', 'keydown', 'touchstart', 'wheel'];

function useIdleLogout() {
  const session = useSession();
  const navigate = useNavigate();
  const toast = useToast();
  const minutes = session.permissions?.idleLogoutMinutes || 0;
  useEffect(() => {
    if (!minutes) return undefined;
    let timer;
    const expire = async () => {
      await session.logout();
      toast('Hareketsizlik nedeniyle oturum kapatıldı.', 'info');
      navigate('/login');
    };
    const reset = () => {
      clearTimeout(timer);
      timer = setTimeout(expire, minutes * 60000);
    };
    reset();
    ACTIVITY_EVENTS.forEach((e) => window.addEventListener(e, reset, { passive: true }));
    return () => {
      clearTimeout(timer);
      ACTIVITY_EVENTS.forEach((e) => window.removeEventListener(e, reset));
    };
  }, [minutes, session.logout, navigate, toast]);
}

function InSettings({ children }) {
  return <SettingsShell>{children}</SettingsShell>;
}

function Guard({ policy, children }) {
  const session = useSession();
  const location = useLocation();
  if (!session.permissions) return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  if (session.permissions.mustChangePassword && location.pathname !== '/change-password') return <Navigate to="/change-password" replace />;
  if (session.permissions.mfaRequired && !session.permissions.mustChangePassword && location.pathname !== MFA_PAGE) return <Navigate to={MFA_PAGE} replace />;
  if (policy && !session.can(policy)) return <Navigate to="/" replace />;
  return <Layout>{children}</Layout>;
}

export default function App() {
  const session = useSession();
  const navigate = useNavigate();

  useIdleLogout();

  useEffect(() => {
    const onChange = () => navigate('/change-password');
    const onMfa = () => session.reload().then(() => navigate(MFA_PAGE));
    window.addEventListener(PASSWORD_CHANGE_EVENT, onChange);
    window.addEventListener(MFA_REQUIRED_EVENT, onMfa);
    return () => {
      window.removeEventListener(PASSWORD_CHANGE_EVENT, onChange);
      window.removeEventListener(MFA_REQUIRED_EVENT, onMfa);
    };
  }, [navigate, session.reload]);

  if (session.loading) {
    return <div className="d-flex justify-content-center align-items-center min-vh-100"><Spinner /></div>;
  }

  return (
    <>
      <Routes>
        <Route path="/login" element={session.permissions ? <Navigate to="/" replace /> : <Login />} />
        <Route path="/change-password" element={<Guard><ChangePassword /></Guard>} />
        <Route path="/account" element={<Navigate to="/settings/account" replace />} />
        <Route path="/" element={<Guard><Dashboard /></Guard>} />
        <Route path="/invoices" element={<Guard policy="InvoiceRead"><InvoiceList /></Guard>} />
        <Route path="/invoices/new" element={<Guard policy="InvoiceCreate"><InvoiceForm /></Guard>} />
        <Route path="/invoices/:id" element={<Guard policy="InvoiceRead"><InvoiceDetail /></Guard>} />
        <Route path="/incoming" element={<Guard policy="IncomingManage"><Incoming /></Guard>} />
        <Route path="/gib-portal" element={<Guard policy="GibPortal"><GibPortal /></Guard>} />
        <Route path="/gib-portal/documents" element={<Guard policy="GibPortal"><GibDocuments /></Guard>} />
        <Route path="/customers" element={<Guard policy="InvoiceRead"><Customers /></Guard>} />
        <Route path="/products" element={<Guard policy="InvoiceRead"><Products /></Guard>} />
        <Route path="/settings" element={<Guard><SettingsHub /></Guard>} />
        <Route path="/settings/company" element={<Guard policy="InvoiceRead"><CompanySettings /></Guard>} />
        <Route path="/settings/invoice" element={<Guard policy="InvoiceRead"><InvoiceDefaultsSettings /></Guard>} />
        <Route path="/settings/notes" element={<Guard policy="InvoiceRead"><NoteTemplateSettings /></Guard>} />
        <Route path="/settings/banks" element={<Guard policy="InvoiceRead"><BankAccountSettings /></Guard>} />
        <Route path="/settings/numbering" element={<Guard policy="InvoiceRead"><NumberingSettings /></Guard>} />
        <Route path="/settings/users" element={<Guard policy="UserManage"><UsersSettings /></Guard>} />
        <Route path="/settings/roles" element={<Guard><RolesSettings /></Guard>} />
        <Route path="/settings/api-keys" element={<Guard policy="ApiKeyManage"><ApiKeySettings /></Guard>} />
        <Route path="/settings/locations" element={<Guard><LocationSettings /></Guard>} />
        <Route path="/settings/account" element={<Guard><InSettings><Account /></InSettings></Guard>} />
        <Route path="/settings/security" element={<Guard><SecuritySettings /></Guard>} />
        <Route path="/settings/certificate" element={<Guard><CertificateSettings /></Guard>} />
        <Route path="/settings/appearance" element={<Guard><AppearanceSettings /></Guard>} />
        <Route path="/settings/system" element={<Guard policy="SystemRead"><SystemSettings /></Guard>} />
        <Route path="/settings/email" element={<Guard policy="MessagingManage"><EmailSettings /></Guard>} />
        <Route path="/settings/sms" element={<Guard policy="MessagingManage"><SmsSettings /></Guard>} />
        <Route path="/settings/templates" element={<Guard policy="MessagingManage"><TemplateSettings /></Guard>} />
        <Route path="/settings/messages" element={<Guard policy="MessagingManage"><MessageLogSettings /></Guard>} />
        <Route path="/settings/integrations" element={<Guard policy="IntegrationManage"><IntegrationSettings /></Guard>} />
        <Route path="/settings/integrations/:id" element={<Guard policy="IntegrationManage"><IntegrationDetailSettings /></Guard>} />
        <Route path="/settings/audit" element={<Guard policy="AuditRead"><InSettings><Audit /></InSettings></Guard>} />
        <Route path="/settings/tax-offices" element={<Guard><InSettings><TaxOffices /></InSettings></Guard>} />
        <Route path="/settings/gib-portal" element={<Guard policy="GibPortal"><InSettings><GibPortal /></InSettings></Guard>} />
        <Route path="/settings/developers" element={<Guard><InSettings><Developers /></InSettings></Guard>} />
        <Route path="/settings/tenants" element={<Guard policy="PlatformAdmin"><InSettings><Tenants /></InSettings></Guard>} />
        <Route path="/settings/announcements" element={<Guard policy="PlatformAdmin"><InSettings><Announcements /></InSettings></Guard>} />
        <Route path="/developers" element={<Guard><Developers /></Guard>} />
        <Route path="/integration" element={<Navigate to="/settings/api-keys" replace />} />
        <Route path="/users" element={<Navigate to="/settings/users" replace />} />
        <Route path="/compliance" element={<Guard><Compliance /></Guard>} />
        <Route path="/tax-offices" element={<Guard><TaxOffices /></Guard>} />
        <Route path="/audit" element={<Navigate to="/settings/audit" replace />} />
        <Route path="/tenants" element={<Navigate to="/settings/tenants" replace />} />
        <Route path="/announcements" element={<Navigate to="/settings/announcements" replace />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
      <AnnouncementPopup trigger={session.permissions?.userId || 'anonymous'} />
    </>
  );
}
