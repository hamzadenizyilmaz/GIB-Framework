import { useState } from 'react';
import { api } from '../../api.js';
import { Card, Empty, Spinner, useLoad } from '../../components/ui.jsx';
import { SettingsLayout } from './SettingsLayout.jsx';

const match = (name, q) => !q.trim() || name.toLocaleLowerCase('tr-TR').includes(q.trim().toLocaleLowerCase('tr-TR'));

function Column({ title, icon, items, selected, onSelect, loading, message, complete, empty }) {
  const [q, setQ] = useState('');
  const list = (items || []).filter((i) => match(i.name, q));
  return (
    <Card title={<>{title} <span className="badge text-bg-light border ms-1">{items?.length || 0}</span></>} icon={icon} bodyClass="">
      <div className="p-2 border-bottom">
        <input className="form-control form-control-sm" placeholder="Ara" aria-label={`${title} ara`} value={q} onChange={(e) => setQ(e.target.value)} />
      </div>
      <div style={{ maxHeight: '28rem', overflowY: 'auto' }}>
        {loading ? <div className="px-3"><Spinner /></div> : list.length ? (
          <div className="list-group list-group-flush">
            {list.map((i) => (
              onSelect ? (
                <button key={i.id} type="button" className={`list-group-item list-group-item-action d-flex justify-content-between ${selected === i.id ? 'active' : ''}`} onClick={() => onSelect(i)}>
                  <span>{i.name}</span><span className={`small ${selected === i.id ? '' : 'text-body-tertiary'} font-monospace`}>{i.id}</span>
                </button>
              ) : (
                <div key={i.id} className="list-group-item d-flex justify-content-between"><span>{i.name}</span><span className="small text-body-tertiary font-monospace">{i.id}</span></div>
              )
            ))}
          </div>
        ) : <Empty icon="geo-alt">{empty}</Empty>}
      </div>
      {(message || complete === false) && <div className="card-footer bg-body small text-warning-emphasis"><i className="bi bi-hourglass-split me-1" />{message || 'Aktarım sürüyor.'}</div>}
    </Card>
  );
}

export function LocationSettings() {
  const [province, setProvince] = useState(null);
  const [district, setDistrict] = useState(null);
  const { data: provinces, loading: lp } = useLoad(() => api.get('/api/v1/locations/provinces'), []);
  const { data: districts, loading: ld } = useLoad(() => (province ? api.get(`/api/v1/locations/provinces/${province.id}/districts`) : Promise.resolve(null)), [province?.id]);
  const { data: neighborhoods, loading: ln } = useLoad(() => (district ? api.get(`/api/v1/locations/districts/${district.id}/neighborhoods`) : Promise.resolve(null)), [district?.id]);

  return (
    <SettingsLayout icon="geo-alt" title="İl / ilçe / mahalle">
      <div className="row g-3">
        <div className="col-lg-4">
          <Column title="İller" icon="map" items={provinces} loading={lp} selected={province?.id} empty="İl yok."
            onSelect={(p) => { setProvince(p); setDistrict(null); }} />
        </div>
        <div className="col-lg-4">
          <Column title={province ? `${province.name} ilçeleri` : 'İlçeler'} icon="signpost-split" items={districts?.items} loading={ld} selected={district?.id}
            message={districts?.message} complete={districts?.complete} empty={province ? 'İlçe bulunamadı.' : 'Bir il seçin.'} onSelect={setDistrict} />
        </div>
        <div className="col-lg-4">
          <Column title={district ? `${district.name} mahalleleri` : 'Mahalleler'} icon="houses" items={neighborhoods?.items} loading={ln}
            message={neighborhoods?.message} complete={neighborhoods?.complete} empty={district ? 'Mahalle bulunamadı.' : 'Bir ilçe seçin.'} />
        </div>
      </div>
    </SettingsLayout>
  );
}
