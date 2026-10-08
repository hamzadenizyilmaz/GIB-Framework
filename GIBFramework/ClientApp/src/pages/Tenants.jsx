import { useNavigate } from 'react-router';
import { api } from '../api.js';
import { useSession } from '../session.jsx';
import { BusyButton, Card, Empty, PageHeader, Spinner, useDialogs, useLoad, useToast } from '../components/ui.jsx';

export function Tenants() {
  const session = useSession();
  const dialogs = useDialogs();
  const toast = useToast();
  const navigate = useNavigate();
  const { data: rows, loading, reload } = useLoad(() => api.get('/api/v1/tenants'), []);

  if (loading && !rows) return <Spinner />;

  const create = async () => {
    const f = await dialogs.form({
      title: 'Yeni firma', submitText: 'Oluştur', size: 'lg',
      fields: [
        { name: 'name', label: 'Firma adı', required: true },
        { name: 'taxId', label: 'VKN / TCKN', required: true, pattern: '[0-9]{10,11}', inputmode: 'numeric' },
        { name: 'title', label: 'Ticaret unvanı', required: true },
        {
          name: 'regime', label: 'Defter / vergilendirme', type: 'select', value: 'Bilanco',
          options: [{ value: 'Bilanco', label: 'Bilanço esası' }, { value: 'IsletmeHesabi', label: 'İşletme hesabı' }, { value: 'BasitUsul', label: 'Basit usul' }, { value: 'SerbestMeslek', label: 'Serbest meslek' }],
        },
        { name: 'taxOffice', label: 'Vergi dairesi', required: true },
        { name: 'street', label: 'Adres (cadde/sokak, no)' },
        { name: 'district', label: 'İlçe' },
        { name: 'city', label: 'İl', required: true },
        { name: 'registrations', label: 'e-Belge kayıtları', type: 'checkboxes', value: ['efatura', 'earsiv'], options: [{ value: 'efatura', label: 'e-Fatura' }, { value: 'earsiv', label: 'e-Arşiv' }] },
        { name: 'eFaturaPrefix', label: 'e-Fatura seri (3 karakter)', required: true, value: 'EFT', pattern: '[A-Z0-9]{3}' },
        { name: 'eArsivPrefix', label: 'e-Arşiv seri (3 karakter)', required: true, value: 'EAR', pattern: '[A-Z0-9]{3}' },
      ],
    });
    if (!f) return;
    await api.post('/api/v1/tenants', {
      name: f.name,
      profile: {
        taxId: f.taxId, kind: f.taxId.length === 11 ? 'NaturalPerson' : 'LegalEntity', regime: f.regime, title: f.title,
        taxOffice: f.taxOffice, street: f.street || null, district: f.district || null, city: f.city,
      },
      isEFaturaRegistered: f.registrations.includes('efatura'),
      isEArchiveRegistered: f.registrations.includes('earsiv'),
      eFaturaPrefix: f.eFaturaPrefix,
      eArsivPrefix: f.eArsivPrefix,
    });
    toast('Firma oluşturuldu.');
    await reload();
  };

  return (
    <>
      <PageHeader icon="buildings" title="Firmalar" actions={<BusyButton onClick={create}><i className="bi bi-plus-lg me-1" />Yeni firma</BusyButton>} />
      <Card bodyClass="">
        {rows.length ? (
          <div className="table-responsive">
            <table className="table align-middle mb-0">
              <thead><tr><th>Firma</th><th>VKN/TCKN</th><th>Vergi dairesi</th><th>Seriler</th><th>e-Belge</th><th /></tr></thead>
              <tbody>
                {rows.map((t) => (
                  <tr key={t.id} className={session.tenantOverride === t.id ? 'table-primary' : ''}>
                    <td className="fw-semibold">{t.name}<div className="small mono text-body-secondary fw-normal user-select-all">{t.id}</div></td>
                    <td className="mono">{t.profile.taxId}</td>
                    <td>{t.profile.taxOffice || '—'}</td>
                    <td className="mono small">{t.eFaturaPrefix} / {t.eArsivPrefix}</td>
                    <td>
                      {t.profile.isEFaturaRegistered && <span className="badge text-bg-primary me-1">e-Fatura</span>}
                      {t.profile.isEArchiveRegistered && <span className="badge text-bg-info">e-Arşiv</span>}
                    </td>
                    <td className="text-end">
                      <BusyButton className="btn btn-sm btn-outline-primary" onClick={async () => {
                        await session.selectTenant(t.id);
                        toast('Firma bağlamı seçildi.');
                        navigate('/');
                      }}>Bu firmada çalış</BusyButton>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : <Empty icon="buildings">Firma yok.</Empty>}
      </Card>
    </>
  );
}
