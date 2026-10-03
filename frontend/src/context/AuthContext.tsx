import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api, tokenStore } from '../api/client';
import { resetConnection } from '../api/realtime';
import type { AuthResponse, User } from '../api/types';

interface AuthContextValue {
  user: User | null;
  loading: boolean;
  isAdmin: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (userName: string, email: string, password: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: Readonly<{ children: ReactNode }>) {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState<boolean>(() => tokenStore.get() !== null);

  useEffect(() => {
    if (!tokenStore.get()) return;
    api
      .me()
      .then(setUser)
      .catch(() => tokenStore.clear())
      .finally(() => setLoading(false));
  }, []);

  const applySession = useCallback((auth: AuthResponse) => {
    tokenStore.set(auth.token);
    setUser(auth.user);
    void resetConnection();
  }, []);

  const login = useCallback(
    async (email: string, password: string) => applySession(await api.login(email.trim(), password)),
    [applySession],
  );

  const register = useCallback(
    async (userName: string, email: string, password: string) =>
      applySession(await api.register(userName.trim(), email.trim(), password)),
    [applySession],
  );

  const logout = useCallback(() => {
    tokenStore.clear();
    setUser(null);
    void resetConnection();
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({ user, loading, isAdmin: user?.role === 'Admin', login, register, logout }),
    [user, loading, login, register, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth debe usarse dentro de AuthProvider');
  return ctx;
}
