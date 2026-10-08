import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { api, storage, UNAUTHORIZED_EVENT } from './api.js';

const SessionContext = createContext(null);

export function SessionProvider({ children }) {
  const [state, setState] = useState({ loading: true, permissions: null, tenant: null });

  const load = useCallback(async () => {
    if (!storage.token) {
      setState({ loading: false, permissions: null, tenant: null });
      return null;
    }

    try {
      const permissions = await api.get('/api/v1/auth/permissions');
      const tenant = permissions.tenantId || storage.tenantOverride
        ? await api.get('/api/v1/tenants/current').catch(() => null)
        : null;
      setState({ loading: false, permissions, tenant });
      return permissions;
    } catch {
      storage.clear();
      setState({ loading: false, permissions: null, tenant: null });
      return null;
    }
  }, []);

  useEffect(() => {
    load();
    const onUnauthorized = () => setState({ loading: false, permissions: null, tenant: null });
    window.addEventListener(UNAUTHORIZED_EVENT, onUnauthorized);
    return () => window.removeEventListener(UNAUTHORIZED_EVENT, onUnauthorized);
  }, [load]);

  const value = useMemo(() => {
    const p = state.permissions;
    return {
      ...state,
      reload: load,
      can: (policy) => !!p && p.policies.includes(policy),
      hasRole: (role) => !!p && p.roles.includes(role),
      async login(userCode, password, totpCode) {
        const result = await api.post('/api/v1/auth/login', { userCode, password, totpCode: totpCode || null });
        storage.token = result.accessToken;
        await load();
        return result;
      },
      async logout() {
        try { await api.post('/api/v1/auth/logout'); } catch { }
        storage.clear();
        setState({ loading: false, permissions: null, tenant: null });
      },
      async selectTenant(tenantId) {
        storage.tenantOverride = tenantId || null;
        await load();
      },
      tenantOverride: storage.tenantOverride,
    };
  }, [state, load]);

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export const useSession = () => useContext(SessionContext);
