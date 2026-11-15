import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { api, storage, UNAUTHORIZED_EVENT } from './api.js';

const SessionContext = createContext(null);
const empty = { loading: false, permissions: null, tenant: null, error: null };

export function SessionProvider({ children }) {
  const [state, setState] = useState({ ...empty, loading: true });
  const request = useRef(0);

  const load = useCallback(async () => {
    const id = ++request.current;
    const version = storage.sessionVersion;
    const override = storage.tenantOverride;
    const current = () => request.current === id && storage.sessionVersion === version && storage.tenantOverride === override;

    setState({ ...empty, loading: true });
    try {
      const permissions = await api.get('/api/v1/auth/permissions', { quietUnauthorized: true });
      if (!current()) return null;
      const tenant = (permissions.tenantId || override) && !permissions.mustChangePassword && !permissions.mfaRequired
        ? await api.get('/api/v1/tenants/current')
        : null;
      if (!current()) return null;
      setState({ ...empty, permissions, tenant });
      return permissions;
    } catch (error) {
      if (current()) {
        if (error.status === 401) storage.clear();
        setState(error.status === 401 ? empty : { ...empty, error });
      }
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
    const version = storage.sessionVersion;
    try {
      await api.post('/api/v1/auth/logout', undefined, { quietUnauthorized: true });
    } catch (error) {
      if (error.status !== 401) throw error;
    }
    if (storage.sessionVersion !== version) return;
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
        storage.clear();
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
