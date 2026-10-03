import { useEffect, useState, type ReactNode } from 'react';
import { Link, Navigate, useLocation } from 'react-router-dom';
import { resolveUrl } from '../api/client';
import type { AuctionStatus, AuctionSummary } from '../api/types';
import { useAuth } from '../context/AuthContext';
import { formatMoney, formatRemaining, STATUS_LABELS } from '../utils/format';

export function RequireAuth({ children, admin = false }: Readonly<{ children: ReactNode; admin?: boolean }>) {
  const { user, loading, isAdmin } = useAuth();
  const location = useLocation();
  if (loading) return <Spinner />;
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  if (admin && !isAdmin) return <Navigate to="/" replace />;
  return <>{children}</>;
}

export function Spinner() {
  return <div className="spinner" role="status" aria-label="Cargando" />;
}

export function ErrorMessage({ message }: Readonly<{ message: string | null }>) {
  if (!message) return null;
  return (
    <div className="alert alert-error" role="alert">
      {message}
    </div>
  );
}

export function StatusBadge({ status }: Readonly<{ status: AuctionStatus }>) {
  return <span className={`status status-${status}`}>{STATUS_LABELS[status]}</span>;
}

/** Cuenta regresiva hasta una fecha (se actualiza cada segundo). */
export function Countdown({ to, prefix }: Readonly<{ to: string; prefix?: string }>) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const id = globalThis.setInterval(() => setNow(Date.now()), 1000);
    return () => globalThis.clearInterval(id);
  }, []);
  const remaining = new Date(to).getTime() - now;
  return (
    <span className={remaining < 60_000 ? 'countdown urgent' : 'countdown'}>
      {prefix} {formatRemaining(remaining)}
    </span>
  );
}

export function AuctionCard({ auction }: Readonly<{ auction: AuctionSummary }>) {
  const image = resolveUrl(auction.imageUrl);
  return (
    <Link to={`/auctions/${auction.id}`} className="card auction-card">
      <div className="card-image">
        {image ? <img src={image} alt={auction.title} loading="lazy" /> : <div className="no-image">Sin imagen</div>}
        <StatusBadge status={auction.status} />
      </div>
      <div className="card-body">
        <span className="muted small">{auction.category}</span>
        <h3>{auction.title}</h3>
        <div className="price-row">
          <div>
            <span className="muted small">{auction.bidCount > 0 ? 'Oferta actual' : 'Precio inicial'}</span>
            <strong className="price">{formatMoney(auction.currentPrice)}</strong>
          </div>
          <span className="muted small">{auction.bidCount} pujas</span>
        </div>
        <div className="card-footer small">
          {auction.status === 'active' && <Countdown to={auction.endAt} prefix="Cierra en" />}
          {auction.status === 'upcoming' && <Countdown to={auction.startAt} prefix="Inicia en" />}
          {auction.status === 'finished' && <span className="muted">Subasta finalizada</span>}
          {auction.status === 'cancelled' && <span className="muted">Subasta cancelada</span>}
        </div>
      </div>
    </Link>
  );
}

export function EmptyState({ children }: Readonly<{ children: ReactNode }>) {
  return <div className="empty">{children}</div>;
}
