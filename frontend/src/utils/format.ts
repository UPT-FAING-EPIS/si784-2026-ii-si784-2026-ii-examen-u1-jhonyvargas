import type { AuctionStatus } from '../api/types';

const currency = new Intl.NumberFormat('es-PE', { style: 'currency', currency: 'USD' });
const dateTime = new Intl.DateTimeFormat('es-PE', { dateStyle: 'medium', timeStyle: 'short' });

export const formatMoney = (value: number): string => currency.format(value);

export const formatDate = (iso: string): string => dateTime.format(new Date(iso));

export const STATUS_LABELS: Record<AuctionStatus, string> = {
  active: 'Activa',
  upcoming: 'Próxima',
  finished: 'Finalizada',
  cancelled: 'Cancelada',
};

/** Devuelve el tiempo restante en formato legible ("2d 03h 15m", "04m 10s"). */
export function formatRemaining(ms: number): string {
  if (ms <= 0) return 'Finalizada';
  const totalSeconds = Math.floor(ms / 1000);
  const days = Math.floor(totalSeconds / 86_400);
  const hours = Math.floor((totalSeconds % 86_400) / 3_600);
  const minutes = Math.floor((totalSeconds % 3_600) / 60);
  const seconds = totalSeconds % 60;
  const pad = (n: number) => n.toString().padStart(2, '0');
  if (days > 0) return `${days}d ${pad(hours)}h ${pad(minutes)}m`;
  if (hours > 0) return `${hours}h ${pad(minutes)}m ${pad(seconds)}s`;
  return `${pad(minutes)}m ${pad(seconds)}s`;
}

/** Convierte una fecha a valor para <input type="datetime-local"> en hora local. */
export function toLocalInputValue(date: Date): string {
  const offset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
}
