import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api } from '../api/client';
import { subscribe } from '../api/realtime';
import type { AppNotification } from '../api/types';
import { useAuth } from './AuthContext';

interface Toast {
  id: string;
  message: string;
}

interface NotificationContextValue {
  notifications: AppNotification[];
  unread: number;
  toasts: Toast[];
  refresh: () => Promise<void>;
  markRead: (id: string) => Promise<void>;
  markAllRead: () => Promise<void>;
  dismissToast: (id: string) => void;
}

const NotificationContext = createContext<NotificationContextValue | undefined>(undefined);

export function NotificationProvider({ children }: Readonly<{ children: ReactNode }>) {
  const { user } = useAuth();
  const [notifications, setNotifications] = useState<AppNotification[]>([]);
  const [toasts, setToasts] = useState<Toast[]>([]);

  const refresh = useCallback(async () => {
    if (!user) {
      setNotifications([]);
      return;
    }
    setNotifications(await api.notifications());
  }, [user]);

  const dismissToast = useCallback((id: string) => setToasts((t) => t.filter((x) => x.id !== id)), []);

  useEffect(() => {
    refresh().catch(() => undefined);
    if (!user) return;

    // Notificaciones en tiempo real vía WebSocket
    const unsubscribe = subscribe<AppNotification>('Notification', (n) => {
      setNotifications((prev) => [n, ...prev.filter((p) => p.id !== n.id)]);
      setToasts((prev) => [...prev, { id: n.id, message: n.message }]);
      globalThis.setTimeout(() => dismissToast(n.id), 6000);
    });
    return unsubscribe;
  }, [user, refresh, dismissToast]);

  const markRead = useCallback(async (id: string) => {
    await api.markNotificationRead(id);
    setNotifications((prev) => prev.map((n) => (n.id === id ? { ...n, isRead: true } : n)));
  }, []);

  const markAllRead = useCallback(async () => {
    await api.markAllNotificationsRead();
    setNotifications((prev) => prev.map((n) => ({ ...n, isRead: true })));
  }, []);

  const value = useMemo<NotificationContextValue>(
    () => ({
      notifications,
      unread: notifications.filter((n) => !n.isRead).length,
      toasts,
      refresh,
      markRead,
      markAllRead,
      dismissToast,
    }),
    [notifications, toasts, refresh, markRead, markAllRead, dismissToast],
  );

  return <NotificationContext.Provider value={value}>{children}</NotificationContext.Provider>;
}

export function useNotifications(): NotificationContextValue {
  const ctx = useContext(NotificationContext);
  if (!ctx) throw new Error('useNotifications debe usarse dentro de NotificationProvider');
  return ctx;
}
