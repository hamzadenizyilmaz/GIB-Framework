import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { api, storage, UNAUTHORIZED_EVENT } from './api.js';

const SessionContext = createContext(null);
const empty = { loading: false, permissions: null, tenant: null, error: null };

export function SessionProvider({ children }) {
  const [state, setState] = useState({ ...empty, loading: true });
  const request = useRef(0);

  const load = useCallback(async () => {
    const id = ++request.current;
    const token = storage.token;
    const override = storage.tenantOverride;
    const current = () => request.current === id && storage.token === token && storage.tenantOverride === override;
    if (!token) {
      setState(empty);
      return null;
    }

    setState({ ...empty, loading: true });
    try {
      const permissions = await api.get('/api/v1/auth/permissions');
      if (!current()) return null;
      const tenant = (permissions.tenantId || override) && !permissions.mustChangePassword && !permissions.mfaRequired
        ? await api.get('/api/v1/tenants/current')
        : null;
      if (!current()) return null;
      setState({ ...empty, permissions, tenant });
      return permissions;
    } catch (error) {
      if (current()) setState({ ...empty, error });
      return null;
    }
  }, []);

  useEffect(() => {
    load();
    const onUnauthorized = () => {
      ++request.current;
      setState(empty);
    };
    window.addEventListener(UNAUTHORIZED_EVENT, onUnauthorized);
    return () => {
      ++request.current;
      window.removeEventListener(UNAUTHORIZED_EVENT, onUnauthorized);
    };
  }, [load]);

  const logout = useCallback(async () => {
    const token = storage.token;
    try { await api.post('/api/v1/auth/logout'); } catch { }
    if (storage.token !== token) return;
    ++request.current;
    storage.clear();
    setState(empty);
  }, []);

  const value = useMemo(() => {
    const p = state.permissions;
    return {
      ...state,
      reload: load,
      can: (policy) => !!p && p.policies.includes(policy),
      hasRole: (role) => !!p && p.roles.includes(role),
      async login(userCode, password, totpCode) {
        const result = await api.post('/api/v1/auth/login', { userCode, password, totpCode: totpCode || null });
        storage.tenantOverride = null;
        storage.token = result.accessToken;
        await load();
        return result;
      },
      logout,
      async selectTenant(tenantId) {
        storage.tenantOverride = tenantId || null;
        return await load();
      },
      tenantOverride: storage.tenantOverride,
    };
  }, [state, load, logout]);

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export const useSession = () => useContext(SessionContext);
