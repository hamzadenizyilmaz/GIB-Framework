import { useEffect, useState } from 'react';
import { api } from '../api.js';
import { ANNOUNCEMENT_LEVELS, fmtDate } from '../format.js';
import { Modal } from './ui.jsx';

const DISMISSED_KEY = 'gibframework.announcements.dismissed';
const SEEN_KEY = 'gibframework.announcements.seen';

function readIds(storage, key) {
  try { return new Set(JSON.parse(storage.getItem(key) || '[]')); } catch { return new Set(); }
}

function writeIds(storage, key, ids) {
  try { storage.setItem(key, JSON.stringify([...ids].slice(-200))); } catch { }
}

export function AnnouncementPopup({ trigger }) {
  const [items, setItems] = useState([]);
  const [dontShow, setDontShow] = useState(false);

  useEffect(() => {
    let cancelled = false;
    api.get('/api/v1/announcements/active').then((list) => {
      if (cancelled) return;
      const dismissed = readIds(localStorage, DISMISSED_KEY);
      const seen = readIds(sessionStorage, SEEN_KEY);
      const unseen = (list || []).filter((a) => !dismissed.has(a.id) && !seen.has(a.id));
      if (!unseen.length) return;
      unseen.forEach((a) => seen.add(a.id));
      writeIds(sessionStorage, SEEN_KEY, seen);
      setDontShow(false);
      setItems(unseen);
    }).catch(() => { });
    return () => { cancelled = true; };
  }, [trigger]);

  if (!items.length) return null;

  const close = () => {
    if (dontShow) {
      const dismissed = readIds(localStorage, DISMISSED_KEY);
      items.filter((a) => a.level !== 'Danger').forEach((a) => dismissed.add(a.id));
      writeIds(localStorage, DISMISSED_KEY, dismissed);
    }
    setItems([]);
  };

  const ordered = [...items].sort((a, b) => (a.level === 'Danger' ? -1 : 0) - (b.level === 'Danger' ? -1 : 0));
  const hasDanger = ordered.some((a) => a.level === 'Danger');
  const canDismiss = ordered.some((a) => a.level !== 'Danger');
  const first = ordered[0];
  const [, color, icon] = ANNOUNCEMENT_LEVELS[first.level] || ANNOUNCEMENT_LEVELS.Info;
  const title = ordered.length === 1 ? first.title : 'Duyurular';

  return (
    <Modal title={<><i className={`bi bi-${icon} text-${color} me-2`} />{title}</>} onClose={hasDanger ? undefined : close} scrollable
      footer={(
        <div className="modal-footer justify-content-between">
          {canDismiss ? (
            <div className="form-check mb-0">
              <input className="form-check-input" type="checkbox" id="announcement-dismiss" checked={dontShow} onChange={(e) => setDontShow(e.target.checked)} />
              <label className="form-check-label small" htmlFor="announcement-dismiss">Duyuruları bir daha gösterme</label>
            </div>
          ) : <span />}
          <button type="button" className={`btn btn-${hasDanger ? 'danger' : color === 'warning' ? 'warning' : 'primary'}`} onClick={close}>
            {hasDanger ? 'Okudum, anladım' : 'Tamam'}
          </button>
        </div>
      )}>
      <div className="modal-body">
        {ordered.length === 1 && first.level !== 'Danger'
          ? <p className="mb-0 announcement-text">{first.message}</p>
          : ordered.map((a) => {
            const [, c, i] = ANNOUNCEMENT_LEVELS[a.level] || ANNOUNCEMENT_LEVELS.Info;
            return (
              <div key={a.id} className={`alert alert-${c} d-flex gap-2 mb-2`}>
                <i className={`bi bi-${i} flex-shrink-0 fs-5`} />
                <div>
                  {a.level !== 'Danger' && <div className="fw-semibold">{a.title}</div>}
                  <div className={`announcement-text ${a.level === 'Danger' ? 'fw-semibold' : ''}`}>{a.message}</div>
                  {a.level !== 'Danger' && <div className="small text-body-secondary mt-1">{fmtDate(a.startsAt)}</div>}
                </div>
              </div>
            );
          })}
      </div>
    </Modal>
  );
}
