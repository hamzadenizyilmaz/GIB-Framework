const money = new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const number = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 4 });

export const fmtMoney = (value, currency = 'TRY') => `${money.format(Number(value || 0))} ${currency === 'TRY' ? '₺' : currency}`;

export const fmtNumber = (value) => number.format(Number(value || 0));

export const TIME_ZONE = 'Europe/Istanbul';

const dateTimeFormat = new Intl.DateTimeFormat('tr-TR', {
  timeZone: TIME_ZONE, day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', second: '2-digit',
});

const isoParts = new Intl.DateTimeFormat('en-CA', {
  timeZone: TIME_ZONE, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h23',
});

function istanbulParts(date = new Date()) {
  const p = Object.fromEntries(isoParts.formatToParts(date).map((x) => [x.type, x.value]));
  return { date: `${p.year}-${p.month}-${p.day}`, time: `${p.hour}:${p.minute}:${p.second}`, hm: `${p.hour}:${p.minute}` };
}

export const istanbulNow = () => istanbulParts();

export function fmtDate(value) {
  if (!value) return '';
  const s = String(value);
  if (/^\d{4}-\d{2}-\d{2}$/.test(s)) {
    const [y, m, d] = s.split('-');
    return `${d}.${m}.${y}`;
  }
  const date = new Date(s);
  return Number.isNaN(date.getTime()) ? s : dateTimeFormat.format(date);
}

export const todayIso = () => istanbulParts().date;

export const toLocalInput = (iso) => {
  if (!iso) return '';
  const p = istanbulParts(new Date(iso));
  return `${p.date}T${p.hm}`;
};

export const fromLocalInput = (value) => (value ? new Date(`${value}:00+03:00`).toISOString() : null);

export const STATUS = {
  Draft: ['Taslak', 'secondary'],
  Validating: ['Doğrulanıyor', 'info'],
  Validated: ['Doğrulandı', 'info'],
  AwaitingApproval: ['Onay bekliyor', 'warning'],
  Approved: ['Onaylandı', 'primary'],
  Signing: ['İmzalanıyor', 'primary'],
  Signed: ['İmzalandı', 'primary'],
  Queued: ['Gönderim kuyruğunda', 'info'],
  Transmitting: ['Gönderiliyor', 'info'],
  InDoubt: ['Belirsiz (uzlaştırma)', 'warning'],
  Sent: ['Gönderildi', 'success'],
  Acknowledged: ['Alındı', 'success'],
  Delivered: ['Teslim edildi', 'success'],
  Accepted: ['Kabul edildi', 'success'],
  Rejected: ['Reddedildi', 'danger'],
  Cancelled: ['İptal edildi', 'dark'],
  Objected: ['İtiraz edildi', 'dark'],
  Failed: ['Başarısız', 'danger'],
};

export const DOC_TYPES = { EFatura: 'e-Fatura', EArsiv: 'e-Arşiv', PaperInvoice: 'Kâğıt fatura', Undetermined: 'Belirlenemedi' };

export const ROLE_LABELS = {
  PlatformSuperAdmin: 'Platform yöneticisi',
  TenantOwner: 'Genel Müdür / CEO',
  CompanyAdmin: 'Firma yöneticisi',
  AccountingManager: 'Muhasebe müdürü',
  SecurityOfficer: 'Güvenlik sorumlusu',
  ComplianceOfficer: 'Uyum sorumlusu',
  InvoiceApprover: 'Onay yetkilisi',
  InvoiceSigner: 'İmza yetkilisi',
  IntegratorManager: 'Entegrasyon yöneticisi',
  Accountant: 'Muhasebeci',
  InvoiceCreator: 'Fatura hazırlayan',
  ArchiveAuditor: 'Arşiv denetçisi',
  ReadOnlyAuditor: 'Salt okunur denetçi',
  ApiClient: 'API istemcisi',
};

export const ANNOUNCEMENT_LEVELS = {
  Info: ['Bilgi', 'info', 'info-circle'],
  Success: ['Başarılı', 'success', 'check-circle'],
  Warning: ['Uyarı', 'warning', 'exclamation-triangle'],
  Danger: ['Önemli', 'danger', 'exclamation-octagon'],
};

export const ANNOUNCEMENT_AUDIENCES = { Everyone: 'Herkes', Authenticated: 'Giriş yapanlar', Tenant: 'Firma' };

export const UNITS = [
  ['C62', 'Adet'], ['HUR', 'Saat'], ['DAY', 'Gün'], ['MON', 'Ay'], ['KGM', 'Kilogram'], ['GRM', 'Gram'], ['LTR', 'Litre'],
  ['MTR', 'Metre'], ['MTK', 'Metrekare'], ['MTQ', 'Metreküp'], ['KWH', 'Kilovatsaat'], ['SET', 'Set'], ['PA', 'Paket'],
];

export const round2 = (v) => Math.round((v + Number.EPSILON) * 100) / 100;
