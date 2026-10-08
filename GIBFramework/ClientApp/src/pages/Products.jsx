import { useState } from 'react';
import { api } from '../api.js';
import { fmtMoney, UNITS } from '../format.js';
import { useSession } from '../session.jsx';
import { BusyButton, Card, Empty, PageHeader, Spinner, useDialogs, useLoad, useToast } from '../components/ui.jsx';

export function Products() {
  const session = useSession();
  const dialogs = useDialogs();
  const toast = useToast();
  const [q, setQ] = useState('');
  const canEdit = session.can('CatalogManage');
  const { data, loading, reload } = useLoad(async () => {
    const [rows, defs] = await Promise.all([
      api.get('/api/v1/products'),
      api.get('/api/v1/compliance/tax-definitions').catch(() => ({ vatRates: [20, 10, 1, 0], withholdings: [], exemptions: [] })),
    ]);
    return { rows, defs };
  }, []);

  if (loading && !data) return <Spinner />;
  const { rows, defs } = data;
  const unitLabel = Object.fromEntries(UNITS);
  const filtered = rows.filter((p) => !q.trim() || `${p.code || ''} ${p.name}`.toLocaleLowerCase('tr-TR').includes(q.toLocaleLowerCase('tr-TR')));

  const edit = async (p) => {
    const f = await dialogs.form({
      title: p ? 'Ürün / hizmet düzenle' : 'Yeni ürün / hizmet', size: 'lg',
      fields: [
        { name: 'code', label: 'Kod', value: p?.code || '' },
        { name: 'name', label: 'Ad', required: true, value: p?.name || '' },
        { name: 'description', label: 'Açıklama', type: 'textarea', rows: 2, value: p?.description || '' },
        { name: 'unitCode', label: 'Birim', type: 'select', value: p?.unitCode || 'C62', options: UNITS.map(([value, label]) => ({ value, label })) },
        { name: 'unitPrice', label: 'Birim fiyat', type: 'number', step: '0.0001', minValue: '0', required: true, value: p ? String(p.unitPrice) : '' },
        { name: 'currency', label: 'Para birimi', type: 'select', value: p?.currency || 'TRY', options: ['TRY', 'USD', 'EUR', 'GBP'].map((c) => ({ value: c, label: c })) },
        { name: 'vatRate', label: 'KDV oranı', type: 'select', value: String(Number(p?.vatRate ?? 20)), options: defs.vatRates.map((r) => ({ value: String(r), label: `%${r}` })) },
        { name: 'vatExemptionCode', label: 'KDV istisna kodu (KDV %0 ise)', type: 'select', value: p?.vatExemptionCode || '', options: [{ value: '', label: '—' }, ...defs.exemptions.map((x) => ({ value: x.code, label: `${x.code} · ${x.name}` }))] },
        { name: 'withholdingCode', label: 'KDV tevkifatı', type: 'select', value: p?.withholdingCode || '', options: [{ value: '', label: '—' }, ...defs.withholdings.map((w) => ({ value: w.code, label: `${w.code} (${w.ratio}) · ${w.name}` }))] },
        { name: 'isActive', label: 'Durum', type: 'select', value: p && !p.isActive ? 'false' : 'true', options: [{ value: 'true', label: 'Aktif' }, { value: 'false', label: 'Pasif' }] },
      ],
    });
    if (!f) return;
    const body = {
      code: f.code || null,
      name: f.name,
      description: f.description || null,
      unitCode: f.unitCode,
      unitPrice: Number(String(f.unitPrice).replace(',', '.')),
      currency: f.currency,
      vatRate: Number(f.vatRate),
      vatExemptionCode: f.vatExemptionCode || null,
      withholdingCode: f.withholdingCode || null,
      isActive: f.isActive === 'true',
    };
    if (p) await api.put(`/api/v1/products/${p.id}`, body);
    else await api.post('/api/v1/products', body);
    toast('Ürün kaydedildi.');
    await reload();
  };

  return (
    <>
      <PageHeader icon="box-seam" title="Ürün & hizmetler" subtitle={`${rows.length} kayıt`}
        actions={canEdit && <BusyButton onClick={() => edit(null)}><i className="bi bi-plus-lg me-1" />Yeni ürün / hizmet</BusyButton>} />
      <Card className="mb-4">
        <input className="form-control" placeholder="Kod veya ad ile ara" aria-label="Ara" value={q} onChange={(e) => setQ(e.target.value)} />
      </Card>
      <Card bodyClass="">
        {filtered.length ? (
          <div className="table-responsive">
            <table className="table table-hover align-middle mb-0">
              <thead><tr><th>Kod</th><th>Ad</th><th>Birim</th><th className="num">Birim fiyat</th><th>KDV</th><th>Durum</th><th /></tr></thead>
              <tbody>
                {filtered.map((p) => (
                  <tr key={p.id} className={p.isActive ? '' : 'opacity-50'}>
                    <td className="font-monospace small">{p.code || '—'}</td>
                    <td><div className="fw-semibold">{p.name}</div>{p.description && <div className="small text-body-secondary">{p.description}</div>}</td>
                    <td>{unitLabel[p.unitCode] || p.unitCode}</td>
                    <td className="num">{fmtMoney(p.unitPrice, p.currency)}</td>
                    <td>%{Number(p.vatRate)}{p.vatExemptionCode && <span className="badge text-bg-light border ms-1">{p.vatExemptionCode}</span>}{p.withholdingCode && <span className="badge text-bg-light border ms-1">T{p.withholdingCode}</span>}</td>
                    <td>{p.isActive ? <span className="badge text-bg-success">Aktif</span> : <span className="badge text-bg-secondary">Pasif</span>}</td>
                    <td className="text-end">
                      {canEdit && (
                        <div className="btn-group btn-group-sm">
                          <BusyButton className="btn btn-outline-primary" title="Düzenle" onClick={() => edit(p)}><i className="bi bi-pencil" /></BusyButton>
                          <BusyButton className="btn btn-outline-danger" title="Sil" onClick={async () => {
                            if (!await dialogs.confirm('Ürünü sil', `"${p.name}" silinsin mi?`, 'Sil', 'danger')) return;
                            await api.del(`/api/v1/products/${p.id}`);
                            toast('Ürün silindi.');
                            await reload();
                          }}><i className="bi bi-trash" /></BusyButton>
                        </div>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : <Empty icon="box-seam">{q ? 'Eşleşen ürün yok.' : 'Henüz ürün / hizmet yok.'}</Empty>}
      </Card>
    </>
  );
}
