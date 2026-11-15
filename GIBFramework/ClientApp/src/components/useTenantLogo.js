import { useEffect, useState } from 'react';
import { api, LOGO_EVENT } from '../api.js';

export function useTenantLogo(tenantId) {
  const [logo, setLogo] = useState(null);
  useEffect(() => {
    if (!tenantId) return;
    let controller;
    let objectUrl;
    const load = async () => {
      controller?.abort();
      const request = new AbortController();
      controller = request;
      try {
        const { blob } = await api.blob(`/api/v1/tenants/${tenantId}/logo`, { signal: request.signal });
        if (request.signal.aborted) return;
        if (objectUrl) URL.revokeObjectURL(objectUrl);
        objectUrl = URL.createObjectURL(blob);
        setLogo({ tenantId, url: objectUrl });
      } catch {
        if (request.signal.aborted) return;
        if (objectUrl) URL.revokeObjectURL(objectUrl);
        objectUrl = undefined;
        setLogo(null);
      }
    };
    load();
    window.addEventListener(LOGO_EVENT, load);
    return () => {
      controller?.abort();
      if (objectUrl) URL.revokeObjectURL(objectUrl);
      window.removeEventListener(LOGO_EVENT, load);
    };
  }, [tenantId]);
  return logo?.tenantId === tenantId ? logo?.url : null;
}
